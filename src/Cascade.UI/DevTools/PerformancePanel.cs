using System;
using System.Collections.Generic;
using System.Text;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Performance monitoring panel. Provides real-time frame timing, render
/// statistics, signal dependency graphs, and trace recording with JSON export.
/// </summary>
internal static class PerformancePanel
{
    private static readonly RingBuffer<FrameSample> frameSamples = new(600);
    private static readonly RingBuffer<SignalChangeEvent> signalChanges = new(5000);
    private static readonly Dictionary<string, ComponentRenderStats> componentStats = new();
    private static bool isRecording;
    private static readonly List<TraceEvent> traceEvents = [];
    private static DateTime? recordingStartedAt;
    private static TimeSpan recordingDuration = TimeSpan.FromSeconds(5);

    /// <summary>A single frame timing sample.</summary>
    public sealed class FrameSample
    {
        /// <summary>Monotonic timestamp in milliseconds.</summary>
        public double TimestampMs { get; init; }

        /// <summary>Total frame time in milliseconds.</summary>
        public float FrameTimeMs { get; init; }

        /// <summary>Time spent in layout pass.</summary>
        public float LayoutTimeMs { get; init; }

        /// <summary>Time spent building the scene graph.</summary>
        public float RenderTimeMs { get; init; }

        /// <summary>Time spent in GPU rendering (Etch).</summary>
        public float GpuTimeMs { get; init; }

        /// <summary>Whether this frame was a dropped frame (exceeded target).</summary>
        public bool Dropped { get; init; }
    }

    /// <summary>Records a signal value change for the event history log.</summary>
    public sealed class SignalChangeEvent
    {
        /// <summary>Monotonic timestamp in milliseconds.</summary>
        public long TimestampMs { get; init; }

        /// <summary>Component type name that owns the signal.</summary>
        public string ComponentName { get; init; } = "";

        /// <summary>Signal field name.</summary>
        public string SignalName { get; init; } = "";

        /// <summary>New value as string.</summary>
        public string Value { get; init; } = "";
    }

    /// <summary>Render statistics for a single component type.</summary>
    public sealed class ComponentRenderStats
    {
        /// <summary>Full component type name.</summary>
        public required string ComponentName { get; init; }

        /// <summary>Total render count since last reset.</summary>
        public int RenderCount { get; set; }

        /// <summary>Average render time in milliseconds.</summary>
        public float AverageRenderMs { get; set; }

        /// <summary>Maximum render time observed.</summary>
        public float MaxRenderMs { get; set; }

        /// <summary>Signal that triggered the most recent render.</summary>
        public string? LastTrigger { get; set; }

        /// <summary>Timestamp of the last render.</summary>
        public DateTime? LastRenderAt { get; set; }
    }

    /// <summary>
    /// A trace event captured during recording.
    /// </summary>
    public sealed class TraceEvent
    {
        /// <summary>Monotonic timestamp relative to recording start.</summary>
        public double TimestampMs { get; init; }

        /// <summary>Event type.</summary>
        public TraceEventType Type { get; init; }

        /// <summary>Component name (if applicable).</summary>
        public string? Component { get; init; }

        /// <summary>Signal that triggered the event (if applicable).</summary>
        public string? Trigger { get; init; }

        /// <summary>Duration of the event in milliseconds.</summary>
        public float DurationMs { get; init; }

        /// <summary>Additional details.</summary>
        public string? Detail { get; init; }
    }

    /// <summary>Types of trace events.</summary>
    public enum TraceEventType
    {
        Render,
        Layout,
        Gpu,
        SignalWrite,
        AsyncOperation,
        FrameStart,
        FrameEnd,
    }

    /// <summary>
    /// Memory statistics snapshot.
    /// </summary>
    public sealed class MemoryStats
    {
        /// <summary>Current managed heap size in bytes.</summary>
        public long ManagedHeapBytes { get; init; }

        /// <summary>Allocation rate in bytes per second.</summary>
        public long AllocationRatePerSecond { get; init; }

        /// <summary>Number of GC collections per generation.</summary>
        public IReadOnlyList<int> GcCollections { get; init; } = [];
    }

    /// <summary>Whether a performance trace is currently recording.</summary>
    public static bool IsRecording => isRecording;

    /// <summary>Gets the most recent frame samples (up to 600 = 10 seconds at 60fps).</summary>
    public static IReadOnlyList<FrameSample> GetRecentFrames()
    {
        return frameSamples.ToList();
    }

    /// <summary>
    /// Records a frame timing sample. Called by the framework's render loop.
    /// </summary>
    internal static void RecordFrame(float frameTimeMs, float layoutTimeMs, float renderTimeMs, float gpuTimeMs, float targetFrameTimeMs)
    {
        var sample = new FrameSample
        {
            TimestampMs = Environment.TickCount64,
            FrameTimeMs = frameTimeMs,
            LayoutTimeMs = layoutTimeMs,
            RenderTimeMs = renderTimeMs,
            GpuTimeMs = gpuTimeMs,
            Dropped = frameTimeMs > targetFrameTimeMs,
        };

        frameSamples.Add(sample);

        if (isRecording)
        {
            RecordTraceEvent(new TraceEvent
            {
                TimestampMs = (DateTime.UtcNow - recordingStartedAt!.Value).TotalMilliseconds,
                Type = TraceEventType.FrameEnd,
                DurationMs = frameTimeMs,
                Detail = $"layout={layoutTimeMs:F2}ms render={renderTimeMs:F2}ms gpu={gpuTimeMs:F2}ms",
            });
        }
    }

    /// <summary>
    /// Records a component render event. Called by the framework's reconciler.
    /// </summary>
    internal static void RecordComponentRender(string componentName, float renderTimeMs, string? trigger)
    {
        if (!componentStats.TryGetValue(componentName, out var stats))
        {
            stats = new ComponentRenderStats { ComponentName = componentName };
            componentStats[componentName] = stats;
        }

        stats.RenderCount++;
        stats.AverageRenderMs = ((stats.AverageRenderMs * (stats.RenderCount - 1)) + renderTimeMs) / stats.RenderCount;
        if (renderTimeMs > stats.MaxRenderMs)
        {
            stats.MaxRenderMs = renderTimeMs;
        }
        stats.LastTrigger = trigger;
        stats.LastRenderAt = DateTime.UtcNow;

        if (isRecording)
        {
            RecordTraceEvent(new TraceEvent
            {
                TimestampMs = (DateTime.UtcNow - recordingStartedAt!.Value).TotalMilliseconds,
                Type = TraceEventType.Render,
                Component = componentName,
                Trigger = trigger,
                DurationMs = renderTimeMs,
            });
        }
    }

    /// <summary>
    /// Gets component render statistics sorted by render count (descending).
    /// </summary>
    public static IReadOnlyList<ComponentRenderStats> GetComponentStats(int topN = 10)
    {
        var sorted = new List<ComponentRenderStats>(componentStats.Values);
        sorted.Sort((a, b) => b.RenderCount.CompareTo(a.RenderCount));
        if (sorted.Count > topN)
        {
            sorted.RemoveRange(topN, sorted.Count - topN);
        }
        return sorted;
    }

    /// <summary>
    /// Gets the signal dependency graph for a component.
    /// </summary>
    public static SignalDependencyGraph? GetDependencyGraph(string componentName)
    {
        return NodeTreeWalker.GetSignalDependencies(componentName);
    }

    /// <summary>Gets current memory statistics.</summary>
    public static MemoryStats GetMemoryStats()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        return new MemoryStats
        {
            ManagedHeapBytes = gcInfo.HeapSizeBytes,
            AllocationRatePerSecond = GC.GetTotalAllocatedBytes(precise: false),
            GcCollections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)],
        };
    }

    /// <summary>Starts recording a detailed performance trace.</summary>
    public static void StartRecording(TimeSpan? duration = null)
    {
        recordingDuration = duration ?? TimeSpan.FromSeconds(5);
        traceEvents.Clear();
        recordingStartedAt = DateTime.UtcNow;
        isRecording = true;
    }

    /// <summary>Stops recording and returns the captured trace events.</summary>
    public static IReadOnlyList<TraceEvent> StopRecording()
    {
        isRecording = false;
        recordingStartedAt = null;
        return [.. traceEvents];
    }

    /// <summary>
    /// Exports the most recent trace recording as a JSON string
    /// suitable for offline analysis or sharing.
    /// </summary>
    public static string ExportTraceAsJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < traceEvents.Count; i++)
        {
            var evt = traceEvents[i];
            sb.Append("  { ");
            sb.Append($"\"timestampMs\": {evt.TimestampMs:F2}");
            sb.Append($", \"type\": \"{evt.Type}\"");
            if (evt.Component is not null)
            {
                sb.Append($", \"component\": \"{EscapeJson(evt.Component)}\"");
            }
            if (evt.Trigger is not null)
            {
                sb.Append($", \"trigger\": \"{EscapeJson(evt.Trigger)}\"");
            }
            sb.Append($", \"durationMs\": {evt.DurationMs:F2}");
            if (evt.Detail is not null)
            {
                sb.Append($", \"detail\": \"{EscapeJson(evt.Detail)}\"");
            }
            sb.Append(" }");
            if (i < traceEvents.Count - 1)
            {
                sb.Append(',');
            }
            sb.AppendLine();
        }
        sb.AppendLine("]");
        return sb.ToString();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }

    /// <summary>Resets all component render statistics.</summary>
    public static void ResetStats()
    {
        componentStats.Clear();
    }

    /// <summary>
    /// Records a signal value change for the event history log.
    /// Call from signal setter code (source generator or manual).
    /// </summary>
    internal static void RecordSignalChange(string componentName, string signalName, string value)
    {
        signalChanges.Add(new SignalChangeEvent
        {
            TimestampMs = Environment.TickCount64,
            ComponentName = componentName,
            SignalName = signalName,
            Value = value,
        });
    }

    /// <summary>
    /// Gets recent signal change events, optionally filtered by component or signal name.
    /// Returns in chronological order (oldest first).
    /// </summary>
    public static IReadOnlyList<SignalChangeEvent> GetSignalChanges(int limit = 50, string? component = null, string? signal = null)
    {
        var all = signalChanges.ToList();
        if (component is not null || signal is not null)
        {
            all = all.FindAll(e =>
                (component is null || e.ComponentName.Contains(component, StringComparison.OrdinalIgnoreCase)) &&
                (signal is null || e.SignalName.Equals(signal, StringComparison.OrdinalIgnoreCase)));
        }

        if (all.Count <= limit)
        {
            return all;
        }

        return all.GetRange(all.Count - limit, limit);
    }

    /// <summary>Total signal change events in the buffer.</summary>
    public static int TotalSignalChanges => signalChanges.Count;

    /// <summary>
    /// Gets the most recent frame samples, up to <paramref name="limit"/>.
    /// Returns in chronological order (oldest first).
    /// </summary>
    public static IReadOnlyList<FrameSample> GetFrameSamples(int limit = 50)
    {
        var all = frameSamples.ToList();
        if (all.Count <= limit)
        {
            return all;
        }

        return all.GetRange(all.Count - limit, limit);
    }

    /// <summary>
    /// Gets the total number of frame samples in the ring buffer.
    /// </summary>
    public static int TotalFrameSamples => frameSamples.Count;

    private static void RecordTraceEvent(TraceEvent evt)
    {
        traceEvents.Add(evt);

        if (recordingStartedAt is not null &&
            (DateTime.UtcNow - recordingStartedAt.Value) > recordingDuration)
        {
            isRecording = false;
        }
    }
}

/// <summary>
/// Simple ring buffer for fixed-size sliding window data.
/// </summary>
internal sealed class RingBuffer<T>
{
    private readonly T[] buffer;
    private int head;
    private int count;

    public RingBuffer(int capacity)
    {
        buffer = new T[capacity];
    }

    public int Count => count;

    public void Add(T item)
    {
        buffer[head] = item;
        head = (head + 1) % buffer.Length;
        if (count < buffer.Length)
        {
            count++;
        }
    }

    public List<T> ToList()
    {
        var result = new List<T>(count);
        int start = (head - count + buffer.Length) % buffer.Length;
        for (int i = 0; i < count; i++)
        {
            result.Add(buffer[(start + i) % buffer.Length]);
        }
        return result;
    }
}

#endif

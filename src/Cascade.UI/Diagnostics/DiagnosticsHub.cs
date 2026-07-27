using System;
using System.Diagnostics;
using System.Threading;

namespace Cascade.UI.Diagnostics;

/// <summary>
/// Always-on allocation + timing telemetry for the UI frame loop.
/// Maintains a lock-free ring buffer of the most recent <see cref="FrameStats"/>
/// samples. Designed for zero contention on the UI thread: per-frame record
/// operations are a handful of field writes. Consumers (MCP, test harnesses,
/// regression gates) pull snapshots via <see cref="GetRecent"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="GC.GetAllocatedBytesForCurrentThread"/> which is reliable
/// under NativeAOT (unlike <c>GC.GetGCMemoryInfo</c> and
/// <c>GC.CollectionCount</c> which return degraded data in published AOT
/// binaries). Because the call is per-thread, all record points must be
/// invoked on the UI thread — which is the contract for the frame loop.
/// </remarks>
public static class DiagnosticsHub
{
    private const int BufferSize = 240; // 4 seconds at 60 fps

    private static readonly FrameStats[] buffer = new FrameStats[BufferSize];
    private static int writeIndex;
    private static long frameCount;

    private static long frameStartBytes;
    private static long layoutStartBytes;
    private static long layoutEndBytes;
    private static long paintStartBytes;
    private static long paintEndBytes;

    private static long frameStartTimestamp;
    private static long layoutStartTimestamp;
    private static long layoutEndTimestamp;
    private static long paintStartTimestamp;
    private static long paintEndTimestamp;

    // Ad-hoc phase markers for narrowing down allocation sources.
    // Record via MarkPhase("name") and read summaries via GetPhaseSnapshot().
    //
    // Zero-allocation contract: the hot path (MarkPhase/EndPhase) must not
    // allocate, otherwise it contaminates the very measurements it reports.
    // We use a fixed-size struct array indexed by interned name slot instead
    // of a ConcurrentDictionary + Func closure.
    private const int MaxPhases = 32;

    private struct PhaseEntry
    {
        public string? Name;
        public long Bytes;
        public long Count;
    }

    private static readonly PhaseEntry[] phases = new PhaseEntry[MaxPhases];
    private static int phaseCount;
    private static long lastPhaseBytes;
    private static int lastPhaseSlot = -1;

    /// <summary>
    /// Returns the slot index for <paramref name="name"/>, allocating a new
    /// slot on first use. The <paramref name="name"/> string MUST be a stable
    /// reference (string literal or cached constant) — we compare by reference
    /// for zero-allocation lookup. Uses <see cref="string.Intern"/> as a safety
    /// net for non-literal callers.
    /// </summary>
    private static int GetOrAddSlot(string name)
    {
        // Linear scan — phaseCount is tiny (a handful in practice).
        // Reference equality is safe for literals; Intern covers the rest.
        int count = phaseCount;
        for (int i = 0; i < count; i++)
        {
            if (ReferenceEquals(phases[i].Name, name))
            {
                return i;
            }
        }

        // Slow path: try string equality (handles dynamic names).
        for (int i = 0; i < count; i++)
        {
            if (phases[i].Name == name)
            {
                return i;
            }
        }

        if (count >= MaxPhases)
        {
            return -1;
        }

        phases[count].Name = name;
        phaseCount = count + 1;
        return count;
    }

    /// <summary>
    /// Starts a named phase marker. On the next MarkPhase/EndPhase call, the
    /// allocation delta between the two points is added to that phase's total.
    /// UI-thread only. Use for ad-hoc narrowing of allocation sources.
    /// </summary>
    /// <remarks>
    /// Zero-allocation: no closures, no dictionary resize, no boxing.
    /// Pass string literals for the <paramref name="name"/> parameter to get
    /// reference-equality fast path.
    /// </remarks>
    public static void MarkPhase(string name)
    {
        long now = GC.GetAllocatedBytesForCurrentThread();
        int prevSlot = lastPhaseSlot;
        if (prevSlot >= 0)
        {
            long delta = now - lastPhaseBytes;
            phases[prevSlot].Bytes += delta;
            phases[prevSlot].Count++;
        }
        lastPhaseSlot = GetOrAddSlot(name);
        lastPhaseBytes = now;
    }

    /// <summary>Ends the current phase marker without starting a new one.</summary>
    public static void EndPhase()
    {
        int prevSlot = lastPhaseSlot;
        if (prevSlot >= 0)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            long delta = now - lastPhaseBytes;
            phases[prevSlot].Bytes += delta;
            phases[prevSlot].Count++;
        }
        lastPhaseSlot = -1;
    }

    /// <summary>Snapshot of phase totals as (name, total_bytes, call_count).</summary>
    public static System.Collections.Generic.IReadOnlyList<(string Name, long Bytes, long Count)> GetPhaseSnapshot()
    {
        int count = phaseCount;
        var list = new System.Collections.Generic.List<(string, long, long)>(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly var p = ref phases[i];
            if (p.Name is not null)
            {
                list.Add((p.Name, p.Bytes, p.Count));
            }
        }
        list.Sort(static (a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    /// <summary>Resets all phase totals.</summary>
    public static void ResetPhases()
    {
        for (int i = 0; i < phaseCount; i++)
        {
            phases[i] = default;
        }
        phaseCount = 0;
        lastPhaseSlot = -1;
    }

    /// <summary>Total number of frames recorded since process start.</summary>
    public static long TotalFrames => Interlocked.Read(ref frameCount);

    /// <summary>Size of the ring buffer (newest N samples retained).</summary>
    public static int Capacity => BufferSize;

    /// <summary>
    /// Marks the beginning of a frame tick. Records the baseline allocation
    /// counter for delta calculations.
    /// </summary>
    public static void BeginFrame()
    {
        frameStartBytes = GC.GetAllocatedBytesForCurrentThread();
        frameStartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Marks the start of the layout pass.</summary>
    public static void BeginLayout()
    {
        layoutStartBytes = GC.GetAllocatedBytesForCurrentThread();
        layoutStartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Marks the end of the layout pass.</summary>
    public static void EndLayout()
    {
        layoutEndBytes = GC.GetAllocatedBytesForCurrentThread();
        layoutEndTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Marks the start of the paint pass.</summary>
    public static void BeginPaint()
    {
        paintStartBytes = GC.GetAllocatedBytesForCurrentThread();
        paintStartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Marks the end of the paint pass.</summary>
    public static void EndPaint()
    {
        paintEndBytes = GC.GetAllocatedBytesForCurrentThread();
        paintEndTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Finalizes the frame, computes deltas, and writes a sample to the
    /// ring buffer. Must be called exactly once per <see cref="BeginFrame"/>
    /// on the same thread.
    /// </summary>
    public static void EndFrame()
    {
        long frameEndBytes = GC.GetAllocatedBytesForCurrentThread();
        long frameEndTimestamp = Stopwatch.GetTimestamp();

        long total = frameEndBytes - frameStartBytes;
        long layout = layoutEndBytes - layoutStartBytes;
        long paint = paintEndBytes - paintStartBytes;

        var stats = new FrameStats
        {
            FrameIndex = (ulong)Interlocked.Increment(ref frameCount),
            BeginTicks = frameStartTimestamp,
            TotalBytes = total,
            LayoutBytes = layout,
            PaintBytes = paint,
            FrameMs = (float)Stopwatch.GetElapsedTime(frameStartTimestamp, frameEndTimestamp).TotalMilliseconds,
            LayoutMs = (float)Stopwatch.GetElapsedTime(layoutStartTimestamp, layoutEndTimestamp).TotalMilliseconds,
            PaintMs = (float)Stopwatch.GetElapsedTime(paintStartTimestamp, paintEndTimestamp).TotalMilliseconds,
        };

        int idx = writeIndex % BufferSize;
        buffer[idx] = stats;
        writeIndex = idx + 1;
    }

    /// <summary>
    /// Copies up to <paramref name="count"/> most recent frame samples into
    /// <paramref name="destination"/> in chronological order (oldest first).
    /// Returns the number of samples actually written.
    /// </summary>
    public static int GetRecent(Span<FrameStats> destination, int count = BufferSize)
    {
        int available = (int)Math.Min(TotalFrames, BufferSize);
        int wanted = Math.Min(Math.Min(count, available), destination.Length);
        if (wanted <= 0)
        {
            return 0;
        }

        int currentWriteIndex = writeIndex;
        int start = (currentWriteIndex - wanted + BufferSize) % BufferSize;

        for (int i = 0; i < wanted; i++)
        {
            destination[i] = buffer[(start + i) % BufferSize];
        }

        return wanted;
    }

    /// <summary>
    /// Summary of allocation behavior over the last <paramref name="windowFrames"/>
    /// frames. Computes mean/max bytes per frame and extrapolated rate per second.
    /// </summary>
    public static AllocationSummary Summarize(int windowFrames = BufferSize)
    {
        Span<FrameStats> window = stackalloc FrameStats[BufferSize];
        int n = GetRecent(window, windowFrames);
        if (n <= 0)
        {
            return default;
        }

        long totalSum = 0, layoutSum = 0, paintSum = 0;
        long totalMax = 0, layoutMax = 0, paintMax = 0;
        float frameMsSum = 0f;

        for (int i = 0; i < n; i++)
        {
            ref readonly var s = ref window[i];
            totalSum += s.TotalBytes;
            layoutSum += s.LayoutBytes;
            paintSum += s.PaintBytes;
            if (s.TotalBytes > totalMax) { totalMax = s.TotalBytes; }
            if (s.LayoutBytes > layoutMax) { layoutMax = s.LayoutBytes; }
            if (s.PaintBytes > paintMax) { paintMax = s.PaintBytes; }
            frameMsSum += s.FrameMs;
        }

        float avgWorkMs = frameMsSum / n;

        // Real FPS from wall-clock span between first and last sample's BeginTicks.
        float realFps = 0f;
        float wallMs = 0f;
        if (n >= 2)
        {
            wallMs = (float)Stopwatch.GetElapsedTime(window[0].BeginTicks, window[n - 1].BeginTicks).TotalMilliseconds;
            if (wallMs > 0f)
            {
                realFps = (n - 1) * 1000f / wallMs;
            }
        }

        long bytesPerSecond = 0;
        if (wallMs > 0f)
        {
            // Allocation over the wall-clock window, extrapolated to 1 second.
            bytesPerSecond = (long)(totalSum * 1000.0 / wallMs);
        }

        return new AllocationSummary
        {
            FrameCount = n,
            MeanBytesPerFrame = totalSum / n,
            MeanLayoutBytesPerFrame = layoutSum / n,
            MeanPaintBytesPerFrame = paintSum / n,
            MaxBytesPerFrame = totalMax,
            MaxLayoutBytesPerFrame = layoutMax,
            MaxPaintBytesPerFrame = paintMax,
            EstimatedBytesPerSecond = bytesPerSecond,
            AverageFrameMs = avgWorkMs,
            EstimatedFps = realFps,
        };
    }
}

/// <summary>
/// Counts frames actually presented to the window, across every present path
/// (GPU scene, CPU-fallback blit through the GPU presenter, and the
/// no-presenter window blit). The render backend calls
/// <see cref="NotifyPresented"/> after each successful present; MCP tool
/// handlers block on <see cref="WaitForFrame"/> to make mutating tools
/// frame-synchronous (WP-3504).
///
/// Presents are invalidation-driven: an idle app presents rarely, and a
/// mutation with no visual effect may never present. A timed-out wait is
/// therefore a structured answer ("nothing repainted"), never an error, and
/// nothing here forces a present — that would mask invalidation bugs.
/// </summary>
internal static class PresentMonitor
{
    private static readonly object gate = new();
    private static long presentedFrames;
    private static long lastCapturedFrame;
    private static volatile bool cpuRenderActive;

    /// <summary>
    /// True when the most recently presented frame was produced by the CPU
    /// software-render fallback rather than the GPU presenter. CPU frames take
    /// an order of magnitude longer to produce (WP-3514: seconds, not
    /// milliseconds), so frame-synchronous MCP waits use a much larger timeout
    /// in this mode instead of spuriously reporting <c>timed_out</c> under load
    /// (WP-3516). Set by the render backend at each present.
    /// </summary>
    internal static bool CpuRenderActive
    {
        get => cpuRenderActive;
        set => cpuRenderActive = value;
    }

    /// <summary>Total frames presented since process start.</summary>
    internal static long PresentedFrames
    {
        get
        {
            lock (gate)
            {
                return presentedFrames;
            }
        }
    }

    /// <summary>
    /// Frame number whose pixels the capture buffer currently holds
    /// (the backend captures the frame it is about to present).
    /// Zero until the first capture.
    /// </summary>
    internal static long LastCapturedFrame
    {
        get
        {
            lock (gate)
            {
                return lastCapturedFrame;
            }
        }
    }

    /// <summary>
    /// Called by the render backend after each successful present, on the
    /// UI thread. Wakes every <see cref="WaitForFrame"/> waiter.
    /// </summary>
    internal static void NotifyPresented()
    {
        lock (gate)
        {
            presentedFrames++;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>
    /// Called by the render backend when it captures the frame it is about to
    /// present (capture precedes the present call on the UI thread, so the
    /// captured pixels belong to frame <c>PresentedFrames + 1</c>).
    /// </summary>
    internal static void MarkCapture()
    {
        lock (gate)
        {
            lastCapturedFrame = presentedFrames + 1;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>
    /// Blocks until a frame is captured to the CPU buffer beyond
    /// <paramref name="afterCaptured"/> (i.e. a present serviced a
    /// <c>RequestCapture()</c>), or <paramref name="timeout"/> elapses. Returns
    /// true if a fresh capture landed. MCP/CLI-thread only — never the UI thread.
    /// Capture is on-demand (a full GPU→CPU readback stalls the present pipeline,
    /// so it only runs when a screenshot is requested), so a caller must
    /// <c>RequestCapture()</c> and force a present before waiting here.
    /// </summary>
    internal static bool WaitForCapture(long afterCaptured, TimeSpan timeout)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        lock (gate)
        {
            while (lastCapturedFrame <= afterCaptured)
            {
                long remainingTicks = deadline - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0)
                {
                    return false;
                }

                Monitor.Wait(gate, TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency));
            }

            return true;
        }
    }

    /// <summary>
    /// Blocks until at least <paramref name="targetFrame"/> frames have been
    /// presented, or <paramref name="timeout"/> elapses. Returns the
    /// presented-frame count at return time and whether the wait timed out.
    /// Call from MCP handler threads only — never from the UI thread, which
    /// is the thread that presents.
    /// </summary>
    internal static (long PresentedFrame, bool TimedOut) WaitForFrame(long targetFrame, TimeSpan timeout)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        lock (gate)
        {
            while (presentedFrames < targetFrame)
            {
                long remainingTicks = deadline - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0)
                {
                    return (presentedFrames, true);
                }

                Monitor.Wait(gate, TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency));
            }

            return (presentedFrames, false);
        }
    }

    /// <summary>
    /// Blocks until the render is "settled": at least one frame has presented and
    /// no further present occurs for a full <paramref name="quiescence"/> window
    /// (the initial layout/paint burst has finished), or <paramref name="timeout"/>
    /// elapses. Returns true if a settled, real frame is available. This lets a
    /// screenshot taken without an explicit target frame avoid capturing the blank
    /// initial window or a mid-burst intermediate frame (WP-3528). MCP-thread only.
    /// </summary>
    internal static bool WaitForSettle(TimeSpan quiescence, TimeSpan timeout)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        lock (gate)
        {
            while (true)
            {
                long before = presentedFrames;
                long remainingTicks = deadline - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0)
                {
                    return before > 0; // timed out: usable only if at least one frame exists
                }

                double remainingSeconds = (double)remainingTicks / Stopwatch.Frequency;
                var waitSpan = TimeSpan.FromSeconds(Math.Min(quiescence.TotalSeconds, remainingSeconds));

                // Monitor.Wait returns true if pulsed (a present occurred), false on
                // timeout (a full quiet window with no present).
                bool pulsed = Monitor.Wait(gate, waitSpan);
                if (!pulsed && before > 0 && presentedFrames == before)
                {
                    return true; // ≥1 frame and a full quiescence window with no new present
                }
            }
        }
    }
}

/// <summary>Aggregated view of frame allocation behavior over a window.</summary>
public readonly struct AllocationSummary
{
    public int FrameCount { get; init; }
    public long MeanBytesPerFrame { get; init; }
    public long MeanLayoutBytesPerFrame { get; init; }
    public long MeanPaintBytesPerFrame { get; init; }
    public long MaxBytesPerFrame { get; init; }
    public long MaxLayoutBytesPerFrame { get; init; }
    public long MaxPaintBytesPerFrame { get; init; }
    public long EstimatedBytesPerSecond { get; init; }
    public float AverageFrameMs { get; init; }
    public float EstimatedFps { get; init; }
}

namespace Cascade.UI.Diagnostics;

/// <summary>
/// Per-frame allocation and timing sample recorded by
/// <see cref="DiagnosticsHub"/>. Values are the delta for a single tick of
/// the UI frame loop.
/// </summary>
/// <remarks>
/// Byte counts come from <see cref="System.GC.GetAllocatedBytesForCurrentThread"/>
/// which is reliable under NativeAOT. They reflect allocations on the UI
/// thread only — native allocations made via FFI are tracked separately in
/// <see cref="NativeMemoryCounters"/>.
/// </remarks>
public readonly struct FrameStats
{
    /// <summary>Monotonically increasing frame index.</summary>
    public ulong FrameIndex { get; init; }

    /// <summary>UTC timestamp (ticks) when the frame began.</summary>
    public long BeginTicks { get; init; }

    /// <summary>Bytes allocated on the UI thread during this frame, total.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Bytes allocated during the layout pass.</summary>
    public long LayoutBytes { get; init; }

    /// <summary>Bytes allocated during the paint pass.</summary>
    public long PaintBytes { get; init; }

    /// <summary>
    /// Bytes allocated outside layout/paint (input, animation, reactive
    /// reconcile, render tree rebuild, diagnostics overhead).
    /// </summary>
    public long OtherBytes => TotalBytes - LayoutBytes - PaintBytes;

    /// <summary>Total wall-clock duration of the frame in milliseconds.</summary>
    public float FrameMs { get; init; }

    /// <summary>Layout pass duration in milliseconds.</summary>
    public float LayoutMs { get; init; }

    /// <summary>Paint pass duration in milliseconds.</summary>
    public float PaintMs { get; init; }
}

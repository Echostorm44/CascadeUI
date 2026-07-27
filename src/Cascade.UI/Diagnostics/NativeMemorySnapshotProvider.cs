using System;

namespace Cascade.UI.Diagnostics;

/// <summary>
/// On-demand snapshot of native-side memory usage produced by the active
/// render backend. All byte fields are <see cref="ulong"/>.
/// </summary>
/// <remarks>
/// This is a DevTools-only reporting surface. It is not part of the
/// framework's render hot path and must not be called during frame
/// production. Populated by the backend from <c>cascade_memory_stats</c>
/// in the native Etch library. Fields backed by <c>wgpu/counters</c>
/// (a Rust-side Cargo feature, gated on <c>CASCADE_DEVTOOLS</c> on the
/// C# side) return 0 when the feature is disabled; check
/// <see cref="CountersFeatureEnabled"/> before treating those fields
/// as authoritative.
/// </remarks>
public readonly record struct NativeMemorySnapshot
{
    /// <summary>ABI version of the native snapshot struct. Currently 1.</summary>
    public uint Version { get; init; }

    /// <summary>True when the native library was built with wgpu/counters.</summary>
    public bool CountersFeatureEnabled { get; init; }

    /// <summary>True when wgpu's allocator report is available on this backend.</summary>
    public bool AllocatorReportAvailable { get; init; }

    // ── Native handle-map populations ─────────────────────────────────
    /// <summary>Devices currently held in the native device map.</summary>
    public ulong DeviceCount { get; init; }

    /// <summary>Surfaces currently held in the native surface map.</summary>
    public ulong SurfaceCount { get; init; }

    /// <summary>Fonts currently held in the native font map.</summary>
    public ulong FontCount { get; init; }

    /// <summary>Images currently held in the native image map.</summary>
    public ulong ImageCount { get; init; }

    /// <summary>Compiled paths currently held in the native path map.</summary>
    public ulong PathCount { get; init; }

    /// <summary>Scene frames currently in flight on the native side.</summary>
    public ulong SceneFrameCount { get; init; }

    // ── CPU-side byte totals tracked in Rust ──────────────────────────
    /// <summary>Total bytes of font file data held in native font handles.</summary>
    public ulong FontDataBytes { get; init; }

    /// <summary>Total bytes of pixel data held in native image handles.</summary>
    public ulong ImagePixelBytes { get; init; }

    /// <summary>Total bytes of path element data held in native path handles.</summary>
    public ulong PathElementBytes { get; init; }

    // ── GPU surface estimates ─────────────────────────────────────────
    /// <summary>Estimated bytes for the intermediate render target (width*height*4).</summary>
    public ulong SurfaceIntermediateBytes { get; init; }

    /// <summary>Estimated bytes for the swapchain backbuffers (frame latency * width * height * bpp).</summary>
    public ulong SurfaceSwapchainBytesEst { get; init; }

    // ── wgpu allocator report ─────────────────────────────────────────
    /// <summary>Total bytes reported allocated by wgpu's allocator.</summary>
    public ulong WgpuAllocatedBytes { get; init; }

    /// <summary>Total bytes reserved (not necessarily in use) by wgpu's allocator.</summary>
    public ulong WgpuReservedBytes { get; init; }

    /// <summary>Number of memory blocks held by wgpu's allocator.</summary>
    public ulong WgpuMemoryBlocks { get; init; }

    /// <summary>Number of live allocations held by wgpu's allocator.</summary>
    public ulong WgpuLiveAllocations { get; init; }

    // ── wgpu internal counters (require wgpu/counters) ────────────────
    /// <summary>Buffer memory bytes reported by wgpu HAL counters. 0 without wgpu/counters.</summary>
    public ulong WgpuBufferMemoryBytes { get; init; }

    /// <summary>Texture memory bytes reported by wgpu HAL counters. 0 without wgpu/counters.</summary>
    public ulong WgpuTextureMemoryBytes { get; init; }

    /// <summary>Live buffer objects. 0 without wgpu/counters.</summary>
    public ulong WgpuBuffers { get; init; }

    /// <summary>Live texture objects. 0 without wgpu/counters.</summary>
    public ulong WgpuTextures { get; init; }

    /// <summary>Live texture view objects. 0 without wgpu/counters.</summary>
    public ulong WgpuTextureViews { get; init; }

    /// <summary>Live bind group objects. 0 without wgpu/counters.</summary>
    public ulong WgpuBindGroups { get; init; }

    /// <summary>Live render pipeline objects. 0 without wgpu/counters.</summary>
    public ulong WgpuRenderPipelines { get; init; }

    /// <summary>Live compute pipeline objects. 0 without wgpu/counters.</summary>
    public ulong WgpuComputePipelines { get; init; }

    /// <summary>Live shader module objects. 0 without wgpu/counters.</summary>
    public ulong WgpuShaderModules { get; init; }
}

/// <summary>
/// Registry for the active render backend's native memory snapshot provider.
/// The backend registers a callback during initialization; diagnostic
/// tooling calls <see cref="TrySnapshot"/> to get a current reading.
/// </summary>
/// <remarks>
/// Designed to preserve assembly layering — <c>Cascade.UI</c> does not
/// reference the backend, so the backend pushes its P/Invoke-backed
/// implementation up through this provider rather than exposing native
/// types publicly.
/// </remarks>
public static class NativeMemorySnapshotProvider
{
    private static Func<NativeMemorySnapshot?>? provider;

    /// <summary>
    /// Registers the backend's snapshot provider. Called by the backend
    /// during initialization. Passing <c>null</c> clears the registration.
    /// </summary>
    public static void Register(Func<NativeMemorySnapshot?>? snapshotProvider)
    {
        provider = snapshotProvider;
    }

    /// <summary>
    /// Attempts to produce a snapshot of native memory usage. Returns
    /// <c>false</c> when no backend is registered or the backend reports
    /// no data (for example, before any device has been created).
    /// </summary>
    public static bool TrySnapshot(out NativeMemorySnapshot snapshot)
    {
        Func<NativeMemorySnapshot?>? p = provider;
        if (p is null)
        {
            snapshot = default;
            return false;
        }

        NativeMemorySnapshot? result = p();
        if (result is null)
        {
            snapshot = default;
            return false;
        }

        snapshot = result.Value;
        return true;
    }
}

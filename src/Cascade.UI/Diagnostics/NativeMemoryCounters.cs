using System.Threading;

namespace Cascade.UI.Diagnostics;

/// <summary>
/// Monotonic counters for native resources held by the render backend
/// (<c>EtchBackend</c>), incremented on creation and decremented on destruction
/// so we can detect unbounded growth of GPU-side handles (compiled paths,
/// images, fonts).
/// </summary>
/// <remarks>
/// All counters use <see cref="Interlocked"/> so they are safe to update
/// from any thread with effectively zero overhead. Live counts should
/// stabilize at steady state — any monotonic growth indicates a leak.
/// </remarks>
public static class NativeMemoryCounters
{
    private static long compiledPathsLive;
    private static long compiledPathsTotalCreated;
    private static long compiledPathsTotalDestroyed;

    private static long imagesLive;
    private static long imagesTotalCreated;
    private static long imagesTotalDestroyed;

    private static long fontsLive;
    private static long fontsTotalCreated;
    private static long fontsTotalDestroyed;

    /// <summary>Currently alive compiled paths on the GPU.</summary>
    public static long CompiledPathsLive => Interlocked.Read(ref compiledPathsLive);

    /// <summary>Total compiled paths ever created (monotonic).</summary>
    public static long CompiledPathsTotalCreated => Interlocked.Read(ref compiledPathsTotalCreated);

    /// <summary>Total compiled paths ever destroyed (monotonic).</summary>
    public static long CompiledPathsTotalDestroyed => Interlocked.Read(ref compiledPathsTotalDestroyed);

    /// <summary>Currently alive GPU images.</summary>
    public static long ImagesLive => Interlocked.Read(ref imagesLive);

    /// <summary>Total GPU images ever uploaded (monotonic).</summary>
    public static long ImagesTotalCreated => Interlocked.Read(ref imagesTotalCreated);

    /// <summary>Total GPU images ever destroyed (monotonic).</summary>
    public static long ImagesTotalDestroyed => Interlocked.Read(ref imagesTotalDestroyed);

    /// <summary>Currently loaded fonts in the backend.</summary>
    public static long FontsLive => Interlocked.Read(ref fontsLive);

    /// <summary>Total fonts ever loaded (monotonic).</summary>
    public static long FontsTotalCreated => Interlocked.Read(ref fontsTotalCreated);

    /// <summary>Total fonts ever unloaded (monotonic).</summary>
    public static long FontsTotalDestroyed => Interlocked.Read(ref fontsTotalDestroyed);

    internal static void CompiledPathCreated()
    {
        Interlocked.Increment(ref compiledPathsLive);
        Interlocked.Increment(ref compiledPathsTotalCreated);
    }

    internal static void CompiledPathDestroyed()
    {
        Interlocked.Decrement(ref compiledPathsLive);
        Interlocked.Increment(ref compiledPathsTotalDestroyed);
    }

    internal static void ImageCreated()
    {
        Interlocked.Increment(ref imagesLive);
        Interlocked.Increment(ref imagesTotalCreated);
    }

    internal static void ImageDestroyed()
    {
        Interlocked.Decrement(ref imagesLive);
        Interlocked.Increment(ref imagesTotalDestroyed);
    }

    internal static void FontCreated()
    {
        Interlocked.Increment(ref fontsLive);
        Interlocked.Increment(ref fontsTotalCreated);
    }

    internal static void FontDestroyed()
    {
        Interlocked.Decrement(ref fontsLive);
        Interlocked.Increment(ref fontsTotalDestroyed);
    }
}

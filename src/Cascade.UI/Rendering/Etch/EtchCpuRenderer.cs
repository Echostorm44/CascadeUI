using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Etch.Compose;
using Etch.Compose.Cpu;
using Cascade.UI.Diagnostics;

namespace Cascade.UI.Backend.Etch;

/// <summary>
/// Renders recorded frames on the CPU at native resolution with Etch's <see cref="CpuComposer"/> —
/// the same draw list the GPU path executes, so the two agree to within the parity tolerance —
/// and presents them: through the GPU presenter when there is one (BGRA upload of the damaged
/// rects), else straight to the window with GDI (<c>SetDIBitsToDevice</c> of the damaged rects).
/// </summary>
/// <remarks>
/// Frames render incrementally: only the 64×64 tiles whose draws changed are rendered and
/// presented, so a caret blink touches a tile or two. <see cref="Release"/> drops the framebuffer
/// and the composer's frame buffers while the window is hidden; the next frame renders in full.
/// </remarks>
internal sealed partial class EtchCpuRenderer : IDisposable
{
    private readonly CpuComposer _composer = new();
    private readonly DrawListBuilder _builder = new();
    private readonly DrawList _drawList = new();
    private readonly CpuFramebuffer _framebuffer = new();
    private readonly List<PlacedGlyph> _placedGlyphs = new();
    private CpuDirtyRect[] _dirty = new CpuDirtyRect[1];
    // Render runs on the UI thread; MCP captures read the framebuffer from another. Both, and
    // Release, hold this so a capture never reads a half-rendered or released frame.
    private readonly System.Threading.Lock _frameGate = new();
    private readonly bool _forceAtlasReset;
    private int _dirtyCount;
    private bool _presentAll = true;
    private bool _disposed;

    /// <summary>The framebuffer holding the last rendered frame (BGRA, sRGB-encoded).</summary>
    public CpuFramebuffer Framebuffer => _framebuffer;

    /// <summary>The last frame's draw list (tests and diagnostics).</summary>
    internal DrawList DrawList => _drawList;

    /// <summary>The rects the last <see cref="Render"/> changed.</summary>
    public ReadOnlySpan<CpuDirtyRect> Dirty => _dirty.AsSpan(0, _dirtyCount);

    /// <summary>Milliseconds the last <see cref="Render"/> took (build + composite).</summary>
    public double LastRenderMs { get; private set; }

    /// <summary>Milliseconds the last <see cref="Render"/> spent building the draw list.</summary>
    public double LastBuildMs { get; private set; }

    /// <summary>Pixels the last <see cref="Render"/> re-rendered.</summary>
    public long LastDamagedPixels { get; private set; }

    /// <summary>
    /// Creates a renderer. The debug switches default to the environment's
    /// (<c>CASCADE_SKIP_GLYPHS</c>, <c>CASCADE_FORCE_ATLAS_RESET</c>), as on the GPU path.
    /// </summary>
    public EtchCpuRenderer(bool? skipGlyphs = null, bool? forceAtlasReset = null)
    {
        _composer.SkipGlyphs = skipGlyphs ?? RenderDebugSwitches.SkipGlyphs;
        _forceAtlasReset = forceAtlasReset ?? RenderDebugSwitches.ForceAtlasReset;
    }

    /// <summary>The glyph atlas side in texels (MCP <c>atlas_dimension</c>).</summary>
    public int AtlasDimension => _composer.MonoAtlas.Dimension;

    /// <summary>The glyph atlas's reset generation (tests).</summary>
    internal int MonoAtlasGeneration => _composer.MonoAtlas.Generation;

    /// <summary>The next present sends the whole frame (the window surface was lost or uncovered).</summary>
    public void InvalidatePresentation() => _presentAll = true;

    /// <summary>
    /// Renders <paramref name="main"/> at <paramref name="width"/> × <paramref name="height"/> into
    /// the framebuffer, re-rendering only the changed tiles. Glyph placements are reported to
    /// <paramref name="glyphRecords"/> when given (draw provenance).
    /// </summary>
    public void Render(DrawRecording main, ComposeParameters parameters, uint width, uint height,
        List<GlyphDrawRecord>? glyphRecords, IReadOnlyDictionary<ulong, int>? layerCompositeIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_frameGate)
        {
            RenderLocked(main, parameters, width, height, glyphRecords, layerCompositeIndex);
        }
    }

    private void RenderLocked(DrawRecording main, ComposeParameters parameters, uint width, uint height,
        List<GlyphDrawRecord>? glyphRecords, IReadOnlyDictionary<ulong, int>? layerCompositeIndex)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        _composer.ResetAtlasesIfExhausted(_forceAtlasReset);
        _placedGlyphs.Clear();
        _builder.Begin(_drawList, width, height, _composer.Masks, _composer.MonoAtlas, _composer.ColorAtlas,
            glyphRecords is null ? null : _placedGlyphs);
        _builder.Replay(main);
        _builder.End();
        _drawList.Parameters = parameters;
        if (glyphRecords is not null)
        {
            GlyphProvenance.Report(_placedGlyphs, _drawList, glyphRecords, layerCompositeIndex);
        }

        LastBuildMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        ReadOnlySpan<CpuDirtyRect> dirty = _composer.RenderIncremental(_drawList, _framebuffer);
        if (_presentAll)
        {
            // The whole frame, in the (never empty) damage array: no per-frame allocation.
            _dirty[0] = new CpuDirtyRect(0, 0, _framebuffer.Width, _framebuffer.Height);
            dirty = _dirty.AsSpan(0, 1);
            _presentAll = false;
        }
        if (_dirty.Length < dirty.Length)
        {
            _dirty = new CpuDirtyRect[Math.Max(dirty.Length, _dirty.Length * 2)];
        }
        dirty.CopyTo(_dirty);
        _dirtyCount = dirty.Length;
        long pixels = 0;
        foreach (var rect in dirty)
        {
            pixels += (long)rect.Width * rect.Height;
        }
        LastDamagedPixels = pixels;
        LastRenderMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>Copies the whole last frame to the window with GDI (WM_PAINT). False when GDI failed.</summary>
    public bool BlitAll(nint hwnd)
    {
        Span<CpuDirtyRect> all = [new CpuDirtyRect(0, 0, _framebuffer.Width, _framebuffer.Height)];
        bool ok = Blit(hwnd, all);
        if (!ok)
        {
            _presentAll = true;
        }
        return ok;
    }

    /// <summary>
    /// Copies the last frame's damaged rects to the window with GDI. When any part fails the damage
    /// is not lost: the next frame presents the whole framebuffer. Returns false on failure.
    /// </summary>
    public bool BlitToWindow(nint hwnd)
    {
        bool ok = Blit(hwnd, Dirty);
        if (!ok)
        {
            _presentAll = true;
        }
        return ok;
    }

    /// <summary>
    /// The frame's damage was not presented (the swapchain could not be acquired, say): the next
    /// frame presents the whole framebuffer, so no rect is left stale.
    /// </summary>
    public void PresentationFailed() => _presentAll = true;

    private unsafe bool Blit(nint hwnd, ReadOnlySpan<CpuDirtyRect> rects)
    {
        if (rects.IsEmpty || _framebuffer.Width == 0)
        {
            return true;
        }
        nint hdc = GetDC(hwnd);
        if (hdc == 0)
        {
            DebugLog.Write(DebugLogCategory.Present,
                $"[{DateTime.Now:O}] GetDC failed: {Marshal.GetLastPInvokeError()}");
            return false;
        }
        bool ok = true;
        try
        {
            int width = _framebuffer.Width;
            // The framebuffer's 0xAARRGGBB words are BGRA bytes: a top-down 32-bpp DIB as is. Each
            // rect is sent as its own DIB of the rect's rows (stride = the frame width), source
            // origin (rect.X, 0): SetDIBitsToDevice's source y is measured from the DIB's bottom
            // even when it is top-down, so a whole-frame DIB with ySrc = rect.Y would mirror rows.
            fixed (uint* pixels = _framebuffer.Pixels)
            {
                foreach (var rect in rects)
                {
                    var info = new BITMAPINFOHEADER
                    {
                        biSize = (uint)sizeof(BITMAPINFOHEADER),
                        biWidth = width,
                        biHeight = -rect.Height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0,
                    };
                    if (SetDIBitsToDevice(hdc, rect.X, rect.Y, (uint)rect.Width, (uint)rect.Height,
                        rect.X, 0, 0, (uint)rect.Height, pixels + (long)rect.Y * width, &info, 0) == 0)
                    {
                        ok = false;
                        DebugLog.Write(DebugLogCategory.Present,
                            $"[{DateTime.Now:O}] SetDIBitsToDevice failed: {Marshal.GetLastPInvokeError()}");
                    }
                }
            }
        }
        finally
        {
            if (ReleaseDC(hwnd, hdc) == 0)
            {
                DebugLog.Write(DebugLogCategory.Present, $"[{DateTime.Now:O}] ReleaseDC failed");
            }
        }
        return ok;
    }
    /// <summary>
    /// A rect of the monochrome glyph atlas as RGBA (coverage as gray, alpha 255), rows flipped
    /// upright — the CPU twin of <see cref="EtchGpuPresenter.CaptureAtlasRegion"/>. Width/height of
    /// 0 mean "to the atlas edge".
    /// </summary>
    public AtlasRegionCapture? CaptureAtlasRegion(int u, int v, int width, int height)
    {
        lock (_frameGate)
        {
            return CaptureAtlasRegionLocked(u, v, width, height);
        }
    }

    private AtlasRegionCapture? CaptureAtlasRegionLocked(int u, int v, int width, int height)
    {
        var atlas = _composer.MonoAtlas;
        var page = atlas.GetPage(0).Pixels;
        if (page is null)
        {
            return null;
        }
        int dim = atlas.Dimension;
        int rx = Math.Clamp(u, 0, dim - 1);
        int ry = Math.Clamp(v, 0, dim - 1);
        int rw = width <= 0 ? dim - rx : Math.Clamp(width, 1, dim - rx);
        int rh = height <= 0 ? dim - ry : Math.Clamp(height, 1, dim - ry);
        var pixels = new byte[rw * rh * 4];
        for (int row = 0; row < rh; row++)
        {
            // Glyph bitmaps are stored bottom-up; flip so a captured glyph reads upright.
            int sourceRow = ry + (rh - 1 - row);
            for (int col = 0; col < rw; col++)
            {
                byte coverage = page[sourceRow * dim + rx + col];
                int offset = (row * rw + col) * 4;
                pixels[offset] = coverage;
                pixels[offset + 1] = coverage;
                pixels[offset + 2] = coverage;
                pixels[offset + 3] = 255;
            }
        }
        var image = new ImageData { Pixels = pixels, Width = rw, Height = rh, Stride = rw * 4 };
        return new AtlasRegionCapture(image, dim, rx, ry, rw, rh);
    }

    /// <summary>The last frame as RGBA at native resolution, or null before the first frame.</summary>
    public ImageData? CaptureFrame()
    {
        lock (_frameGate)
        {
            return CaptureFrameLocked();
        }
    }

    private ImageData? CaptureFrameLocked()
    {
        if (_framebuffer.Width == 0 || _framebuffer.Height == 0)
        {
            return null;
        }
        var rgba = new byte[_framebuffer.Width * _framebuffer.Height * 4];
        _framebuffer.CopyToRgba(rgba);
        return new ImageData
        {
            Pixels = rgba,
            Width = _framebuffer.Width,
            Height = _framebuffer.Height,
            Stride = _framebuffer.Width * 4,
        };
    }

    /// <summary>
    /// Frees the framebuffer, the draw list (and the images it references), the composer's frame
    /// buffers and its mask pages (the window is hidden). The glyph atlases stay: they are what
    /// makes the next frame fast.
    /// </summary>
    public void Release()
    {
        lock (_frameGate)
        {
            _framebuffer.Release();
            _drawList.Trim();
            _placedGlyphs.Clear();
            _placedGlyphs.TrimExcess();
            _composer.Trim();
            _composer.Masks.Trim();
            _dirtyCount = 0;
            _presentAll = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _composer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static unsafe partial int SetDIBitsToDevice(nint hdc, int xDest, int yDest, uint width, uint height,
        int xSrc, int ySrc, uint startScan, uint scanLines, void* bits, BITMAPINFOHEADER* info, uint colorUse);
}

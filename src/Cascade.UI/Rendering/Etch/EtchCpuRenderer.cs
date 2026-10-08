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
    private CpuDirtyRect[] _dirty = Array.Empty<CpuDirtyRect>();
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
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        _composer.ResetAtlasesIfExhausted(force: false);
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
        var dirty = _composer.RenderIncremental(_drawList, _framebuffer);
        if (_presentAll)
        {
            dirty = new[] { new CpuDirtyRect(0, 0, _framebuffer.Width, _framebuffer.Height) };
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

    /// <summary>Copies the whole last frame to the window with GDI (WM_PAINT).</summary>
    public void BlitAll(nint hwnd)
    {
        Blit(hwnd, [new CpuDirtyRect(0, 0, _framebuffer.Width, _framebuffer.Height)]);
    }

    /// <summary>Copies the last frame's damaged rects to the window with GDI.</summary>
    public void BlitToWindow(nint hwnd) => Blit(hwnd, Dirty);

    private unsafe void Blit(nint hwnd, ReadOnlySpan<CpuDirtyRect> rects)
    {
        if (rects.IsEmpty || _framebuffer.Width == 0)
        {
            return;
        }
        nint hdc = GetDC(hwnd);
        if (hdc == 0)
        {
            return;
        }
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
                        DebugLog.Write(DebugLogCategory.Present,
                            $"[{DateTime.Now:O}] SetDIBitsToDevice failed: {Marshal.GetLastPInvokeError()}");
                    }
                }
            }
        }
        finally
        {
            _ = ReleaseDC(hwnd, hdc);
        }
    }

    /// <summary>The last frame as RGBA at native resolution, or null before the first frame.</summary>
    public ImageData? CaptureFrame()
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
    /// Frees the framebuffer, the composer's frame buffers and its mask pages (the window is
    /// hidden). The glyph atlases stay: they are what makes the next frame fast.
    /// </summary>
    public void Release()
    {
        _framebuffer.Release();
        _composer.Trim();
        _composer.Masks.Trim();
        _presentAll = true;
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

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static unsafe partial int SetDIBitsToDevice(nint hdc, int xDest, int yDest, uint width, uint height,
        int xSrc, int ySrc, uint startScan, uint scanLines, void* bits, BITMAPINFOHEADER* info, uint colorUse);
}

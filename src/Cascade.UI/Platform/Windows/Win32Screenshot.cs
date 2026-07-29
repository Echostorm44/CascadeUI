using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Captures the client area of a Win32 window as raw RGBA pixel data.
/// Uses PrintWindow + GDI to read back window content including GPU-rendered surfaces.
/// </summary>
internal static class Win32Screenshot
{
    /// <summary>
    /// Captures the client area of the given window handle.
    /// Returns null if the capture fails.
    /// </summary>
    internal static ImageData? Capture(nint hWnd)
    {
        if (hWnd == nint.Zero)
        {
            return null;
        }

        if (!Win32.GetClientRect(hWnd, out var rect))
        {
            return null;
        }

        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        nint hdcWindow = Win32.GetDC(hWnd);
        if (hdcWindow == nint.Zero)
        {
            return null;
        }

        nint hdcMem = nint.Zero;
        nint hBitmap = nint.Zero;
        try
        {
            hdcMem = Win32.CreateCompatibleDC(hdcWindow);
            if (hdcMem == nint.Zero)
            {
                return null;
            }

            hBitmap = Win32.CreateCompatibleBitmap(hdcWindow, width, height);
            if (hBitmap == nint.Zero)
            {
                return null;
            }

            nint oldBmp = Win32.SelectObject(hdcMem, hBitmap);

            // PrintWindow with PW_RENDERFULLCONTENT captures DWM-composed content
            bool captured = Win32.PrintWindow(hWnd, hdcMem, Win32.PW_CLIENTONLY | Win32.PW_RENDERFULLCONTENT);
            if (!captured)
            {
                // Fallback to BitBlt from screen DC
                Win32.BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, Win32.SRCCOPY);
            }

            Win32.SelectObject(hdcMem, oldBmp);

            // Read back pixels via GetDIBits (bottom-up BGR → top-down RGBA)
            var bmi = new Win32.BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // negative = top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];

            nint pPixels = Marshal.AllocHGlobal(stride * height);
            try
            {
                int lines = Win32.GetDIBits(hdcMem, hBitmap, 0, (uint)height, pPixels, ref bmi, Win32.DIB_RGB_COLORS);
                if (lines == 0)
                {
                    return null;
                }

                Marshal.Copy(pPixels, pixels, 0, pixels.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(pPixels);
            }

            // Convert BGRA → RGBA in-place
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                pixels[i] = pixels[i + 2];     // R
                pixels[i + 2] = b;             // B
                pixels[i + 3] = 255;           // A (force opaque since GDI doesn't preserve alpha)
            }

            return new ImageData { Pixels = pixels, Width = width, Height = height, Stride = stride };
        }
        finally
        {
            if (hBitmap != nint.Zero)
            {
                Win32.DeleteObject(hBitmap);
            }
            if (hdcMem != nint.Zero)
            {
                Win32.DeleteDC(hdcMem);
            }
            _ = Win32.ReleaseDC(hWnd, hdcWindow);
        }
    }

    /// <summary>
    /// Crops an ImageData to the specified rectangle.
    /// Returns null if the region is entirely outside the image bounds.
    /// Clamps the region to image bounds if partially outside.
    /// </summary>
    internal static ImageData? CropRegion(ImageData image, int x, int y, int cropWidth, int cropHeight)
    {
        // Clamp to image bounds
        int x1 = Math.Max(0, x);
        int y1 = Math.Max(0, y);
        int x2 = Math.Min(image.Width, x + cropWidth);
        int y2 = Math.Min(image.Height, y + cropHeight);

        int w = x2 - x1;
        int h = y2 - y1;
        if (w <= 0 || h <= 0)
        {
            return null;
        }

        int newStride = w * 4;
        byte[] newPixels = new byte[newStride * h];

        for (int row = 0; row < h; row++)
        {
            int srcOffset = (y1 + row) * image.Stride + x1 * 4;
            int dstOffset = row * newStride;
            Buffer.BlockCopy(image.Pixels, srcOffset, newPixels, dstOffset, newStride);
        }

        return new ImageData { Pixels = newPixels, Width = w, Height = h, Stride = newStride };
    }

    /// <summary>
    /// Scales an ImageData by the given factor using nearest-neighbor interpolation.
    /// Scale > 1 enlarges (useful for zoomed inspection), scale &lt; 1 shrinks.
    /// </summary>
    internal static ImageData ScaleImage(ImageData image, double scale)
    {
        if (Math.Abs(scale - 1.0) < 0.001)
        {
            return image;
        }

        int newWidth = Math.Max(1, (int)(image.Width * scale));
        int newHeight = Math.Max(1, (int)(image.Height * scale));
        int newStride = newWidth * 4;
        byte[] newPixels = new byte[newStride * newHeight];

        for (int dy = 0; dy < newHeight; dy++)
        {
            int sy = Math.Min((int)(dy / scale), image.Height - 1);
            for (int dx = 0; dx < newWidth; dx++)
            {
                int sx = Math.Min((int)(dx / scale), image.Width - 1);
                int srcOffset = sy * image.Stride + sx * 4;
                int dstOffset = dy * newStride + dx * 4;
                newPixels[dstOffset]     = image.Pixels[srcOffset];
                newPixels[dstOffset + 1] = image.Pixels[srcOffset + 1];
                newPixels[dstOffset + 2] = image.Pixels[srcOffset + 2];
                newPixels[dstOffset + 3] = image.Pixels[srcOffset + 3];
            }
        }

        return new ImageData { Pixels = newPixels, Width = newWidth, Height = newHeight, Stride = newStride };
    }

    /// <summary>
    /// Samples pixel data at a specific point. Returns RGBA values.
    /// Returns null if the point is outside the image.
    /// </summary>
    internal static (byte r, byte g, byte b, byte a)? SamplePixel(ImageData image, int x, int y)
    {
        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
        {
            return null;
        }

        int offset = y * image.Stride + x * 4;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2], image.Pixels[offset + 3]);
    }

    /// <summary>
    /// Compares two ImageData of the same dimensions pixel-by-pixel.
    /// Returns diff statistics: total different pixels, max channel difference,
    /// the bounding box of the whole changed region, and per-region bounding
    /// rects (8-connected components of changed pixels, largest first, capped
    /// at <see cref="DiffResult.MaxRects"/>). Tolerance is per-channel threshold.
    /// </summary>
    internal static DiffResult CompareImages(ImageData a, ImageData b, int tolerance = 2)
    {
        var result = new DiffResult();

        if (a.Width != b.Width || a.Height != b.Height)
        {
            result.SizeMismatch = true;
            result.BaselineSize = (a.Width, a.Height);
            result.CurrentSize = (b.Width, b.Height);
            return result;
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // Connected components over changed pixels via row runs + union-find:
        // a run of consecutive changed pixels joins every run on the previous
        // row it touches (8-connectivity → ranges may meet diagonally).
        var parents = new List<int>();
        var rects = new List<(int MinX, int MinY, int MaxX, int MaxY)>();
        var prevRuns = new List<(int Start, int End, int Label)>();
        var curRuns = new List<(int Start, int End, int Label)>();

        int Find(int label)
        {
            while (parents[label] != label)
            {
                parents[label] = parents[parents[label]];
                label = parents[label];
            }
            return label;
        }

        void Union(int x, int y)
        {
            int rootX = Find(x);
            int rootY = Find(y);
            if (rootX == rootY)
            {
                return;
            }
            parents[rootY] = rootX;
            var rx = rects[rootX];
            var ry = rects[rootY];
            rects[rootX] = (
                Math.Min(rx.MinX, ry.MinX), Math.Min(rx.MinY, ry.MinY),
                Math.Max(rx.MaxX, ry.MaxX), Math.Max(rx.MaxY, ry.MaxY));
        }

        for (int y = 0; y < a.Height; y++)
        {
            curRuns.Clear();
            int runStart = -1;

            for (int x = 0; x <= a.Width; x++)
            {
                bool changed = false;
                if (x < a.Width)
                {
                    int offset = y * a.Stride + x * 4;
                    int dr = Math.Abs(a.Pixels[offset] - b.Pixels[offset]);
                    int dg = Math.Abs(a.Pixels[offset + 1] - b.Pixels[offset + 1]);
                    int db = Math.Abs(a.Pixels[offset + 2] - b.Pixels[offset + 2]);
                    int da = Math.Abs(a.Pixels[offset + 3] - b.Pixels[offset + 3]);
                    int maxDiff = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                    changed = maxDiff > tolerance;

                    if (changed)
                    {
                        result.DiffCount++;
                        if (maxDiff > result.MaxDiff)
                        {
                            result.MaxDiff = maxDiff;
                        }

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }

                if (changed && runStart < 0)
                {
                    runStart = x;
                }
                else if (!changed && runStart >= 0)
                {
                    int runEnd = x - 1;
                    int label = -1;
                    foreach (var prev in prevRuns)
                    {
                        if (prev.Start <= runEnd + 1 && prev.End >= runStart - 1)
                        {
                            if (label < 0)
                            {
                                label = Find(prev.Label);
                            }
                            else
                            {
                                Union(label, prev.Label);
                                label = Find(label);
                            }
                        }
                    }

                    if (label < 0)
                    {
                        label = parents.Count;
                        parents.Add(label);
                        rects.Add((runStart, y, runEnd, y));
                    }
                    else
                    {
                        var r = rects[label];
                        rects[label] = (
                            Math.Min(r.MinX, runStart), Math.Min(r.MinY, y),
                            Math.Max(r.MaxX, runEnd), Math.Max(r.MaxY, y));
                    }

                    curRuns.Add((runStart, runEnd, label));
                    runStart = -1;
                }
            }

            (prevRuns, curRuns) = (curRuns, prevRuns);
        }

        result.TotalPixels = a.Width * a.Height;
        if (result.DiffCount > 0)
        {
            result.DiffBounds = (minX, minY, maxX - minX + 1, maxY - minY + 1);

            // Collect one rect per root component, largest area first.
            var components = new List<(int X, int Y, int Width, int Height)>();
            for (int label = 0; label < parents.Count; label++)
            {
                if (Find(label) != label)
                {
                    continue;
                }
                var r = rects[label];
                components.Add((r.MinX, r.MinY, r.MaxX - r.MinX + 1, r.MaxY - r.MinY + 1));
            }

            components.Sort((p, q) => (q.Width * q.Height).CompareTo(p.Width * p.Height));
            result.DiffRectCount = components.Count;
            if (components.Count > DiffResult.MaxRects)
            {
                components.RemoveRange(DiffResult.MaxRects, components.Count - DiffResult.MaxRects);
            }
            result.DiffRects = components;
        }

        return result;
    }

    internal sealed class DiffResult
    {
        /// <summary>Cap on rects returned in <see cref="DiffRects"/> — the
        /// total component count is always in <see cref="DiffRectCount"/>.</summary>
        public const int MaxRects = 50;

        public bool SizeMismatch { get; set; }
        public (int Width, int Height) BaselineSize { get; set; }
        public (int Width, int Height) CurrentSize { get; set; }
        public int DiffCount { get; set; }
        public int MaxDiff { get; set; }
        public int TotalPixels { get; set; }
        public (int X, int Y, int Width, int Height)? DiffBounds { get; set; }

        /// <summary>Bounding rects of 8-connected changed regions, largest
        /// first, capped at <see cref="MaxRects"/>. Empty when nothing changed.</summary>
        public List<(int X, int Y, int Width, int Height)> DiffRects { get; set; } = [];

        /// <summary>Total number of changed regions (before the cap).</summary>
        public int DiffRectCount { get; set; }
    }

    /// <summary>
    /// Encodes raw RGBA8 pixel data as a PNG via SharpImage
    /// (see <see cref="Cascade.UI.Imaging.ImageCodec"/>). Pixels are tightly
    /// packed RGBA (stride = width * 4).
    /// </summary>
    internal static byte[] EncodePng(ImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Cascade.UI.Imaging.ImageCodec.EncodePng(image.Pixels, image.Width, image.Height);
    }

    /// <summary>
    /// Encodes raw RGBA pixel data as base64-encoded PNG.
    /// </summary>
    internal static string EncodeBase64Png(ImageData image)
    {
        byte[] png = EncodePng(image);
        return Convert.ToBase64String(png);
    }

}

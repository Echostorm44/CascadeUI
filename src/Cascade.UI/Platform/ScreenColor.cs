namespace Cascade.UI;

/// <summary>
/// Reads the colour of the screen pixel under the mouse pointer, for the colour picker's eye
/// dropper. Windows only: other platforms do not let an app read the screen without a prompt.
/// </summary>
internal static class ScreenColor
{
    /// <summary>A sampler tests can put in place of the screen.</summary>
    internal static Func<ColorValue?>? OverrideForTests { get; set; }

    internal static bool IsSupported => OverrideForTests is not null || OperatingSystem.IsWindows();

    /// <summary>The colour under the pointer, or null when it cannot be read.</summary>
    internal static ColorValue? AtPointer()
    {
        if (OverrideForTests is { } sample)
        {
            return sample();
        }
        if (!OperatingSystem.IsWindows() || !Win32.GetCursorPos(out var point))
        {
            return null;
        }
        nint dc = Win32.GetDC(0);
        if (dc == 0)
        {
            return null;
        }
        try
        {
            uint pixel = Win32.GetPixel(dc, point.x, point.y);
            if (pixel == 0xFFFFFFFF) // CLR_INVALID
            {
                return null;
            }
            // COLORREF is 0x00BBGGRR.
            return ColorValue.FromRgba((pixel & 0xFF) / 255f, ((pixel >> 8) & 0xFF) / 255f, ((pixel >> 16) & 0xFF) / 255f);
        }
        finally
        {
            _ = Win32.ReleaseDC(0, dc);
        }
    }
}

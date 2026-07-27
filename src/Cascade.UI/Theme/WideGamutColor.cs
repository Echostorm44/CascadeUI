namespace Cascade.UI;

/// <summary>
/// Convenience factory for wide-gamut color values used in themes.
/// All methods delegate to <see cref="ColorValue"/> constructors but provide
/// a clearer intent when defining theme tokens that target P3+ displays.
/// On sRGB displays, wide-gamut values are gamut-mapped gracefully by the
/// rendering backend — no visual breakage, no exceptions.
/// </summary>
public static class WideGamutColor
{
    /// <summary>
    /// Creates a Display P3 color. The sRGB gamut covers ~75% of P3;
    /// colors outside that range render correctly on P3 displays and are
    /// gamut-mapped on sRGB displays.
    /// </summary>
    public static ColorValue P3(float r, float g, float b, float a = 1.0f)
    {
        return ColorValue.P3(r, g, b, a);
    }

    /// <summary>
    /// Creates a Rec.2020 color for HDR content.
    /// </summary>
    public static ColorValue Rec2020(float r, float g, float b, float a = 1.0f)
    {
        return ColorValue.Rec2020(r, g, b, a);
    }

    /// <summary>
    /// Creates a perceptually uniform OkLCH color. Preferred for theme
    /// tokens because lightness and chroma are perceptually linear.
    /// </summary>
    public static ColorValue OkLch(float l, float c, float h, float a = 1.0f)
    {
        return ColorValue.OkLch(l, c, h, a);
    }

    /// <summary>
    /// Creates an OkLCH color with Extended Dynamic Range support.
    /// Lightness values above 1.0 render on HDR displays.
    /// </summary>
    public static ColorValue OkLchHdr(float l, float c, float h, float a = 1.0f)
    {
        return ColorValue.OkLchHdr(l, c, h, a);
    }

    /// <summary>
    /// Returns true if the current display supports colors outside the
    /// sRGB gamut (P3 or wider). When false, wide-gamut colors are
    /// automatically gamut-mapped.
    /// Note: actual display capability detection requires the native backend.
    /// This can be set manually for testing.
    /// </summary>
    public static bool IsWideGamutDisplay { get; set; }
}

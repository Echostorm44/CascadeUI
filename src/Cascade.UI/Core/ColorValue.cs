namespace Cascade.UI;

/// <summary>
/// An immutable color value supporting sRGB, Display P3, Rec2020, and OkLCH
/// color spaces. This is the universal color type throughout Cascade UI —
/// theme tokens, inline overrides, and runtime-computed colors all use it.
/// </summary>
/// <remarks>
/// <para>
/// The simplest construction is from a hex string: <c>new ColorValue("#FF6B6B")</c>.
/// Wide gamut colors use the <see cref="P3"/>, <see cref="Rec2020"/>, or
/// <see cref="OkLch"/> factory methods. Interpolation is always performed
/// in the OkLCH perceptual color space via <see cref="Lerp"/>.
/// </para>
/// <para>
/// Chroma values above the sRGB boundary are valid and render correctly on
/// P3+ displays. On standard sRGB displays they are gamut-mapped gracefully —
/// no visual breakage, no exceptions.
/// </para>
/// </remarks>
public readonly struct ColorValue : IEquatable<ColorValue>
{
    // Internal storage in linear sRGB (premultiplied alpha) for rendering.
    // Conversion happens at construction time; all runtime operations use this form.
    private readonly float r;
    private readonly float g;
    private readonly float b;
    private readonly float a;

    /// <summary>Red channel in premultiplied linear sRGB.</summary>
    public float R => r;

    /// <summary>Green channel in premultiplied linear sRGB.</summary>
    public float G => g;

    /// <summary>Blue channel in premultiplied linear sRGB.</summary>
    public float B => b;

    /// <summary>Alpha channel (0.0 = transparent, 1.0 = opaque).</summary>
    public float A => a;

    /// <summary>
    /// Fully transparent color.
    /// </summary>
    public static readonly ColorValue Transparent = new(0, 0, 0, 0);

    /// <summary>
    /// Constructs a color from a hex string in sRGB color space.
    /// Supports "#RGB", "#RGBA", "#RRGGBB", and "#RRGGBBAA" formats.
    /// The '#' prefix is optional.
    /// </summary>
    public ColorValue(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        ReadOnlySpan<char> span = hex.AsSpan();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        float red, green, blue, alpha;

        if (span.Length == 3)
        {
            red = ParseHexDigit(span[0]) / 15f;
            green = ParseHexDigit(span[1]) / 15f;
            blue = ParseHexDigit(span[2]) / 15f;
            alpha = 1f;
        }
        else if (span.Length == 4)
        {
            red = ParseHexDigit(span[0]) / 15f;
            green = ParseHexDigit(span[1]) / 15f;
            blue = ParseHexDigit(span[2]) / 15f;
            alpha = ParseHexDigit(span[3]) / 15f;
        }
        else if (span.Length == 6)
        {
            red = ParseHexByte(span[0], span[1]) / 255f;
            green = ParseHexByte(span[2], span[3]) / 255f;
            blue = ParseHexByte(span[4], span[5]) / 255f;
            alpha = 1f;
        }
        else if (span.Length == 8)
        {
            red = ParseHexByte(span[0], span[1]) / 255f;
            green = ParseHexByte(span[2], span[3]) / 255f;
            blue = ParseHexByte(span[4], span[5]) / 255f;
            alpha = ParseHexByte(span[6], span[7]) / 255f;
        }
        else
        {
            throw new FormatException($"Invalid hex color format: '{hex}'. Expected #RGB, #RGBA, #RRGGBB, or #RRGGBBAA.");
        }

        // Convert from sRGB gamma to linear sRGB
        r = SrgbToLinear(red) * alpha;
        g = SrgbToLinear(green) * alpha;
        b = SrgbToLinear(blue) * alpha;
        a = alpha;
    }

    private static float ParseHexDigit(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }

        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }

        throw new FormatException($"Invalid hex character: '{c}'.");
    }

    private static float ParseHexByte(char hi, char lo)
    {
        return ParseHexDigit(hi) * 16 + ParseHexDigit(lo);
    }

    private static float SrgbToLinear(float srgb)
    {
        if (srgb <= 0.04045f)
        {
            return srgb / 12.92f;
        }

        return MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    private static float LinearToSrgb(float linear)
    {
        if (linear <= 0.0031308f)
        {
            return linear * 12.92f;
        }

        return 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;
    }

    private ColorValue(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    /// <summary>
    /// Creates a color from sRGB floating-point channel values (0.0–1.0).
    /// Values are converted from sRGB gamma to linear and premultiplied by alpha.
    /// </summary>
    /// <param name="r">Red channel (0.0–1.0).</param>
    /// <param name="g">Green channel (0.0–1.0).</param>
    /// <param name="b">Blue channel (0.0–1.0).</param>
    /// <param name="a">Alpha channel (0.0–1.0). Defaults to fully opaque.</param>
    public static ColorValue FromRgba(float r, float g, float b, float a = 1.0f)
    {
        float linearR = SrgbToLinear(r) * a;
        float linearG = SrgbToLinear(g) * a;
        float linearB = SrgbToLinear(b) * a;
        return new ColorValue(linearR, linearG, linearB, a);
    }

    /// <summary>
    /// Returns this color as a hex string in "#RRGGBBAA" format.
    /// Converts from premultiplied linear sRGB back to sRGB gamma space.
    /// </summary>
    public string ToHex()
    {
        float invA = a > 0 ? 1f / a : 0f;
        byte rb = (byte)MathF.Round(MathF.Min(1f, MathF.Max(0f, LinearToSrgb(r * invA))) * 255f);
        byte gb = (byte)MathF.Round(MathF.Min(1f, MathF.Max(0f, LinearToSrgb(g * invA))) * 255f);
        byte bb = (byte)MathF.Round(MathF.Min(1f, MathF.Max(0f, LinearToSrgb(b * invA))) * 255f);
        byte ab = (byte)MathF.Round(MathF.Min(1f, MathF.Max(0f, a)) * 255f);
        return $"#{rb:X2}{gb:X2}{bb:X2}{ab:X2}";
    }

    /// <summary>
    /// Creates a color in the Display P3 color space.
    /// </summary>
    /// <param name="r">Red channel (0.0–1.0).</param>
    /// <param name="g">Green channel (0.0–1.0).</param>
    /// <param name="b">Blue channel (0.0–1.0).</param>
    /// <param name="a">Alpha channel (0.0–1.0). Defaults to fully opaque.</param>
    public static ColorValue P3(float r, float g, float b, float a = 1.0f)
    {
        // Display P3 uses the sRGB transfer function
        float lr = SrgbToLinear(r);
        float lg = SrgbToLinear(g);
        float lb = SrgbToLinear(b);

        // Display P3 linear to sRGB linear matrix (D65 white point)
        float sr =  1.2249401f * lr - 0.2249402f * lg;
        float sg = -0.0420569f * lr + 1.0420571f * lg;
        float sb = -0.0196376f * lr - 0.0786361f * lg + 1.0982735f * lb;

        return new ColorValue(sr * a, sg * a, sb * a, a);
    }

    /// <summary>
    /// Creates a color in the Rec.2020 color space.
    /// </summary>
    /// <param name="r">Red channel (0.0–1.0).</param>
    /// <param name="g">Green channel (0.0–1.0).</param>
    /// <param name="b">Blue channel (0.0–1.0).</param>
    /// <param name="a">Alpha channel (0.0–1.0). Defaults to fully opaque.</param>
    public static ColorValue Rec2020(float r, float g, float b, float a = 1.0f)
    {
        // Rec.2020 uses its own transfer function (not sRGB gamma)
        float lr = Rec2020ToLinear(r);
        float lg = Rec2020ToLinear(g);
        float lb = Rec2020ToLinear(b);

        // Rec.2020 linear to sRGB linear matrix (D65 white point)
        float sr =  1.6605f * lr - 0.5876f * lg - 0.0728f * lb;
        float sg = -0.1246f * lr + 1.1329f * lg - 0.0084f * lb;
        float sb = -0.0182f * lr - 0.1006f * lg + 1.1187f * lb;

        return new ColorValue(sr * a, sg * a, sb * a, a);
    }

    /// <summary>
    /// Creates a color in the OkLCH perceptual color space.
    /// Chroma values above the sRGB boundary are valid — they render on P3+
    /// displays and are gamut-mapped gracefully on sRGB displays.
    /// </summary>
    /// <param name="l">Lightness (0.0–1.0).</param>
    /// <param name="c">Chroma (0.0–0.4+, unbounded for wide gamut).</param>
    /// <param name="h">Hue angle in degrees (0–360).</param>
    /// <param name="a">Alpha (0.0–1.0). Defaults to fully opaque.</param>
    public static ColorValue OkLch(float l, float c, float h, float a = 1.0f)
    {
        l = Math.Clamp(l, 0f, 1f);
        float hRad = h * (MathF.PI / 180f);
        float labA = c * MathF.Cos(hRad);
        float labB = c * MathF.Sin(hRad);
        var (lr, lg, lb) = OkLabToLinearSrgb(l, labA, labB);
        return new ColorValue(lr * a, lg * a, lb * a, a);
    }

    /// <summary>
    /// Creates a color in OkLCH with Extended Dynamic Range (EDR) support.
    /// Lightness values above 1.0 are rendered on EDR-capable displays and
    /// gracefully clamped to 1.0 on standard displays.
    /// </summary>
    /// <param name="l">Lightness (0.0–2.0+, values above 1.0 for EDR).</param>
    /// <param name="c">Chroma (0.0–0.4+).</param>
    /// <param name="h">Hue angle in degrees (0–360).</param>
    /// <param name="a">Alpha (0.0–1.0). Defaults to fully opaque.</param>
    public static ColorValue OkLchHdr(float l, float c, float h, float a = 1.0f)
    {
        // No clamping on L — allows values above 1.0 for EDR displays
        float hRad = h * (MathF.PI / 180f);
        float labA = c * MathF.Cos(hRad);
        float labB = c * MathF.Sin(hRad);
        var (lr, lg, lb) = OkLabToLinearSrgb(l, labA, labB);
        return new ColorValue(lr * a, lg * a, lb * a, a);
    }

    /// <summary>
    /// Decomposes this color into its OkLCH components — the inverse of
    /// <see cref="OkLch(float, float, float, float)"/>. Useful for deriving
    /// related colors (e.g. rotating hue to build a categorical palette) while
    /// preserving perceptual lightness and chroma.
    /// </summary>
    /// <returns>
    /// Lightness (0.0–1.0), chroma (0.0–0.4+), hue in degrees (0–360), and alpha
    /// (0.0–1.0). Fully transparent colors return all zeros; the hue of an
    /// achromatic color is reported as 0.
    /// </returns>
    public (float L, float C, float H, float A) ToOkLch() => ToOkLch(this);

    /// <summary>
    /// Interpolates between two colors in the OkLCH perceptual color space.
    /// </summary>
    /// <param name="a">Start color (returned when t = 0).</param>
    /// <param name="b">End color (returned when t = 1).</param>
    /// <param name="t">Interpolation factor (0.0–1.0).</param>
#pragma warning disable CA1000 // Do not declare static members on generic types — ColorValue is not generic
    public static ColorValue Lerp(ColorValue a, ColorValue b, float t)
#pragma warning restore CA1000
    {
        t = Math.Clamp(t, 0f, 1f);

        var (aL, aC, aH, aAlpha) = ToOkLch(a);
        var (bL, bC, bH, bAlpha) = ToOkLch(b);

        // Shortest-path hue interpolation
        float dH = bH - aH;
        if (dH > 180f)
        {
            dH -= 360f;
        }
        else if (dH < -180f)
        {
            dH += 360f;
        }

        float lerpL = aL + (bL - aL) * t;
        float lerpC = aC + (bC - aC) * t;
        float lerpH = aH + dH * t;
        float lerpAlpha = aAlpha + (bAlpha - aAlpha) * t;

        // Handle achromatic colors where hue is undefined
        if (aC < 1e-6f && bC < 1e-6f)
        {
            lerpH = 0f;
        }
        else if (aC < 1e-6f)
        {
            lerpH = bH;
        }
        else if (bC < 1e-6f)
        {
            lerpH = aH;
        }

        if (lerpH < 0f)
        {
            lerpH += 360f;
        }
        if (lerpH >= 360f)
        {
            lerpH -= 360f;
        }

        return FromOkLchPremultiplied(lerpL, lerpC, lerpH, lerpAlpha);
    }

    /// <summary>
    /// Returns a copy of this color with its current alpha multiplied by
    /// <paramref name="factor"/>. Unlike <see cref="Opacity(float)"/> (which
    /// <em>sets</em> the alpha), this preserves a pre-existing transparency — use it
    /// to fade a semi-transparent color in/out (e.g. animating a 25%-alpha focus ring
    /// by progress without making it fully opaque at progress 1).
    /// </summary>
    /// <param name="factor">Multiplier applied to the current alpha (0.0–1.0).</param>
    public ColorValue ScaleAlpha(float factor) => Opacity(a * Math.Clamp(factor, 0f, 1f));

    /// <summary>
    /// Returns a copy of this color with the specified opacity.
    /// </summary>
    /// <param name="opacity">Opacity from 0.0 (transparent) to 1.0 (fully opaque).</param>
    public ColorValue Opacity(float opacity)
    {
        if (a == 0f)
        {
            return Transparent;
        }

        // Undo premultiplication, apply new alpha, re-premultiply
        float invA = 1f / a;
        float newA = MathF.Max(0f, MathF.Min(1f, opacity));
        return new ColorValue(r * invA * newA, g * invA * newA, b * invA * newA, newA);
    }

    /// <summary>
    /// Returns a lightened copy of this color.
    /// </summary>
    /// <param name="amount">Amount to lighten (0.0 = unchanged, 1.0 = white).</param>
    public ColorValue Lighten(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var (l, c, h, alpha) = ToOkLch(this);
        l += amount * (1f - l);
        return FromOkLchPremultiplied(l, c, h, alpha);
    }

    /// <summary>
    /// Returns a darkened copy of this color.
    /// </summary>
    /// <param name="amount">Amount to darken (0.0 = unchanged, 1.0 = black).</param>
    public ColorValue Darken(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var (l, c, h, alpha) = ToOkLch(this);
        l -= amount * l;
        return FromOkLchPremultiplied(l, c, h, alpha);
    }

    /// <inheritdoc/>
    public bool Equals(ColorValue other)
    {
        return r == other.r && g == other.g && b == other.b && a == other.a;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ColorValue other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(r, g, b, a);
    }

    /// <summary>
    /// Returns this color as a hex string in sRGB. Fully opaque colors
    /// use <c>#RRGGBB</c>; colors with alpha use <c>#RRGGBBAA</c>.
    /// </summary>
    public override string ToString()
    {
        // Unpremultiply and convert linear → sRGB
        float sR, sG, sB;
        if (a > 0f)
        {
            sR = LinearToSrgb(r / a);
            sG = LinearToSrgb(g / a);
            sB = LinearToSrgb(b / a);
        }
        else
        {
            sR = 0f;
            sG = 0f;
            sB = 0f;
        }

        byte rb = (byte)Math.Clamp(MathF.Round(sR * 255f), 0, 255);
        byte gb = (byte)Math.Clamp(MathF.Round(sG * 255f), 0, 255);
        byte bb = (byte)Math.Clamp(MathF.Round(sB * 255f), 0, 255);

        if (a >= 1f)
        {
            return $"#{rb:X2}{gb:X2}{bb:X2}";
        }

        byte ab = (byte)Math.Clamp(MathF.Round(a * 255f), 0, 255);
        return $"#{rb:X2}{gb:X2}{bb:X2}{ab:X2}";
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ColorValue left, ColorValue right)
    {
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ColorValue left, ColorValue right)
    {
        return !left.Equals(right);
    }

    // ── Color space conversion helpers ────────────────────────────────

    private static float Rec2020ToLinear(float v)
    {
        const float alpha = 1.09929682680944f;
        const float betaLinear = 0.018053968510807f;
        float betaEncoded = betaLinear * 4.5f;
        if (v < betaEncoded)
        {
            return v / 4.5f;
        }
        return MathF.Pow((v + alpha - 1f) / alpha, 1f / 0.45f);
    }

    private static (float r, float g, float b) OkLabToLinearSrgb(float L, float labA, float labB)
    {
        // OkLab to LMS (cube root space)
        float lc = L + 0.3963377774f * labA + 0.2158037573f * labB;
        float mc = L - 0.1055613458f * labA - 0.0638541728f * labB;
        float sc = L - 0.0894841775f * labA - 1.2914855480f * labB;

        // Cube to get LMS
        float l = lc * lc * lc;
        float m = mc * mc * mc;
        float s = sc * sc * sc;

        // LMS to linear sRGB
        float r =  4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

        return (r, g, b);
    }

    private static (float L, float labA, float labB) LinearSrgbToOkLab(float r, float g, float b)
    {
        // Linear sRGB to LMS
        float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
        float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
        float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

        // Cube root
        float lc = MathF.Cbrt(l);
        float mc = MathF.Cbrt(m);
        float sc = MathF.Cbrt(s);

        // LMS (cube root) to OkLab
        float L2 = 0.2104542553f * lc + 0.7936177850f * mc - 0.0040720468f * sc;
        float a2 = 1.9779984951f * lc - 2.4285922050f * mc + 0.4505937099f * sc;
        float b2 = 0.0259040371f * lc + 0.7827717662f * mc - 0.8086757660f * sc;

        return (L2, a2, b2);
    }

    private static (float L, float C, float H, float A) ToOkLch(ColorValue color)
    {
        if (color.a <= 0f)
        {
            return (0f, 0f, 0f, 0f);
        }

        // Undo premultiplication
        float invA = 1f / color.a;
        float lr = color.r * invA;
        float lg = color.g * invA;
        float lb = color.b * invA;

        var (L, labA, labB) = LinearSrgbToOkLab(lr, lg, lb);

        float C = MathF.Sqrt(labA * labA + labB * labB);
        float H = MathF.Atan2(labB, labA) * (180f / MathF.PI);
        if (H < 0f)
        {
            H += 360f;
        }

        return (L, C, H, color.a);
    }

    private static ColorValue FromOkLchPremultiplied(float L, float C, float H, float alpha)
    {
        float hRad = H * (MathF.PI / 180f);
        float labA = C * MathF.Cos(hRad);
        float labB = C * MathF.Sin(hRad);
        var (lr, lg, lb) = OkLabToLinearSrgb(L, labA, labB);
        return new ColorValue(lr * alpha, lg * alpha, lb * alpha, alpha);
    }
}

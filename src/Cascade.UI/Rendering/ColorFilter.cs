namespace Cascade.UI;

/// <summary>
/// A color filter that transforms pixel colors during rendering.
/// Created via static factory methods. Internally stores a 4×5 color matrix
/// in row-major order: [R_out, G_out, B_out, A_out] each with 5 components
/// [Rcoeff, Gcoeff, Bcoeff, Acoeff, Offset].
/// </summary>
public sealed class ColorFilter
{
    /// <summary>
    /// The 4×5 color transformation matrix in row-major order (20 elements).
    /// Row 0: R output = R*[0] + G*[1] + B*[2] + A*[3] + [4]
    /// Row 1: G output = R*[5] + G*[6] + B*[7] + A*[8] + [9]
    /// Row 2: B output = R*[10] + G*[11] + B*[12] + A*[13] + [14]
    /// Row 3: A output = R*[15] + G*[16] + B*[17] + A*[18] + [19]
    /// </summary>
    internal float[] Matrix { get; }

    private ColorFilter(float[] matrix)
    {
        Matrix = matrix;
    }

    /// <summary>Converts to grayscale using luminance weights (Rec. 709).</summary>
    public static ColorFilter Grayscale()
    {
        // Standard luminance coefficients: R=0.2126, G=0.7152, B=0.0722
        const float r = 0.2126f;
        const float g = 0.7152f;
        const float b = 0.0722f;

        return new ColorFilter(new[]
        {
            r, g, b, 0, 0,
            r, g, b, 0, 0,
            r, g, b, 0, 0,
            0, 0, 0, 1, 0f
        });
    }

    /// <summary>Applies a sepia tone.</summary>
    public static ColorFilter Sepia()
    {
        return new ColorFilter(new[]
        {
            0.393f, 0.769f, 0.189f, 0, 0,
            0.349f, 0.686f, 0.168f, 0, 0,
            0.272f, 0.534f, 0.131f, 0, 0,
            0f,     0f,     0f,     1, 0f
        });
    }

    /// <summary>Inverts all colors.</summary>
    public static ColorFilter Invert()
    {
        return new ColorFilter(new[]
        {
            -1, 0, 0, 0, 1f,
             0,-1, 0, 0, 1f,
             0, 0,-1, 0, 1f,
             0, 0, 0, 1, 0f
        });
    }

    /// <summary>Applies a color tint at the given intensity.</summary>
    public static ColorFilter Tint(ColorValue color, float intensity = 0.5f)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);
        float inv = 1f - intensity;

        // Extract the non-premultiplied sRGB values for the tint color
        // The color stores premultiplied linear sRGB, but for a tint filter
        // we want the tint to blend in the filter matrix space
        float tr = color.A > 0 ? color.R / color.A : 0;
        float tg = color.A > 0 ? color.G / color.A : 0;
        float tb = color.A > 0 ? color.B / color.A : 0;

        return new ColorFilter(new[]
        {
            inv, 0, 0, 0, tr * intensity,
            0, inv, 0, 0, tg * intensity,
            0, 0, inv, 0, tb * intensity,
            0f, 0, 0, 1, 0f
        });
    }

    /// <summary>Adjusts color saturation (1.0 = unchanged, 0.0 = grayscale, &gt;1.0 = oversaturated).</summary>
    public static ColorFilter Saturate(float factor = 1.5f)
    {
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        float inv = 1f - factor;

        return new ColorFilter(new[]
        {
            inv * lr + factor, inv * lg,          inv * lb,          0, 0,
            inv * lr,          inv * lg + factor,  inv * lb,          0, 0,
            inv * lr,          inv * lg,          inv * lb + factor,  0, 0,
            0f,                0f,                0f,                1, 0f
        });
    }

    /// <summary>Adjusts brightness (1.0 = unchanged, &lt;1.0 = darker, &gt;1.0 = brighter).</summary>
    public static ColorFilter Brightness(float factor = 1.2f)
    {
        return new ColorFilter(new[]
        {
            factor, 0, 0, 0, 0,
            0, factor, 0, 0, 0,
            0, 0, factor, 0, 0,
            0f, 0, 0,    1, 0f
        });
    }

    /// <summary>Adjusts contrast (1.0 = unchanged, 0.0 = all gray, &gt;1.0 = increased contrast).</summary>
    public static ColorFilter Contrast(float factor = 1.1f)
    {
        float offset = 0.5f * (1f - factor);

        return new ColorFilter(new[]
        {
            factor, 0, 0, 0, offset,
            0, factor, 0, 0, offset,
            0, 0, factor, 0, offset,
            0f, 0, 0,    1, 0f
        });
    }
}

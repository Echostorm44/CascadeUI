namespace Cascade.UI;

/// <summary>
/// Describes a drop shadow effect: offset, blur, spread, and color.
/// </summary>
public sealed class ShadowValue
{
    /// <summary>Creates a shadow value.</summary>
    /// <param name="offsetX">Horizontal offset in logical pixels.</param>
    /// <param name="offsetY">Vertical offset in logical pixels.</param>
    /// <param name="blurRadius">Blur radius in logical pixels.</param>
    /// <param name="spreadRadius">Spread radius in logical pixels.</param>
    /// <param name="color">Shadow color.</param>
    public ShadowValue(float offsetX, float offsetY, float blurRadius, float spreadRadius, ColorValue color)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        BlurRadius = blurRadius;
        SpreadRadius = spreadRadius;
        Color = color;
    }

    /// <summary>Horizontal offset in logical pixels.</summary>
    public float OffsetX { get; }

    /// <summary>Vertical offset in logical pixels.</summary>
    public float OffsetY { get; }

    /// <summary>Blur radius in logical pixels.</summary>
    public float BlurRadius { get; }

    /// <summary>Spread radius in logical pixels.</summary>
    public float SpreadRadius { get; }

    /// <summary>Shadow color.</summary>
    public ColorValue Color { get; }
}

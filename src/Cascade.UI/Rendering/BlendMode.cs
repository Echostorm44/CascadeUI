namespace Cascade.UI;

/// <summary>
/// Standard compositing blend modes for layered rendering.
/// </summary>
public enum BlendMode
{
    /// <summary>Normal alpha compositing (default).</summary>
    Normal,

    /// <summary>Multiplies color values (darkening).</summary>
    Multiply,

    /// <summary>Inverse multiply (lightening).</summary>
    Screen,

    /// <summary>Combines Multiply and Screen based on base color.</summary>
    Overlay,

    /// <summary>Darkens more aggressively than Multiply.</summary>
    ColorBurn,

    /// <summary>Lightens more aggressively than Screen.</summary>
    ColorDodge,

    /// <summary>Selects the darker of each channel.</summary>
    Darken,

    /// <summary>Selects the lighter of each channel.</summary>
    Lighten,

    /// <summary>Subtracts one layer from the other.</summary>
    Difference,

    /// <summary>Similar to Difference but lower contrast.</summary>
    Exclusion,

    /// <summary>Preserves hue from source, luminance and saturation from destination.</summary>
    Hue,

    /// <summary>Preserves saturation from source.</summary>
    Saturation,

    /// <summary>Preserves hue and saturation from source.</summary>
    Color,

    /// <summary>Preserves luminance from source.</summary>
    Luminosity,
}

using System;

namespace Cascade.UI;

/// <summary>
/// A backdrop effect applied to content behind a node. Sits alongside
/// Background — does not replace it. Every effect requires a
/// <see cref="ReducedTransparencyFallback"/> for accessibility.
/// </summary>
public abstract record BackdropEffect
{
    /// <summary>Gaussian blur on compositor content behind this node.</summary>
    public static BackdropBlur Blur(float radius)
    {
        return new BackdropBlur(radius);
    }

    /// <summary>Blur plus color tint — Acrylic style.</summary>
    public static BackdropTint Tint(float blurRadius, ColorValue tint, float tintOpacity = 0.6f)
    {
        return new BackdropTint(blurRadius, tint, tintOpacity);
    }

    /// <summary>
    /// Vibrancy — blur plus luminosity curve plus Apple-matched grain.
    /// Matches macOS NSVisualEffectView materials precisely on macOS.
    /// </summary>
    public static BackdropVibrant Vibrant(VibrantMaterial material)
    {
        return new BackdropVibrant(material);
    }

    /// <summary>
    /// Mica — compositor samples from desktop wallpaper (Windows 11).
    /// Falls back gracefully on other platforms and older Windows.
    /// </summary>
    public static BackdropMica Mica(ColorValue fallbackTint)
    {
        return new BackdropMica(fallbackTint);
    }

    /// <summary>
    /// Solid color shown when the OS ReducedTransparency accessibility setting is active.
    /// Must be set before assigning to any node property.
    /// </summary>
    public abstract ColorValue ReducedTransparencyFallback { get; }

    /// <summary>Returns a copy of this effect with the specified fallback color.</summary>
    public BackdropEffect WithFallback(ColorValue color)
    {
        return this switch
        {
            BackdropBlur blur       => blur with { Fallback = color },
            BackdropTint tint       => tint with { Fallback = color },
            BackdropVibrant vibrant => vibrant with { Fallback = color },
            BackdropMica mica       => mica with { Fallback = color },
            _ => throw new InvalidOperationException("Unknown backdrop effect type.")
        };
    }
}

/// <summary>Gaussian blur backdrop effect.</summary>
public record BackdropBlur(float Radius) : BackdropEffect
{
    /// <summary>Configured fallback color.</summary>
    public ColorValue Fallback { get; init; } = ColorValue.Transparent;

    /// <inheritdoc/>
    public override ColorValue ReducedTransparencyFallback => Fallback;
}

/// <summary>Blur plus tint backdrop effect (Acrylic style).</summary>
public record BackdropTint(float BlurRadius, ColorValue TintColor, float TintOpacity) : BackdropEffect
{
    /// <summary>Configured fallback color.</summary>
    public ColorValue Fallback { get; init; } = ColorValue.Transparent;

    /// <inheritdoc/>
    public override ColorValue ReducedTransparencyFallback => Fallback;
}

/// <summary>Vibrancy backdrop effect (Apple NSVisualEffectView style).</summary>
public record BackdropVibrant(VibrantMaterial Material) : BackdropEffect
{
    /// <summary>Configured fallback color.</summary>
    public ColorValue Fallback { get; init; } = ColorValue.Transparent;

    /// <inheritdoc/>
    public override ColorValue ReducedTransparencyFallback => Fallback;
}

/// <summary>Mica backdrop effect (Windows 11 wallpaper sampling).</summary>
public record BackdropMica(ColorValue FallbackTint) : BackdropEffect
{
    /// <summary>Configured fallback color.</summary>
    public ColorValue Fallback { get; init; } = FallbackTint;

    /// <inheritdoc/>
    public override ColorValue ReducedTransparencyFallback => Fallback;
}

/// <summary>
/// Named vibrancy materials matching macOS NSVisualEffectView.
/// </summary>
public enum VibrantMaterial
{
    /// <summary>Sidebar material.</summary>
    Sidebar,

    /// <summary>Menu material.</summary>
    Menu,

    /// <summary>Popover material.</summary>
    Popover,

    /// <summary>Sheet material.</summary>
    Sheet,

    /// <summary>HUD window material.</summary>
    HudWindow,

    /// <summary>Tooltip material.</summary>
    Tooltip,

    /// <summary>Generic light material for non-macOS themes.</summary>
    Light,

    /// <summary>Generic dark material for non-macOS themes.</summary>
    Dark,

    /// <summary>Generic ultra-light material.</summary>
    UltraLight,

    /// <summary>Generic ultra-dark material.</summary>
    UltraDark,
}

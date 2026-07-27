namespace Cascade.UI;

/// <summary>
/// Platform material effects for window backgrounds and surfaces.
/// Materials are compositor-level effects that blend the window content
/// with the desktop behind it. Not all materials are available on all
/// platforms — use <see cref="PlatformMaterials"/> to check availability
/// at runtime.
/// </summary>
public enum WindowMaterial
{
    /// <summary>Solid opaque background. No compositor effect. Works everywhere.</summary>
    None = 0,

    /// <summary>
    /// Wallpaper-derived tinted material. The window background subtly
    /// reflects the desktop wallpaper color. Windows 11 build 22621+ only.
    /// Falls back to <see cref="None"/> on unsupported platforms.
    /// </summary>
    Mica = 1,

    /// <summary>
    /// Higher-contrast variant of Mica with stronger tinting. Useful for
    /// title bars and nav bars that need visual separation from the content area.
    /// Windows 11 build 22621+ only.
    /// </summary>
    MicaAlt = 2,

    /// <summary>
    /// Real-time Gaussian blur of content behind the window. More visually
    /// dynamic than Mica but uses more GPU. Windows 10 Anniversary Update+.
    /// Falls back to <see cref="None"/> on unsupported platforms.
    /// </summary>
    Acrylic = 3,

    /// <summary>
    /// macOS NSVisualEffectView vibrancy material. Blends content behind
    /// the window using the system vibrancy effect. macOS only.
    /// Falls back to <see cref="None"/> on Windows and Linux.
    /// </summary>
    Vibrancy = 4
}

namespace Cascade.UI;

/// <summary>
/// Reports material effect availability for the current platform and provides
/// fallback resolution. Use this class to check whether a <see cref="WindowMaterial"/>
/// can be applied before requesting it from the compositor.
/// </summary>
public static class PlatformMaterials
{
    /// <summary>Whether Mica is available (Windows 11 build 22621+).</summary>
    public static bool IsMicaAvailable => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    /// <summary>Whether Mica Alt is available (Windows 11 build 22621+).</summary>
    public static bool IsMicaAltAvailable => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    /// <summary>Whether Acrylic is available (Windows 10 Anniversary Update+).</summary>
    public static bool IsAcrylicAvailable => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393);

    /// <summary>Whether Vibrancy is available (macOS only).</summary>
    public static bool IsVibrancyAvailable => OperatingSystem.IsMacOS();

    /// <summary>
    /// Whether the user has enabled "Reduce transparency" in OS accessibility settings.
    /// When true, all materials should fall back to solid backgrounds.
    /// Note: actual OS preference detection requires the native backend. This
    /// property can be set manually by application code for testing.
    /// </summary>
    public static bool ReducedTransparency { get; set; }

    /// <summary>
    /// Returns true if the specified material is available on the current platform
    /// and the user has not requested reduced transparency.
    /// </summary>
    public static bool IsAvailable(WindowMaterial material)
    {
        if (ReducedTransparency)
        {
            return material == WindowMaterial.None;
        }

        return material switch
        {
            WindowMaterial.Mica     => IsMicaAvailable,
            WindowMaterial.MicaAlt  => IsMicaAltAvailable,
            WindowMaterial.Acrylic  => IsAcrylicAvailable,
            WindowMaterial.Vibrancy => IsVibrancyAvailable,
            WindowMaterial.None     => true,
            _                       => false
        };
    }

    /// <summary>
    /// Returns the requested material if available, otherwise <see cref="WindowMaterial.None"/>.
    /// </summary>
    public static WindowMaterial Resolve(WindowMaterial requested)
    {
        return IsAvailable(requested) ? requested : WindowMaterial.None;
    }

    /// <summary>
    /// Returns the requested material if available, otherwise the solid fallback color.
    /// The caller receives either a material to apply or a color to fill with.
    /// </summary>
    /// <param name="requested">The desired material effect.</param>
    /// <param name="solidFallback">Color to use when the material is unavailable.</param>
    /// <returns>
    /// A tuple of the resolved material and the fallback color. When the material is
    /// available, <paramref name="solidFallback"/> is returned unchanged but should be
    /// ignored. When unavailable, the material is <see cref="WindowMaterial.None"/>.
    /// </returns>
    public static (WindowMaterial Material, ColorValue Fallback) Fallback(
        WindowMaterial requested,
        ColorValue solidFallback)
    {
        if (IsAvailable(requested))
        {
            return (requested, solidFallback);
        }

        return (WindowMaterial.None, solidFallback);
    }
}

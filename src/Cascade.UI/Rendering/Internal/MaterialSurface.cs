namespace Cascade.UI.Rendering.Internal;

/// <summary>
/// An internal rendering primitive that marks a rectangular region for
/// compositor material treatment. The rendering backend reads this node
/// during scene composition to set up the appropriate platform material
/// (Mica, Acrylic, Vibrancy) on the compositor surface.
/// </summary>
/// <remarks>
/// Application code does not create MaterialSurface nodes directly.
/// They are emitted by the framework when a theme specifies a
/// <see cref="WindowMaterial"/> and the platform supports it.
/// </remarks>
internal sealed class MaterialSurface : Node
{
    /// <summary>The material effect to apply.</summary>
    public WindowMaterial Material { get; }

    /// <summary>
    /// Optional fallback color used when the material is not available
    /// on the current platform or when reduced transparency is enabled.
    /// </summary>
    public ColorValue FallbackColor { get; }

    /// <summary>
    /// Corner radius for the material region, in logical pixels.
    /// </summary>
    public float CornerRadius { get; }

    public MaterialSurface(
        WindowMaterial material,
        ColorValue fallbackColor = default,
        float cornerRadius = 0f)
    {
        Material = material;
        FallbackColor = fallbackColor;
        CornerRadius = cornerRadius;
    }

    /// <summary>
    /// Returns the effective material after platform availability check.
    /// If the material is not available, returns <see cref="WindowMaterial.None"/>.
    /// </summary>
    public WindowMaterial EffectiveMaterial => PlatformMaterials.Resolve(Material);

    /// <summary>
    /// Whether this surface should render as a solid color fallback
    /// (material not available or reduced transparency enabled).
    /// </summary>
    public bool IsFallback => EffectiveMaterial == WindowMaterial.None && Material != WindowMaterial.None;
}

using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the icon set source generator pipeline.
/// Reported when the generator detects issues with icon references.
/// </summary>
internal static class IconDiagnostics
{
    private const string Category = "Cascade.Icons";

    /// <summary>
    /// CS-CASCADE-ICON-001: A static icon field reference doesn't exist in the registered icon set.
    /// </summary>
    internal static readonly DiagnosticDescriptor IconNotFound = new(
        id: "CASCADEICON001",
        title: "Referenced icon not found in icon set",
        messageFormat: "Icon '{0}' was not found in the registered icon set '{1}'. Available icons: {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CASCADEICON002: An SVG uses something icon path data cannot carry (a transform, a
    /// <c>use</c> reference, a percentage length). The icon is generated without it, so it may
    /// not look like the file.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedSvgFeature = new(
        id: "CASCADEICON002",
        title: "SVG feature not supported in icons",
        messageFormat: "Icon '{0}' uses {1}, which icons do not support; it is drawn without it. Flatten the SVG (for example with an editor's 'convert to path' or SVGO's convertTransform) and regenerate.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CASCADEICON003: An SVG in an icons folder has no shape that draws anything, so no icon
    /// field is generated for it.
    /// </summary>
    internal static readonly DiagnosticDescriptor NoDrawableShapes = new(
        id: "CASCADEICON003",
        title: "SVG has nothing to draw",
        messageFormat: "Icon '{0}' has no path, circle, ellipse, rect, line, polyline or polygon outside defs, so no icon was generated for it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

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
}

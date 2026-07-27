using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the theme source generator pipeline.
/// Reported when the generator detects issues with CascadeTheme subclasses.
/// </summary>
internal static class ThemeDiagnostics
{
    private const string Category = "Cascade.Themes";

    /// <summary>
    /// CS-CASCADE-THEME-001: A CascadeTheme subclass doesn't override a required virtual property.
    /// The default value from CascadeTheme will be used, which may not match the intended design.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingRequiredTokenOverride = new(
        id: "CASCADETHEME001",
        title: "Custom theme missing required token override",
        messageFormat: "Theme '{0}' does not override required token '{1}'. The default value from CascadeTheme will be used, which may not match the intended design.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

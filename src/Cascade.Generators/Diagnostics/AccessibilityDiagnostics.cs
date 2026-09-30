using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for accessibility analysis.
/// Reported when the generator detects interactive elements or images
/// missing required accessibility metadata.
/// </summary>
internal static class AccessibilityDiagnostics
{
    private const string Category = "Cascade.Accessibility";

    /// <summary>
    /// CS-CASCADE-A11Y-001: An interactive control has no accessible label for screen readers.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingAccessibleLabel = new(
        id: "CASCADEA11Y001",
        title: "Interactive element has no accessible label",
        messageFormat: "Interactive element '{0}' has no accessible label, so screen readers fall back to the icon's name. Add .Tooltip(...) (also shown on hover) or .AccessibleLabel(...).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-A11Y-002: An Image node has no alt text for screen readers.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingImageAltText = new(
        id: "CASCADEA11Y002",
        title: "Image has no alt text",
        messageFormat: "Image '{0}' has no alt text. Add .AccessibleLabel(\"description\") for screen reader support, or .AccessibleLabel(\"\") for a decorative image.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the font verification source generator pipeline.
/// </summary>
internal static class FontDiagnostics
{
    private const string Category = "Cascade.Fonts";

    /// <summary>
    /// CASCADE_FONT001: Declared font file was not found.
    /// A font declared via CascadeFont metadata could not be located at the declared path.
    /// </summary>
    internal static readonly DiagnosticDescriptor FontFileNotFound = new(
        id: "CASCADE_FONT001",
        title: "Declared font file not found",
        messageFormat: "Font file '{0}' declared as a CascadeFont was not found. Verify the file path in the project file.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

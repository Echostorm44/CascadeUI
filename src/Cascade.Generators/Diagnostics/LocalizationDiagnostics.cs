using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the localization source generator pipeline.
/// </summary>
internal static class LocalizationDiagnostics
{
    private const string Category = "Cascade.Localization";

    /// <summary>
    /// CASCADE_LOC001: Reference locale file is missing.
    /// The generator expects at least one JSON file in the strings/ directory
    /// (typically <c>strings/en.json</c>) to discover localization keys.
    /// </summary>
    internal static readonly DiagnosticDescriptor ReferenceLocaleMissing = new(
        id: "CASCADE_LOC001",
        title: "Reference locale file missing",
        messageFormat: "No JSON locale files found in the 'strings/' directory. Add a reference locale file (e.g. strings/en.json) to generate the S localization class.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CASCADE_LOC002: A hardcoded string literal is used where a localization key is expected.
    /// </summary>
    internal static readonly DiagnosticDescriptor HardcodedString = new(
        id: "CASCADELOC001",
        title: "Hardcoded user-visible string",
        messageFormat: "Hardcoded string '{0}' detected where a localization key is expected. Consider using S.{1} for localization support.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CASCADE_LOC003: A S.* reference doesn't exist in any resource file.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingLocalizationKey = new(
        id: "CASCADELOC002",
        title: "Missing localization key",
        messageFormat: "Localization key '{0}' not found in any resource file. Add the key to your .resx or .json localization files.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

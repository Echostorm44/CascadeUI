using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the <see cref="StorageKeyGenerator"/> pipeline.
/// Reported when the generator detects issues with StorageKey declarations
/// in [StorageKeys]-attributed classes.
/// </summary>
internal static class StorageDiagnostics
{
    private const string Category = "Cascade.Storage";

    /// <summary>
    /// CS-CASCADE-STOR-001: Two or more StorageKey fields share the same key string.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicateKey = new(
        id: "CASCADESTOR001",
        title: "Duplicate storage key",
        messageFormat: "Storage key '{0}' is declared by both '{1}' and '{2}'. Each key must be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-STOR-002: A StorageKey has a key string with invalid format.
    /// Keys must be non-empty and contain only alphanumeric characters, dots,
    /// hyphens, and underscores.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidKeyFormat = new(
        id: "CASCADESTOR002",
        title: "Invalid storage key format",
        messageFormat: "Storage key '{0}' on field '{1}' has an invalid format. Keys must be non-empty and contain only alphanumeric characters, dots, hyphens, and underscores.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-STOR-003: A StorageKey uses a type parameter T that is not
    /// a supported serializable type.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedType = new(
        id: "CASCADESTOR003",
        title: "Unsupported StorageKey type",
        messageFormat: "StorageKey<{0}> on field '{1}' uses a type that is not supported for storage serialization. Use a primitive, string, or a type implementing IStorageSerializable<T>.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-STOR-004: A class declares StorageKey fields but is missing
    /// the [StorageKeys] attribute.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingAttribute = new(
        id: "CASCADESTOR004",
        title: "Missing [StorageKeys] attribute",
        messageFormat: "Class '{0}' declares StorageKey<T> fields but is missing the [StorageKeys] attribute. Add [StorageKeys] to enable compile-time validation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

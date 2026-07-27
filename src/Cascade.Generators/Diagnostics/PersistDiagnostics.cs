using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the <see cref="PersistStateGenerator"/> pipeline.
/// Reported when the generator detects issues with [PersistState]-attributed
/// fields in Component subclasses.
/// </summary>
internal static class PersistDiagnostics
{
    private const string Category = "Cascade.Persistence";

    /// <summary>
    /// CS-CASCADE-PERS-001: A [PersistState] field has a type that cannot
    /// be serialized by the storage system.
    /// </summary>
    internal static readonly DiagnosticDescriptor NonSerializableType = new(
        id: "CASCADEPERS001",
        title: "Non-serializable [PersistState] field type",
        messageFormat: "[PersistState] field '{0}' has type '{1}' which is not serializable. Use a primitive, string, or a type implementing IStorageSerializable<T>.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-PERS-002: A [PersistState] field has a Key expression that
    /// references a field that does not exist on the component.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingKey = new(
        id: "CASCADEPERS002",
        title: "Missing persistence key field",
        messageFormat: "[PersistState] on field '{0}' references key '{1}' which does not exist on the component",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-PERS-003: A [PersistState] attribute was applied to a
    /// readonly field, which cannot be restored on mount.
    /// </summary>
    internal static readonly DiagnosticDescriptor ReadonlyField = new(
        id: "CASCADEPERS003",
        title: "[PersistState] on readonly field",
        messageFormat: "[PersistState] cannot be applied to readonly field '{0}'. Remove 'readonly' to allow state restoration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

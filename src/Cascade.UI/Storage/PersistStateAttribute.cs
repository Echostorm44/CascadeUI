namespace Cascade.UI;

/// <summary>
/// Marks a component field for automatic state persistence via <see cref="LocalStorage"/>.
/// The source generator saves the field value on change and restores it on construction.
/// </summary>
/// <remarks>
/// <para>
/// The generated storage key follows the pattern:
/// <c>cascade.persist.{TypeName}.{fieldName}</c>.
/// </para>
/// <para>
/// For multi-instance components, set <see cref="Key"/> to reference a distinguishing
/// field or parameter to produce unique keys per instance.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class PersistStateAttribute : Attribute
{
    /// <summary>
    /// Optional key expression referencing a field or parameter whose value
    /// distinguishes multiple instances of the same component type.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Controls when the state is persisted.
    /// </summary>
    public PersistWhen When { get; set; } = PersistWhen.Immediate;
}

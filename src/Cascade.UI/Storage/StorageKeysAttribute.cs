namespace Cascade.UI;

/// <summary>
/// Marks a static partial class as a container for typed <see cref="StorageKey{T}"/>
/// declarations. The source generator validates key uniqueness, type serializability,
/// and naming conventions.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StorageKeysAttribute : Attribute
{
}

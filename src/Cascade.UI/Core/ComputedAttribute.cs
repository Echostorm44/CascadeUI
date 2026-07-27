namespace Cascade.UI;

/// <summary>
/// Marks a property as a computed (derived) value that is automatically
/// memoized by the source generator. The property recalculates only when
/// its reactive inputs change.
/// </summary>
/// <remarks>
/// <para>
/// Inside a <see cref="Component"/> subclass, computed properties that read
/// reactive fields are automatically memoized without annotation. The generator
/// detects the dependency chain and handles caching.
/// </para>
/// <para>
/// This attribute is required only when memoization behavior needs to be
/// explicit — typically for expensive computations in open-scope classes
/// (shared state objects, stores) where the generator cannot infer the
/// reactive dependency graph from a closed component scope.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComputedAttribute : Attribute
{
}

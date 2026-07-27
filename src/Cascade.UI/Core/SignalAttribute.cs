namespace Cascade.UI;

/// <summary>
/// Marks a field or property as reactive signal state. The source generator
/// wraps the member in signal accessors that track reads and notify on writes.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is required only for reactive state defined outside a
/// <see cref="Component"/> — shared state objects, stores, and cross-component
/// communication — where the generator cannot infer a closed scope.
/// </para>
/// <para>
/// Inside a Component subclass, reactivity is inferred automatically from
/// field usage: non-readonly fields that are read in <see cref="Component.Render"/>
/// and written anywhere in the class are reactive without annotation.
/// See <see cref="ReadonlySemantics"/> for the opt-out mechanism.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SignalAttribute : Attribute
{
}

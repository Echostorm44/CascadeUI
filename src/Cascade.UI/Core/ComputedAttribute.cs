namespace Cascade.UI;

/// <summary>
/// Marks a property as a computed (derived) value for tooling — the DevTools inspector
/// lists <c>[Computed]</c> properties and their current values in a component's state view.
/// </summary>
/// <remarks>
/// <para>
/// A computed property is just an ordinary expression-bodied property that derives its
/// value from other fields, e.g. <c>public bool IsValid =&gt; email.Contains('@');</c>. It
/// is evaluated on each read (there is no automatic memoization — a source generator cannot
/// own an existing property's getter, and caching across plain <c>field = x</c> writes would
/// go stale because such writes cannot be intercepted). For a simple expression this is
/// exactly what you want; for an expensive computation you would cache it yourself.
/// </para>
/// <para>
/// The value re-appears in the UI because a state change re-renders the component (a
/// <see cref="Component"/> re-renders when a bound value changes via <c>Bind</c>, or when you
/// call <c>Invalidate()</c>), and Render() reads the computed property afresh.
/// </para>
/// <para>
/// This attribute is optional and affects tooling only; it does not change runtime behavior.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComputedAttribute : Attribute
{
}

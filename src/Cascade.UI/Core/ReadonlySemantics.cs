namespace Cascade.UI;

/// <summary>
/// Documents the reactivity opt-out semantics for readonly fields in Cascade UI.
/// </summary>
/// <remarks>
/// <para>
/// In Cascade UI, fields in a <see cref="Component"/> subclass are reactive by
/// default when they are non-readonly, read inside <see cref="Component.Render"/>,
/// and written anywhere in the class. The source generator infers this automatically.
/// No <see cref="SignalAttribute"/> is needed.
/// </para>
/// <para>
/// The <c>readonly</c> keyword opts a field out of reactivity. A readonly field
/// is never tracked by the reactive system, never triggers re-renders, and is
/// never wrapped in signal accessors by the source generator.
/// </para>
/// <para>
/// This is the only opt-out mechanism. There is no <c>[NonReactive]</c> attribute.
/// The <c>readonly</c> keyword is standard C#, and its semantic meaning ("this value
/// does not change after construction") aligns precisely with "this does not need
/// change tracking."
/// </para>
/// <para>
/// Examples:
/// <code>
/// public class MyComponent : Component
/// {
///     // Reactive — non-readonly, read in Render(), written in handlers
///     private int count = 0;
///
///     // NOT reactive — readonly means the value never changes after construction
///     private readonly string title;
///
///     // NOT reactive — readonly NodeRef is populated by the framework, not by assignment
///     private readonly NodeRef&lt;Panel&gt; headerRef = new();
///
///     // NOT reactive — readonly AnimatedValue is mutated through its methods, not reassigned
///     private readonly AnimatedValue&lt;float&gt; opacity = new(0f);
/// }
/// </code>
/// </para>
/// </remarks>
public static class ReadonlySemantics
{
    // This type exists for documentation purposes only.
    // The readonly opt-out is enforced by the Cascade source generator at compile time.
}

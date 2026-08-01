using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the reactivity analysis pipeline.
/// These are reported as compile-time errors or warnings when the generator
/// detects misuse of reactive state in Component subclasses.
/// </summary>
internal static class ReactivityDiagnostics
{
    private const string ReactivityCategory = "Cascade.Reactivity";

    /// <summary>
    /// CS-CASCADE-001: Writing a reactive field inside Render() is a compile error.
    /// Render() must be pure — no side effects, no signal writes.
    /// </summary>
    internal static readonly DiagnosticDescriptor WriteInRender = new(
        id: "CASCADE001",
        title: "Reactive field written inside Render()",
        messageFormat: "Reactive field '{0}' must not be written inside Render(). Render() must be pure — move the write to an event handler or OnMounted().",
        category: ReactivityCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-002: Bind() targeting a readonly field is a compile error.
    /// A readonly field cannot be the target of a two-way binding setter.
    /// </summary>
    internal static readonly DiagnosticDescriptor BindOnReadonly = new(
        id: "CASCADE002",
        title: "Bind() targets a readonly field",
        messageFormat: "Bind() requires a writable reactive field. '{0}' is readonly. Remove 'readonly' or use a direct value instead of Bind().",
        category: ReactivityCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-003: a Component with reactive state must be declared <c>partial</c>.
    /// The reactivity pipeline augments the class with a generated partial (reactive setters,
    /// __ScheduleReactiveRender, Bind helpers); without <c>partial</c> the compiler reports a
    /// confusing CS0260 collision instead. This diagnostic points at the real fix.
    /// </summary>
    internal static readonly DiagnosticDescriptor ComponentMustBePartial = new(
        id: "CASCADE003",
        title: "Reactive Component must be partial",
        messageFormat: "Component '{0}' has reactive state (fields, computed properties, or Bind()), so it must be declared 'partial' — the Cascade generator adds a partial class with the reactive plumbing. Add the 'partial' modifier to '{0}'.",
        category: ReactivityCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

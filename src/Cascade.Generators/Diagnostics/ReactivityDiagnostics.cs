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
    private const string PerformanceCategory = "Cascade.Performance";

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
    /// CS-CASCADE-PERF-001: Allocation detected inside Render() body.
    /// Render() is called on every state change and should be allocation-free
    /// on the hot path.
    /// </summary>
    internal static readonly DiagnosticDescriptor AllocationInRender = new(
        id: "CASCADEPERF001",
        title: "Allocation detected in Render() body",
        messageFormat: "Allocation of '{0}' detected in Render(). Render() should be allocation-free — consider caching or moving the allocation outside Render().",
        category: PerformanceCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

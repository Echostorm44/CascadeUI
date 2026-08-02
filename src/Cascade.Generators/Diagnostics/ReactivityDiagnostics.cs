using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the reactivity analysis pipeline.
/// Reactivity is a diagnostics-only pass (no code is generated); this is the one rule
/// it enforces on Component subclasses.
/// </summary>
internal static class ReactivityDiagnostics
{
    private const string ReactivityCategory = "Cascade.Reactivity";

    /// <summary>
    /// CS-CASCADE-001: Writing a reactive field inside Render() is a compile error.
    /// Render() must be pure — no side effects, no state writes.
    /// </summary>
    internal static readonly DiagnosticDescriptor WriteInRender = new(
        id: "CASCADE001",
        title: "Reactive field written inside Render()",
        messageFormat: "Reactive field '{0}' must not be written inside Render(). Render() must be pure — move the write to an event handler or OnMounted().",
        category: ReactivityCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

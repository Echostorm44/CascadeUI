using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for NativeAOT compatibility analysis.
/// Reported when the generator detects patterns that are incompatible
/// with NativeAOT compilation.
/// </summary>
internal static class AotDiagnostics
{
    private const string Category = "Cascade.AOT";

    /// <summary>
    /// CS-CASCADE-AOT-001: A reflection-heavy pattern is used that won't work with NativeAOT.
    /// </summary>
    internal static readonly DiagnosticDescriptor AotIncompatiblePattern = new(
        id: "CASCADEAOT001",
        title: "Pattern incompatible with NativeAOT",
        messageFormat: "The pattern '{0}' uses reflection that is incompatible with NativeAOT compilation. Use source generation or explicit registration instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

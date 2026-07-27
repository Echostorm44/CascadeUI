using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the <see cref="AiSurfaceGenerator"/> pipeline.
/// Reported when the generator detects issues with [AiSurface] and
/// [AiCapability] usage.
/// </summary>
internal static class AiDiagnostics
{
    private const string Category = "Cascade.AI";

    /// <summary>
    /// CS-CASCADE-AI-001: An [AiCapability] method is declared in a class
    /// that does not have the [AiSurface] attribute.
    /// </summary>
    internal static readonly DiagnosticDescriptor MissingSurface = new(
        id: "CASCADEAI001",
        title: "Missing [AiSurface] attribute",
        messageFormat: "[AiCapability] method '{0}' is declared in class '{1}' which is missing the [AiSurface] attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-AI-002: Two [AiCapability] methods resolve to the same tool name.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicateToolName = new(
        id: "CASCADEAI002",
        title: "Duplicate AI tool name",
        messageFormat: "AI tool name '{0}' is used by both '{1}' and '{2}'. Each tool name must be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-AI-003: An [AiCapability] method has a parameter type
    /// that cannot be represented in a tool parameter schema.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedParameterType = new(
        id: "CASCADEAI003",
        title: "Unsupported AI capability parameter type",
        messageFormat: "[AiCapability] method '{0}' has parameter '{1}' of type '{2}' which cannot be represented in a tool parameter schema",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-AI-004: An [AiCapability] method is not public or static,
    /// making it inaccessible for tool invocation.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidAccessibility = new(
        id: "CASCADEAI004",
        title: "AI capability must be public",
        messageFormat: "[AiCapability] method '{0}' must be public to be exposed as an AI tool",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

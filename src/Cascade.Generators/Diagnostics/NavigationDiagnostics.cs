using Microsoft.CodeAnalysis;

namespace Cascade.Generators;

/// <summary>
/// Diagnostic descriptors for the navigation and routing source generator pipeline.
/// Reported when the generator detects issues with [Route] attribute usage.
/// </summary>
internal static class NavigationDiagnostics
{
    private const string Category = "Cascade.Navigation";

    /// <summary>
    /// CS-CASCADE-NAV-001: Two [Route] attributes map to the same path.
    /// Each route path must be unique within the application.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicateRoute = new(
        id: "CASCADENAV001",
        title: "Duplicate route detected",
        messageFormat: "Route '{0}' is already registered by '{1}'. Each route path must be unique within the application.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// CS-CASCADE-NAV-002: A route parameter type doesn't match the component property type.
    /// </summary>
    internal static readonly DiagnosticDescriptor RouteParameterTypeMismatch = new(
        id: "CASCADENAV002",
        title: "Route parameter type mismatch",
        messageFormat: "Route parameter '{0}' has type '{1}' but the target property '{2}' has type '{3}'. These types must match.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

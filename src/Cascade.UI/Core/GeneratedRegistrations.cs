using System.ComponentModel;

namespace Cascade.UI;

/// <summary>
/// Entry points for code that Cascade.Generators emits into the app's assembly as
/// <c>[ModuleInitializer]</c> methods, so the framework learns about the app's routes (and,
/// in DevTools builds, its API index) without reflecting over the app. Not for hand-written code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedRegistrations
{
    private static readonly Lock routesLock = new();
    private static readonly List<GeneratedRoute> routes = [];

    /// <summary>
    /// Registers a <c>[Route]</c> component. <paramref name="create"/> constructs the component and
    /// binds the route values to its properties.
    /// </summary>
    public static void RegisterRoute(string pattern, Type componentType, Func<IReadOnlyDictionary<string, string>, Component> create)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(create);
        lock (routesLock)
        {
            routes.Add(new GeneratedRoute(pattern, componentType, create));
        }
    }

    /// <summary>Sets the generated API index served by the MCP <c>api-index</c> resource.</summary>
    public static void RegisterApiIndex(string content)
    {
        ApiIndex = content ?? "";
    }

    internal static string ApiIndex { get; private set; } = "";

    /// <summary>The generated routes registered at or after <paramref name="start"/>.</summary>
    internal static GeneratedRoute[] RoutesFrom(int start)
    {
        lock (routesLock)
        {
            return start >= routes.Count ? [] : routes.GetRange(start, routes.Count - start).ToArray();
        }
    }
}

/// <summary>A route registered by generated code.</summary>
internal sealed record GeneratedRoute(string Pattern, Type ComponentType, Func<IReadOnlyDictionary<string, string>, Component> Create);

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Cascade.UI;

/// <summary>
/// Resolves URL-style route paths to component types. Routes come from the registrations that
/// Cascade.Generators emits for every <see cref="RouteAttribute"/> component (see
/// <see cref="GeneratedRegistrations"/>); untrimmed apps also scan loaded assemblies for them
/// (<see cref="ReflectionRouting.IsSupported"/>).
/// Thread-safe for concurrent registration and resolution.
/// </summary>
internal sealed partial class RouteResolver
{
    private readonly ConcurrentDictionary<string, RouteRegistration> routes = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool scanned;
    private volatile bool manualOnly;
    private int importedGeneratedCount;
    private readonly Lock scanLock = new();

    /// <summary>The number of registered routes.</summary>
    internal int RouteCount => routes.Count;

    /// <summary>
    /// Registers a component type with its route pattern and, when known, a factory that builds it
    /// without reflection. Registering the same type for the same pattern again is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the pattern is already registered to a different component type.
    /// </exception>
    internal void Register(Type componentType, string pattern, Func<IReadOnlyDictionary<string, string>, Component>? factory = null)
    {
        var normalized = NormalizePattern(pattern);
        var registration = new RouteRegistration(componentType, pattern, BuildRegex(normalized), factory);

        if (!routes.TryAdd(normalized, registration))
        {
            var existing = routes[normalized];
            if (existing.ComponentType != componentType)
            {
                throw new InvalidOperationException(
                    $"Duplicate route pattern '{pattern}' — already registered to {existing.ComponentType.Name}.");
            }

            if (existing.Factory is null && factory is not null)
            {
                routes[normalized] = registration;
            }
        }
    }

    /// <summary>
    /// Resolves a URL path to a component type and extracted path parameters.
    /// Returns null if no route matches.
    /// </summary>
    internal RouteMatch? Resolve(string path)
    {
        EnsureRoutes();

        var normalizedPath = NormalizePath(path);
        var trimmedPath = path.Trim('/');

        foreach (var kvp in routes)
        {
            var match = kvp.Value.Pattern.Match(normalizedPath);
            if (match.Success)
            {
                // Re-match against the trimmed (non-uppercased) path to preserve
                // original casing of parameter values.
                var originalMatch = kvp.Value.Pattern.Match(trimmedPath);
                var parameters = ExtractParameters(kvp.Value.OriginalPattern, originalMatch.Success ? originalMatch : match);
                return new RouteMatch(kvp.Value.ComponentType, parameters, kvp.Value.Factory);
            }
        }

        return null;
    }

    /// <summary>
    /// Scans all loaded assemblies for <see cref="RouteAttribute"/> on
    /// <see cref="Component"/> subclasses and registers them.
    /// </summary>
    [RequiresUnreferencedCode("Route scanning uses reflection to discover Component types with [Route] attributes.")]
    internal void ScanAssemblies()
    {
        lock (scanLock)
        {
            if (scanned)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ScanAssembly(assembly);
            }

            scanned = true;
        }
    }

    /// <summary>
    /// Scans a single assembly for components with [Route] attributes.
    /// </summary>
    [RequiresUnreferencedCode("Route scanning uses reflection to discover Component types with [Route] attributes.")]
    internal void ScanAssembly(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (!type.IsAbstract && type.IsSubclassOf(typeof(Component)))
            {
                var routeAttr = type.GetCustomAttribute<RouteAttribute>();
                if (routeAttr is not null)
                {
                    Register(type, routeAttr.Pattern);
                }
            }
        }
    }

    /// <summary>
    /// Resolves a full URI (with scheme, query, fragment) by extracting the path
    /// and delegating to the standard path-based resolution. Query parameters
    /// and fragments are discarded — use <see cref="DeepLinkResolver"/> for
    /// full deep link handling.
    /// </summary>
    internal RouteMatch? ResolveUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            string path = parsed.AbsolutePath;
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                path = "/" + parsed.Host;
            }

            return Resolve(path);
        }

        return Resolve(uri);
    }

    /// <summary>
    /// Resets all registrations and scan state. Used for testing.
    /// </summary>
    internal void Reset()
    {
        routes.Clear();
        lock (scanLock)
        {
            scanned = false;
            manualOnly = false;
            importedGeneratedCount = 0;
        }
    }

    /// <summary>
    /// Restricts the resolver to routes registered through <see cref="Register"/>: no generated
    /// registrations and no assembly scan. Used for testing with manual registration.
    /// </summary>
    internal void MarkScanned()
    {
        lock (scanLock)
        {
            scanned = true;
            manualOnly = true;
        }
    }

    /// <summary>
    /// Imports generated registrations not seen yet (an assembly's module initializer can run after
    /// the first navigation), then scans assemblies once when reflection is allowed, which also picks up
    /// route components generated code can't name (private nested, generic). The generated factory wins
    /// for a component found both ways.
    /// </summary>
    private void EnsureRoutes()
    {
        if (manualOnly)
        {
            return;
        }

        lock (scanLock)
        {
            if (manualOnly)
            {
                return;
            }

            var generated = GeneratedRegistrations.RoutesFrom(importedGeneratedCount);
            foreach (var route in generated)
            {
                Register(route.ComponentType, route.Pattern, route.Create);
            }
            importedGeneratedCount += generated.Length;

            if (!scanned && ReflectionRouting.IsSupported)
            {
                ScanAssemblies();
            }
        }
    }

    private static string NormalizePattern(string pattern)
    {
        return pattern.Trim('/').ToUpperInvariant();
    }

    private static string NormalizePath(string path)
    {
        return path.Trim('/').ToUpperInvariant();
    }

    private static Regex BuildRegex(string normalizedPattern)
    {
        var segments = normalizedPattern.Split('/');
        var regexParts = new List<string>();

        foreach (var segment in segments)
        {
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                regexParts.Add("(?<" + ParamName(segment[1..^1]) + ">[^/]+)");
            }
            else
            {
                regexParts.Add(Regex.Escape(segment));
            }
        }

        return new Regex("^" + string.Join("/", regexParts) + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    // A route parameter may declare a type constraint (e.g. {id:int}); that type is used by
    // the CASCADENAV002 analyzer to check the target property. The value is bound at runtime
    // by converting to the property's own type, so the constraint is stripped for matching.
    private static string ParamName(string inner)
    {
        int colon = inner.IndexOf(':', System.StringComparison.Ordinal);
        return colon < 0 ? inner : inner.Substring(0, colon);
    }

    private static Dictionary<string, string> ExtractParameters(string originalPattern, Match match)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = NormalizePattern(originalPattern);
        var segments = normalized.Split('/');

        foreach (var segment in segments)
        {
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                var paramName = ParamName(segment[1..^1]);
                var group = match.Groups[paramName];
                if (group.Success)
                {
                    parameters[paramName] = group.Value;
                }
            }
        }

        return parameters;
    }
}

/// <summary>
/// A registered route mapping a pattern to a component type.
/// </summary>
internal sealed class RouteRegistration
{
    internal RouteRegistration(Type componentType, string originalPattern, Regex pattern, Func<IReadOnlyDictionary<string, string>, Component>? factory)
    {
        ComponentType = componentType;
        OriginalPattern = originalPattern;
        Pattern = pattern;
        Factory = factory;
    }

    /// <summary>Builds the component without reflection; null when only the reflection path can.</summary>
    internal Func<IReadOnlyDictionary<string, string>, Component>? Factory { get; }

    internal Type ComponentType { get; }
    internal string OriginalPattern { get; }
    internal Regex Pattern { get; }
}

/// <summary>
/// The result of resolving a route path: the target component type and
/// any extracted path parameters.
/// </summary>
internal sealed class RouteMatch
{
    internal RouteMatch(Type componentType, Dictionary<string, string> parameters, Func<IReadOnlyDictionary<string, string>, Component>? factory = null)
    {
        ComponentType = componentType;
        Parameters = parameters;
        Factory = factory;
    }

    /// <summary>Builds the component without reflection; null when only the reflection path can.</summary>
    internal Func<IReadOnlyDictionary<string, string>, Component>? Factory { get; }

    internal Type ComponentType { get; }
    internal Dictionary<string, string> Parameters { get; }
}

/// <summary>
/// Whether routing may fall back to reflection: scanning loaded assemblies for <see cref="RouteAttribute"/>
/// and constructing components through <see cref="Activator"/>. On by default; the package's build targets
/// turn it off for trimmed and Native AOT publishes, so the trimmer removes that code and the generated
/// registrations do the work.
/// </summary>
internal static class ReflectionRouting
{
    private const string SwitchName = "Cascade.UI.Navigation.ReflectionRouting.IsSupported";

    [FeatureSwitchDefinition(SwitchName)]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    internal static bool IsSupported { get; } = !AppContext.TryGetSwitch(SwitchName, out bool enabled) || enabled;
}

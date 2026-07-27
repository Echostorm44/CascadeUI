using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Cascade.UI;

/// <summary>
/// Resolves URL-style route paths to component types by scanning for
/// <see cref="RouteAttribute"/> on <see cref="Component"/> subclasses.
/// Thread-safe for concurrent registration and resolution.
/// </summary>
internal sealed partial class RouteResolver
{
    private readonly ConcurrentDictionary<string, RouteRegistration> routes = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool scanned;
    private readonly Lock scanLock = new();

    /// <summary>The number of registered routes.</summary>
    internal int RouteCount => routes.Count;

    /// <summary>
    /// Registers a component type with its route pattern. Called during
    /// assembly scanning or manual registration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a duplicate route pattern is registered.
    /// </exception>
    internal void Register(Type componentType, string pattern)
    {
        var normalized = NormalizePattern(pattern);
        var registration = new RouteRegistration(componentType, pattern, BuildRegex(normalized));

        if (!routes.TryAdd(normalized, registration))
        {
            throw new InvalidOperationException(
                $"Duplicate route pattern '{pattern}' — already registered to {routes[normalized].ComponentType.Name}.");
        }
    }

    /// <summary>
    /// Resolves a URL path to a component type and extracted path parameters.
    /// Returns null if no route matches.
    /// </summary>
    [RequiresUnreferencedCode("Route scanning uses reflection to discover Component types with [Route] attributes.")]
    internal RouteMatch? Resolve(string path)
    {
        EnsureScanned();

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
                return new RouteMatch(kvp.Value.ComponentType, parameters);
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
    [RequiresUnreferencedCode("Route scanning uses reflection to discover Component types with [Route] attributes.")]
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
        }
    }

    /// <summary>
    /// Marks the resolver as already scanned, preventing automatic assembly
    /// scanning on first Resolve call. Used for testing with manual registration.
    /// </summary>
    internal void MarkScanned()
    {
        lock (scanLock)
        {
            scanned = true;
        }
    }

    [RequiresUnreferencedCode("Route scanning uses reflection to discover Component types with [Route] attributes.")]
    private void EnsureScanned()
    {
        if (!scanned)
        {
            ScanAssemblies();
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
                regexParts.Add("(?<" + segment[1..^1] + ">[^/]+)");
            }
            else
            {
                regexParts.Add(Regex.Escape(segment));
            }
        }

        return new Regex("^" + string.Join("/", regexParts) + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
                var paramName = segment[1..^1];
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
    internal RouteRegistration(Type componentType, string originalPattern, Regex pattern)
    {
        ComponentType = componentType;
        OriginalPattern = originalPattern;
        Pattern = pattern;
    }

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
    internal RouteMatch(Type componentType, Dictionary<string, string> parameters)
    {
        ComponentType = componentType;
        Parameters = parameters;
    }

    internal Type ComponentType { get; }
    internal Dictionary<string, string> Parameters { get; }
}

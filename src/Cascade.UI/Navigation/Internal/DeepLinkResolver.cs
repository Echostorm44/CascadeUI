using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// Parses deep link URIs into navigation route matches. Handles the
/// scheme, path extraction, query parameter parsing, and fragment
/// identification. Delegates route resolution to <see cref="RouteResolver"/>.
/// </summary>
internal sealed class DeepLinkResolver
{
    private readonly RouteResolver routeResolver;
    private readonly HashSet<string> registeredSchemes = new(StringComparer.OrdinalIgnoreCase);

    public DeepLinkResolver(RouteResolver routeResolver)
    {
        this.routeResolver = routeResolver;
    }

    /// <summary>Registers a URI scheme this app handles (e.g., "myapp", "cascade").</summary>
    public void RegisterScheme(string scheme)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        registeredSchemes.Add(scheme);
    }

    /// <summary>
    /// Attempts to resolve a deep link URI to a route match.
    /// Returns null if the URI is malformed, the scheme is unregistered,
    /// or no route matches the path.
    /// </summary>
    [RequiresUnreferencedCode("Route resolution uses reflection to discover Component types with [Route] attributes.")]
    public DeepLinkResult? Resolve(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            // Try as relative path (e.g., "/settings/profile")
            var routeMatch = routeResolver.Resolve(uri);
            if (routeMatch == null)
            {
                return null;
            }

            return new DeepLinkResult(routeMatch, new Dictionary<string, string>(), null);
        }

        // Validate scheme
        if (registeredSchemes.Count > 0 && !registeredSchemes.Contains(parsed.Scheme))
        {
            return null;
        }

        // Extract path — for URIs like "myapp://settings/profile", the host is
        // "settings" and AbsolutePath is "/profile". Combine them to get the
        // full route path.
        string path = parsed.AbsolutePath.TrimStart('/');
        if (!string.IsNullOrEmpty(parsed.Host))
        {
            path = string.IsNullOrEmpty(path)
                ? parsed.Host
                : parsed.Host + "/" + path;
        }

        // Parse query parameters
        var queryParams = ParseQueryString(parsed.Query);

        // Extract fragment
        string? fragment = string.IsNullOrEmpty(parsed.Fragment)
            ? null
            : parsed.Fragment.TrimStart('#');

        // Resolve route
        var match = routeResolver.Resolve("/" + path);
        if (match == null)
        {
            return null;
        }

        // Merge query params into route params (route params take precedence)
        foreach (var kvp in queryParams)
        {
            match.Parameters.TryAdd(kvp.Key, kvp.Value);
        }

        return new DeepLinkResult(match, queryParams, fragment);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        string trimmed = query.TrimStart('?');
        foreach (string pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
            {
                string key = Uri.UnescapeDataString(pair[..eq]);
                string value = Uri.UnescapeDataString(pair[(eq + 1)..]);
                result[key] = value;
            }
            else
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
        }

        return result;
    }
}

/// <summary>Result of resolving a deep link URI.</summary>
internal sealed class DeepLinkResult
{
    public DeepLinkResult(
        RouteMatch route,
        IReadOnlyDictionary<string, string> queryParameters,
        string? fragment)
    {
        Route = route;
        QueryParameters = queryParameters;
        Fragment = fragment;
    }

    public RouteMatch Route { get; }
    public IReadOnlyDictionary<string, string> QueryParameters { get; }
    public string? Fragment { get; }
}

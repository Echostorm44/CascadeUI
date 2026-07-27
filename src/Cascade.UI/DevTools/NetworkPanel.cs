using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Network monitoring panel. Logs HTTP requests made by the application
/// via Cascade's HTTP client integration. Opt-in via EnableNetworkLogging().
/// </summary>
internal static class NetworkPanel
{
    private static readonly List<NetworkRequest> requests = [];
    private static bool loggingEnabled;
    private static int nextRequestId;
    private static readonly object syncLock = new();

    /// <summary>A logged network request with response.</summary>
    public sealed class NetworkRequest
    {
        /// <summary>Unique request ID.</summary>
        public int Id { get; init; }

        /// <summary>HTTP method (GET, POST, etc.).</summary>
        public required string Method { get; init; }

        /// <summary>Full request URL.</summary>
        public required string Url { get; init; }

        /// <summary>Request headers.</summary>
        public IReadOnlyDictionary<string, string> RequestHeaders { get; init; } = new Dictionary<string, string>();

        /// <summary>Request body (if captured, truncated to 64KB).</summary>
        public string? RequestBody { get; init; }

        /// <summary>HTTP status code (0 if no response yet).</summary>
        public int StatusCode { get; set; }

        /// <summary>Response headers.</summary>
        public IReadOnlyDictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>Response body (truncated to 64KB).</summary>
        public string? ResponseBody { get; set; }

        /// <summary>Request start time.</summary>
        public DateTime StartedAt { get; init; }

        /// <summary>Request end time (null if still in progress).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Duration in milliseconds (null if still in progress).</summary>
        public float? DurationMs => CompletedAt.HasValue
            ? (float)(CompletedAt.Value - StartedAt).TotalMilliseconds
            : null;

        /// <summary>Error message if the request failed.</summary>
        public string? Error { get; set; }

        /// <summary>Whether the request is still in progress.</summary>
        public bool InProgress => CompletedAt is null && Error is null;
    }

    /// <summary>Whether network request logging is enabled.</summary>
    public static bool IsLoggingEnabled => loggingEnabled;

    /// <summary>
    /// Enables network request logging. When enabled, all HTTP requests
    /// made through Cascade's HTTP integration will be captured.
    /// </summary>
    public static void EnableNetworkLogging()
    {
        loggingEnabled = true;
    }

    /// <summary>
    /// Disables network request logging and clears captured requests.
    /// </summary>
    public static void DisableNetworkLogging()
    {
        loggingEnabled = false;
        lock (syncLock)
        {
            requests.Clear();
        }
    }

    /// <summary>
    /// Registers the start of a network request. Called by the framework's
    /// HTTP handler. Returns a request ID that must be passed to CompleteRequest.
    /// </summary>
    internal static int RegisterRequest(string method, string url, IReadOnlyDictionary<string, string>? headers = null, string? body = null)
    {
        if (!loggingEnabled)
        {
            return -1;
        }

        lock (syncLock)
        {
            int id = nextRequestId++;
            requests.Add(new NetworkRequest
            {
                Id = id,
                Method = method,
                Url = url,
                RequestHeaders = headers ?? new Dictionary<string, string>(),
                RequestBody = TruncateBody(body),
                StartedAt = DateTime.UtcNow,
            });
            return id;
        }
    }

    /// <summary>
    /// Registers the completion of a network request.
    /// </summary>
    internal static void CompleteRequest(int requestId, int statusCode, IReadOnlyDictionary<string, string>? headers = null, string? body = null, string? error = null)
    {
        if (!loggingEnabled || requestId < 0)
        {
            return;
        }

        lock (syncLock)
        {
            var request = FindById(requestId);
            if (request is null)
            {
                return;
            }

            request.StatusCode = statusCode;
            request.ResponseHeaders = headers ?? new Dictionary<string, string>();
            request.ResponseBody = TruncateBody(body);
            request.CompletedAt = DateTime.UtcNow;
            request.Error = error;
        }
    }

    /// <summary>
    /// Registers a client type name for display in the panel.
    /// Called by DevToolsPanel when initializing the MCP connection.
    /// </summary>
    internal static void RegisterClient(string clientName)
    {
        // Client name is used for display in the network panel header.
        // Currently stored but not surfaced beyond identification.
    }

    /// <summary>Returns all captured requests, newest first.</summary>
    public static IReadOnlyList<NetworkRequest> GetRequests()
    {
        lock (syncLock)
        {
            var copy = new List<NetworkRequest>(requests);
            copy.Reverse();
            return copy;
        }
    }

    /// <summary>
    /// Returns requests filtered by method, URL pattern, or status code range.
    /// </summary>
    public static IReadOnlyList<NetworkRequest> GetFilteredRequests(
        string? methodFilter = null,
        string? urlContains = null,
        int? minStatus = null,
        int? maxStatus = null)
    {
        lock (syncLock)
        {
            var results = new List<NetworkRequest>();
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                var req = requests[i];
                if (methodFilter is not null && !string.Equals(req.Method, methodFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (urlContains is not null && !req.Url.Contains(urlContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (minStatus.HasValue && req.StatusCode < minStatus.Value)
                {
                    continue;
                }
                if (maxStatus.HasValue && req.StatusCode > maxStatus.Value)
                {
                    continue;
                }
                results.Add(req);
            }
            return results;
        }
    }

    /// <summary>Gets a specific request by ID.</summary>
    public static NetworkRequest? GetRequestById(int requestId)
    {
        lock (syncLock)
        {
            return FindById(requestId);
        }
    }

    /// <summary>Clears all captured requests.</summary>
    public static void ClearRequests()
    {
        lock (syncLock)
        {
            requests.Clear();
        }
    }

    private static NetworkRequest? FindById(int id)
    {
        foreach (var req in requests)
        {
            if (req.Id == id)
            {
                return req;
            }
        }
        return null;
    }

    private static string? TruncateBody(string? body)
    {
        if (body is null)
        {
            return null;
        }
        const int maxLength = 65536; // 64KB
        return body.Length <= maxLength ? body : body[..maxLength];
    }
}

#endif

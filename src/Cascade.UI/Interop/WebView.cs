namespace Cascade.UI;

/// <summary>
/// A high-level node for embedding web content inside a Cascade UI window.
/// Built on <see cref="NativeViewAdapter"/> with platform-specific backends:
/// WebView2 (Windows), WKWebView (macOS), WebKitGTK (Linux).
/// </summary>
public class WebView : Node
{
    /// <summary>The URL to navigate to.</summary>
    public string? Url { get; init; }

    /// <summary>HTML content to load directly instead of a URL.</summary>
    public string? Html { get; init; }

    /// <summary>
    /// A typed ref for programmatic control of the WebView.
    /// Attach via this property to get access to navigation, JS interop, etc.
    /// </summary>
    public NodeRef<WebViewRef>? WebViewRef { get; init; }

    // ── Internal state (stored until adapter is created) ─────────────

    internal bool configAllowNavigation = true;
    internal bool configAllowJavaScript = true;
    internal bool configAllowDevTools;
    internal string? configUserAgent;
    internal ColorValue? configBackgroundColor;
    internal NativeCompositingMode configCompositingMode;
    internal string? configProfileName;
    internal WebViewProfile? configProfileType;
    internal Action<WebViewNavigationStartedArgs>? handlerNavigationStarted;
    internal Action<WebViewNavigationCompletedArgs>? handlerNavigationCompleted;
    internal Action<WebViewNewWindowArgs>? handlerNewWindowRequested;
    internal Action<WebViewMessage>? handlerWebMessage;
    internal Func<WebViewDownloadRequestedArgs, Task>? handlerDownloadRequested;
    internal Action<WebViewDownloadProgressArgs>? handlerDownloadProgress;
    internal Action<WebViewDownloadCompletedArgs>? handlerDownloadCompleted;
    internal List<(string Script, InjectionTiming Timing)>? injectedScripts;
    internal Action? handlerFocusEntered;
    internal Action? handlerFocusExited;

    // ── Configuration ────────────────────────────────────────────────

    /// <summary>
    /// Sets whether the user can click links to navigate. Default: true.
    /// </summary>
    public WebView AllowNavigation(bool allow)
    {
        configAllowNavigation = allow;
        return this;
    }

    /// <summary>
    /// Sets whether JavaScript is enabled. Default: true.
    /// </summary>
    public WebView AllowJavaScript(bool allow)
    {
        configAllowJavaScript = allow;
        return this;
    }

    /// <summary>
    /// Sets whether the F12 developer tools are available. Default: false.
    /// </summary>
    public WebView AllowDevTools(bool allow)
    {
        configAllowDevTools = allow;
        return this;
    }

    /// <summary>
    /// Overrides the User-Agent header for all requests from this WebView.
    /// </summary>
    public WebView UserAgent(string userAgent)
    {
        configUserAgent = userAgent;
        return this;
    }

    /// <summary>
    /// Sets the background color shown before the first page load.
    /// </summary>
    public WebView BackgroundColor(ColorValue color)
    {
        configBackgroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets the compositing mode for this WebView.
    /// </summary>
    public WebView NativeCompositing(NativeCompositingMode mode)
    {
        configCompositingMode = mode;
        return this;
    }

    // ── Profile ──────────────────────────────────────────────────────

    /// <summary>
    /// Sets a named profile for cookie and storage persistence across app restarts.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    public WebView Profile(string profileName)
    {
        configProfileName = profileName;
        return this;
    }

    /// <summary>
    /// Sets the profile to a well-known profile type (e.g. in-memory/ephemeral).
    /// </summary>
    /// <param name="profile">The profile type.</param>
    public WebView Profile(WebViewProfile profile)
    {
        configProfileType = profile;
        return this;
    }

    // ── Navigation Events ────────────────────────────────────────────

    /// <summary>
    /// Registers a handler invoked when navigation starts. The handler
    /// can cancel navigation by setting <c>args.Cancel = true</c>.
    /// </summary>
    public WebView OnNavigationStarted(Action<WebViewNavigationStartedArgs> handler)
    {
        handlerNavigationStarted = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler invoked when navigation completes.
    /// </summary>
    public WebView OnNavigationCompleted(Action<WebViewNavigationCompletedArgs> handler)
    {
        handlerNavigationCompleted = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler invoked when a new window is requested
    /// (e.g. target="_blank" links).
    /// </summary>
    public WebView OnNewWindowRequested(Action<WebViewNewWindowArgs> handler)
    {
        handlerNewWindowRequested = handler;
        return this;
    }

    // ── JavaScript Interop ───────────────────────────────────────────

    /// <summary>
    /// Registers a handler for messages sent from JavaScript via
    /// <c>window.cascade.postMessage()</c>.
    /// </summary>
    public WebView OnWebMessage(Action<WebViewMessage> handler)
    {
        handlerWebMessage = handler;
        return this;
    }

    // ── Download Events ──────────────────────────────────────────────

    /// <summary>
    /// Registers a handler invoked when a download is requested.
    /// </summary>
    public WebView OnDownloadRequested(Func<WebViewDownloadRequestedArgs, Task> handler)
    {
        handlerDownloadRequested = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler invoked with download progress updates.
    /// </summary>
    public WebView OnDownloadProgress(Action<WebViewDownloadProgressArgs> handler)
    {
        handlerDownloadProgress = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler invoked when a download completes.
    /// </summary>
    public WebView OnDownloadCompleted(Action<WebViewDownloadCompletedArgs> handler)
    {
        handlerDownloadCompleted = handler;
        return this;
    }

    // ── Content Injection ────────────────────────────────────────────

    /// <summary>
    /// Injects a JavaScript script that runs at the specified timing
    /// for every page loaded in this WebView.
    /// </summary>
    /// <param name="script">The JavaScript code to inject.</param>
    /// <param name="timing">When the script should be injected relative to page load.</param>
    public WebView InjectScript(string script, InjectionTiming timing = InjectionTiming.DocumentEnd)
    {
        injectedScripts ??= new List<(string, InjectionTiming)>();
        injectedScripts.Add((script, timing));
        return this;
    }

    // ── Focus Events ─────────────────────────────────────────────────

    /// <summary>
    /// Registers a handler invoked when the WebView gains focus
    /// from Cascade's perspective.
    /// </summary>
    public WebView OnFocusEntered(Action handler)
    {
        handlerFocusEntered = handler;
        return this;
    }

    /// <summary>
    /// Registers a handler invoked when focus leaves the WebView
    /// and returns to a Cascade control.
    /// </summary>
    public WebView OnFocusExited(Action handler)
    {
        handlerFocusExited = handler;
        return this;
    }
}

/// <summary>
/// Well-known WebView profile types.
/// </summary>
public enum WebViewProfile
{
    /// <summary>
    /// Ephemeral profile — cookies, localStorage, sessionStorage, and cache
    /// are all cleared when the WebView is disposed. Nothing persists.
    /// </summary>
    InMemory
}

/// <summary>
/// When to inject a script relative to the page lifecycle.
/// </summary>
public enum InjectionTiming
{
    /// <summary>
    /// Injected before any page scripts run (equivalent to
    /// document_start in browser extensions).
    /// </summary>
    DocumentStart,

    /// <summary>
    /// Injected after DOM is ready but before the load event.
    /// </summary>
    DocumentEnd
}

/// <summary>
/// Event args for WebView navigation start events.
/// </summary>
public sealed class WebViewNavigationStartedArgs
{
    /// <summary>The URL being navigated to.</summary>
    public string Url { get; init; } = "";

    /// <summary>Set to true to cancel this navigation.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Event args for WebView navigation completion events.
/// </summary>
public sealed class WebViewNavigationCompletedArgs
{
    /// <summary>The URL that was navigated to.</summary>
    public string Url { get; init; } = "";

    /// <summary>The HTTP status code of the navigation.</summary>
    public int StatusCode { get; init; }

    /// <summary>Whether the navigation succeeded.</summary>
    public bool IsSuccess { get; init; }
}

/// <summary>
/// Event args for new window requests (target="_blank" links).
/// </summary>
public sealed class WebViewNewWindowArgs
{
    /// <summary>The URL requested for the new window.</summary>
    public string Url { get; init; } = "";

    /// <summary>Set to true to prevent the default new window behavior.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// A message sent from JavaScript to Cascade via window.cascade.postMessage().
/// </summary>
public sealed class WebViewMessage
{
    /// <summary>The JSON string body of the message.</summary>
    public string Body { get; init; } = "";
}

/// <summary>
/// Event args for download request events.
/// </summary>
public sealed class WebViewDownloadRequestedArgs
{
    /// <summary>The suggested file name from the server.</summary>
    public string SuggestedFileName { get; init; } = "";

    /// <summary>The MIME type of the download.</summary>
    public string MimeType { get; init; } = "";

    /// <summary>The total size in bytes, if known.</summary>
    public long? TotalBytes { get; init; }

    /// <summary>Set to the path where the file should be saved.</summary>
    public string? SavePath { get; set; }

    /// <summary>Set to true to allow the download, false to cancel.</summary>
    public bool Allow { get; set; }
}

/// <summary>
/// Event args for download progress updates.
/// </summary>
public sealed class WebViewDownloadProgressArgs
{
    /// <summary>Bytes received so far.</summary>
    public long BytesReceived { get; init; }

    /// <summary>Total bytes expected, if known.</summary>
    public long? TotalBytes { get; init; }
}

/// <summary>
/// Event args for download completion.
/// </summary>
public sealed class WebViewDownloadCompletedArgs
{
    /// <summary>The path where the file was saved.</summary>
    public string Path { get; init; } = "";

    /// <summary>The file name.</summary>
    public string FileName { get; init; } = "";

    /// <summary>Whether the download succeeded.</summary>
    public bool IsSuccess { get; init; }
}

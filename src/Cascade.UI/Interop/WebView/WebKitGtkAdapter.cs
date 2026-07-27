using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Linux WebKitGTK adapter.
///
/// <para><b>STATUS: NOT IMPLEMENTED — this is a stub.</b> The class defines the intended
/// <see cref="NativeViewAdapter"/> surface (WebKitGTK navigation, JS interop, cairo-surface
/// capture), but the private native methods below currently discard their arguments and return
/// <c>0</c>/<c>false</c>. Wiring them to libwebkit2gtk-4.1.so (or libwebkit6gtk.so for GTK4) is
/// still to be done.</para>
/// </summary>
internal sealed class WebKitGtkAdapter : NativeViewAdapter
{
    private nint webView;
    private nint settings;
    private bool created;
    private string? pendingUrl;
    private string? pendingHtml;
    private NativeCompositingMode compositingMode;

    // Event callbacks
    private Action<WebViewNavigationStartedArgs>? navigationStarted;
    private Action<WebViewNavigationCompletedArgs>? navigationCompleted;
    private Action<WebViewNewWindowArgs>? newWindowRequested;
    private Action<WebViewMessage>? webMessageReceived;
    private Action<WebViewDownloadRequestedArgs>? downloadRequested;
    private Action<WebViewDownloadProgressArgs>? downloadProgress;
    private Action<WebViewDownloadCompletedArgs>? downloadCompleted;

    // Configuration
    private bool allowJavaScript = true;
    private bool allowDevTools;
    private string? userAgent;
    private ColorValue? backgroundColor;
    private string? profileName;
    private WebViewProfile? profileType;
    private List<(string Script, InjectionTiming Timing)>? injectedScripts;

    internal WebKitGtkAdapter()
    {
    }

    /// <summary>
    /// Checks whether WebKitGTK is available on this system.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        return CheckWebKitGtkInstalled();
    }

    // ── Configuration ───────────────────────────────────────────────

    internal void SetPendingUrl(string url) { pendingUrl = url; }
    internal void SetPendingHtml(string html) { pendingHtml = html; }
    internal void SetAllowJavaScript(bool allow) { allowJavaScript = allow; }
    internal void SetAllowDevTools(bool allow) { allowDevTools = allow; }
    internal void SetUserAgent(string? agent) { userAgent = agent; }
    internal void SetBackgroundColor(ColorValue? color) { backgroundColor = color; }
    internal void SetProfileName(string? name) { profileName = name; }
    internal void SetProfileType(WebViewProfile? type) { profileType = type; }
    internal void SetCompositingMode(NativeCompositingMode mode) { compositingMode = mode; }

    internal void AddInjectedScript(string script, InjectionTiming timing)
    {
        injectedScripts ??= new List<(string, InjectionTiming)>();
        injectedScripts.Add((script, timing));
    }

    // ── Event registration ──────────────────────────────────────────

    internal void OnNavigationStarted(Action<WebViewNavigationStartedArgs> handler)
    {
        navigationStarted = handler;
    }

    internal void OnNavigationCompleted(Action<WebViewNavigationCompletedArgs> handler)
    {
        navigationCompleted = handler;
    }

    internal void OnNewWindowRequested(Action<WebViewNewWindowArgs> handler)
    {
        newWindowRequested = handler;
    }

    internal void OnWebMessageReceived(Action<WebViewMessage> handler)
    {
        webMessageReceived = handler;
    }

    internal void OnDownloadRequested(Action<WebViewDownloadRequestedArgs> handler)
    {
        downloadRequested = handler;
    }

    internal void OnDownloadProgress(Action<WebViewDownloadProgressArgs> handler)
    {
        downloadProgress = handler;
    }

    internal void OnDownloadCompleted(Action<WebViewDownloadCompletedArgs> handler)
    {
        downloadCompleted = handler;
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    protected override void OnCreate(NativeViewHost host)
    {
        if (created)
        {
            return;
        }

        try
        {
            // 1. Create WebKitSettings
            settings = CreateWebKitSettings();

            // 2. Apply settings
            ApplySettings();

            // 3. Create the WebKitWebView widget
            webView = CreateWebKitWebView(settings);

            if (webView == 0)
            {
                return;
            }

            // 4. Embed the widget into the parent window
            nint parentWidget = host.ParentHandle;
            EmbedWidget(webView, parentWidget);

            // 5. Register signal handlers
            RegisterSignalHandlers();

            // 6. Inject the cascade.postMessage bridge
            InjectCascadeBridge();

            // 7. Process user injection scripts
            ProcessInjectedScripts();

            // 8. Navigate to initial content
            if (!string.IsNullOrEmpty(pendingHtml))
            {
                LoadHtml(webView, pendingHtml);
            }
            else if (!string.IsNullOrEmpty(pendingUrl))
            {
                Navigate(webView, pendingUrl);
            }

            created = true;
        }
        catch (DllNotFoundException)
        {
            // WebKitGTK not installed — degrade gracefully
        }
    }

    protected override void OnDestroy()
    {
        if (!created)
        {
            return;
        }

        if (webView != 0)
        {
            DestroyWidget(webView);
            webView = 0;
        }

        settings = 0;
        created = false;
    }

    // ── Layout ───────────────────────────────────────────────────────

    protected override void OnResize(int widthPx, int heightPx, float scale)
    {
        if (webView == 0)
        {
            return;
        }

        _ = scale;
        SetWidgetSize(webView, widthPx, heightPx);
    }

    // ── Focus ────────────────────────────────────────────────────────

    protected override void OnFocusGained()
    {
        if (webView != 0)
        {
            GrabFocus(webView);
        }
    }

    protected override void OnFocusLost()
    {
        // GTK handles focus loss via the focus chain
    }

    // ── Texture Bridge Frame Capture ────────────────────────────────

    protected override bool CaptureFrame(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        if (webView == 0)
        {
            return false;
        }

        // WebKitWebView.get_snapshot for offscreen capture
        return CaptureSnapshot(webView, buffer, widthPx, heightPx, strideBytes);
    }

    // ── Navigation ──────────────────────────────────────────────────

    internal Task NavigateAsync(string url)
    {
        if (webView != 0) { Navigate(webView, url); }
        return Task.CompletedTask;
    }

    internal Task GoBackAsync()
    {
        if (webView != 0) { GoBack(webView); }
        return Task.CompletedTask;
    }

    internal Task GoForwardAsync()
    {
        if (webView != 0) { GoForward(webView); }
        return Task.CompletedTask;
    }

    internal Task ReloadAsync()
    {
        if (webView != 0) { Reload(webView); }
        return Task.CompletedTask;
    }

    internal Task StopAsync()
    {
        if (webView != 0) { StopLoading(webView); }
        return Task.CompletedTask;
    }

    // ── JavaScript Interop ──────────────────────────────────────────

    internal async Task<string> EvaluateScriptAsync(string script)
    {
        if (webView == 0) { return ""; }
        return await Task.FromResult(EvaluateJavaScript(webView, script));
    }

    internal Task ExecuteScriptAsync(string script)
    {
        if (webView != 0) { ExecuteJavaScript(webView, script); }
        return Task.CompletedTask;
    }

    internal Task PostMessageAsync(string message)
    {
        if (webView != 0)
        {
            string escaped = message.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            ExecuteJavaScript(webView,
                $"window.cascade && window.cascade.onMessage && window.cascade.onMessage('{escaped}')");
        }
        return Task.CompletedTask;
    }

    internal Task InjectCssAsync(string css)
    {
        if (webView != 0)
        {
            string escapedCss = css.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            string script = $"(function(){{var s=document.createElement('style');s.textContent='{escapedCss}';document.head.appendChild(s);}})()";
            ExecuteJavaScript(webView, script);
        }
        return Task.CompletedTask;
    }

    // ── Cookie & Storage ────────────────────────────────────────────

    internal Task ClearCookiesAsync()
    {
        if (webView != 0) { ClearCookies(webView); }
        return Task.CompletedTask;
    }

    internal Task ClearStorageAsync()
    {
        if (webView != 0) { ClearStorage(webView); }
        return Task.CompletedTask;
    }

    // ── Print ───────────────────────────────────────────────────────

    internal Task PrintAsync()
    {
        if (webView != 0) { PrintWebView(webView); }
        return Task.CompletedTask;
    }

    internal Task PrintToPdfAsync(string path)
    {
        if (webView != 0) { CreatePdf(webView, path); }
        return Task.CompletedTask;
    }

    // ── State ───────────────────────────────────────────────────────

    internal bool CanGoBack => webView != 0 && GetCanGoBack(webView);
    internal bool CanGoForward => webView != 0 && GetCanGoForward(webView);
    internal string CurrentUrl => webView != 0 ? GetCurrentUrl(webView) : "";
    internal string Title => webView != 0 ? GetTitle(webView) : "";
    internal bool IsLoading { get; private set; }

    // ─── Private implementation ─────────────────────────────────────

    private void ApplySettings()
    {
        if (settings == 0)
        {
            return;
        }

        SetJavaScriptEnabled(settings, allowJavaScript);
        SetDeveloperExtrasEnabled(settings, allowDevTools);

        if (userAgent is not null)
        {
            SetUserAgentOverride(settings, userAgent);
        }

        _ = backgroundColor;
        _ = profileName;
        _ = profileType;
        _ = compositingMode;
    }

    private void RegisterSignalHandlers()
    {
        // GTK signal connections:
        // g_signal_connect(webView, "decide-policy", ...) for navigation
        // g_signal_connect(webView, "load-changed", ...) for load events
        // g_signal_connect(webView, "create", ...) for new window
        // g_signal_connect(webView, "script-message-received::cascade", ...) for messages
        _ = navigationStarted;
        _ = navigationCompleted;
        _ = newWindowRequested;
        _ = webMessageReceived;
        _ = downloadRequested;
        _ = downloadProgress;
        _ = downloadCompleted;
    }

    private void InjectCascadeBridge()
    {
        if (webView == 0)
        {
            return;
        }

        // Register the cascade message handler and inject the bridge script
        const string bridgeScript = """
            window.cascade = window.cascade || {};
            window.cascade.postMessage = function(msg) {
                window.webkit.messageHandlers.cascade.postMessage(JSON.stringify(msg));
            };
            """;
        AddUserScript(webView, bridgeScript, InjectionTiming.DocumentStart);
    }

    private void ProcessInjectedScripts()
    {
        if (injectedScripts is null || webView == 0)
        {
            return;
        }

        foreach (var (script, timing) in injectedScripts)
        {
            AddUserScript(webView, script, timing);
        }
    }

    // ─── GTK/WebKit stubs ───────────────────────────────────────────

    private static bool CheckWebKitGtkInstalled() => false;
    private static nint CreateWebKitSettings() => 0;
    private static nint CreateWebKitWebView(nint settings) { _ = settings; return 0; }
    private static void EmbedWidget(nint widget, nint parent) { _ = widget; _ = parent; }
    private static void DestroyWidget(nint widget) { _ = widget; }
    private static void SetWidgetSize(nint widget, int w, int h) { _ = widget; _ = w; _ = h; }
    private static void GrabFocus(nint widget) { _ = widget; }

    private static bool CaptureSnapshot(nint wv, Span<byte> buffer, int w, int h, int stride)
    {
        _ = wv; _ = buffer; _ = w; _ = h; _ = stride;
        return false;
    }

    private static void Navigate(nint wv, string url) { _ = wv; _ = url; }
    private static void LoadHtml(nint wv, string html) { _ = wv; _ = html; }
    private static void GoBack(nint wv) { _ = wv; }
    private static void GoForward(nint wv) { _ = wv; }
    private static void Reload(nint wv) { _ = wv; }
    private static void StopLoading(nint wv) { _ = wv; }

    private static string EvaluateJavaScript(nint wv, string script)
    {
        _ = wv; _ = script;
        return "";
    }

    private static void ExecuteJavaScript(nint wv, string script) { _ = wv; _ = script; }
    private static void SetJavaScriptEnabled(nint settings, bool enabled) { _ = settings; _ = enabled; }
    private static void SetDeveloperExtrasEnabled(nint settings, bool enabled) { _ = settings; _ = enabled; }
    private static void SetUserAgentOverride(nint settings, string agent) { _ = settings; _ = agent; }
    private static void AddUserScript(nint wv, string script, InjectionTiming timing)
    {
        _ = wv; _ = script; _ = timing;
    }
    private static void ClearCookies(nint wv) { _ = wv; }
    private static void ClearStorage(nint wv) { _ = wv; }
    private static void PrintWebView(nint wv) { _ = wv; }
    private static void CreatePdf(nint wv, string path) { _ = wv; _ = path; }
    private static bool GetCanGoBack(nint wv) { _ = wv; return false; }
    private static bool GetCanGoForward(nint wv) { _ = wv; return false; }
    private static string GetCurrentUrl(nint wv) { _ = wv; return ""; }
    private static string GetTitle(nint wv) { _ = wv; return ""; }
}

using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Windows WebView2 (Chromium) adapter.
///
/// <para><b>STATUS: NOT IMPLEMENTED — this is a stub.</b> The class defines the intended
/// <see cref="NativeViewAdapter"/> surface (navigation, JS interop, downloads, offscreen
/// capture via ICoreWebView2CompositionController), but the private native methods below
/// (<c>CreateWebView2Environment</c>, <c>Navigate</c>, <c>CaptureFromCompositionController</c>,
/// …) currently return <c>0</c>/<c>false</c> and do nothing. Hosting real WebView2 content
/// requires wiring these to WebView2Loader.dll P/Invokes. Do not treat the WebView control as
/// functional until that is done.</para>
/// </summary>
internal sealed class WebView2Adapter : NativeViewAdapter
{
    private nint environment;
    private nint controller;
    private nint webView;
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
    private bool allowNavigation = true;
    private bool allowJavaScript = true;
    private bool allowDevTools;
    private string? userAgent;
    private ColorValue? backgroundColor;
    private string? profileName;
    private WebViewProfile? profileType;
    private List<(string Script, InjectionTiming Timing)>? injectedScripts;

    internal WebView2Adapter()
    {
    }

    /// <summary>
    /// Checks whether the WebView2 runtime is installed on this system.
    /// </summary>
    internal static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        // Check for WebView2 runtime via registry or by trying to load WebView2Loader.dll
        return CheckWebView2Runtime();
    }

    // ── Configuration (set before Create) ───────────────────────────

    internal void SetPendingUrl(string url) { pendingUrl = url; }
    internal void SetPendingHtml(string html) { pendingHtml = html; }
    internal void SetAllowNavigation(bool allow) { allowNavigation = allow; }
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

        // 1. Create WebView2 environment with profile settings
        string? userDataFolder = ResolveUserDataFolder();
        environment = CreateWebView2Environment(userDataFolder);

        if (environment == 0)
        {
            return; // WebView2 runtime not installed
        }

        // 2. Create controller (composition or windowed based on compositing mode)
        nint parentHwnd = host.ParentHandle;
        controller = compositingMode == NativeCompositingMode.TextureBridge
            ? CreateCompositionController(environment, parentHwnd)
            : CreateWindowedController(environment, parentHwnd);

        if (controller == 0)
        {
            return;
        }

        // 3. Get the CoreWebView2 from the controller
        webView = GetCoreWebView2(controller);

        // 4. Apply configuration
        ApplySettings();

        // 5. Register event handlers
        RegisterEventHandlers();

        // 6. Add the cascade.postMessage bridge script
        InjectCascadeBridge();

        // 7. Process any pre-registered injection scripts
        ProcessInjectedScripts();

        // 8. Navigate to initial URL or HTML
        if (!string.IsNullOrEmpty(pendingHtml))
        {
            NavigateToString(webView, pendingHtml);
        }
        else if (!string.IsNullOrEmpty(pendingUrl))
        {
            Navigate(webView, pendingUrl);
        }

        created = true;
    }

    protected override void OnDestroy()
    {
        if (!created)
        {
            return;
        }

        if (controller != 0)
        {
            CloseController(controller);
            controller = 0;
        }

        webView = 0;
        environment = 0;
        created = false;
    }

    // ── Layout ───────────────────────────────────────────────────────

    protected override void OnResize(int widthPx, int heightPx, float scale)
    {
        if (controller == 0)
        {
            return;
        }

        SetControllerBounds(controller, 0, 0, widthPx, heightPx);
    }

    // ── Input (Texture Bridge Mode) ──────────────────────────────────

    protected override void OnMouseEvent(NativeMouseEvent e)
    {
        if (controller == 0)
        {
            return;
        }

        // Forward to composition controller for hit testing and event delivery
        SendMouseEventToController(controller, e);
    }

    protected override void OnKeyEvent(NativeKeyEvent e)
    {
        if (controller == 0)
        {
            return;
        }

        SendKeyEventToController(controller, e);
    }

    // ── Focus ────────────────────────────────────────────────────────

    protected override void OnFocusGained()
    {
        if (controller != 0)
        {
            FocusController(controller);
        }
    }

    protected override void OnFocusLost()
    {
        if (controller != 0)
        {
            BlurController(controller);
        }
    }

    // ── Texture Bridge Frame Capture ────────────────────────────────

    protected override bool CaptureFrame(Span<byte> buffer, int widthPx, int heightPx, int strideBytes)
    {
        if (controller == 0)
        {
            return false;
        }

        // WebView2's composition controller supports offscreen rendering
        // via ICoreWebView2CompositionController
        return CaptureFromCompositionController(controller, buffer, widthPx, heightPx, strideBytes);
    }

    // ── Navigation (programmatic via WebViewRef) ────────────────────

    internal Task NavigateAsync(string url)
    {
        if (webView != 0)
        {
            Navigate(webView, url);
        }
        return Task.CompletedTask;
    }

    internal Task GoBackAsync()
    {
        if (webView != 0)
        {
            GoBack(webView);
        }
        return Task.CompletedTask;
    }

    internal Task GoForwardAsync()
    {
        if (webView != 0)
        {
            GoForward(webView);
        }
        return Task.CompletedTask;
    }

    internal Task ReloadAsync()
    {
        if (webView != 0)
        {
            Reload(webView);
        }
        return Task.CompletedTask;
    }

    internal Task StopAsync()
    {
        if (webView != 0)
        {
            Stop(webView);
        }
        return Task.CompletedTask;
    }

    // ── JavaScript Interop ──────────────────────────────────────────

    internal async Task<string> EvaluateScriptAsync(string script)
    {
        if (webView == 0)
        {
            return "";
        }

        return await Task.FromResult(EvaluateScript(webView, script));
    }

    internal Task ExecuteScriptAsync(string script)
    {
        if (webView != 0)
        {
            ExecuteScript(webView, script);
        }
        return Task.CompletedTask;
    }

    internal Task PostMessageAsync(string message)
    {
        if (webView != 0)
        {
            PostWebMessage(webView, message);
        }
        return Task.CompletedTask;
    }

    internal async Task InjectCssAsync(string css)
    {
        if (webView != 0)
        {
            string escapedCss = css.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            string script = $"(function(){{var s=document.createElement('style');s.textContent='{escapedCss}';document.head.appendChild(s);}})()";
            await ExecuteScriptAsync(script);
        }
    }

    // ── Cookie & Storage ────────────────────────────────────────────

    internal Task ClearCookiesAsync()
    {
        if (webView != 0)
        {
            ClearCookies(webView);
        }
        return Task.CompletedTask;
    }

    internal Task ClearStorageAsync()
    {
        if (webView != 0)
        {
            ClearStorage(webView);
        }
        return Task.CompletedTask;
    }

    // ── Print ───────────────────────────────────────────────────────

    internal Task PrintAsync()
    {
        if (webView != 0)
        {
            Print(webView);
        }
        return Task.CompletedTask;
    }

    internal Task PrintToPdfAsync(string path)
    {
        if (webView != 0)
        {
            PrintToPdf(webView, path);
        }
        return Task.CompletedTask;
    }

    // ── State (reactive properties for WebViewRef) ──────────────────

    internal bool CanGoBack => webView != 0 && GetCanGoBack(webView);
    internal bool CanGoForward => webView != 0 && GetCanGoForward(webView);
    internal string CurrentUrl => webView != 0 ? GetCurrentUrl(webView) : "";
    internal string Title => webView != 0 ? GetTitle(webView) : "";
    internal bool IsLoading { get; private set; }

    // ─── Private implementation ─────────────────────────────────────

    private string? ResolveUserDataFolder()
    {
        if (profileType == WebViewProfile.InMemory)
        {
            return null; // In-memory, no persistence
        }

        if (!string.IsNullOrEmpty(profileName))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(appData, "CascadeUI", "WebView2", profileName);
        }

        return null; // Default profile
    }

    private void ApplySettings()
    {
        if (webView == 0)
        {
            return;
        }

        SetJavaScriptEnabled(webView, allowJavaScript);
        SetDevToolsEnabled(webView, allowDevTools);
        SetNavigationAllowed(webView, allowNavigation);

        if (userAgent is not null)
        {
            SetUserAgentOverride(webView, userAgent);
        }

        if (backgroundColor.HasValue)
        {
            SetWebViewBackgroundColor(webView, backgroundColor.Value);
        }
    }

    private void RegisterEventHandlers()
    {
        // WebView2 event registration via COM callbacks
        // The actual runtime registers event tokens; here we store the delegates
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

        // Inject the cascade.postMessage bridge for web→native communication
        const string bridgeScript = """
            window.cascade = window.cascade || {};
            window.cascade.postMessage = function(msg) {
                window.chrome.webview.postMessage(JSON.stringify(msg));
            };
            """;
        AddScriptToExecuteOnDocumentCreated(webView, bridgeScript);
    }

    private void ProcessInjectedScripts()
    {
        if (injectedScripts is null || webView == 0)
        {
            return;
        }

        foreach (var (script, timing) in injectedScripts)
        {
            if (timing == InjectionTiming.DocumentStart)
            {
                AddScriptToExecuteOnDocumentCreated(webView, script);
            }
            else
            {
                // DocumentEnd: execute after page load
                ExecuteScript(webView, script);
            }
        }
    }

    // ─── P/Invoke stubs ─────────────────────────────────────────────
    // At runtime these become COM interop calls via WebView2Loader.dll

    private static bool CheckWebView2Runtime()
    {
        // GetAvailableCoreWebView2BrowserVersionString
        return false; // Will be true at runtime if installed
    }

    private static nint CreateWebView2Environment(string? userDataFolder)
    {
        _ = userDataFolder;
        return 0;
    }

    private static nint CreateCompositionController(nint env, nint parentHwnd)
    {
        _ = env; _ = parentHwnd;
        return 0;
    }

    private static nint CreateWindowedController(nint env, nint parentHwnd)
    {
        _ = env; _ = parentHwnd;
        return 0;
    }

    private static nint GetCoreWebView2(nint controller)
    {
        _ = controller;
        return 0;
    }

    private static void CloseController(nint controller)
    {
        _ = controller;
    }

    private static void SetControllerBounds(nint controller, int x, int y, int w, int h)
    {
        _ = controller; _ = x; _ = y; _ = w; _ = h;
    }

    private static void SendMouseEventToController(nint controller, NativeMouseEvent e)
    {
        _ = controller; _ = e;
    }

    private static void SendKeyEventToController(nint controller, NativeKeyEvent e)
    {
        _ = controller; _ = e;
    }

    private static void FocusController(nint controller)
    {
        _ = controller;
    }

    private static void BlurController(nint controller)
    {
        _ = controller;
    }

    private static bool CaptureFromCompositionController(nint controller,
        Span<byte> buffer, int w, int h, int stride)
    {
        _ = controller; _ = buffer; _ = w; _ = h; _ = stride;
        return false;
    }

    private static void Navigate(nint webView, string url)
    {
        _ = webView; _ = url;
    }

    private static void NavigateToString(nint webView, string html)
    {
        _ = webView; _ = html;
    }

    private static void GoBack(nint webView) { _ = webView; }
    private static void GoForward(nint webView) { _ = webView; }
    private static void Reload(nint webView) { _ = webView; }
    private static void Stop(nint webView) { _ = webView; }

    private static string EvaluateScript(nint webView, string script)
    {
        _ = webView; _ = script;
        return "";
    }

    private static void ExecuteScript(nint webView, string script)
    {
        _ = webView; _ = script;
    }

    private static void PostWebMessage(nint webView, string message)
    {
        _ = webView; _ = message;
    }

    private static void AddScriptToExecuteOnDocumentCreated(nint webView, string script)
    {
        _ = webView; _ = script;
    }

    private static void SetJavaScriptEnabled(nint webView, bool enabled)
    {
        _ = webView; _ = enabled;
    }

    private static void SetDevToolsEnabled(nint webView, bool enabled)
    {
        _ = webView; _ = enabled;
    }

    private static void SetNavigationAllowed(nint webView, bool allowed)
    {
        _ = webView; _ = allowed;
    }

    private static void SetUserAgentOverride(nint webView, string agent)
    {
        _ = webView; _ = agent;
    }

    private static void SetWebViewBackgroundColor(nint webView, ColorValue color)
    {
        _ = webView; _ = color;
    }

    private static void ClearCookies(nint webView) { _ = webView; }
    private static void ClearStorage(nint webView) { _ = webView; }
    private static void Print(nint webView) { _ = webView; }

    private static void PrintToPdf(nint webView, string path)
    {
        _ = webView; _ = path;
    }

    private static bool GetCanGoBack(nint webView)
    {
        _ = webView;
        return false;
    }

    private static bool GetCanGoForward(nint webView)
    {
        _ = webView;
        return false;
    }

    private static string GetCurrentUrl(nint webView)
    {
        _ = webView;
        return "";
    }

    private static string GetTitle(nint webView)
    {
        _ = webView;
        return "";
    }
}

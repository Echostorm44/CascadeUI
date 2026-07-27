namespace Cascade.UI;

/// <summary>
/// A typed reference to a <see cref="WebView"/> control that provides
/// programmatic access to navigation, JavaScript interop, cookie management,
/// printing, and content injection after the WebView is mounted.
/// </summary>
public sealed class WebViewRef : Node
{
    // The platform adapter is set internally when the WebView is created
    internal NativeViewAdapter? adapter;

    // ── Navigation ───────────────────────────────────────────────────

    /// <summary>Navigates to the specified URL.</summary>
    public Task NavigateAsync(string url)
    {
        if (adapter is WebView2Adapter win)
        {
            return win.NavigateAsync(url);
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.NavigateAsync(url);
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.NavigateAsync(url);
        }
        return Task.CompletedTask;
    }

    /// <summary>Navigates back in the browsing history.</summary>
    public Task GoBackAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.GoBackAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.GoBackAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.GoBackAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>Navigates forward in the browsing history.</summary>
    public Task GoForwardAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.GoForwardAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.GoForwardAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.GoForwardAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>Reloads the current page.</summary>
    public Task ReloadAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.ReloadAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.ReloadAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.ReloadAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>Stops any in-progress navigation or loading.</summary>
    public Task StopAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.StopAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.StopAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.StopAsync();
        }
        return Task.CompletedTask;
    }

    // ── Navigation State (reactive) ──────────────────────────────────

    /// <summary>True if the WebView can navigate back. Reactive.</summary>
    public bool CanGoBack
    {
        get
        {
            if (adapter is WebView2Adapter win) { return win.CanGoBack; }
            if (adapter is WkWebViewAdapter mac) { return mac.CanGoBack; }
            if (adapter is WebKitGtkAdapter linux) { return linux.CanGoBack; }
            return false;
        }
    }

    /// <summary>True if the WebView can navigate forward. Reactive.</summary>
    public bool CanGoForward
    {
        get
        {
            if (adapter is WebView2Adapter win) { return win.CanGoForward; }
            if (adapter is WkWebViewAdapter mac) { return mac.CanGoForward; }
            if (adapter is WebKitGtkAdapter linux) { return linux.CanGoForward; }
            return false;
        }
    }

    /// <summary>The URL of the currently loaded page. Reactive.</summary>
    public string CurrentUrl
    {
        get
        {
            if (adapter is WebView2Adapter win) { return win.CurrentUrl; }
            if (adapter is WkWebViewAdapter mac) { return mac.CurrentUrl; }
            if (adapter is WebKitGtkAdapter linux) { return linux.CurrentUrl; }
            return "";
        }
    }

    /// <summary>The title of the currently loaded page. Reactive.</summary>
    public string Title
    {
        get
        {
            if (adapter is WebView2Adapter win) { return win.Title; }
            if (adapter is WkWebViewAdapter mac) { return mac.Title; }
            if (adapter is WebKitGtkAdapter linux) { return linux.Title; }
            return "";
        }
    }

    /// <summary>True if the WebView is currently loading a page. Reactive.</summary>
    public bool IsLoading
    {
        get
        {
            if (adapter is WebView2Adapter win) { return win.IsLoading; }
            if (adapter is WkWebViewAdapter mac) { return mac.IsLoading; }
            if (adapter is WebKitGtkAdapter linux) { return linux.IsLoading; }
            return false;
        }
    }

    // ── JavaScript Interop ───────────────────────────────────────────

    /// <summary>
    /// Evaluates a JavaScript expression and returns the result,
    /// deserialized to the specified type.
    /// Supports string, int, long, double, float, bool, and decimal.
    /// For complex types, use ExecuteAsync and parse the result yourself.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="script">The JavaScript expression to evaluate.</param>
    public async Task<T> EvaluateAsync<T>(string script)
    {
        string result;
        if (adapter is WebView2Adapter win)
        {
            result = await win.EvaluateScriptAsync(script);
        }
        else if (adapter is WkWebViewAdapter mac)
        {
            result = await mac.EvaluateScriptAsync(script);
        }
        else if (adapter is WebKitGtkAdapter linux)
        {
            result = await linux.EvaluateScriptAsync(script);
        }
        else
        {
            result = "";
        }

        return ConvertResult<T>(result);
    }

    /// <summary>
    /// Executes JavaScript without expecting a return value.
    /// </summary>
    /// <param name="script">The JavaScript code to execute.</param>
    public Task ExecuteAsync(string script)
    {
        if (adapter is WebView2Adapter win)
        {
            return win.ExecuteScriptAsync(script);
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.ExecuteScriptAsync(script);
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.ExecuteScriptAsync(script);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message to the web content. The message is delivered to
    /// <c>window.cascade.onMessage</c> in JavaScript.
    /// </summary>
    /// <param name="message">The JSON string to send.</param>
    public Task PostMessageAsync(string message)
    {
        if (adapter is WebView2Adapter win)
        {
            return win.PostMessageAsync(message);
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.PostMessageAsync(message);
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.PostMessageAsync(message);
        }
        return Task.CompletedTask;
    }

    // ── Content Injection ────────────────────────────────────────────

    /// <summary>
    /// Injects CSS into the currently loaded page.
    /// </summary>
    /// <param name="css">The CSS to inject.</param>
    public Task InjectCssAsync(string css)
    {
        if (adapter is WebView2Adapter win)
        {
            return win.InjectCssAsync(css);
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.InjectCssAsync(css);
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.InjectCssAsync(css);
        }
        return Task.CompletedTask;
    }

    // ── Cookie and Storage ───────────────────────────────────────────

    /// <summary>Clears all cookies for this WebView's profile.</summary>
    public Task ClearCookiesAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.ClearCookiesAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.ClearCookiesAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.ClearCookiesAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears web storage (localStorage, sessionStorage, and cache).
    /// </summary>
    public Task ClearStorageAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.ClearStorageAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.ClearStorageAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.ClearStorageAsync();
        }
        return Task.CompletedTask;
    }

    // ── Print ────────────────────────────────────────────────────────

    /// <summary>
    /// Prints the WebView content using the OS print dialog.
    /// </summary>
    public Task PrintAsync()
    {
        if (adapter is WebView2Adapter win)
        {
            return win.PrintAsync();
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.PrintAsync();
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.PrintAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exports the WebView content as a PDF file.
    /// </summary>
    /// <param name="outputPath">The file path to write the PDF to.</param>
    public Task PrintToPdfAsync(string outputPath)
    {
        if (adapter is WebView2Adapter win)
        {
            return win.PrintToPdfAsync(outputPath);
        }
        if (adapter is WkWebViewAdapter mac)
        {
            return mac.PrintToPdfAsync(outputPath);
        }
        if (adapter is WebKitGtkAdapter linux)
        {
            return linux.PrintToPdfAsync(outputPath);
        }
        return Task.CompletedTask;
    }

    // ── Internal ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a JavaScript result string to the requested type.
    /// NativeAOT-compatible — no reflection-based JSON deserialization.
    /// </summary>
    private static T ConvertResult<T>(string result)
    {
        var targetType = typeof(T);

        if (targetType == typeof(string))
        {
            // Strip surrounding quotes from JS string results
            string val = result;
            if (val.Length >= 2 && val[0] == '"' && val[^1] == '"')
            {
                val = val[1..^1];
            }
            return (T)(object)val;
        }

        if (targetType == typeof(int))
        {
            return (T)(object)int.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(long))
        {
            return (T)(object)long.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return (T)(object)double.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            return (T)(object)float.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return (T)(object)decimal.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return (T)(object)bool.Parse(result);
        }

        throw new NotSupportedException(
            $"WebViewRef.EvaluateAsync<{targetType.Name}> is not supported. " +
            "Use string and parse the result manually for complex types.");
    }
}

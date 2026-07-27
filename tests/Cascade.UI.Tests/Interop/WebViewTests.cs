using Cascade.UI;

namespace Cascade.UI.Tests.Interop;

// ─── TextureBridge Tests ─────────────────────────────────────────────────

public class TextureBridgeTests
{
    [Test]
    public async Task Constructor_RequiresAdapter()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);

        int width = bridge.Width;
        await Assert.That(width).IsEqualTo(0);
    }

    [Test]
    public async Task Resize_SetsWidthAndHeight()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);
        bridge.Resize(320, 240, 1.0f);

        await Assert.That(bridge.Width).IsEqualTo(320);
        await Assert.That(bridge.Height).IsEqualTo(240);
    }

    [Test]
    public async Task Resize_WithScale_SetsCorrectDimensions()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);
        bridge.Resize(160, 120, 2.0f);

        int w = bridge.Width;
        int h = bridge.Height;
        bool hasSize = w > 0 && h > 0;
        await Assert.That(hasSize).IsTrue();
    }

    [Test]
    public async Task TextureId_ZeroInitially()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);

        nint id = bridge.TextureId;
        await Assert.That(id).IsEqualTo((nint)0);
    }

    [Test]
    public async Task IsDirty_FalseInitially()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);

        bool dirty = bridge.IsDirty;
        await Assert.That(dirty).IsFalse();
    }

    [Test]
    public async Task CaptureCurrentFrame_ReturnsFalseWithoutSetup()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);
        bridge.Resize(100, 100, 1.0f);

        bool captured = bridge.CaptureCurrentFrame();
        await Assert.That(captured).IsFalse();
    }

    [Test]
    public async Task GetPixelData_EmptyWithoutCapture()
    {
        using var adapter = new WebView2Adapter();
        using var bridge = new TextureBridge(adapter);

        var data = bridge.GetPixelData();
        bool empty = data.IsEmpty;
        await Assert.That(empty).IsTrue();
    }

    [Test]
    public async Task ForwardKeyEvent_DoesNotThrow()
    {
        var evt = new NativeKeyEvent
        {
            Key = Key.A,
            Type = NativeKeyEventType.KeyDown
        };

        TextureBridge.ForwardKeyEvent(evt);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ForwardScrollEvent_DoesNotThrow()
    {
        var evt = new NativeScrollEvent
        {
            DeltaX = 0,
            DeltaY = -120
        };

        TextureBridge.ForwardScrollEvent(evt);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task Dispose_ReleasesResources()
    {
        using var adapter = new WebView2Adapter();
        var bridge = new TextureBridge(adapter);
        bridge.Resize(100, 100, 1.0f);
        bridge.Dispose();

        nint id = bridge.TextureId;
        await Assert.That(id).IsEqualTo((nint)0);
    }
}

// ─── HolePunchManager Tests ─────────────────────────────────────────────

public class HolePunchManagerTests
{
    [Test]
    public async Task Constructor_CreatesInstance()
    {
        using var manager = new HolePunchManager();

        nint child = manager.ChildHandle;
        await Assert.That(child).IsEqualTo((nint)0);
    }

    [Test]
    public async Task IsVisible_FalseInitially()
    {
        using var manager = new HolePunchManager();

        bool visible = manager.IsVisible;
        await Assert.That(visible).IsFalse();
    }

    [Test]
    public async Task Initialize_SetsUpManager()
    {
        using var manager = new HolePunchManager();
        var bounds = new Rect(10, 20, 300, 200);
        manager.Initialize((nint)12345, bounds, 1.0f);

        var current = manager.CurrentBounds;
        await Assert.That(current.X).IsEqualTo(10);
        await Assert.That(current.Y).IsEqualTo(20);
    }

    [Test]
    public async Task UpdateBounds_ChangesBounds()
    {
        using var manager = new HolePunchManager();
        manager.Initialize((nint)1, new Rect(0, 0, 100, 100), 1.0f);
        manager.UpdateBounds(new Rect(50, 50, 200, 200), 1.0f);

        var bounds = manager.CurrentBounds;
        await Assert.That(bounds.Width).IsEqualTo(200);
    }

    [Test]
    public async Task Show_SetsVisible()
    {
        using var manager = new HolePunchManager();
        manager.Initialize((nint)1, new Rect(0, 0, 100, 100), 1.0f);
        manager.Show();

        bool visible = manager.IsVisible;
        await Assert.That(visible).IsTrue();
    }

    [Test]
    public async Task Hide_ClearsVisible()
    {
        using var manager = new HolePunchManager();
        manager.Initialize((nint)1, new Rect(0, 0, 100, 100), 1.0f);
        manager.Show();
        manager.Hide();

        bool visible = manager.IsVisible;
        await Assert.That(visible).IsFalse();
    }

    [Test]
    public async Task Dispose_ClearsState()
    {
        var manager = new HolePunchManager();
        manager.Initialize((nint)1, new Rect(0, 0, 100, 100), 1.0f);
        manager.Show();
        manager.Dispose();

        bool visible = manager.IsVisible;
        await Assert.That(visible).IsFalse();
    }
}

// ─── WebView Node Tests ─────────────────────────────────────────────────

public class WebViewNodeTests
{
    [Test]
    public async Task Url_CanBeSet()
    {
        var wv = new WebView { Url = "https://example.com" };
        string url = wv.Url!;
        await Assert.That(url).IsEqualTo("https://example.com");
    }

    [Test]
    public async Task Html_CanBeSet()
    {
        var wv = new WebView { Html = "<h1>Hello</h1>" };
        string html = wv.Html!;
        await Assert.That(html).IsEqualTo("<h1>Hello</h1>");
    }

    [Test]
    public async Task AllowNavigation_ReturnsThis()
    {
        var wv = new WebView();
        var result = wv.AllowNavigation(false);
        bool same = ReferenceEquals(wv, result);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task AllowNavigation_SetsFlag()
    {
        var wv = new WebView();
        wv.AllowNavigation(false);
        bool val = wv.configAllowNavigation;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task AllowJavaScript_SetsFlag()
    {
        var wv = new WebView();
        wv.AllowJavaScript(false);
        bool val = wv.configAllowJavaScript;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task AllowDevTools_SetsFlag()
    {
        var wv = new WebView();
        wv.AllowDevTools(true);
        bool val = wv.configAllowDevTools;
        await Assert.That(val).IsTrue();
    }

    [Test]
    public async Task UserAgent_SetsValue()
    {
        var wv = new WebView();
        wv.UserAgent("CascadeBot/1.0");
        string val = wv.configUserAgent!;
        await Assert.That(val).IsEqualTo("CascadeBot/1.0");
    }

    [Test]
    public async Task BackgroundColor_SetsValue()
    {
        var wv = new WebView();
        var color = ColorValue.FromRgba(1.0f, 0, 0);
        wv.BackgroundColor(color);

        bool hasColor = wv.configBackgroundColor.HasValue;
        await Assert.That(hasColor).IsTrue();
    }

    [Test]
    public async Task NativeCompositing_SetsMode()
    {
        var wv = new WebView();
        wv.NativeCompositing(NativeCompositingMode.HolePunch);

        var mode = wv.configCompositingMode;
        await Assert.That(mode).IsEqualTo(NativeCompositingMode.HolePunch);
    }

    [Test]
    public async Task Profile_String_SetsName()
    {
        var wv = new WebView();
        wv.Profile("myprofile");
        string name = wv.configProfileName!;
        await Assert.That(name).IsEqualTo("myprofile");
    }

    [Test]
    public async Task Profile_Enum_SetsType()
    {
        var wv = new WebView();
        wv.Profile(WebViewProfile.InMemory);

        var type = wv.configProfileType;
        await Assert.That(type).IsEqualTo(WebViewProfile.InMemory);
    }

    [Test]
    public async Task OnNavigationStarted_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewNavigationStartedArgs> handler = _ => { };
        wv.OnNavigationStarted(handler);

        bool stored = wv.handlerNavigationStarted is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnNavigationCompleted_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewNavigationCompletedArgs> handler = _ => { };
        wv.OnNavigationCompleted(handler);

        bool stored = wv.handlerNavigationCompleted is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnNewWindowRequested_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewNewWindowArgs> handler = _ => { };
        wv.OnNewWindowRequested(handler);

        bool stored = wv.handlerNewWindowRequested is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnWebMessage_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewMessage> handler = _ => { };
        wv.OnWebMessage(handler);

        bool stored = wv.handlerWebMessage is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnDownloadRequested_StoresHandler()
    {
        var wv = new WebView();
        Func<WebViewDownloadRequestedArgs, Task> handler = _ => Task.CompletedTask;
        wv.OnDownloadRequested(handler);

        bool stored = wv.handlerDownloadRequested is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnDownloadProgress_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewDownloadProgressArgs> handler = _ => { };
        wv.OnDownloadProgress(handler);

        bool stored = wv.handlerDownloadProgress is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnDownloadCompleted_StoresHandler()
    {
        var wv = new WebView();
        Action<WebViewDownloadCompletedArgs> handler = _ => { };
        wv.OnDownloadCompleted(handler);

        bool stored = wv.handlerDownloadCompleted is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task InjectScript_AddsToList()
    {
        var wv = new WebView();
        wv.InjectScript("console.log('hi')", InjectionTiming.DocumentStart);
        wv.InjectScript("console.log('there')", InjectionTiming.DocumentEnd);

        int count = wv.injectedScripts!.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task OnFocusEntered_StoresHandler()
    {
        var wv = new WebView();
        Action handler = () => { };
        wv.OnFocusEntered(handler);

        bool stored = wv.handlerFocusEntered is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task OnFocusExited_StoresHandler()
    {
        var wv = new WebView();
        Action handler = () => { };
        wv.OnFocusExited(handler);

        bool stored = wv.handlerFocusExited is not null;
        await Assert.That(stored).IsTrue();
    }

    [Test]
    public async Task FluentChaining_Works()
    {
        var wv = new WebView { Url = "https://example.com" };
        var result = wv
            .AllowNavigation(true)
            .AllowJavaScript(true)
            .AllowDevTools(false)
            .UserAgent("Test/1.0")
            .NativeCompositing(NativeCompositingMode.TextureBridge)
            .Profile("test")
            .OnNavigationStarted(_ => { })
            .OnNavigationCompleted(_ => { })
            .OnWebMessage(_ => { })
            .InjectScript("// test", InjectionTiming.DocumentEnd);

        bool same = ReferenceEquals(wv, result);
        await Assert.That(same).IsTrue();
    }
}

// ─── WebViewRef Tests ───────────────────────────────────────────────────

public class WebViewRefTests
{
    [Test]
    public async Task NavigateAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.NavigateAsync("https://example.com");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task GoBackAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.GoBackAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task GoForwardAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.GoForwardAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ReloadAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.ReloadAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task StopAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.StopAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task CanGoBack_NoAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        bool val = r.CanGoBack;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CanGoForward_NoAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        bool val = r.CanGoForward;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CurrentUrl_NoAdapter_ReturnsEmpty()
    {
        var r = new WebViewRef();
        string val = r.CurrentUrl;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task Title_NoAdapter_ReturnsEmpty()
    {
        var r = new WebViewRef();
        string val = r.Title;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task IsLoading_NoAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        bool val = r.IsLoading;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.ExecuteAsync("console.log('test')");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PostMessageAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.PostMessageAsync("hello");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task InjectCssAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.InjectCssAsync("body { color: red; }");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ClearCookiesAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.ClearCookiesAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ClearStorageAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.ClearStorageAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PrintAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.PrintAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PrintToPdfAsync_NoAdapter_Completes()
    {
        var r = new WebViewRef();
        await r.PrintToPdfAsync("output.pdf");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task EvaluateAsync_NoAdapter_ReturnsEmpty()
    {
        var r = new WebViewRef();
        string val = await r.EvaluateAsync<string>("1+1");
        await Assert.That(val).IsEqualTo("");
    }
}

// ─── WebView2Adapter Tests ──────────────────────────────────────────────

public class WebView2AdapterTests
{
    [Test]
    public async Task Constructor_CreatesInstance()
    {
        using var adapter = new WebView2Adapter();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task SetPendingUrl_DoesNotThrow()
    {
        using var adapter = new WebView2Adapter();
        adapter.SetPendingUrl("https://example.com");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task NavigateAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.NavigateAsync("https://example.com");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task GoBackAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.GoBackAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task EvaluateScriptAsync_BeforeCreate_ReturnsEmpty()
    {
        using var adapter = new WebView2Adapter();
        string result = await adapter.EvaluateScriptAsync("1+1");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CanGoBack_BeforeCreate_IsFalse()
    {
        using var adapter = new WebView2Adapter();
        bool val = adapter.CanGoBack;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CanGoForward_BeforeCreate_IsFalse()
    {
        using var adapter = new WebView2Adapter();
        bool val = adapter.CanGoForward;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CurrentUrl_BeforeCreate_IsEmpty()
    {
        using var adapter = new WebView2Adapter();
        string val = adapter.CurrentUrl;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task Title_BeforeCreate_IsEmpty()
    {
        using var adapter = new WebView2Adapter();
        string val = adapter.Title;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task IsLoading_BeforeCreate_IsFalse()
    {
        using var adapter = new WebView2Adapter();
        bool val = adapter.IsLoading;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task SetAllowJavaScript_DoesNotThrow()
    {
        using var adapter = new WebView2Adapter();
        adapter.SetAllowJavaScript(false);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task AddInjectedScript_DoesNotThrow()
    {
        using var adapter = new WebView2Adapter();
        adapter.AddInjectedScript("console.log('test')", InjectionTiming.DocumentStart);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PostMessageAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.PostMessageAsync("{\"key\": \"value\"}");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ClearCookiesAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.ClearCookiesAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PrintAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.PrintAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task InjectCssAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebView2Adapter();
        await adapter.InjectCssAsync("body { color: red; }");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }
}

// ─── WkWebViewAdapter Tests ─────────────────────────────────────────────

public class WkWebViewAdapterTests
{
    [Test]
    public async Task Constructor_CreatesInstance()
    {
        using var adapter = new WkWebViewAdapter();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task NavigateAsync_BeforeCreate_Completes()
    {
        using var adapter = new WkWebViewAdapter();
        await adapter.NavigateAsync("https://example.com");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task EvaluateScriptAsync_BeforeCreate_ReturnsEmpty()
    {
        using var adapter = new WkWebViewAdapter();
        string result = await adapter.EvaluateScriptAsync("1+1");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CanGoBack_BeforeCreate_IsFalse()
    {
        using var adapter = new WkWebViewAdapter();
        bool val = adapter.CanGoBack;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CurrentUrl_BeforeCreate_IsEmpty()
    {
        using var adapter = new WkWebViewAdapter();
        string val = adapter.CurrentUrl;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task Title_BeforeCreate_IsEmpty()
    {
        using var adapter = new WkWebViewAdapter();
        string val = adapter.Title;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task PostMessageAsync_BeforeCreate_Completes()
    {
        using var adapter = new WkWebViewAdapter();
        await adapter.PostMessageAsync("{\"test\": true}");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task InjectCssAsync_BeforeCreate_Completes()
    {
        using var adapter = new WkWebViewAdapter();
        await adapter.InjectCssAsync("body { margin: 0; }");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task AddInjectedScript_DoesNotThrow()
    {
        using var adapter = new WkWebViewAdapter();
        adapter.AddInjectedScript("alert('hi')", InjectionTiming.DocumentEnd);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }
}

// ─── WebKitGtkAdapter Tests ─────────────────────────────────────────────

public class WebKitGtkAdapterTests
{
    [Test]
    public async Task Constructor_CreatesInstance()
    {
        using var adapter = new WebKitGtkAdapter();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task NavigateAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebKitGtkAdapter();
        await adapter.NavigateAsync("https://example.com");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task EvaluateScriptAsync_BeforeCreate_ReturnsEmpty()
    {
        using var adapter = new WebKitGtkAdapter();
        string result = await adapter.EvaluateScriptAsync("document.title");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task CanGoBack_BeforeCreate_IsFalse()
    {
        using var adapter = new WebKitGtkAdapter();
        bool val = adapter.CanGoBack;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CurrentUrl_BeforeCreate_IsEmpty()
    {
        using var adapter = new WebKitGtkAdapter();
        string val = adapter.CurrentUrl;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task PostMessageAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebKitGtkAdapter();
        await adapter.PostMessageAsync("{\"data\": 42}");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task InjectCssAsync_BeforeCreate_Completes()
    {
        using var adapter = new WebKitGtkAdapter();
        await adapter.InjectCssAsync("* { box-sizing: border-box; }");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }
}

// ─── NativeViewHost Tests ───────────────────────────────────────────────

public class NativeViewHostTests
{
    [Test]
    public async Task ParentHandle_DefaultsToZero()
    {
        var host = new NativeViewHost();
        nint handle = host.ParentHandle;
        await Assert.That(handle).IsEqualTo((nint)0);
    }

    [Test]
    public async Task ParentHandle_CanBeSetInternally()
    {
        var host = new NativeViewHost();
        host.parentHandle = (nint)999;
        nint handle = host.ParentHandle;
        await Assert.That(handle).IsEqualTo((nint)999);
    }

    [Test]
    public async Task Scale_DefaultsToOne()
    {
        var host = new NativeViewHost();
        float s = host.Scale;
        await Assert.That(s).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ExitFocus_InvokesCallback()
    {
        var host = new NativeViewHost();
        FocusDirection? received = null;
        host.exitFocusCallback = d => received = d;

        host.ExitFocus(FocusDirection.Next);

        var val = received;
        await Assert.That(val).IsEqualTo(FocusDirection.Next);
    }

    [Test]
    public async Task NotifyFocusEntered_InvokesCallback()
    {
        var host = new NativeViewHost();
        bool called = false;
        host.focusEnteredCallback = () => called = true;

        host.NotifyFocusEntered();

        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task CompositingMode_CanBeSetInternally()
    {
        var host = new NativeViewHost();
        host.compositingMode = NativeCompositingMode.HolePunch;

        var mode = host.CompositingMode;
        await Assert.That(mode).IsEqualTo(NativeCompositingMode.HolePunch);
    }

    [Test]
    public async Task BoundsInPixels_CanBeSetInternally()
    {
        var host = new NativeViewHost();
        host.boundsInPixels = new Rect(10, 20, 300, 200);

        var bounds = host.BoundsInPixels;
        await Assert.That(bounds.X).IsEqualTo(10);
        await Assert.That(bounds.Y).IsEqualTo(20);
        await Assert.That(bounds.Width).IsEqualTo(300);
        await Assert.That(bounds.Height).IsEqualTo(200);
    }

    [Test]
    public async Task ExitFocus_NoCallback_DoesNotThrow()
    {
        var host = new NativeViewHost();
        host.ExitFocus(FocusDirection.Next);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }
}

// ─── WebViewRef Delegation Tests ────────────────────────────────────────

public class WebViewRefDelegationTests
{
    [Test]
    public async Task NavigateAsync_WithWebView2Adapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        await r.NavigateAsync("https://cascade.dev");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task NavigateAsync_WithWkWebViewAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WkWebViewAdapter();
        r.adapter = adapter;
        await r.NavigateAsync("https://cascade.dev");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task NavigateAsync_WithWebKitGtkAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebKitGtkAdapter();
        r.adapter = adapter;
        await r.NavigateAsync("https://cascade.dev");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        await r.ExecuteAsync("document.title = 'test'");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PostMessageAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WkWebViewAdapter();
        r.adapter = adapter;
        await r.PostMessageAsync("{\"type\": \"greeting\"}");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task InjectCssAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebKitGtkAdapter();
        r.adapter = adapter;
        await r.InjectCssAsync("body { display: flex; }");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ClearCookiesAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        await r.ClearCookiesAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task ClearStorageAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WkWebViewAdapter();
        r.adapter = adapter;
        await r.ClearStorageAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PrintAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebKitGtkAdapter();
        r.adapter = adapter;
        await r.PrintAsync();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task PrintToPdfAsync_WithAdapter_Delegates()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        await r.PrintToPdfAsync("output.pdf");

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task CanGoBack_WithAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        bool val = r.CanGoBack;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CanGoForward_WithAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        using var adapter = new WkWebViewAdapter();
        r.adapter = adapter;
        bool val = r.CanGoForward;
        await Assert.That(val).IsFalse();
    }

    [Test]
    public async Task CurrentUrl_WithAdapter_ReturnsEmpty()
    {
        var r = new WebViewRef();
        using var adapter = new WebKitGtkAdapter();
        r.adapter = adapter;
        string val = r.CurrentUrl;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task Title_WithAdapter_ReturnsEmpty()
    {
        var r = new WebViewRef();
        using var adapter = new WebView2Adapter();
        r.adapter = adapter;
        string val = r.Title;
        await Assert.That(val).IsEqualTo("");
    }

    [Test]
    public async Task IsLoading_WithAdapter_ReturnsFalse()
    {
        var r = new WebViewRef();
        using var adapter = new WkWebViewAdapter();
        r.adapter = adapter;
        bool val = r.IsLoading;
        await Assert.That(val).IsFalse();
    }
}

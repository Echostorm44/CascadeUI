using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000 // Test components are managed by the test lifecycle

namespace Cascade.UI.Tests;

/// <summary>
/// Regression tests for theme application and scroll state preservation.
/// These tests exist to prevent regressions like the ones fixed in commit 859792d:
/// - Theme not applied (App wrote to no-op setter, read back default FluentTheme)
/// - Scroll-to-top on click (Reconciler didn't transfer ScrollView.OffsetY)
/// </summary>
[NotInParallel("ThemeSwitcher")]
public class ThemeScrollRegressionTests
{
    // ── ThemeSwitcher.Apply tests ────────────────────────────────

    [Test]
    public async Task ThemeSwitcher_Apply_SetsCurrentTheme()
    {
        ThemeSwitcher.Reset();

        var apple = new AppleTheme(ThemeMode.Dark);
        ThemeSwitcher.Apply(apple);

        await Assert.That(ThemeSwitcher.Current).IsSameReferenceAs(apple);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_Apply_OverridesDefault()
    {
        ThemeSwitcher.Reset();

        // Default should be FluentTheme
        await Assert.That(ThemeSwitcher.Current).IsTypeOf<FluentTheme>();

        // Apply AppleTheme
        var apple = new AppleTheme(ThemeMode.Dark);
        ThemeSwitcher.Apply(apple);

        await Assert.That(ThemeSwitcher.Current).IsTypeOf<AppleTheme>();

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_Apply_ThemeColorsReflectDarkMode()
    {
        ThemeSwitcher.Reset();

        var darkApple = new AppleTheme(ThemeMode.Dark);
        ThemeSwitcher.Apply(darkApple);

        // Verify the current theme's colors are the dark palette
        await Assert.That(ThemeSwitcher.Current.Colors.Background)
            .IsEqualTo(AppleTokens.DarkBackground);
        await Assert.That(ThemeSwitcher.Current.Colors.Text)
            .IsEqualTo(AppleTokens.DarkText);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_Apply_NotifiesListeners()
    {
        ThemeSwitcher.Reset();

        int notifyCount = 0;
        var subscription = ThemeSwitcher.Subscribe(() => notifyCount++);

        ThemeSwitcher.Apply(new AppleTheme());
        await Assert.That(notifyCount).IsEqualTo(1);

        ThemeSwitcher.Apply(new FluentTheme());
        await Assert.That(notifyCount).IsEqualTo(2);

        subscription.Dispose();
        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_MultipleApply_LastWins()
    {
        ThemeSwitcher.Reset();

        ThemeSwitcher.Apply(new AppleTheme(ThemeMode.Light));
        ThemeSwitcher.Apply(new FluentTheme());
        var finalTheme = new AppleTheme(ThemeMode.Dark);
        ThemeSwitcher.Apply(finalTheme);

        await Assert.That(ThemeSwitcher.Current).IsSameReferenceAs(finalTheme);

        ThemeSwitcher.Reset();
    }

    // ── Theme version / layer-cache invalidation tests (THEME-002) ──
    // A ScrollView composites a retained layer texture; a theme/dark-mode/
    // high-contrast switch recolours and restyles its content without changing
    // structure or size, so the size- and dirty-based recapture checks never
    // fire. ThemeSwitcher.Version is bumped on every appearance change and the
    // painter recaptures whenever a layer's CapturedThemeVersion lags it — without
    // this, a layer captured under one theme is composited under the next, which
    // (e.g. dark text from a light theme over a dark sidebar) made nav buttons
    // vanish after switching themes.

    [Test]
    public async Task ThemeSwitcher_Apply_AdvancesVersion()
    {
        ThemeSwitcher.Reset();

        int before = ThemeSwitcher.Version;
        ThemeSwitcher.Apply(new AppleTheme());

        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 1);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_SetDarkMode_AdvancesVersion_OnlyWhenChanged()
    {
        ThemeSwitcher.Reset();           // resets to light mode
        int before = ThemeSwitcher.Version;

        ThemeSwitcher.SetDarkMode(true); // light → dark: a real change
        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 1);

        ThemeSwitcher.SetDarkMode(true); // no-op: already dark
        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 1);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ThemeSwitcher_SetHighContrast_AdvancesVersion_OnlyWhenChanged()
    {
        ThemeSwitcher.Reset();
        int before = ThemeSwitcher.Version;

        ThemeSwitcher.SetHighContrast(true);
        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 1);

        ThemeSwitcher.SetHighContrast(true); // no-op
        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 1);

        ThemeSwitcher.SetHighContrast(false);
        await Assert.That(ThemeSwitcher.Version).IsEqualTo(before + 2);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task ScrollViewLayer_BecomesStale_AfterThemeSwitch()
    {
        ThemeSwitcher.Reset();

        var scroll = new ScrollView(new Label("content"));

        // Model a fresh layer capture at the current theme version.
        scroll.CapturedThemeVersion = ThemeSwitcher.Version;

        // Not stale immediately after capture — the painter would composite the
        // cached layer rather than re-render every frame.
        await Assert.That(scroll.CapturedThemeVersion).IsEqualTo(ThemeSwitcher.Version);

        // A dark-mode toggle (same theme, identical control geometry) is exactly
        // the case the size/dirty checks miss. The version bump makes the layer
        // stale, forcing the recapture that fixes the vanishing-nav bug.
        ThemeSwitcher.SetDarkMode(true);
        await Assert.That(scroll.CapturedThemeVersion).IsNotEqualTo(ThemeSwitcher.Version);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task Reconciler_TransfersCapturedThemeVersion()
    {
        ControlStateAnimator.Reset();

        var content = new Label("content");
        var oldScroll = new ScrollView(content);
        oldScroll.CapturedThemeVersion = 42;

        var newScroll = new ScrollView(content);
        // A freshly constructed ScrollView has never been captured.
        await Assert.That(newScroll.CapturedThemeVersion).IsEqualTo(-1);

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // The captured-at marker must survive the re-render, otherwise every
        // reconcile would discard the cache and recapture needlessly.
        await Assert.That(newScroll.CapturedThemeVersion).IsEqualTo(42);

        ControlStateAnimator.Reset();
    }

    // ── Reconciler ScrollView state transfer tests ──────────────

    [Test]
    public async Task Reconciler_TransfersScrollViewOffsetY()
    {
        ControlStateAnimator.Reset();

        var content = new Label("content");
        var oldScroll = new ScrollView(content);
        oldScroll.OffsetY = 250f;
        oldScroll.MaxY = 1000f;

        var newScroll = new ScrollView(content);

        // Verify new scroll starts at 0
        await Assert.That(newScroll.OffsetY).IsEqualTo(0f);

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // After reconciliation, scroll position should be preserved
        await Assert.That(newScroll.OffsetY).IsEqualTo(250f);
        await Assert.That(newScroll.MaxY).IsEqualTo(1000f);

        ControlStateAnimator.Reset();
    }

    [Test]
    public async Task Reconciler_TransfersScrollViewState_ZeroStaysZero()
    {
        ControlStateAnimator.Reset();

        var content = new Label("content");
        var oldScroll = new ScrollView(content);
        oldScroll.OffsetY = 0f;

        var newScroll = new ScrollView(content);

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        await Assert.That(newScroll.OffsetY).IsEqualTo(0f);

        ControlStateAnimator.Reset();
    }

    [Test]
    public async Task Reconciler_TransfersScrollbarGeometry()
    {
        ControlStateAnimator.Reset();

        var content = new Label("content");
        var oldScroll = new ScrollView(content);
        oldScroll.OffsetY = 100f;
        oldScroll.MaxY = 500f;
        oldScroll.ScrollbarTrackBounds = new Rect(290f, 0f, 10f, 400f);
        oldScroll.ScrollbarThumbHeight = 80f;

        var newScroll = new ScrollView(content);

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        await Assert.That(newScroll.ScrollbarTrackBounds).IsEqualTo(new Rect(290f, 0f, 10f, 400f));
        await Assert.That(newScroll.ScrollbarThumbHeight).IsEqualTo(80f);

        ControlStateAnimator.Reset();
    }

    // ── ScrollView cached-layer recapture on in-place edit (INVOICE fix) ──
    // A ScrollView composites a retained layer texture and only recaptures on a
    // dirty flag, size change, or theme-version bump. Typing into a focused
    // TextInput/TextArea/etc. inside that content mutates the buffer but changes
    // none of those, so the cached layer kept compositing the stale pre-edit
    // bitmap — typed text never appeared and no caret/focus ring showed. The
    // painter now recaptures while (and once after) the focus lives in the content,
    // detected against the live painted tree via SubtreeContainsFocusedEditable.

    [Test]
    public async Task SubtreeContainsFocusedEditable_TrueForFocusedTextInputInside()
    {
        FocusManager.ClearFocus();

        var input = new TextInput(new Bindable<string>("", _ => { }));
        var content = new Column(children: new Node[] { new Label("Client Name"), input });
        var scroll = new ScrollView(content);

        FocusManager.RequestFocus(input);

        await Assert.That(NodePainter.SubtreeContainsFocusedEditable(scroll.Content)).IsTrue();

        FocusManager.ClearFocus();
    }

    [Test]
    public async Task SubtreeContainsFocusedEditable_FalseWhenFocusIsOutsideContent()
    {
        FocusManager.ClearFocus();

        var inside = new TextInput(new Bindable<string>("", _ => { }));
        var outside = new TextInput(new Bindable<string>("", _ => { }));
        var scroll = new ScrollView(new Column(children: new Node[] { inside }));

        // Focus a control that is NOT in this ScrollView's content.
        FocusManager.RequestFocus(outside);

        await Assert.That(NodePainter.SubtreeContainsFocusedEditable(scroll.Content)).IsFalse();

        FocusManager.ClearFocus();
    }

    [Test]
    public async Task SubtreeContainsFocusedEditable_FalseForNonEditableFocus()
    {
        FocusManager.ClearFocus();

        var button = new Button("Save", () => { });
        var scroll = new ScrollView(new Column(children: new Node[] { button }));

        FocusManager.RequestFocus(button);

        // A focused Button is interactive but not keyboard-editable, so it must not
        // force per-frame layer recapture.
        await Assert.That(NodePainter.SubtreeContainsFocusedEditable(scroll.Content)).IsFalse();

        FocusManager.ClearFocus();
    }

    [Test]
    public async Task SubtreeContainsFocusedEditable_FalseWhenNothingFocused()
    {
        FocusManager.ClearFocus();

        var scroll = new ScrollView(new Column(children: new Node[]
        {
            new TextInput(new Bindable<string>("", _ => { }))
        }));

        await Assert.That(NodePainter.SubtreeContainsFocusedEditable(scroll.Content)).IsFalse();
    }

    [Test]
    public async Task Reconciler_TransfersCapturedFocusedEditable()
    {
        ControlStateAnimator.Reset();

        var content = new Label("content");
        var oldScroll = new ScrollView(content);
        oldScroll.CapturedFocusedEditable = true;

        var newScroll = new ScrollView(content);
        // A freshly constructed ScrollView has not captured a focused edit.
        await Assert.That(newScroll.CapturedFocusedEditable).IsFalse();

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // The "captured while focused" marker must survive the re-render, otherwise
        // the layer would not recapture the one time focus leaves and the stale
        // focus ring would linger.
        await Assert.That(newScroll.CapturedFocusedEditable).IsTrue();

        ControlStateAnimator.Reset();
    }

    // ── ScrollView layer recapture on window resize ─────────────
    // A resize recreates the GPU surface, invalidating cached layer textures. The
    // size-based recapture check misses this when a ScrollView's content height is
    // unchanged (a taller viewport is offset by less max-scroll), so the panel would
    // composite a now-blank texture (repro: maximizing hid the whole right panel).
    // FrameOrchestrator.HandleResize marks every ScrollView layer dirty to force a
    // recapture against the new surface.

    [Test]
    public async Task Resize_MarksEveryScrollViewLayerDirty_Nested()
    {
        var outer = new ScrollView(new Label("outer"));
        var inner = new ScrollView(new Label("inner"));

        // Model layers that were captured (clean) before the resize.
        outer.IsLayerDirty = false;
        inner.IsLayerDirty = false;

        // Nest one ScrollView a few containers deep to prove the walk recurses
        // through the same child accessors the painter uses.
        var tree = new Column(children: new Node[]
        {
            outer,
            new Row(children: new Node[]
            {
                new SplitView(first: new Label("x"), second: inner)
            })
        });

        FrameOrchestrator.MarkAllScrollLayersDirty(tree);

        await Assert.That(outer.IsLayerDirty).IsTrue();
        await Assert.That(inner.IsLayerDirty).IsTrue();
    }

    // ── NumberInput hover state tests ───────────────────────────

    [Test]
    public async Task NumberInput_HoveredStepperButton_DefaultsToNone()
    {
        var ni = new NumberInput<int>(
            new Bindable<int>(0, _ => { }),
            min: 0, max: 10, step: 1);

        INumberInput iface = ni;
        await Assert.That(iface.HoveredStepperButton).IsEqualTo(-1);
    }

    // ── Integration: theme + render cycle ───────────────────────

    [Test]
    public async Task ThemeSwitcher_AppliedTheme_SurvivesReset_WhenReapplied()
    {
        ThemeSwitcher.Reset();

        var apple = new AppleTheme(ThemeMode.Dark);
        ThemeSwitcher.Apply(apple);
        await Assert.That(ThemeSwitcher.Current).IsTypeOf<AppleTheme>();

        // Reset simulates what would happen if state were lost
        ThemeSwitcher.Reset();
        await Assert.That(ThemeSwitcher.Current).IsTypeOf<FluentTheme>();

        // Re-apply restores the theme
        ThemeSwitcher.Apply(apple);
        await Assert.That(ThemeSwitcher.Current).IsSameReferenceAs(apple);

        ThemeSwitcher.Reset();
    }

    [Test]
    public async Task Reconciler_PreservesHoverAndScroll_Together()
    {
        ControlStateAnimator.Reset();

        var content = new Button("Click", () => { });
        var oldScroll = new ScrollView(content);
        oldScroll.OffsetY = 300f;
        oldScroll.MaxY = 800f;
        content.IsHovered = true;

        var newContent = new Button("Click", () => { });
        var newScroll = new ScrollView(newContent);

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldScroll });
        var newTree = new Column(children: new Node[] { newScroll });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // Both scroll state and hover should be preserved
        await Assert.That(newScroll.OffsetY).IsEqualTo(300f);
        await Assert.That(newContent.IsHovered).IsTrue();

        ControlStateAnimator.Reset();
    }
}

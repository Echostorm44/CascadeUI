using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Modifiers that were stored and never drawn (see ModifierCoverageTests): each renders a real
/// frame through the CPU path over black and checks the setting reached the pixels.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ModifierRenderTests
{
    private static readonly ColorValue Red = ColorValue.FromRgba(1f, 0f, 0f);
    private static readonly ColorValue Green = ColorValue.FromRgba(0f, 1f, 0f);

    [Test]
    public async Task SpinnerColor_DrawsTheArcInThatColor()
    {
        using var app = new RenderHarness(() => new Spinner().Color(Red).Size(48));
        var frame = app.Frame();

        await Assert.That(frame.Count(p => p.R > 200 && p.G < 60 && p.B < 60)).IsGreaterThan(30);
    }

    [Test]
    public async Task TagColor_TintsTheTag()
    {
        using var app = new RenderHarness(() => new Tag("Chillout").Color(Green));
        var frame = app.Frame();

        // Text and border in the colour, fill tinted with it: plenty of clearly green pixels.
        await Assert.That(frame.Count(p => p.G > 150 && p.R < 80 && p.B < 80)).IsGreaterThan(20);
    }

    [Test]
    public async Task CardElevation_ReplacesTheThemeShadow()
    {
        var shadow = new ShadowValue(0, 30, 2, 0, Red);
        using var app = new RenderHarness(() => new Card(new Label("x")).Elevation(shadow).Size(120, 60), crossAxisCenter: true);
        var frame = app.Frame();

        // A red shadow 30 px below the card, where the theme's shadow would be dark.
        await Assert.That(frame.Count(p => p.R > 120 && p.G < 60 && p.B < 60)).IsGreaterThan(200);
    }

    [Test]
    public async Task ProgressBarShowLabel_DrawsThePercentage_BesideTheBar()
    {
        using var withLabel = new RenderHarness(() => new ProgressBar(0.42f).ShowLabel(true).Width(300));
        using var without = new RenderHarness(() => new ProgressBar(0.42f).Width(300));
        var labelled = withLabel.Frame();
        var plain = without.Frame();

        // Text pixels only appear in the labelled bar's last 40 px.
        await Assert.That(labelled.Count(p => p.X > 262 && p.X < 300 && p.R > 80)).IsGreaterThan(10);
        await Assert.That(plain.Count(p => p.X > 262 && p.X < 300 && p.R > 80 && p.G > 80 && p.B > 80)).IsEqualTo(0);
        await Assert.That(ProgressBar.FormatLabel(new ProgressBar(0.42f), 0.42f)).IsEqualTo($"{42}%");
        await Assert.That(ProgressBar.FormatLabel(new ProgressBar(0.5f).LabelFormat(v => $"{v:0.0}"), 0.5f)).IsEqualTo($"{0.5:0.0}");
    }

    [Test]
    public async Task LinkButtonUnderlineFalse_NeverUnderlines()
    {
        var underlined = HoveredLinkPixels(underline: true);
        var plain = HoveredLinkPixels(underline: false);

        // The hovered link with an underline has a long horizontal run of ink that the plain one lacks.
        await Assert.That(RenderHarness.LongestRun(underlined)).IsGreaterThan(RenderHarness.LongestRun(plain) + 20);
    }

    [Test]
    public async Task SeparatorLabel_DrawsTheTextWithTheLineEitherSide()
    {
        using var app = new RenderHarness(() => new Separator().Label("OR").Width(300));
        var frame = app.Frame();
        var size = LayoutSolver.MeasureSeparatorLabel("OR");

        // Text in the middle, lines at both ends, and the line stops short of the text.
        await Assert.That(size.Height).IsGreaterThan(8f);
        await Assert.That(frame.Count(p => p.X > 140 && p.X < 160 && p.R > 70)).IsGreaterThan(5);
        await Assert.That(frame.Count(p => p.X < 40 && p.R > 25)).IsGreaterThan(5);
        await Assert.That(frame.Count(p => p.X > 260 && p.X < 300 && p.R > 25)).IsGreaterThan(5);
    }

    [Test]
    public async Task ButtonLoading_DrawsASpinner_AndIgnoresClicks()
    {
        int clicks = 0;
        using var loading = new RenderHarness(() => new Button("Save changes", () => clicks++).Loading().Width(160));
        using var normal = new RenderHarness(() => new Button("Save changes", () => clicks++).Width(160));
        var spinning = loading.Frame();
        var labelled = normal.Frame();

        // The label's wide run of text is gone; something (the arc) is drawn in the middle.
        int Bright(RenderHarness.Pixels f) => f.Count(p => p.R > 200 && p.G > 200 && p.B > 200);
        await Assert.That(Bright(spinning)).IsGreaterThan(0);
        await Assert.That(Bright(spinning)).IsLessThan(Bright(labelled) / 2);
        loading.Click(80, 15);
        await Assert.That(clicks).IsEqualTo(0);
        normal.Click(80, 15);
        await Assert.That(clicks).IsEqualTo(1);
    }

    [Test]
    public async Task IconButtonLoading_IgnoresClicks()
    {
        int clicks = 0;
        using var app = new RenderHarness(() => new IconButton(Icons.Play, () => clicks++).Loading());
        app.Frame();
        app.Click(20, 20);

        await Assert.That(clicks).IsEqualTo(0);
    }

    [Test]
    public async Task ProgressBarAnimatedFalse_DrawsTheValueAtOnce()
    {
        using var app = new RenderHarness(() => new ProgressBar(1f).Animated(false).FillColor(Red).Width(300));
        var frame = app.Frame();

        // Without the spring the first frame is already full: red to the far end of the track.
        await Assert.That(frame.Count(p => p.X > 280 && p.R > 200 && p.G < 60)).IsGreaterThan(0);
    }

    [Test]
    public async Task GroupAvatar_DrawsOverlappingMembers_AndAnOverflowBadge()
    {
        var people = new AvatarInfo[] { new("Ann Lee"), new("Bo Ray"), new("Cy Tan"), new("Di Moe"), new("Ed Fox") };
        var group = new GroupAvatar(people, max: 3).Size(AvatarSize.Lg);
        using var app = new RenderHarness(() => group);
        var frame = app.Frame();

        // Three 48 px avatars and a "+2" badge, each 32 px after the last: 48 + 3 * 33.
        await Assert.That(group.GroupWidth).IsEqualTo(48f + 3 * 33f);
        await Assert.That(group.OverflowCount).IsEqualTo(2);
        await Assert.That(frame.Count(p => p.X < 147 && p.Y < 48 && Math.Max(p.R, Math.Max(p.G, p.B)) > 60)).IsGreaterThan(3000);
    }

    [Test]
    public async Task TagIcon_IsDrawnBeforeTheLabel_AndWidensThePill()
    {
        var plain = LayoutSolver.Measure(new Tag("Chillout"), LayoutConstraints.Loose(new Size(400, 100)));
        var iconTag = new Tag("Chillout").Icon(new IconView(Icons.Star, 14).Color(Red));
        using var app = new RenderHarness(() => iconTag);
        var frame = app.Frame();
        var withIcon = LayoutSolver.Measure(iconTag, LayoutConstraints.Loose(new Size(400, 100)));

        await Assert.That(withIcon.Width).IsGreaterThan(plain.Width + 14f);
        await Assert.That(frame.Count(p => p.X < 40 && p.R > 150 && p.G < 60)).IsGreaterThan(10);
    }

    [Test]
    public async Task ButtonTabIndex_IsTheNodesTabIndex()
    {
        var button = new Button("x", () => { }).TabIndex(5);
        var other = new Label("y").TabIndex(5);

        await Assert.That(button.LayoutData.FocusData!.TabIndexValue).IsEqualTo(5);
        await Assert.That(other.LayoutData.FocusData!.TabIndexValue).IsEqualTo(5);
    }
    [Test]
    public async Task BreadcrumbMaxVisible_CollapsesTheMiddle_IntoAnEllipsis()
    {
        var segments = new BreadcrumbSegment[] { new("Home"), new("Music"), new("Trance"), new("Vocal"), new("Now") };
        var arranged = new Breadcrumb(segments).MaxVisible(3).Arrange(14f, null, float.PositiveInfinity);

        await Assert.That(string.Join("/", arranged.Items.Select(i => i.Text))).IsEqualTo("Home/…/Vocal/Now");
        await Assert.That(arranged.Items[1].Segment).IsEqualTo(-1);
    }

    [Test]
    public async Task Breadcrumb_CollapsesWhenItDoesNotFit()
    {
        var segments = new BreadcrumbSegment[] { new("Home"), new("Music"), new("Trance"), new("Vocal"), new("Now") };
        var bc = new Breadcrumb(segments);
        float full = bc.Arrange(14f, null, float.PositiveInfinity).Width;
        var narrow = bc.Arrange(14f, null, full - 10f);

        await Assert.That(narrow.Width).IsLessThanOrEqualTo(full - 10f);
        await Assert.That(narrow.Items.Any(i => i.Segment == -1)).IsTrue();
        await Assert.That(narrow.Items[0].Text).IsEqualTo("Home");
        await Assert.That(narrow.Items[^1].Text).IsEqualTo("Now");
    }

    [Test]
    public async Task BreadcrumbSeparator_DrawsTheCustomNode_BetweenSegments()
    {
        var bc = new Breadcrumb([new("Home", () => { }), new("Now")]).Separator(new IconView(Icons.Star, 12).Color(Red));
        using var app = new RenderHarness(() => bc);
        var frame = app.Frame();

        await Assert.That(frame.Count(p => p.R > 200 && p.G < 60 && p.B < 60)).IsGreaterThan(20);
        await Assert.That(bc.Arrange(14f, null, float.PositiveInfinity).Items[0].SeparatorWidth).IsEqualTo(12f);
    }

    [Test]
    public async Task BreadcrumbClick_HitsTheSegmentDrawnThere()
    {
        var clicked = new List<string>();
        var bc = new Breadcrumb([new("Home", () => clicked.Add("Home")), new("Stations", () => clicked.Add("Stations")), new("Now")]);
        using var app = new RenderHarness(() => bc);
        app.Frame();
        var items = bc.Arrange(LayoutSolver.BodyFontSize, LayoutSolver.DefaultFontPath, bc.AbsoluteBounds.Width).Items;

        app.Click(bc.AbsoluteBounds.X + items[1].X + items[1].Width / 2f, bc.AbsoluteBounds.Y + bc.AbsoluteBounds.Height / 2f);
        app.Click(bc.AbsoluteBounds.X + items[0].X + 2f, bc.AbsoluteBounds.Y + bc.AbsoluteBounds.Height / 2f);

        await Assert.That(string.Join(",", clicked)).IsEqualTo("Stations,Home");
    }

    [Test]
    public async Task ReadOnlyAndDisabled_CheckboxAndToggle_IgnoreClicks()
    {
        foreach (var (readOnly, disabled) in new[] { (true, false), (false, true) })
        {
            int changes = 0;
            var binding = new Bindable<bool>(false, _ => changes++);
            using (var box = new RenderHarness(() => new Checkbox(binding, "Pin").ReadOnly(readOnly).Disabled(disabled)))
            {
                box.Frame();
                box.Click(8, 10);
            }
            using (var toggle = new RenderHarness(() => new Toggle(binding, "Autoplay").ReadOnly(readOnly).Disabled(disabled)))
            {
                toggle.Frame();
                toggle.Click(20, 15);
            }
            await Assert.That(changes).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CheckboxAndToggle_FlipOnClick()
    {
        var values = new List<bool>();
        var binding = new Bindable<bool>(false, values.Add);
        using (var box = new RenderHarness(() => new Checkbox(binding, "Pin")))
        {
            box.Frame();
            box.Click(8, 10);
        }
        using (var toggle = new RenderHarness(() => new Toggle(binding, "Autoplay")))
        {
            toggle.Frame();
            toggle.Click(20, 15);
        }

        await Assert.That(string.Join(",", values)).IsEqualTo("True,True");
    }

    [Test]
    public async Task Space_FlipsAFocusedCheckboxOrToggle_UnlessReadOnly()
    {
        var values = new List<bool>();
        var binding = new Bindable<bool>(false, values.Add);
        var box = new Checkbox(binding, "Pin");
        var locked = new Toggle(binding, "Autoplay").ReadOnly();
        using var app = new RenderHarness(() => new Column(children: [box, locked]));
        app.Frame();

        FocusManager.RequestFocus(box);
        app.Press(Key.Space);
        FocusManager.RequestFocus(locked);
        app.Press(Key.Space);

        await Assert.That(string.Join(",", values)).IsEqualTo("True");
    }

    [Test]
    public async Task ThreeStateCheckbox_ClickChecks_AndUnchecksWhenChecked()
    {
        var seen = new List<CheckboxValue>();
        using (var mixed = new RenderHarness(() => new Checkbox(CheckboxValue.Indeterminate, seen.Add, "All")))
        {
            mixed.Frame();
            mixed.Click(8, 10);
        }
        using (var on = new RenderHarness(() => new Checkbox(CheckboxValue.Checked, seen.Add, "All")))
        {
            on.Frame();
            on.Click(8, 10);
        }

        await Assert.That(string.Join(",", seen)).IsEqualTo("Checked,Unchecked");
    }

    [Test]
    public async Task ReadOnlyAndDisabled_RadioGroup_KeepsItsValue()
    {
        foreach (var (readOnly, disabled) in new[] { (true, false), (false, true), (false, false) })
        {
            string picked = "a";
            var group = new RadioGroup<string>(new Bindable<string>("a", v => picked = v),
                new Column(children: [new RadioButton<string>("a", "Alpha"), new RadioButton<string>("b", "Beta")]))
                .ReadOnly(readOnly).Disabled(disabled);
            using var app = new RenderHarness(() => group);
            app.Frame();
            var beta = ((Column)group.Content).Children[1].LayoutData.Bounds;
            app.Click(beta.X + 8, beta.Y + beta.Height / 2f);

            await Assert.That(picked).IsEqualTo(readOnly || disabled ? "a" : "b");
        }
    }

    [Test]
    public async Task ToggleLabelPositionLeft_PutsTheTrackAtTheRight()
    {
        using var left = new RenderHarness(() => new Toggle(new Bindable<bool>(true, _ => { }), "Autoplay").LabelPosition(ToggleLabelPosition.Left).Width(240));
        using var right = new RenderHarness(() => new Toggle(new Bindable<bool>(true, _ => { }), "Autoplay").Width(240));
        var leftFrame = left.Frame();
        var rightFrame = right.Frame();

        // The "on" track is the theme's saturated accent; find which end of the row it sits at.
        static int Track(RenderHarness.Pixels f, int x0, int x1) =>
            f.Count(p => p.X >= x0 && p.X < x1 && p.Y < 40 && Math.Max(p.R, Math.Max(p.G, p.B)) - Math.Min(p.R, Math.Min(p.G, p.B)) > 80);
        await Assert.That(Track(leftFrame, 180, 240)).IsGreaterThan(100);
        await Assert.That(Track(leftFrame, 0, 60)).IsLessThan(10);
        await Assert.That(Track(rightFrame, 0, 60)).IsGreaterThan(100);
    }

    private static RenderHarness.Pixels HoveredLinkPixels(bool underline)
    {
        using var app = new RenderHarness(() => new LinkButton("Open the station list", () => { }).Underline(underline));
        app.Frame();
        app.MoveMouse(20, 10);
        var until = DateTime.UtcNow.AddMilliseconds(600);
        while (DateTime.UtcNow < until)
        {
            app.Tick();
            Thread.Sleep(16);
        }
        return app.Frame();
    }
}

/// <summary>A mounted window rendering one node through the CPU path over black.</summary>
internal sealed class RenderHarness : IDisposable
{
    internal const uint Width = 400;
    internal const uint Height = 200;

    private static Func<Node> content = () => Node.Empty;
    private readonly EtchBackendProvider provider = new();
    private readonly FrameOrchestrator orchestrator;

    private sealed class Host : Component
    {
        protected override Node Render() => content();
    }

    public RenderHarness(Func<Node> build, bool crossAxisCenter = false)
    {
        content = () => new Column(
            crossAxisAlignment: crossAxisCenter ? CrossAxisAlignment.Center : CrossAxisAlignment.Start,
            children: [build()]);
        ThemeSwitcher.Apply(new AppleTheme(ThemeMode.Dark));
        orchestrator = new FrameOrchestrator(() => { }, () => { })
        {
            RenderBackend = provider.Backend,
        };
        orchestrator.BeginFrameCallback = () => provider.BeginFrame(Width, Height);
        orchestrator.PresentFrameCallback = (frame, background) => provider.PresentFrame(frame, ColorValue.FromRgba(0f, 0f, 0f));
        orchestrator.EndFrameCallback = frame => provider.EndFrame(frame);
        orchestrator.MountRoot<Host>(Width, Height);
        orchestrator.Tick();
    }

    public void Tick() => orchestrator.Tick();

    public void Click(float x, float y)
    {
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseMove });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
        orchestrator.Tick();
    }

    public void Press(Key key)
    {
        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown });
        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp });
        orchestrator.Tick();
    }

    public void MoveMouse(float x, float y)
    {
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseMove });
        orchestrator.Tick();
    }

    public Pixels Frame()
    {
        orchestrator.Tick();
        return new Pixels(provider.CpuRenderer!.CaptureFrame()!.Pixels);
    }

    public void Dispose()
    {
        orchestrator.Dispose();
        provider.Dispose();
        ThemeSwitcher.Reset();
    }

    internal readonly record struct Pixel(int X, int Y, byte R, byte G, byte B);

    internal sealed class Pixels(byte[] rgba)
    {
        public byte[] Rgba { get; } = rgba;

        public int Count(Func<Pixel, bool> predicate)
        {
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = (y * (int)Width + x) * 4;
                    if (predicate(new Pixel(x, y, Rgba[i], Rgba[i + 1], Rgba[i + 2])))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }

    /// <summary>The longest horizontal run of lit pixels in any row.</summary>
    internal static int LongestRun(Pixels pixels)
    {
        int best = 0;
        for (int y = 0; y < Height; y++)
        {
            int run = 0;
            for (int x = 0; x < Width; x++)
            {
                int i = (y * (int)Width + x) * 4;
                run = Math.Max(pixels.Rgba[i], Math.Max(pixels.Rgba[i + 1], pixels.Rgba[i + 2])) > 90 ? run + 1 : 0;
                best = Math.Max(best, run);
            }
        }
        return best;
    }
}

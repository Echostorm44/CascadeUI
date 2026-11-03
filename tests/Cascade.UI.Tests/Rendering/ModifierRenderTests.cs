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
    [NotInParallel("Clipboard")]
    public async Task PasswordInput_Pastes_SelectsAll_AndKeepsMaxLength()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string value = "old";
        var field = new PasswordInput(new Bindable<string>("old", v => value = v)).MaxLength(10);
        using var app = new RenderHarness(() => field.Width(240));
        app.Frame();
        FocusManager.RequestFocus(field);
        await Clipboard.WriteTextAsync("0123456789abc\r\n");

        // Ctrl+A then paste replaces the old text; the line break goes, and MaxLength cuts the rest.
        app.Press(Key.A, ModifierKeys.Ctrl);
        app.Press(Key.V, ModifierKeys.Ctrl);
        await Assert.That(value).IsEqualTo("0123456789");

        // Select all, then a typed character replaces it all.
        app.Press(Key.A, ModifierKeys.Ctrl);
        app.Press(Key.X, character: 'x');
        await Assert.That(value).IsEqualTo("x");

        // Without a selection, paste appends.
        await Clipboard.WriteTextAsync("yz");
        app.Press(Key.Insert, ModifierKeys.Shift);
        await Assert.That(value).IsEqualTo("xyz");
    }

    [Test]
    public async Task PasswordInput_EditsAtTheCaret_LikeATextBox()
    {
        string value = "abcd";
        var field = new PasswordInput(new Bindable<string>("abcd", v => value = v));
        using var app = new RenderHarness(() => field.Width(240));
        app.Frame();
        FocusManager.RequestFocus(field);

        // Arrows move the caret; typing and Backspace act there, not at the end.
        app.Press(Key.Left);
        app.Press(Key.Left);
        app.Press(Key.X, character: 'X');
        await Assert.That(value).IsEqualTo("abXcd");
        app.Press(Key.Backspace, character: '\b');
        await Assert.That(value).IsEqualTo("abcd");

        // Home, then Delete removes the first character only.
        app.Press(Key.Home);
        app.Press(Key.Delete);
        await Assert.That(value).IsEqualTo("bcd");

        // Shift+Right selects; typing replaces just the selection.
        app.Press(Key.Right, ModifierKeys.Shift);
        app.Press(Key.Right, ModifierKeys.Shift);
        app.Press(Key.Z, character: 'Z');
        await Assert.That(value).IsEqualTo("Zd");

        // End puts the caret after the last character.
        app.Press(Key.End);
        app.Press(Key.Q, character: 'q');
        await Assert.That(value).IsEqualTo("Zdq");
    }

    [Test]
    public async Task PasswordInput_ClickPlacesTheCaret_AndAShortValueIsNeverScrolledAway()
    {
        string value = "";
        var field = new PasswordInput(new Bindable<string>("", v => value = v));
        using var app = new RenderHarness(() => field.Width(240));
        app.Frame();

        // Click at the field's left edge, then fill it well past its width: it scrolls to the caret.
        app.Click(4, 18);
        foreach (char c in new string('k', 60))
        {
            app.Press(Key.K, character: c);
        }
        app.Frame();
        await Assert.That(InputDispatcher.PasswordScrollOffsetX).IsGreaterThan(0f);

        // Select everything and type one character: the text fits again, so nothing is scrolled off.
        app.Press(Key.A, ModifierKeys.Ctrl);
        app.Press(Key.M, character: 'm');
        app.Frame();
        await Assert.That(value).IsEqualTo("m");
        await Assert.That(InputDispatcher.PasswordScrollOffsetX).IsEqualTo(0f);

        // A click at the left padding puts the caret before the first character.
        app.Click(4, 18);
        app.Press(Key.N, character: 'n');
        await Assert.That(value).IsEqualTo("nm");
    }

    [Test]
    public async Task PasswordTypeTextInput_NeverCopiesToTheClipboard()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string value = "secret";
        var field = new TextInput(new Bindable<string>("secret", v => value = v), inputType: InputType.Password);
        using var app = new RenderHarness(() => field.Width(240));
        app.Frame();
        FocusManager.RequestFocus(field);
        await Clipboard.WriteTextAsync("before");

        app.Press(Key.A, ModifierKeys.Ctrl);
        app.Press(Key.C, ModifierKeys.Ctrl);
        app.Press(Key.X, ModifierKeys.Ctrl);

        var content = await Clipboard.GetContentAsync();
        await Assert.That(await content.GetTextAsync()).IsEqualTo("before");
        await Assert.That(value).IsEqualTo("secret");
    }

    [Test]
    public async Task Rating_DrawsStars_NotCircles()
    {
        using var app = new RenderHarness(() => new Rating(1f, max: 1).Size(64).Color(Red, Green), crossAxisCenter: true);
        var (cx, cy, r) = RedShape(app.Frame());

        // A solid star: its middle is filled, but the notch between its top and upper-left points
        // (126° round from the top, three quarters out) is empty, where a circle would be filled.
        var frame = app.Frame();
        await Assert.That(IsRed(frame, cx, cy)).IsTrue();
        double notch = (-90 - 36) * Math.PI / 180;
        await Assert.That(IsRed(frame, cx + (int)(Math.Cos(notch) * r * 0.75), cy + (int)(Math.Sin(notch) * r * 0.75))).IsFalse();
    }

    [Test]
    public async Task RatingHalfStars_FillsOnlyTheLeftHalf()
    {
        using var app = new RenderHarness(() => new Rating(0.5f, max: 1).HalfStars().Size(64).Color(Red, Green), crossAxisCenter: true);
        var frame = app.Frame();

        int centerX = RenderHarnessCenterX(frame);
        int leftRed = frame.Count(p => p.X < centerX && IsRedPixel(p));
        int rightRed = frame.Count(p => p.X > centerX + 2 && IsRedPixel(p));
        await Assert.That(leftRed).IsGreaterThan(100);
        await Assert.That(rightRed).IsEqualTo(0);
    }

    [Test]
    public async Task RatingIcon_DrawsTheGivenGlyphs()
    {
        // Straight down the middle from the top: a star's point is filled there, a heart dips between
        // its lobes. (One harness at a time: they share their content.)
        await Assert.That(TopCentreIsRed(() => new Rating(1f, max: 1).Size(64).Color(Red, Green))).IsTrue();
        await Assert.That(TopCentreIsRed(() => new Rating(1f, max: 1).Icon(Icons.Heart, Icons.Heart).Size(64).Color(Red, Green))).IsFalse();
    }

    private static bool TopCentreIsRed(Func<Node> build)
    {
        using var app = new RenderHarness(build, crossAxisCenter: true);
        var frame = app.Frame();
        var (cx, cy, r) = RedShape(frame);
        // The ink's top edge in the middle column, a few pixels in.
        return IsRed(frame, cx, cy - r + 4);
    }

    [Test]
    public async Task RatingLabels_ShowTheHoveredStarsLabel()
    {
        // Hover the middle star; only the labelled rating adds a tooltip's worth of ink.
        int plainInk = HoveredInk(() => new Rating(new Bindable<float>(0f, _ => { }), max: 3).Size(32));
        int labelledInk = HoveredInk(() => new Rating(new Bindable<float>(0f, _ => { }), max: 3).Size(32).Labels(["Poor", "Fine", "Great"]));
        await Assert.That(labelledInk).IsGreaterThan(plainInk + 200);
    }

    private static int HoveredInk(Func<Node> build)
    {
        using var app = new RenderHarness(build, crossAxisCenter: true);
        app.Frame();
        app.MoveMouse(RenderHarness.Width / 2f, 16);
        app.Frame();
        return app.Frame().Count(p => p.R + p.G + p.B > 60);
    }

    [Test]
    public async Task OneImageSource_DrawsInEveryBackendThatShowsIt()
    {
        // Image handles are numbered per backend: a second window (or a rebuilt backend) drawing the
        // same ImageSource used to get the first backend's handle - nothing, or another image.
        byte[] red = new byte[16 * 16 * 4];
        for (int i = 0; i < red.Length; i += 4)
        {
            red[i] = 255;
            red[i + 3] = 255;
        }
        using var image = ImageSource.FromBytes(red, 16, 16);

        int first, second;
        using (var app = new RenderHarness(() => new Image(image).AccessibleLabel("").Size(40, 40)))
        {
            first = app.Frame().Count(IsRedPixel);
        }
        using (var app = new RenderHarness(() => new Image(image).AccessibleLabel("").Size(40, 40)))
        {
            second = app.Frame().Count(IsRedPixel);
        }

        await Assert.That(first).IsGreaterThan(1000);
        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task VerticalSlider_StandsOnEnd_AndDraggingUpRaisesIt()
    {
        float value = 0f;
        var slider = new Slider(new Bindable<float>(0f, v => value = v), min: 0, max: 100).Orientation(Orientation.Vertical).Width(160);
        using var app = new RenderHarness(() => slider);
        var frame = app.Frame();

        await Assert.That(slider.LayoutData.Bounds.Width).IsEqualTo(32f);
        await Assert.That(slider.LayoutData.Bounds.Height).IsEqualTo(160f);
        // At the minimum the thumb (bright, near white) sits at the bottom of the track.
        await Assert.That(frame.Count(p => p.Y > 130 && p.Y < 160 && p.R > 200 && p.G > 200 && p.B > 200)).IsGreaterThan(20);
        await Assert.That(frame.Count(p => p.Y < 40 && p.R > 200 && p.G > 200 && p.B > 200)).IsEqualTo(0);

        // Dragging the thumb up by half the track raises the value by half the range.
        float track = 160f - ThemeSwitcher.Current.Slider.ThumbHeight;
        app.Drag(16, 150, 16, 150 - track / 2f);
        await Assert.That(value).IsBetween(45f, 55f);
    }

    [Test]
    public async Task Slider_ArrowKeys_PageKeys_HomeAndEnd()
    {
        // Each key from a known value (the harness doesn't re-render on a change).
        static float After(float start, Key key)
        {
            float value = start;
            var slider = new Slider(new Bindable<float>(start, v => value = v), min: 0, max: 100, step: 5);
            using var app = new RenderHarness(() => slider);
            app.Frame();
            FocusManager.RequestFocus(slider);
            app.Press(key);
            return value;
        }

        await Assert.That(After(50, Key.Right)).IsEqualTo(55f);
        await Assert.That(After(50, Key.Up)).IsEqualTo(55f);
        await Assert.That(After(50, Key.Left)).IsEqualTo(45f);
        await Assert.That(After(50, Key.Down)).IsEqualTo(45f);
        await Assert.That(After(50, Key.PageUp)).IsEqualTo(60f);
        await Assert.That(After(50, Key.PageDown)).IsEqualTo(40f);
        await Assert.That(After(50, Key.End)).IsEqualTo(100f);
        await Assert.That(After(50, Key.Home)).IsEqualTo(0f);
        await Assert.That(After(100, Key.Right)).IsEqualTo(100f);    }

    [Test]
    public async Task SliderShowTicks_DrawsAMarkPerStep_BelowTheTrack()
    {
        int plain = TickInk(() => new Slider(new Bindable<float>(0f, _ => { }), min: 0, max: 10, step: 1));
        Slider? ticked = null;
        int marked = TickInk(() => ticked = new Slider(new Bindable<float>(0f, _ => { }), min: 0, max: 10, step: 1).ShowTicks());

        await Assert.That(ticked!.LayoutData.Bounds.Height).IsEqualTo(32f + Slider.TickSpace);
        // Eleven marks, each a few pixels, all below the track's band.
        await Assert.That(marked - plain).IsGreaterThan(11 * 3);
    }

    [Test]
    public async Task RangeSliderShowTicks_DrawsMarksBelowTheTrack()
    {
        int plain = TickInk(() => new RangeSlider(new Bindable<float>(2f, _ => { }), new Bindable<float>(8f, _ => { }), min: 0, max: 10, step: 1));
        int marked = TickInk(() => new RangeSlider(new Bindable<float>(2f, _ => { }), new Bindable<float>(8f, _ => { }), min: 0, max: 10, step: 1).ShowTicks());
        await Assert.That(marked - plain).IsGreaterThan(11 * 3);
    }

    private static readonly Step[] ThreeSteps = [new("Cart"), new("Pay"), new("Done")];

    private static StepIndicator Steps(StepStyle style, int current = 1, Action<int>? onClick = null)
    {
        var indicator = new StepIndicator(new Bindable<int>(current, _ => { }), ThreeSteps).Style(style);
        return onClick is null ? indicator : indicator.OnStepClick(onClick);
    }

    [Test]
    public async Task StepIndicatorDotted_DrawsSmallDots_NotNumberedCircles()
    {
        // Between the current dot's ring (to 15) and the labels (from 18) a dotted indicator has no ink; the
        // numbered one's 28px circles fill that band.
        static int MidBandInk(StepStyle style)
        {
            using var app = new RenderHarness(() => Steps(style).Width(300));
            return app.Frame().Count(p => p.Y >= 16 && p.Y <= 17 && p.R + p.G + p.B > 90);
        }

        await Assert.That(MidBandInk(StepStyle.Numbered)).IsGreaterThan(40);
        await Assert.That(MidBandInk(StepStyle.Dotted)).IsEqualTo(0);
    }

    [Test]
    public async Task StepIndicatorBar_FillsTheSegmentsReached()
    {
        // Step 2 of 3 is current: two of three segments are in the accent colour.
        using var app = new RenderHarness(() => Steps(StepStyle.Bar).Width(300));
        var frame = app.Frame();
        var primary = ThemeSwitcher.Current.Colors.Primary;
        (byte r, byte g, byte b) = primary.ToSrgbBytes();
        int filled = frame.Count(p => p.Y == 3 && Math.Abs(p.R - r) < 20 && Math.Abs(p.G - g) < 20 && Math.Abs(p.B - b) < 20);
        await Assert.That(filled).IsBetween(180, 205);
    }

    [Test]
    public async Task StepIndicatorChecklist_StacksARowPerStep_AndClicksSelectTheRow()
    {
        int clicked = -1;
        StepIndicator? list = null;
        using var app = new RenderHarness(() => list = Steps(StepStyle.Checklist, onClick: i => clicked = i));
        app.Frame();

        await Assert.That(list!.LayoutData.Bounds.Height).IsEqualTo(3 * StepIndicator.ChecklistRow);
        app.Click(40, StepIndicator.ChecklistRow * 2 + 10);
        await Assert.That(clicked).IsEqualTo(2);
    }

    private static readonly ToggleOption<string>[] Sizes = [new("s", "Small"), new("m", "Medium"), new("l", "Large")];

    [Test]
    public async Task VerticalToggleGroup_StacksTheOptions_AndAClickPicksTheRow()
    {
        string chosen = "s";
        ToggleGroup<string>? group = null;
        using var app = new RenderHarness(() => group = new ToggleGroup<string>(new Bindable<string>("s", v => chosen = v), Sizes)
            .Orientation(Orientation.Vertical));
        var frame = app.Frame();

        float row = IToggleGroup.VerticalRowHeight;
        await Assert.That(group!.LayoutData.Bounds.Height).IsEqualTo(3 * row + 4f);
        await Assert.That(group.LayoutData.Bounds.Width).IsLessThan(150f);

        // The selection (accent fill) is on the first row only.
        var primary = ThemeSwitcher.Current.Colors.Primary;
        (byte r, byte g, byte b) = primary.ToSrgbBytes();
        bool Accent(RenderHarness.Pixel p) => Math.Abs(p.R - r) < 20 && Math.Abs(p.G - g) < 20 && Math.Abs(p.B - b) < 20;
        await Assert.That(frame.Count(p => p.Y < 2 + row && Accent(p))).IsGreaterThan(500);
        await Assert.That(frame.Count(p => p.Y > 4 + row && Accent(p))).IsEqualTo(0);

        app.Click(30, 2 + row * 2 + row / 2);
        await Assert.That(chosen).IsEqualTo("l");
    }

    [Test]
    public async Task ToggleGroup_RichOptionLabels_AreDrawn_AndClickable()
    {
        // Two icon options: each draws its icon (red) in its own button, not "Option 1".
        string chosen = "left";
        ToggleOption<string>[] icons =
        [
            new("left", new IconView(Icons.AlignLeft, 20).Color(Red)),
            new("right", new IconView(Icons.AlignRight, 20).Color(Red)),
        ];
        ToggleGroup<string>? group = null;
        using var app = new RenderHarness(() => group = new ToggleGroup<string>(new Bindable<string>("left", v => chosen = v), icons));
        var frame = app.Frame();

        float width = group!.LayoutData.Bounds.Width;
        await Assert.That(frame.Count(p => p.X < width / 2 && IsRedPixel(p))).IsGreaterThan(15);
        await Assert.That(frame.Count(p => p.X > width / 2 && p.X < width && IsRedPixel(p))).IsGreaterThan(15);

        app.Click(width * 0.75f, 18);
        await Assert.That(chosen).IsEqualTo("right");
    }

    [Test]
    public async Task ToggleGroup_ArrowKeysMoveTheSelection()
    {
        static string After(string start, Key key)
        {
            string chosen = start;
            var group = new ToggleGroup<string>(new Bindable<string>(start, v => chosen = v), Sizes);
            using var app = new RenderHarness(() => group);
            app.Frame();
            FocusManager.RequestFocus(group);
            app.Press(key);
            return chosen;
        }

        await Assert.That(After("m", Key.Right)).IsEqualTo("l");
        await Assert.That(After("m", Key.Down)).IsEqualTo("l");
        await Assert.That(After("m", Key.Left)).IsEqualTo("s");
        await Assert.That(After("s", Key.Up)).IsEqualTo("s");
    }

    private static readonly SelectOption<string>[] Fruit = [new("a", "Apple"), new("b", "Banana"), new("c", "Cherry")];

    [Test]
    public async Task ReadOnlySelects_NeverOpen_OrChange()
    {
        // A click or Space/Enter/Down would open each one; read-only, none opens and nothing changes.
        static bool OpensOnInput(Func<Node> build)
        {
            var node = build();
            using var app = new RenderHarness(() => node.Width(200));
            app.Frame();
            app.Click(100, 18);
            if (!IsOpen(node))
            {
                FocusManager.RequestFocus(node);
                foreach (var key in new[] { Key.Space, Key.Enter, Key.Down })
                {
                    app.Press(key);
                }
            }
            return IsOpen(node);
        }

        static bool IsOpen(Node node)
        {
            return node switch
            {
                ISelectNode s => s.IsOpen,
                IMultiSelectNode m => m.IsOpen,
                IComboboxNode c => c.IsOpen,
                _ => throw new InvalidOperationException(node.GetType().Name),
            };
        }

        string single = "a";
        IReadOnlyList<string> many = ["a"];
        await Assert.That(OpensOnInput(() => new Select<string>(new Bindable<string>("a", v => single = v), Fruit))).IsTrue();
        await Assert.That(OpensOnInput(() => new Select<string>(new Bindable<string>("a", v => single = v), Fruit).ReadOnly())).IsFalse();
        await Assert.That(OpensOnInput(() => new MultiSelect<string>(new Bindable<IReadOnlyList<string>>(["a"], v => many = v), Fruit).ReadOnly())).IsFalse();
        await Assert.That(OpensOnInput(() => new Combobox<string>(new Bindable<string>("a", v => single = v), Fruit).ReadOnly())).IsFalse();

        // Nor can the value be set through the control.
        var select = new Select<string>(new Bindable<string>("a", v => single = v), Fruit).ReadOnly();
        single = "a";
        ((ISelectNode)select).SelectIndex(2);
        var multi = new MultiSelect<string>(new Bindable<IReadOnlyList<string>>(["a"], v => many = v), Fruit).ReadOnly();
        ((IMultiSelectNode)multi).ToggleItem(1);
        var combo = new Combobox<string>(new Bindable<string>("a", v => single = v), Fruit).ReadOnly();
        ((IComboboxNode)combo).SelectFilteredIndex(1);
        await Assert.That(single).IsEqualTo("a");
        await Assert.That(string.Join(",", many)).IsEqualTo("a");
    }

    [Test]
    public async Task Select_TakesFocusFromAClickOrTab_AndSpaceOpensIt()
    {
        var select = new Select<string>(new Bindable<string>("a", _ => { }), Fruit);
        var before = new Button("Before", () => { });
        using var app = new RenderHarness(() => new Column(children: [before, select.Width(200)]));
        app.Frame();

        // Tab from the button lands on the select; Space opens it.
        FocusManager.RequestFocus(before);
        app.Press(Key.Tab);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(select);
        app.Press(Key.Space, character: ' ');
        await Assert.That(((ISelectNode)select).IsOpen).IsTrue();

        // A click on it focuses it too.
        app.Press(Key.Escape);
        FocusManager.ClearFocus();
        var b = select.LayoutData.Bounds;
        app.Click(b.X + 50, b.Y + b.Height / 2);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(select);
    }

    [Test]
    public async Task SegmentedControlAndRating_AnswerTheArrowKeys()
    {
        static T After<T>(Func<Action<T>, Node> build, T start, Key key)
        {
            T value = start;
            var node = build(v => value = v);
            using var app = new RenderHarness(() => node);
            app.Frame();
            FocusManager.RequestFocus(node);
            app.Press(key);
            return value;
        }

        SegmentOption<string>[] views = [new("day", "Day"), new("week", "Week"), new("month", "Month")];
        await Assert.That(After<string>(set => new SegmentedControl<string>(new Bindable<string>("day", set), views), "day", Key.Right)).IsEqualTo("week");
        await Assert.That(After<float>(set => new Rating(new Bindable<float>(2f, set)), 2f, Key.Right)).IsEqualTo(3f);
        await Assert.That(After<float>(set => new Rating(new Bindable<float>(2f, set)).HalfStars(), 2f, Key.Left)).IsEqualTo(1.5f);
        await Assert.That(After<float>(set => new Rating(new Bindable<float>(2f, set)), 2f, Key.End)).IsEqualTo(5f);
    }

    [Test]
    public async Task ComboboxAsyncOptions_SearchAfterTheDebounce_AndShowLoadingMeanwhile()
    {
        var queries = new List<string>();
        Task<IEnumerable<SelectOption<string>>> Search(string query)
        {
            queries.Add(query);
            return Task.FromResult<IEnumerable<SelectOption<string>>>(
                Fruit.Where(f => f.Label.Resolve().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        string chosen = "";
        var combo = new Combobox<string>(new Bindable<string>("", v => chosen = v))
            .Options(Search).Debounce(TimeSpan.FromMilliseconds(80));
        IComboboxNode node = combo;
        using var app = new RenderHarness(() => combo.Width(200));
        app.Frame();
        FocusManager.RequestFocus(combo);

        // Typing opens it and searches; before the debounce passes, it says it is loading.
        foreach (char c in "an")
        {
            app.Press(Key.A, character: c);
            app.Frame();
        }
        await Assert.That(node.IsOpen).IsTrue();
        await Assert.That(node.DropdownStatus).IsEqualTo("Loading…");
        await Assert.That(queries.Count).IsEqualTo(0);

        await Task.Delay(120);
        app.Frame();
        app.Frame();
        // One search, for the settled text: "an" matches Banana.
        await Assert.That(string.Join(",", queries)).IsEqualTo("an");
        await Assert.That(node.DropdownStatus).IsNull();
        await Assert.That(node.FilteredOptionCount).IsEqualTo(1);

        node.SelectFilteredIndex(0);
        await Assert.That(chosen).IsEqualTo("b");

        // The app re-renders with the new value: the field shows the label it was picked by.
        var rerendered = new Combobox<string>(new Bindable<string>(chosen, _ => { })).Options(Search);
        ((IPumpedNode)rerendered).AdoptFrom(combo);
        await Assert.That(((IComboboxNode)rerendered).DisplayText).IsEqualTo("Banana");
    }

    [Test]
    public async Task ComboboxRenderOption_DrawsTheAppsContentPerRow()
    {
        var rendered = new List<string>();
        var combo = new Combobox<string>(new Bindable<string>("a", _ => { }), Fruit)
            .RenderOption(v => { rendered.Add(v); return new Label(v.ToUpperInvariant()); });
        IComboboxNode node = combo;
        using var app = new RenderHarness(() => combo.Width(200));
        app.Frame();
        node.ToggleOpen();
        for (int i = 0; i < 20; i++)
        {
            app.Frame();
        }

        await Assert.That(rendered.Distinct().Order().ToList()).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task SearchableSelect_FiltersAsYouType_AndEnterPicksTheFirstMatch()
    {
        string chosen = "a";
        var select = new Select<string>(new Bindable<string>("a", v => chosen = v), Fruit).Searchable();
        ISelectNode node = select;
        using var app = new RenderHarness(() => select.Width(200));
        app.Frame();
        FocusManager.RequestFocus(select);
        app.Press(Key.Enter);
        await Assert.That(node.IsOpen).IsTrue();

        foreach (char c in "er")
        {
            app.Press(Key.A, character: c);
        }
        // "er" matches Cherry only.
        await Assert.That(node.OptionCount).IsEqualTo(1);
        await Assert.That(node.GetOptionLabel(0)).IsEqualTo("Cherry");

        app.Press(Key.Backspace);
        app.Press(Key.Backspace);
        await Assert.That(node.OptionCount).IsEqualTo(3);

        app.Press(Key.B, character: 'b');
        app.Press(Key.Enter);
        await Assert.That(chosen).IsEqualTo("b");
    }

    [Test]
    public async Task SearchableSelect_WithAnAsyncSource_ShowsItsResults()
    {
        var queries = new List<string>();
        Task<IEnumerable<SelectOption<string>>> Search(string query)
        {
            queries.Add(query);
            return Task.FromResult<IEnumerable<SelectOption<string>>>([new($"id-{query}", $"Found {query}")]);
        }

        string chosen = "";
        var select = new Select<string>(new Bindable<string>("", v => chosen = v), Array.Empty<SelectOption<string>>()).Searchable(Search);
        ISelectNode node = select;
        using var app = new RenderHarness(() => select.Width(200));
        app.Frame();
        node.ToggleOpen();
        node.SearchText = "xy";
        app.Frame();
        await Assert.That(node.DropdownStatus).IsEqualTo("Loading…");

        await Task.Delay(300);
        app.Frame();
        app.Frame();
        await Assert.That(string.Join(",", queries)).IsEqualTo("xy");
        await Assert.That(node.GetOptionLabel(0)).IsEqualTo("Found xy");

        node.SelectIndex(0);
        await Assert.That(chosen).IsEqualTo("id-xy");

        // The app re-renders with the new value: the closed field shows the label it was picked by.
        var rerendered = new Select<string>(new Bindable<string>(chosen, _ => { }), Array.Empty<SelectOption<string>>()).Searchable(Search);
        ((IPumpedNode)rerendered).AdoptFrom(select);
        await Assert.That(((ISelectNode)rerendered).SelectedDisplayText).IsEqualTo("Found xy");
    }

    [Test]
    public async Task SelectRenderers_DrawTheAppsContent()
    {
        var options = new List<string>();
        var selected = new List<string>();
        var select = new Select<string>(new Bindable<string>("b", _ => { }), Fruit)
            .RenderOption(v => { options.Add(v); return new Label(v); })
            .RenderSelected(v => { selected.Add(v); return new Label(v.ToUpperInvariant()); });
        using var app = new RenderHarness(() => select.Width(200));
        app.Frame();
        await Assert.That(selected.Distinct().ToList()).IsEquivalentTo(["b"]);

        ((ISelectNode)select).ToggleOpen();
        for (int i = 0; i < 20; i++)
        {
            app.Frame();
        }
        await Assert.That(options.Distinct().Order().ToList()).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task MultiSelectSelectAll_TicksEverything_ThenClearsIt()
    {
        IReadOnlyList<string> chosen = [];
        var multi = new MultiSelect<string>(new Bindable<IReadOnlyList<string>>(["a"], v => chosen = v), Fruit).SelectAll();
        IMultiSelectNode node = multi;
        using var app = new RenderHarness(() => multi.Width(200));
        app.Frame();
        node.ToggleOpen();

        await Assert.That(node.OptionCount).IsEqualTo(4);
        await Assert.That(node.GetOptionLabel(0)).IsEqualTo("Select all");
        node.ToggleItem(0);
        await Assert.That(node.IsItemSelected(0)).IsTrue();
        await Assert.That(node.GetOptionLabel(0)).IsEqualTo("Clear all");
        node.Close();
        await Assert.That(string.Join(",", chosen.Order())).IsEqualTo("a,b,c");
    }

    [Test]
    public async Task SearchableMultiSelect_FiltersAndTicksFromTheKeyboard()
    {
        IReadOnlyList<string> chosen = [];
        var multi = new MultiSelect<string>(new Bindable<IReadOnlyList<string>>([], v => chosen = v), Fruit).Searchable();
        IMultiSelectNode node = multi;
        using var app = new RenderHarness(() => multi.Width(200));
        app.Frame();
        FocusManager.RequestFocus(multi);
        app.Press(Key.Enter);
        await Assert.That(node.IsOpen).IsTrue();

        // "rr" matches Cherry; the first match is highlighted, Space ticks it, Escape closes and commits.
        app.Press(Key.R, character: 'r');
        app.Press(Key.R, character: 'r');
        await Assert.That(node.OptionCount).IsEqualTo(1);
        app.Press(Key.Space);
        app.Press(Key.Escape);
        await Assert.That(string.Join(",", chosen)).IsEqualTo("c");

        // Nothing matching says so.
        app.Press(Key.Enter);
        app.Press(Key.Z, character: 'z');
        await Assert.That(node.DropdownStatus).IsEqualTo("No matches");
    }

    [Test]
    public async Task MultiSelect_KeepsOpenChoicesThroughARerender()
    {
        var first = new MultiSelect<string>(new Bindable<IReadOnlyList<string>>([], _ => { }), Fruit);
        IMultiSelectNode node = first;
        node.ToggleOpen();
        node.ToggleItem(1);

        IReadOnlyList<string> chosen = [];
        var second = new MultiSelect<string>(new Bindable<IReadOnlyList<string>>([], v => chosen = v), Fruit);
        ((IMultiSelectNode)second).IsOpen = true;
        ((IPumpedNode)second).AdoptFrom(first);
        ((IMultiSelectNode)second).Close();
        await Assert.That(string.Join(",", chosen)).IsEqualTo("b");
    }

    // Six buttons recording their index when clicked.
    private static ToolBarItem[] SixButtons(List<int> clicks) =>
        [.. Enumerable.Range(0, 6).Select(i => ToolBarItem.Button(Icons.Star, $"Item {i}", () => clicks.Add(i)))];

    private const float Slot = ToolBar.ButtonSize + ToolBar.ItemGap;

    [Test]
    public async Task VerticalToolBar_StacksItsButtons()
    {
        var clicks = new List<int>();
        ToolBar? bar = null;
        using var app = new RenderHarness(() => bar = new ToolBar(SixButtons(clicks)).Orientation(Orientation.Vertical));
        app.Frame();

        await Assert.That(bar!.LayoutData.Bounds.Width).IsEqualTo(ToolBar.ButtonSize);
        await Assert.That(bar.LayoutData.Bounds.Height).IsEqualTo(6 * ToolBar.ButtonSize + 5 * ToolBar.ItemGap);
        app.Click(16, Slot * 2 + 16);
        await Assert.That(string.Join(",", clicks)).IsEqualTo("2");
    }

    [Test]
    public async Task ToolBarOverflowMenu_KeepsWhatFits_AndListsTheRestBehindAMoreButton()
    {
        var clicks = new List<int>();
        using var app = new RenderHarness(() => new ToolBar(SixButtons(clicks)).Width(120));
        app.Frame();

        // 120px: two buttons and the "more" button at the end.
        app.Click(120 - 16, 16);
        await Assert.That(app.Input.IsMenuOpen).IsTrue();
        var items = app.Input.Menu!.Levels[0].Items;
        await Assert.That(string.Join(",", items.Select(i => i.Label))).IsEqualTo("Item 2,Item 3,Item 4,Item 5");

        // Choosing one runs that button.
        items[1].OnClick!();
        await Assert.That(string.Join(",", clicks)).IsEqualTo("3");
    }

    [Test]
    public async Task ToolBarOverflowWrap_StartsASecondRow()
    {
        var clicks = new List<int>();
        ToolBar? bar = null;
        using var app = new RenderHarness(() => new Column(children: [bar = new ToolBar(SixButtons(clicks)).Overflow(OverflowMode.Wrap)]).Width(120));
        app.Frame();

        // Three to a 120px row.
        await Assert.That(bar!.LayoutData.Bounds.Height).IsEqualTo(2 * ToolBar.ButtonSize + ToolBar.ItemGap);
        app.Click(16, Slot + 16);
        await Assert.That(string.Join(",", clicks)).IsEqualTo("3");
    }

    [Test]
    public async Task ToolBarOverflowScroll_TheWheelScrollsTheButtons()
    {
        var clicks = new List<int>();
        using var app = new RenderHarness(() => new ToolBar(SixButtons(clicks)).Overflow(OverflowMode.Scroll).Width(120));
        app.Frame();

        app.Click(16, 16);
        // Scroll to the end: the last button is now at the right edge.
        for (int i = 0; i < 5; i++)
        {
            app.Wheel(60, 16, -1f);
        }
        app.Click(120 - 16, 16);
        await Assert.That(string.Join(",", clicks)).IsEqualTo("0,5");
    }

    [Test]
    public async Task ToolBarCustomItem_IsLaidOut_Drawn_AndClickable()
    {
        int pressed = 0;
        using var app = new RenderHarness(() => new ToolBar(
            ToolBarItem.Button(Icons.Star, "Star", () => { }),
            ToolBarItem.Custom(new Button("Go", () => pressed++))));
        var frame = app.Frame();

        // The button sits after the first slot and takes a click.
        await Assert.That(frame.Count(p => p.X > Slot && p.R + p.G + p.B > 300)).IsGreaterThan(20);
        app.Click(Slot + 20, 16);
        await Assert.That(pressed).IsEqualTo(1);
    }

    // Ink below the 32px band where tick marks go.
    private static int TickInk(Func<Node> build)
    {
        using var app = new RenderHarness(build);
        return app.Frame().Count(p => p.Y >= 30 && p.Y < 32 + (int)Slider.TickSpace && p.R + p.G + p.B > 90);
    }

    private static bool IsRedPixel(RenderHarness.Pixel p) => p.R > 200 && p.G < 60 && p.B < 60;

    private static bool IsRed(RenderHarness.Pixels frame, int x, int y) =>
        frame.Count(p => Math.Abs(p.X - x) <= 1 && Math.Abs(p.Y - y) <= 1 && IsRedPixel(p)) > 0;

    // Centre and half-width of the red ink.
    private static (int X, int Y, int R) RedShape(RenderHarness.Pixels frame)
    {
        int minX = int.MaxValue, maxX = 0, minY = int.MaxValue, maxY = 0;
        frame.Count(p =>
        {
            if (IsRedPixel(p))
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            return false;
        });
        return ((minX + maxX) / 2, (minY + maxY) / 2, (maxX - minX) / 2);
    }

    // The x the 64px star is centred on: the middle of all its ink (red and green).
    private static int RenderHarnessCenterX(RenderHarness.Pixels frame)
    {
        int minX = int.MaxValue, maxX = 0;
        frame.Count(p =>
        {
            if (p.R > 150 || p.G > 150)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            }
            return false;
        });
        return (minX + maxX) / 2;
    }

    [Test]
    public async Task SelectableTag_WithOnlyABinding_TogglesOnClick()
    {
        var seen = new List<bool>();
        int toggles = 0;
        using (var plain = new RenderHarness(() => new Tag("DI.FM", new Bindable<bool>(true, seen.Add))))
        {
            plain.Frame();
            plain.Click(15, 12);
        }
        using (var both = new RenderHarness(() => new Tag("DI.FM", new Bindable<bool>(false, seen.Add), _ => toggles++)))
        {
            both.Frame();
            both.Click(15, 12);
        }

        await Assert.That(string.Join(",", seen)).IsEqualTo("False,True");
        await Assert.That(toggles).IsEqualTo(1);
    }

    [Test]
    public async Task ListViewScrollbar_DragsTheList()
    {
        var rows = Enumerable.Range(0, 100).Select(i => $"Row {i}").ToList();
        var list = new ListView<string>(rows, row => new Label(row)).ItemHeight(30).Plain(true).Size(300, 180);
        using var app = new RenderHarness(() => new Column(children: [list]).Padding(EdgeInsets.Only(left: 50, top: 20)));
        app.Frame();
        var node = (IListViewNode)list;
        var track = node.ScrollbarTrackBounds;

        // The grab area hugs the list's right edge, where the thumb is drawn.
        await Assert.That(track.Right).IsEqualTo(350f).Within(1f);
        await Assert.That(track.Height).IsGreaterThan(100f);

        float x = track.X + track.Width / 2f;
        float y = track.Y + 10f;
        app.Drag(x, y, x, y + 60f);

        // 60 px of travel over (track - thumb) moves the offset that share of MaxY.
        float expected = 60f / (track.Height - node.ScrollbarThumbHeight) * node.MaxY;
        await Assert.That(node.OffsetY).IsEqualTo(Math.Min(expected, node.MaxY)).Within(1f);
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

    /// <summary>The window's input dispatcher (its open menu, say).</summary>
    public InputDispatcher Input => orchestrator.Input;

    /// <summary>One wheel step at (x, y): positive <paramref name="deltaY"/> scrolls up.</summary>
    public void Wheel(float x, float y, float deltaY, float deltaX = 0f)
    {
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseMove });
        orchestrator.Input.HandleScrollEvent(new NativeScrollEvent { X = x, Y = y, DeltaX = deltaX, DeltaY = deltaY });
        orchestrator.Tick();
    }

    public void Click(float x, float y)
    {
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseMove });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
        orchestrator.Tick();
    }

    public void Press(Key key, ModifierKeys modifiers = ModifierKeys.None, char? character = null)
    {
        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers, Character = character });
        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp });
        orchestrator.Tick();
    }

    public void Drag(float fromX, float fromY, float toX, float toY)
    {
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = fromX, Y = fromY, Type = NativeMouseEventType.MouseMove });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = fromX, Y = fromY, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = toX, Y = toY, Type = NativeMouseEventType.MouseMove });
        orchestrator.Input.HandleMouseEvent(new NativeMouseEvent { X = toX, Y = toY, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
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

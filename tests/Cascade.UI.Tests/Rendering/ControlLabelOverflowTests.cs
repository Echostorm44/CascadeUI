using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// A control narrower than its caption painted the caption past its own edge: a Button pinned
/// narrower than its label (or given an Expand share of a tight row) drew the whole label over its
/// neighbours, and so did LinkButton, Select (under and past its chevron), Toggle, RadioButton
/// and the SegmentedControl (whose first label started left of the control). Each test lays a
/// control out in a real frame (Apple theme, the real fonts), paints it into a recording backend
/// and checks that every glyph run it drew fits inside it — by shaping the run's text, so the
/// check covers the run's real width, not just where its glyphs start.
/// </summary>
[NotInParallel(nameof(ControlLabelOverflowTests))]
public class ControlLabelOverflowTests
{
    private const string Ellipsis = "…";

    [Test]
    public async Task Button_NarrowerThanItsLabel_KeepsTheLabelInside()
    {
        var runs = Paint(() => new Row(children: [new Button("Delete everything", () => { }).Width(56)]));

        await AssertRunsInside(runs, "Delete everything");
    }

    [Test]
    public async Task Button_NarrowerThanItsLabel_StillShowsALeadingLetter()
    {
        // The squeezed button gives up padding before characters: a 70px "Worker confirm" shows
        // "Wo…", not a lone ellipsis (and not nothing).
        var runs = Paint(() => new Row(children: [new Button("Worker confirm", () => { }).Width(70)]));

        await AssertRunsInside(runs, "Worker confirm");
        var drawn = runs.Single(r => r.Text is not null).Text!;
        await Assert.That(drawn.Length).IsGreaterThan(Ellipsis.Length);
        await Assert.That(drawn).EndsWith(Ellipsis);
    }

    [Test]
    public async Task ExpandButtonsInATightRow_KeepTheirLabelsInside()
    {
        var runs = Paint(() => new Column(children:
        [
            new Row(spacing: 8, children:
            [
                new Button("Paste and keep window open", () => { }).Expand(),
                new Button("Cancel everything", () => { }).Variant("outline").Expand(),
            ]).Width(260),
        ]));

        await AssertRunsInside(runs, "Paste and keep window open", "Cancel everything");
    }

    [Test]
    public async Task Button_WithAnIcon_NarrowerThanItsContent_KeepsTheLabelInside()
    {
        var runs = Paint(() => new Row(children: [new Button("Show details", () => { }, PinIcon).Width(96)]));

        await AssertRunsInside(runs, "Show details");
    }

    [Test]
    public async Task Button_FixedNarrowWidth_StaysOneLineTall()
    {
        // Measuring the caption wrapped to the narrow width made the button two lines tall while it
        // still painted one line.
        var natural = MeasureSingle(new Button("Worker confirm", () => { }));
        var narrow = MeasureSingle(new Button("Worker confirm", () => { }).Width(70));

        await Assert.That(narrow.Height).IsEqualTo(natural.Height).Within(0.01f);
        await Assert.That(narrow.Width).IsEqualTo(70f).Within(0.01f);
    }

    [Test]
    public async Task LinkButton_NarrowerThanItsLabel_KeepsItInside()
    {
        var runs = Paint(() => new Row(children: [new LinkButton("Open in browser window", () => { }).Width(90)]));

        await AssertRunsInside(runs, "Open in browser window");
    }

    [Test]
    public async Task Select_NarrowerThanItsValue_StopsBeforeTheChevron()
    {
        var runs = Paint(() => new Row(children:
        [
            new Select<string>(new Bindable<string>("all", _ => { }), TypeOptions).Width(110),
        ]));

        // The chevron box (Apple combo-box style) takes the trailing (height - 3) px.
        await AssertRunsInside(runs, insetRight: 36f - 3f, "All Types and Formats");
    }

    [Test]
    public async Task Toggle_NarrowerThanItsLabel_KeepsItInside()
    {
        var runs = Paint(() => new Row(children:
        [
            new Toggle(new Bindable<bool>(true, _ => { }), "Paste as plain text").Width(130),
        ]));

        await AssertRunsInside(runs, "Paste as plain text");
    }

    [Test]
    public async Task Checkbox_NarrowerThanItsLabel_KeepsItInside_AndStaysOneLineTall()
    {
        var runs = Paint(() => new Row(children:
        [
            new Checkbox(new Bindable<bool>(true, _ => { }), "Start with Windows").Width(110),
        ]));

        await AssertRunsInside(runs, "Start with Windows");
        var natural = MeasureSingle(new Checkbox(new Bindable<bool>(true, _ => { }), "Start with Windows"));
        var narrow = MeasureSingle(new Checkbox(new Bindable<bool>(true, _ => { }), "Start with Windows").Width(110));
        await Assert.That(narrow.Height).IsEqualTo(natural.Height).Within(0.01f);
    }

    [Test]
    public async Task RadioButton_NarrowerThanItsLabel_KeepsItInside()
    {
        // The painter used to give the label at least 200px, whatever the radio's width.
        var runs = Paint(() => new Row(children: [new RadioButton<string>("a", "Pick this one, please").Width(100)]));

        await AssertRunsInside(runs, "Pick this one, please");
    }

    [Test]
    public async Task SegmentedControl_NarrowerThanItsLabels_KeepsEachInside()
    {
        var runs = Paint(() => new Row(children:
        [
            new SegmentedControl<string>(new Bindable<string>("recent", _ => { }), Segments).Width(260),
        ]));

        await AssertRunsInside(runs, "Most recent first", "Pinned on top", "Largest payload");
    }

    [Test]
    public async Task Button_WithRoom_DrawsItsWholeLabel()
    {
        var runs = Paint(() => new Row(children: [new Button("Rename clip", () => { })]));

        await AssertRunsInside(runs, "Rename clip");
        await Assert.That(runs.Single(r => r.Text is not null).Text).IsEqualTo("Rename clip");
    }

    // ── Fixture ──────────────────────────────────────────────────────

    private static readonly Icon PinIcon = new("M12 17v5M9 3h6l-1 7 4 4H6l4-4z", new Size(24, 24), 24f, "Pin");

    private static readonly SelectOption<string>[] TypeOptions =
    [
        new("all", "All Types and Formats"),
        new("text", "Text"),
    ];

    private static readonly SegmentOption<string>[] Segments =
    [
        new("recent", "Most recent first"),
        new("pinned", "Pinned on top"),
        new("size", "Largest payload"),
    ];

    private const float WindowWidth = 560f;
    private const float WindowHeight = 400f;

    private static Func<Node> content = () => Node.Empty;

    /// <summary>A glyph run as painted: its text (null when no candidate matched), and the control it sits in.</summary>
    private sealed record PaintedRun(string? Text, float Left, float Right, Rect ControlBounds, string Label);

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    private static Size MeasureSingle(Node node)
    {
        var engine = new LayoutEngine();
        engine.Layout(new Row(children: [node]), LayoutConstraints.Tight(new Size(WindowWidth, WindowHeight)));
        return node.LayoutData.MeasuredSize;
    }

    /// <summary>
    /// Mounts <paramref name="factory"/>'s tree, paints one frame into a recording backend and
    /// returns every glyph run, matched to the text-bearing control it falls in.
    /// </summary>
    private static List<PaintedRun> Paint(Func<Node> factory)
    {
        content = factory;
        using var backend = new EtchBackend();
        using var orchestrator = new FrameOrchestrator(() => { }, () => { })
        {
            Theme = new AppleTheme(ThemeMode.Dark),
            RenderBackend = backend,
        };
        orchestrator.BeginFrameCallback = () =>
        {
            backend.Reset();
            return (1UL, (uint)WindowWidth, (uint)WindowHeight);
        };

        orchestrator.MountRoot<Host>(WindowWidth, WindowHeight);
        orchestrator.Tick();

        // The fonts this frame measured and painted with (Inter shares glyph ids across weights, so
        // the first candidate whose glyphs match must be the one actually used).
        string[] fonts = [.. new[] { LayoutSolver.DefaultFontPath, LayoutSolver.SemiBoldFontPath }.OfType<string>(), .. CandidateFonts()];

        var controls = new List<(Rect Bounds, string[] Labels)>();
        CollectControls(orchestrator.Input.MainRoot!, 0f, 0f, controls);

        var runs = new List<PaintedRun>();
        foreach (var op in backend.GlyphCommands)
        {
            float firstPen = op.Positions[0];
            var owner = controls.FirstOrDefault(c => op.Positions[1] >= c.Bounds.Y && op.Positions[1] <= c.Bounds.Bottom
                && firstPen >= c.Bounds.X - 40f && firstPen <= c.Bounds.Right);
            string[] labels = owner.Labels ?? [];
            (string? text, float width, float firstGlyphX) = MatchRun(op, labels, fonts);
            float left = firstPen - firstGlyphX;
            runs.Add(new PaintedRun(text, left, left + width, owner.Bounds, string.Join("|", labels)));
        }

        return runs;
    }

    private static void CollectControls(Node node, float x, float y, List<(Rect, string[])> into)
    {
        var b = node.LayoutData.Bounds;
        float ax = x + b.X;
        float ay = y + b.Y;
        string[]? labels = node switch
        {
            Button btn => [btn.Label.Resolve()],
            LinkButton lb => [lb.Label.Resolve()],
            Toggle tog => [tog.Label.Resolve()],
            Checkbox cb => [cb.Label.Resolve()],
            IRadioButton rb => [rb.LabelText],
            ISelectNode => ["All Types and Formats", "Text"],
            ISegmentedControl sc => Enumerable.Range(0, sc.SegmentCount).Select(sc.GetSegmentLabel).ToArray(),
            _ => null,
        };
        if (labels is not null)
        {
            into.Add((new Rect(ax, ay, b.Width, b.Height), labels));
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            CollectControls(child, ax, ay, into);
        }
    }

    /// <summary>
    /// Finds which text the run draws — a label in full, or a prefix of one followed by "…" — by
    /// shaping candidates in the fonts the frame can have used, and returns its laid-out width.
    /// </summary>
    private static (string? Text, float Width, float FirstGlyphX) MatchRun(EtchBackend.GlyphOp op, string[] labels, string[] fonts)
    {
        foreach (string font in fonts)
        {
            foreach (string label in labels)
            {
                for (int k = label.Length; k >= 0; k--)
                {
                    string candidate = k == label.Length ? label : label[..k] + Ellipsis;
                    var layout = TextLayoutEngine.Layout(candidate, new TextLayoutOptions
                    {
                        FontPath = font,
                        FontSize = op.FontSize,
                        MaxLines = 1,
                        NoWrap = true,
                    });
                    if (layout.Lines.Count == 0)
                    {
                        continue;
                    }

                    var glyphs = layout.Lines[0].Glyphs;
                    if (glyphs.Count != op.GlyphIds.Length)
                    {
                        continue;
                    }

                    bool same = true;
                    for (int g = 0; g < glyphs.Count; g++)
                    {
                        if (glyphs[g].GlyphId != op.GlyphIds[g])
                        {
                            same = false;
                            break;
                        }
                    }

                    if (same)
                    {
                        return (candidate, layout.Lines[0].Width, glyphs[0].X);
                    }
                }
            }
        }

        return (null, 0f, 0f);
    }

    private static IEnumerable<string> CandidateFonts()
    {
        string fonts = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts");
        if (Directory.Exists(fonts))
        {
            foreach (string file in Directory.EnumerateFiles(fonts, "*.ttf"))
            {
                yield return file;
            }
        }

        if (FontFallback.FindFallbackFont('A') is { } system)
        {
            yield return system;
        }
    }

    private static Task AssertRunsInside(List<PaintedRun> runs, params string[] labels)
    {
        return AssertRunsInside(runs, 0f, labels);
    }

    private static async Task AssertRunsInside(List<PaintedRun> runs, float insetRight, params string[] labels)
    {
        var mine = runs.Where(r => labels.Any(l => r.Label.Contains(l, StringComparison.Ordinal))).ToList();
        await Assert.That(mine.Count).IsGreaterThan(0).Because("the control must paint its label");
        foreach (var run in mine)
        {
            await Assert.That(run.Text).IsNotNull().Because($"a run in [{run.Label}] must be its label or a prefix of it plus an ellipsis");
            await Assert.That(run.Left).IsGreaterThanOrEqualTo(run.ControlBounds.X - 0.5f)
                .Because($"\"{run.Text}\" starts left of its control");
            await Assert.That(run.Right).IsLessThanOrEqualTo(run.ControlBounds.Right - insetRight + 0.5f)
                .Because($"\"{run.Text}\" ends at {run.Right}, past its control's {run.ControlBounds.Right - insetRight}");
        }
    }
}

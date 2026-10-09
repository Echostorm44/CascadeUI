using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// SegmentedControl and ToggleGroup sized their segments in three places (measure, paint, click hit
/// test) from a character-count estimate, and the toggle group's hit test used 16px of padding where
/// its measure and paint used 24 — so a click just inside a painted segment could select its
/// neighbour. All three now use <see cref="SegmentLayout"/>. These click on either side of a painted
/// boundary in a real frame.
/// </summary>
[NotInParallel(nameof(SegmentGeometryTests))]
public class SegmentGeometryTests
{
    private static readonly ToggleOption<string>[] ToggleOptions =
    [
        new("a", "A"),
        new("long", "A much longer option label"),
    ];

    private static readonly SegmentOption<string>[] SegmentOptions =
    [
        new("a", "A"),
        new("long", "A much longer segment label"),
    ];

    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    [Test]
    public async Task ToggleGroup_ClickJustEitherSideOfAPaintedBoundary_SelectsThatButton()
    {
        string value = "a";
        var group = new ToggleGroup<string>(new Bindable<string>(value, v => { value = v; }), ToggleOptions);

        using var frame = new Frame(group);
        float boundary = frame.Boundary(SegmentLayout.ToggleGroupPaddingH, ((IToggleGroup)group).GetOptionLabel(0), ((IToggleGroup)group).GetOptionLabel(1));
        var dispatcher = frame.Orchestrator.Input;

        Click(dispatcher, boundary + 3f);
        await Assert.That(value).IsEqualTo("long");
        Click(dispatcher, boundary - 3f);
        await Assert.That(value).IsEqualTo("a");
    }

    [Test]
    public async Task SegmentedControl_ClickJustEitherSideOfAPaintedBoundary_SelectsThatSegment()
    {
        string value = "a";
        var control = new SegmentedControl<string>(new Bindable<string>(value, v => { value = v; }), SegmentOptions);

        using var frame = new Frame(control);
        float boundary = frame.Boundary(SegmentLayout.SegmentedPaddingH, ((ISegmentedControl)control).GetSegmentLabel(0), ((ISegmentedControl)control).GetSegmentLabel(1));
        var dispatcher = frame.Orchestrator.Input;

        Click(dispatcher, boundary + 3f);
        await Assert.That(value).IsEqualTo("long");
        Click(dispatcher, boundary - 3f);
        await Assert.That(value).IsEqualTo("a");
    }

    /// <summary>
    /// The control painted in a real frame, which records its absolute bounds for the click hit
    /// test; <see cref="Boundary"/> is the x of the line between its two segments as painted.
    /// </summary>
    private sealed class Frame : IDisposable
    {
        private readonly EtchBackend backend = new();
        private readonly Node control;

        public Frame(Node control)
        {
            this.control = control;
            content = () => new Column(children: [control]);
            Orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                Theme = new AppleTheme(ThemeMode.Dark),
                RenderBackend = backend,
            };
            Orchestrator.BeginFrameCallback = () =>
            {
                backend.Reset();
                return (1UL, 600u, 200u);
            };
            Orchestrator.MountRoot<Host>(600f, 200f);
            Orchestrator.Tick();
        }

        public FrameOrchestrator Orchestrator { get; }

        public float Boundary(float paddingH, string first, string second)
        {
            Span<float> widths = [SegmentLayout.NaturalWidth(first, paddingH), SegmentLayout.NaturalWidth(second, paddingH)];
            SegmentLayout.ScaleToFit(widths, control.LayoutData.Bounds.Width);
            return control.LayoutData.Bounds.X + widths[0];
        }

        public void Dispose()
        {
            Orchestrator.Dispose();
            backend.Dispose();
        }
    }

    private static void Click(InputDispatcher dispatcher, float x)
    {
        const float y = 10f;
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseMove });
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
    }
}

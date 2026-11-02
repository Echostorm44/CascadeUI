using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <see cref="Label.Selectable"/>: the modifier used to be stored and ignored. A press and drag
/// selects text, a double-click a word, a triple-click everything, a press elsewhere clears it,
/// the selection survives a re-render and is drawn under the text.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class LabelSelectionTests
{
    private const string Text = "Hello world again";
    private const uint Width = 600;
    private const uint Height = 120;

    private static bool selectable = true;
    private static int renders;

    private sealed class Host : Component
    {
        public void Refresh() => Invalidate();

        protected override Node Render()
        {
            renders++;
            return new Column(children:
            [
                new Label(Text).FontSize(30).Selectable(selectable).Color(ColorValue.FromRgba(1f, 1f, 1f)),
                new Label("elsewhere").FontSize(30),
            ]);
        }
    }

    [Test]
    public async Task PressAndDrag_SelectsTheTextBetween()
    {
        using var app = new App();
        app.Press(app.XAtOffset(0), 1);
        app.Move(app.XAtOffset(5));
        app.Release(app.XAtOffset(5));

        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo("Hello");
    }

    [Test]
    public async Task DragBackwards_SelectsTheSameText()
    {
        using var app = new App();
        app.Press(app.XAtOffset(11), 1);
        app.Move(app.XAtOffset(6));
        app.Release(app.XAtOffset(6));

        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo("world");
    }

    [Test]
    public async Task DoubleClick_SelectsAWord_TripleClick_SelectsEverything()
    {
        using var app = new App();
        float insideWorld = (app.XAtOffset(7) + app.XAtOffset(8)) / 2f;

        app.Press(insideWorld, 2);
        app.Release(insideWorld);
        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo("world");

        app.Press(insideWorld, 3);
        app.Release(insideWorld);
        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo(Text);
    }

    [Test]
    public async Task PressElsewhere_ClearsTheSelection()
    {
        using var app = new App();
        app.Press(app.XAtOffset(0), 3);
        app.Release(app.XAtOffset(0));
        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo(Text);

        app.PressAt(10, 100); // the second, unselectable label
        app.ReleaseAt(10, 100);
        await Assert.That(InputDispatcher.SelectedLabelText()).IsNull();
    }

    [Test]
    public async Task Selection_SurvivesARerender()
    {
        using var app = new App();
        app.Press(app.XAtOffset(0), 1);
        app.Move(app.XAtOffset(5));
        app.Release(app.XAtOffset(5));
        int before = renders;

        app.Rerender();

        await Assert.That(renders).IsGreaterThan(before);
        await Assert.That(InputDispatcher.SelectedLabelText()).IsEqualTo("Hello");
    }

    [Test]
    public async Task Selection_IsHighlightedUnderTheText()
    {
        using var app = new App();
        int y = (int)app.TextMiddleY;
        // A gap between the words: background before selecting, highlight after.
        int gapX = (int)((app.XAtOffset(5) + app.XAtOffset(6)) / 2f);
        byte[] plain = app.Pixels();

        app.Press(app.XAtOffset(0), 3);
        app.Release(app.XAtOffset(0));
        byte[] selected = app.Pixels();

        int i = (y * (int)Width + gapX) * 4;
        int difference = Math.Abs(plain[i] - selected[i]) + Math.Abs(plain[i + 1] - selected[i + 1]) + Math.Abs(plain[i + 2] - selected[i + 2]);
        await Assert.That(difference).IsGreaterThan(30);
    }

    [Test]
    public async Task UnselectableLabel_IsNotSelected()
    {
        selectable = false;
        try
        {
            using var app = new App();
            app.Press(app.XAtOffset(0), 3);
            app.Release(app.XAtOffset(0));
            await Assert.That(InputDispatcher.SelectedLabelText()).IsNull();
        }
        finally
        {
            selectable = true;
        }
    }

    /// <summary>A mounted, rendered frame with input dispatch.</summary>
    private sealed class App : IDisposable
    {
        private readonly EtchBackendProvider provider = new();
        private readonly FrameOrchestrator orchestrator;
        private readonly Label label;

        public App()
        {
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
            label = FindLabel();
        }

        private Label FindLabel()
        {
            var host = (Host)orchestrator.RootHost!.Component;
            var column = (Column)host.RenderedTree!;
            return (Label)column.Children[0];
        }

        private (TextLayoutResult Layout, float X, float Y) Drawn()
        {
            var current = FindLabel();
            var bounds = current.LayoutData.Bounds;
            return NodePainter.LabelTextLayout(current, new Rect(0, 0, bounds.Width, bounds.Height),
                ThemeSwitcher.Current.Typography.Scale.Body, LayoutSolver.DefaultFontPath)!.Value;
        }

        /// <summary>The window x of the caret before character <paramref name="offset"/>.</summary>
        public float XAtOffset(int offset)
        {
            var drawn = Drawn();
            return FindLabel().LayoutData.Bounds.X + drawn.X + drawn.Layout.GetCaretInfo(offset).X;
        }

        public float TextMiddleY
        {
            get
            {
                var drawn = Drawn();
                var line = drawn.Layout.Lines[0];
                return FindLabel().LayoutData.Bounds.Y + drawn.Y + line.Y + line.Height / 2f;
            }
        }

        public void Press(float x, int clickCount) => Send(NativeMouseEventType.MouseDown, x, TextMiddleY, clickCount);

        public void Move(float x) => Send(NativeMouseEventType.MouseMove, x, TextMiddleY, 1);

        public void Release(float x) => Send(NativeMouseEventType.MouseUp, x, TextMiddleY, 1);

        public void PressAt(float x, float y) => Send(NativeMouseEventType.MouseDown, x, y, 1);

        public void ReleaseAt(float x, float y) => Send(NativeMouseEventType.MouseUp, x, y, 1);

        private void Send(NativeMouseEventType type, float x, float y, int clickCount)
        {
            orchestrator.Input.HandleMouseEvent(new NativeMouseEvent
            {
                X = x,
                Y = y,
                Type = type,
                Button = NativeMouseButton.Left,
                ClickCount = clickCount,
            });
            orchestrator.Tick();
        }

        public void Rerender()
        {
            ((Host)orchestrator.RootHost!.Component).Refresh();
            orchestrator.Tick();
        }

        public byte[] Pixels()
        {
            ((Host)orchestrator.RootHost!.Component).Refresh();
            orchestrator.Tick();
            return provider.CpuRenderer!.CaptureFrame()!.Pixels;
        }

        public void Dispose()
        {
            orchestrator.Dispose();
            provider.Dispose();
            ThemeSwitcher.Reset();
            label.GetHashCode();
        }
    }
}

// CA2000: a mounted component is owned and disposed by the host that mounts it.
#pragma warning disable CA2000
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Core;

/// <summary>
/// A component's <c>Bounds</c>, <c>OnBoundsChanged</c> and <see cref="ContainerQuery"/> all depend on
/// the frame telling each component where layout put it. <c>ComponentHost.UpdateBounds</c> was
/// unit-tested on its own while nothing in the frame ever called it, so every component's bounds
/// stayed empty and a ContainerQuery always built for a zero-sized container. These run the real
/// frame: render, layout, publish, paint.
/// </summary>
[NotInParallel(["FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ComponentBoundsTests
{
    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    /// <summary>A component that remembers what it was told about its own bounds.</summary>
    private sealed class Probe(Func<Node> body) : Component
    {
        public readonly List<(Rect Previous, Rect Current)> Changes = [];

        public Rect CurrentBounds => Bounds;

        protected override Node Render()
        {
            return body();
        }

        protected override void OnBoundsChanged(Rect previousBounds, Rect currentBounds)
        {
            Changes.Add((previousBounds, currentBounds));
        }
    }

    [Test]
    public async Task ANestedComponent_GetsItsWindowBounds()
    {
        var probe = new Probe(() => new Column(children: []).Size(100, 50));
        content = () => new Column(
            spacing: 10,
            crossAxisAlignment: CrossAxisAlignment.Start,
            children:
            [
                new Column(children: []).Size(80, 30),
                probe,
            ]).Padding(EdgeInsets.All(20));

        using var frame = new Frame(400, 300);

        // Inside the padding (20), below the 30-high box and the 10 spacing: (20, 60), 100 x 50.
        await Assert.That(probe.CurrentBounds).IsEqualTo(new Rect(20, 60, 100, 50));
    }

    [Test]
    public async Task AResize_RaisesOnBoundsChanged_WithTheNewSize()
    {
        var probe = new Probe(() => new Column(children: []));
        content = () => new Row(children: [probe.Expand()]);

        using var frame = new Frame(400, 300);
        await Assert.That(probe.CurrentBounds.Width).IsEqualTo(400f);
        await Assert.That(probe.Changes.Count).IsEqualTo(0).Because("the first bounds are not a change");

        frame.Resize(640, 300);

        await Assert.That(probe.CurrentBounds.Width).IsEqualTo(640f);
        await Assert.That(probe.Changes.Count).IsEqualTo(1);
        await Assert.That(probe.Changes[0].Previous.Width).IsEqualTo(400f);
        await Assert.That(probe.Changes[0].Current.Width).IsEqualTo(640f);
    }

    [Test]
    public async Task AContainerQuery_BuildsForItsLaidOutSize_AndAgainAfterAResize()
    {
        var widths = new List<float>();
        content = () => new Column(
            children:
            [
                new ContainerQuery(info =>
                {
                    widths.Add(info.Width);
                    return new Label($"{info.Width}");
                }).Expand(),
            ]).Padding(EdgeInsets.All(10));

        using var frame = new Frame(400, 300);
        frame.Tick();

        // Built first with nothing known, then for the 380 the padding leaves.
        await Assert.That(widths[^1]).IsEqualTo(380f);

        frame.Resize(600, 300);
        frame.Tick();

        await Assert.That(widths[^1]).IsEqualTo(580f);

        // Settled: a frame with nothing changed does not build it again.
        int builds = widths.Count;
        frame.Tick();
        await Assert.That(widths.Count).IsEqualTo(builds);
    }

    /// <summary>A headless window: a real orchestrator painting into a recording backend.</summary>
    private sealed class Frame : IDisposable
    {
        private readonly EtchBackend backend = new();
        private readonly FrameOrchestrator orchestrator;
        private uint width;
        private uint height;

        public Frame(uint width, uint height)
        {
            this.width = width;
            this.height = height;
            orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                Theme = new AppleTheme(ThemeMode.Dark),
                RenderBackend = backend,
            };
            orchestrator.BeginFrameCallback = () =>
            {
                backend.Reset();
                return (1UL, this.width, this.height);
            };
            orchestrator.MountRoot<Host>(width, height);
            orchestrator.Tick();
        }

        public void Tick() => orchestrator.Tick();

        public void Resize(uint newWidth, uint newHeight)
        {
            width = newWidth;
            height = newHeight;
            orchestrator.HandleResize(newWidth, newHeight);
            orchestrator.Tick();
        }

        public void Dispose()
        {
            orchestrator.Dispose();
            backend.Dispose();
        }
    }
}

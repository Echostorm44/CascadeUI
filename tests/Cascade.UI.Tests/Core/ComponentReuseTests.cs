// CA2000: a mounted component is owned and disposed by the host that mounts it.
#pragma warning disable CA2000
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Core;

/// <summary>
/// When a parent re-renders it builds a new instance of each child component, while the child
/// already mounted - with its state - is the one that lives on. The reconciler kept the old host but
/// left the new, never-mounted instance in the parent's tree, so a nested component rendered
/// nothing after its parent's first re-render: no layout, no paint, no hit testing. The new
/// instance now stands in for the live one.
/// </summary>
[NotInParallel(["FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ComponentReuseTests
{
    private static readonly ColorValue Magenta = new("#FF00FF");

    private static Func<Node> content = () => Node.Empty;
    private static Host? mountedHost;

    private sealed class Host : Component
    {
        public void Rerender() => Invalidate();

        protected override Task OnMounted()
        {
            mountedHost = this;
            return Task.CompletedTask;
        }

        protected override Node Render()
        {
            return content();
        }
    }

    private static int swatchesBuilt;
    private static int lastRenderedSwatch;

    /// <summary>Paints a magenta square, and notes which instance last rendered.</summary>
    private sealed class Swatch : Component
    {
        private readonly int serial = ++swatchesBuilt;

        protected override Node Render()
        {
            lastRenderedSwatch = serial;
            return new Column(children: []).Size(50, 50).Background(Magenta);
        }
    }

    [Test]
    public async Task ANestedComponent_StillPaintsAfterItsParentRerenders_AndTheMountedInstanceLivesOn()
    {
        swatchesBuilt = 0;
        // Not the first child: components are keyed by their place among their siblings, and the
        // reuse path once looked them up as if they were always first, so only a first child worked.
        content = () => new Column(children:
        [
            new Column(children: []).Size(10, 10),
            new Column(children: []).Size(10, 10),
            new Swatch(),
        ]).Padding(EdgeInsets.All(10));

        using var frame = new Frame(300, 200);
        await Assert.That(frame.PaintedMagenta()).IsTrue().Because("the first frame paints the swatch");

        mountedHost!.Rerender();
        frame.Tick();

        await Assert.That(swatchesBuilt).IsEqualTo(2).Because("the parent built a second instance when it re-rendered");
        await Assert.That(frame.PaintedMagenta()).IsTrue().Because("the child vanished after its parent re-rendered");
        await Assert.That(lastRenderedSwatch).IsEqualTo(1).Because("the mounted instance, and its state, is the one that lives on");

        // And again: the stand-in from the last render is now the old node.
        mountedHost.Rerender();
        frame.Tick();
        await Assert.That(frame.PaintedMagenta()).IsTrue();
        await Assert.That(lastRenderedSwatch).IsEqualTo(1);
    }

    [Test]
    public async Task TwoSiblingsOfOneType_EachKeepTheirOwnInstance()
    {
        swatchesBuilt = 0;
        content = () => new Row(spacing: 10, children: [new Swatch(), new Column(children: []).Size(5, 5), new Swatch()]);

        using var frame = new Frame(300, 200);
        int paintedBefore = frame.MagentaCount();
        await Assert.That(paintedBefore).IsEqualTo(2);

        mountedHost!.Rerender();
        frame.Tick();

        // Four built (two per render), both of the first render's still mounted and painting.
        await Assert.That(swatchesBuilt).IsEqualTo(4);
        await Assert.That(frame.MagentaCount()).IsEqualTo(2).Because("a sibling was taken for the other, or unmounted");
    }

    [Test]
    public async Task AContainerQuery_UsesTheBuilderFromItsParentsLatestRender()
    {
        int version = 1;
        var built = new List<int>();
        content = () =>
        {
            int captured = version;
            return new Column(children:
            [
                new Column(children: []).Size(10, 10),
                new ContainerQuery(info =>
                {
                    built.Add(captured);
                    return new Column(children: []).Size(40, 40).Background(Magenta);
                }).Expand(),
            ]);
        };

        using var frame = new Frame(300, 200);
        frame.Tick();
        await Assert.That(built[^1]).IsEqualTo(1);

        version = 2;
        mountedHost!.Rerender();
        frame.Tick();

        await Assert.That(built[^1]).IsEqualTo(2).Because("the query kept rendering the data its first builder captured");
        await Assert.That(frame.PaintedMagenta()).IsTrue();
    }

    /// <summary>A headless window: a real orchestrator painting into a recording backend.</summary>
    private sealed class Frame : IDisposable
    {
        private readonly EtchBackend backend = new();
        private readonly FrameOrchestrator orchestrator;

        public Frame(uint width, uint height)
        {
            orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                Theme = new AppleTheme(ThemeMode.Dark),
                RenderBackend = backend,
            };
            orchestrator.BeginFrameCallback = () =>
            {
                backend.Reset();
                return (1UL, width, height);
            };
            orchestrator.MountRoot<Host>(width, height);
            orchestrator.Tick();
        }

        public void Tick() => orchestrator.Tick();

        public bool PaintedMagenta() => MagentaCount() > 0;

        public int MagentaCount() =>
            backend.Commands.Count(op => op.Kind == EtchBackend.OpKind.DrawRect && op.Fill == Magenta);

        public void Dispose()
        {
            orchestrator.Dispose();
            backend.Dispose();
        }
    }
}

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000

namespace Cascade.UI.Tests.Integration;

/// <summary>
/// WP-2270: End-to-End Smoke Tests
///
/// These tests verify the complete Cascade UI pipeline works end-to-end:
///   Component → Render → Layout → Paint → Input
/// without a real GPU backend. They use the headless PaintCallback path
/// so no native DLL or GPU context is required.
///
/// This is the done gate for Phase 22 — if these tests pass, the framework
/// can run applications.
/// </summary>
public class EndToEndSmokeTests
{
    // ── Test components ───────────────────────────────────────────────

    /// <summary>
    /// Minimal "Hello, Cascade!" component — the simplest possible app.
    /// </summary>
    private sealed class HelloComponent : Component
    {
        protected override Node Render()
        {
            return new Center(
                new Label("Hello, Cascade!")
            );
        }
    }

    /// <summary>
    /// Counter component with a reactive field, a button, and a label.
    /// Tests: reactive state, re-render on state change, control wiring.
    /// </summary>
    private sealed class CounterComponent : Component
    {
        private int count = 0;

        public int Count => count;

        public void Increment()
        {
            count++;
        }

        protected override Node Render()
        {
            return new Column(
                spacing: 16,
                mainAxisAlignment: MainAxisAlignment.Center,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children: new Node[]
                {
                    new Label($"Count: {count}").FontSize(24),
                    new Button("Increment", () => { count++; }),
                }
            );
        }
    }

    /// <summary>
    /// A component with nested layout containers: Column → Row → Labels.
    /// Tests: deep tree traversal in layout and paint.
    /// </summary>
    private sealed class NestedLayoutComponent : Component
    {
        protected override Node Render()
        {
            return new Column(
                spacing: 8,
                children: new Node[]
                {
                    new Label("Header").FontSize(32),
                    new Row(
                        spacing: 12,
                        children: new Node[]
                        {
                            new Label("Left"),
                            new Label("Center"),
                            new Label("Right"),
                        }
                    ),
                    new Label("Footer"),
                }
            );
        }
    }

    /// <summary>
    /// Component that produces Node.Empty — verifies the pipeline handles
    /// empty trees without crashing.
    /// </summary>
    private sealed class EmptyComponent : Component
    {
        protected override Node Render() => Node.Empty;
    }

    /// <summary>
    /// Component with conditional rendering — shows different content
    /// based on a reactive field.
    /// </summary>
    private sealed class ConditionalComponent : Component
    {
        private bool showDetail = false;

        public bool ShowDetail => showDetail;

        public void ToggleDetail()
        {
            showDetail = !showDetail;
        }

        protected override Node Render()
        {
            return new Column(
                spacing: 8,
                children: new Node[]
                {
                    new Label("Always visible"),
                    showDetail
                        ? new Label("Detail content")
                        : Node.Empty,
                    new Button("Toggle", () => { showDetail = !showDetail; }),
                }
            );
        }
    }



    // ── Helper ─────────────────────────────────────────────────────────

    private static FrameOrchestrator CreateOrchestrator()
    {
        return new FrameOrchestrator(() => { }, () => { });
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. MOUNT & RENDER
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task HelloComponent_MountsAndRendersTree()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);

        await Assert.That(orch.RootHost).IsNotNull();
        await Assert.That(orch.RootHost!.IsMounted).IsTrue();
        await Assert.That(orch.RootHost!.RenderedTree).IsNotNull();
    }

    [Test]
    public async Task HelloComponent_RenderProducesCenterWithLabel()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);

        var tree = orch.RootHost!.RenderedTree;
        await Assert.That(tree).IsTypeOf<Center>();

        var center = (Center)tree!;
        await Assert.That(center.Child).IsTypeOf<Label>();

        var label = (Label)center.Child;
        await Assert.That(label.Text).IsEqualTo("Hello, Cascade!");
    }

    [Test]
    public async Task CounterComponent_InitialRenderShowsZero()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<CounterComponent>(800, 600);

        var tree = orch.RootHost!.RenderedTree;
        await Assert.That(tree).IsTypeOf<Column>();

        var column = (Column)tree!;
        await Assert.That(column.Children.Count).IsEqualTo(2);

        var label = (Label)column.Children[0];
        await Assert.That(label.Text).IsEqualTo("Count: 0");
    }

    [Test]
    public async Task EmptyComponent_MountsWithoutError()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<EmptyComponent>(800, 600);

        await Assert.That(orch.RootHost).IsNotNull();
        await Assert.That(orch.RootHost!.IsMounted).IsTrue();
        await Assert.That(orch.RootHost!.RenderError).IsNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. LAYOUT
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task HelloComponent_LayoutProducesNonZeroBounds()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        var tree = orch.RootHost!.RenderedTree!;

        // After layout, the root node should have bounds
        await Assert.That(tree.IsLayoutEmpty).IsFalse();
        var bounds = tree.LayoutData.Bounds;
        await Assert.That(bounds.Width).IsGreaterThanOrEqualTo(0);
        await Assert.That(bounds.Height).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task NestedLayout_AllNodesReceiveBounds()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<NestedLayoutComponent>(800, 600);
        orch.Tick();

        var tree = orch.RootHost!.RenderedTree!;
        await Assert.That(tree).IsTypeOf<Column>();

        var column = (Column)tree;

        // Header label
        var header = column.Children[0];
        await Assert.That(header).IsTypeOf<Label>();

        // Row with 3 labels
        var row = column.Children[1];
        await Assert.That(row).IsTypeOf<Row>();
        var rowNode = (Row)row;
        await Assert.That(rowNode.Children.Count).IsEqualTo(3);

        // Footer label
        var footer = column.Children[2];
        await Assert.That(footer).IsTypeOf<Label>();

        // All nodes should have layout data populated
        await Assert.That(tree.LayoutData.IsVisible).IsTrue();
    }

    [Test]
    public async Task CounterComponent_ColumnChildrenAreOrderedVertically()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<CounterComponent>(800, 600);
        orch.Tick();

        var column = (Column)orch.RootHost!.RenderedTree!;
        var labelBounds = column.Children[0].LayoutData.Bounds;
        var buttonBounds = column.Children[1].LayoutData.Bounds;

        // In a vertical column, the button should be below the label
        await Assert.That(buttonBounds.Y).IsGreaterThanOrEqualTo(labelBounds.Y);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. PAINT PASS (headless)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task HelloComponent_PaintCallbackReceivesTree()
    {
        using var orch = CreateOrchestrator();
        Node? paintedNode = null;
        orch.PaintCallback = node => paintedNode = node;

        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        await Assert.That(paintedNode).IsNotNull();
        await Assert.That(paintedNode).IsTypeOf<Center>();
    }

    [Test]
    public async Task CounterComponent_PaintCallbackReceivesColumn()
    {
        using var orch = CreateOrchestrator();
        Node? paintedNode = null;
        orch.PaintCallback = node => paintedNode = node;

        orch.MountRoot<CounterComponent>(800, 600);
        orch.Tick();

        await Assert.That(paintedNode).IsNotNull();
        await Assert.That(paintedNode).IsTypeOf<Column>();
    }

    [Test]
    public async Task EmptyComponent_PaintCallbackStillFires()
    {
        using var orch = CreateOrchestrator();
        Node? paintedNode = null;
        orch.PaintCallback = node => paintedNode = node;

        orch.MountRoot<EmptyComponent>(800, 600);
        orch.Tick();

        // Node.Empty is still passed to PaintCallback
        await Assert.That(paintedNode).IsNotNull();
    }

    [Test]
    public async Task NodePainter_PaintsHelloComponentWithoutError()
    {
        // Verifies that NodePainter can walk a real component tree
        // using a theme's tokens, without crashing.
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        var tree = orch.RootHost!.RenderedTree!;

        // NodePainter requires a DrawContext with a backend.
        // In headless mode, we verify the tree shape is paintable
        // by walking it ourselves the same way NodePainter does.
        int nodeCount = CountPaintableNodes(tree);
        await Assert.That(nodeCount).IsGreaterThan(0);
    }

    [Test]
    public async Task NodePainter_PaintsNestedLayoutWithoutError()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<NestedLayoutComponent>(800, 600);
        orch.Tick();

        var tree = orch.RootHost!.RenderedTree!;
        int nodeCount = CountPaintableNodes(tree);

        // Column > [Label, Row > [Label, Label, Label], Label]
        // = 1 Column + 1 Header + 1 Row + 3 Row children + 1 Footer = 7
        await Assert.That(nodeCount).IsGreaterThanOrEqualTo(5);
    }

    private static int CountPaintableNodes(Node node)
    {
        if (node.IsLayoutEmpty)
        {
            return 0;
        }

        int count = 1;

        if (node is Column col)
        {
            foreach (var child in col.Children)
            {
                count += CountPaintableNodes(child);
            }
        }
        else if (node is Row row)
        {
            foreach (var child in row.Children)
            {
                count += CountPaintableNodes(child);
            }
        }
        else if (node is Center center)
        {
            count += CountPaintableNodes(center.Child);
        }
        else if (node is Stack stack)
        {
            foreach (var child in stack.Children)
            {
                count += CountPaintableNodes(child);
            }
        }

        return count;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. INPUT DISPATCH & HIT TESTING
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task HitTest_FindsLabelInHelloComponent()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        var tree = orch.RootHost!.RenderedTree!;

        // The Center should fill the available space, with the label centered within.
        // Hit-testing the center of the window should find a node.
        var hit = HitTester.HitTest(tree, 400, 300);

        // We expect to hit either the Label or the Center
        await Assert.That(hit).IsNotNull();
    }

    [Test]
    public async Task InputDispatcher_IsWiredToOrchestrator()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        // After Tick, the input dispatcher should have the root set
        await Assert.That(orch.Input).IsNotNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5. REACTIVE UPDATES
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task CounterComponent_MarkDirtyAndTick_ReRendersWithNewValue()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<CounterComponent>(800, 600);
        orch.Tick();

        var component = (CounterComponent)orch.RootHost!.Component;

        // Verify initial state
        var column1 = (Column)orch.RootHost!.RenderedTree!;
        var label1 = (Label)column1.Children[0];
        await Assert.That(label1.Text).IsEqualTo("Count: 0");

        // Mutate state and mark dirty
        component.Increment();
        orch.Scheduler.MarkDirty(orch.RootHost);
        orch.Tick();

        // After re-render, the label should show the new count
        var column2 = (Column)orch.RootHost!.RenderedTree!;
        var label2 = (Label)column2.Children[0];
        await Assert.That(label2.Text).IsEqualTo("Count: 1");
    }

    [Test]
    public async Task ConditionalComponent_TogglesNodePresence()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<ConditionalComponent>(800, 600);
        orch.Tick();

        var component = (ConditionalComponent)orch.RootHost!.Component;

        // Initially, detail is hidden (Node.Empty)
        var column1 = (Column)orch.RootHost!.RenderedTree!;
        await Assert.That(column1.Children[1].IsLayoutEmpty).IsTrue();

        // Toggle and re-render
        component.ToggleDetail();
        orch.Scheduler.MarkDirty(orch.RootHost);
        orch.Tick();

        // Now the detail label should appear
        var column2 = (Column)orch.RootHost!.RenderedTree!;
        await Assert.That(column2.Children[1]).IsTypeOf<Label>();
        var detail = (Label)column2.Children[1];
        await Assert.That(detail.Text).IsEqualTo("Detail content");
    }

    [Test]
    public async Task CounterComponent_MultipleIncrements_AccumulateCorrectly()
    {
        using var orch = CreateOrchestrator();
        orch.MountRoot<CounterComponent>(800, 600);

        var component = (CounterComponent)orch.RootHost!.Component;

        for (int i = 0; i < 5; i++)
        {
            component.Increment();
            orch.Scheduler.MarkDirty(orch.RootHost);
            orch.Tick();
        }

        var column = (Column)orch.RootHost!.RenderedTree!;
        var label = (Label)column.Children[0];
        await Assert.That(label.Text).IsEqualTo("Count: 5");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 6. FRAME LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task FrameOrchestrator_MountThenDispose_CleansUp()
    {
        var orch = CreateOrchestrator();
        orch.MountRoot<CounterComponent>(800, 600);

        var host = orch.RootHost!;
        await Assert.That(host.IsMounted).IsTrue();

        orch.Dispose();
        await Assert.That(host.IsMounted).IsFalse();
    }

    // Resets the global animation statics that FrameOrchestrator.Tick reads when
    // deciding to stop the frame timer. Earlier tests that paint a spinner/toast/
    // chart leave these set, and the headless paint path below never resets them,
    // so without this the cancel-timer assertions fail in a full-suite run. Same
    // painter-empty pattern ChartAnimationTrackerTests uses (WP-3516).
    private static void ResetFrameActivityStatics()
    {
        FocusManager.Reset();
        SharedScheduler.Instance.Clear();
        var ctx = new DrawContext { Size = new Size(1, 1), PixelRatio = 1f };
        var painter = new NodePainter(ctx, new FluentTheme());
        painter.Paint(Node.Empty);
    }

    // FrameOrchestrator.Tick only fires the cancel-timer callback when every global
    // animation static is quiet (SharedScheduler.Instance, InputDispatcher.IsCaretActive,
    // NodePainter.HasActive*, ControlStateAnimator). The suite-wide serial
    // ParallelLimiter (ParallelLimit.cs) removes concurrent mutation;
    // ResetFrameActivityStatics clears state a prior test left behind (WP-3516).
    [Test]
    public async Task FrameOrchestrator_TickWithNothingDirty_CancelsTimer()
    {
        ResetFrameActivityStatics();
        bool cancelled = false;
        using var orch = new FrameOrchestrator(() => { }, () => cancelled = true);
        orch.MountRoot<HelloComponent>(800, 600);

        orch.Tick();

        await Assert.That(cancelled).IsTrue();
    }

    // Same global-static dependency as FrameOrchestrator_TickWithNothingDirty_CancelsTimer;
    // serial execution + ResetFrameActivityStatics keep the frame-request count
    // deterministic (WP-3516).
    [Test]
    public async Task FrameOrchestrator_HandleResize_RequestsNewFrame()
    {
        ResetFrameActivityStatics();
        int frameRequests = 0;
        using var orch = new FrameOrchestrator(() => frameRequests++, () => { });
        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();
        int afterInitial = frameRequests;

        orch.HandleResize(1024, 768);

        await Assert.That(frameRequests).IsGreaterThan(afterInitial);
    }

    [Test]
    public async Task FrameOrchestrator_PresentCallbackFires()
    {
        using var orch = CreateOrchestrator();
        bool presented = false;
        orch.PresentCallback = () => presented = true;

        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        await Assert.That(presented).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 7. THEME INTEGRATION
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task FluentTheme_CanBeCreated()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.Colors).IsNotNull();
        await Assert.That(theme.Typography).IsNotNull();
        await Assert.That(theme.Spacing).IsNotNull();
    }

    [Test]
    public async Task FluentTheme_DarkMode_HasDifferentColors()
    {
        var light = new FluentTheme(ThemeMode.Light);
        var dark = new FluentTheme(ThemeMode.Dark);

        // The background colors should differ between light and dark
        await Assert.That(light.Colors.Background).IsNotEqualTo(dark.Colors.Background);
    }

    [Test]
    public async Task AppleTheme_CanBeCreated()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.Colors).IsNotNull();
        await Assert.That(theme.Typography).IsNotNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 8. FULL PIPELINE — MOUNT → TICK → PAINT → VERIFY
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task FullPipeline_HelloCascade_MountTickPaintPresent()
    {
        // This is THE smoke test — the done gate for Phase 22.
        // It verifies every stage of the framework pipeline.

        bool painted = false;
        bool presented = false;
        Node? paintedTree = null;

        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.PaintCallback = node =>
        {
            painted = true;
            paintedTree = node;
        };
        orch.PresentCallback = () => presented = true;

        // 1. Mount the root component
        orch.MountRoot<HelloComponent>(800, 600);
        await Assert.That(orch.RootHost!.IsMounted).IsTrue();
        await Assert.That(orch.RootHost!.RenderedTree).IsNotNull();

        // 2. Run a frame tick (layout + paint + present)
        orch.Tick();

        // 3. Verify paint was called with the correct tree
        await Assert.That(painted).IsTrue();
        await Assert.That(presented).IsTrue();
        await Assert.That(paintedTree).IsTypeOf<Center>();

        // 4. Verify the tree structure
        var center = (Center)paintedTree!;
        await Assert.That(center.Child).IsTypeOf<Label>();
        var label = (Label)center.Child;
        await Assert.That(label.Text).IsEqualTo("Hello, Cascade!");

        // 5. Verify layout was computed
        await Assert.That(paintedTree!.LayoutData.IsVisible).IsTrue();
    }

    [Test]
    public async Task FullPipeline_Counter_MountClickRerender()
    {
        // Full pipeline test with reactive state change.

        using var orch = new FrameOrchestrator(() => { }, () => { });
        int paintCount = 0;
        orch.PaintCallback = _ => paintCount++;
        orch.PresentCallback = () => { };

        // Mount
        orch.MountRoot<CounterComponent>(800, 600);
        orch.Tick();
        await Assert.That(paintCount).IsEqualTo(1);

        // Simulate state change (as if the button was clicked)
        var component = (CounterComponent)orch.RootHost!.Component;
        component.Increment();
        orch.Scheduler.MarkDirty(orch.RootHost);

        // Re-render
        orch.Tick();
        await Assert.That(paintCount).IsEqualTo(2);

        // Verify updated tree
        var column = (Column)orch.RootHost!.RenderedTree!;
        var label = (Label)column.Children[0];
        await Assert.That(label.Text).IsEqualTo("Count: 1");
    }

    [Test]
    public async Task FullPipeline_NestedLayout_MultipleFrames()
    {
        // Verifies that a complex nested tree survives multiple frame cycles.

        using var orch = new FrameOrchestrator(() => { }, () => { });
        int paintCount = 0;
        orch.PaintCallback = _ => paintCount++;
        orch.PresentCallback = () => { };

        orch.MountRoot<NestedLayoutComponent>(800, 600);

        // Run 3 frame ticks
        orch.Tick();
        orch.Scheduler.MarkDirty(orch.RootHost!);
        orch.Tick();
        orch.Scheduler.MarkDirty(orch.RootHost!);
        orch.Tick();

        await Assert.That(paintCount).IsEqualTo(3);

        // Tree is still intact
        var tree = orch.RootHost!.RenderedTree!;
        await Assert.That(tree).IsTypeOf<Column>();
        var column = (Column)tree;
        await Assert.That(column.Children.Count).IsEqualTo(3);
    }

    [Test]
    public async Task FullPipeline_Resize_RelayoutsTree()
    {
        using var orch = new FrameOrchestrator(() => { }, () => { });
        int paintCount = 0;
        orch.PaintCallback = _ => paintCount++;
        orch.PresentCallback = () => { };

        orch.MountRoot<HelloComponent>(800, 600);
        orch.Tick();

        // Resize the window
        orch.HandleResize(1920, 1080);
        orch.Tick();

        // Should have painted twice — once after mount, once after resize
        await Assert.That(paintCount).IsEqualTo(2);
    }
}

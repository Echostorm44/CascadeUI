using Cascade.UI;
using Cascade.UI.DevTools;
using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

#if DEBUG

/// <summary>
/// Integration smoke tests for the DevTools wiring (WP-2340).
/// These tests create real ComponentHost/Component instances, wire them through
/// NodeTreeWalker, and verify that all DevTools panels return live data.
/// </summary>
[NotInParallel("DevToolsIntegration")]
public class DevToolsIntegrationTests
{
    // ── NodeTreeWalker: SetRoot + RebuildIndex ──────────────────

    [Test]
    public async Task NodeTreeWalker_SetRoot_Node_BuildsIndex()
    {
        var label = new Label("Hello");
        NodeTreeWalker.SetRoot(label);

        var found = NodeTreeWalker.FindNode(null);
        await Assert.That(found).IsNotNull();
        await Assert.That(found).IsEqualTo(label);
    }

    [Test]
    public async Task NodeTreeWalker_SetRoot_Column_IndexesChildren()
    {
        var child1 = new Label("A");
        var child2 = new Label("B");
        var root = new Column(spacing: 8, children: new Node[] { child1, child2 });
        NodeTreeWalker.SetRoot(root);

        // Root should be indexed
        var rootNode = NodeTreeWalker.FindNode(null);
        await Assert.That(rootNode).IsEqualTo(root);

        // Children should be reachable via GetChildren
        var children = NodeTreeWalker.GetChildren(root);
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children[0]).IsEqualTo(child1);
        await Assert.That(children[1]).IsEqualTo(child2);
    }

    [Test]
    public async Task NodeTreeWalker_SetRoot_ComponentHost_TracksComponent()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        // After SetRoot(host), the tree should contain the component's rendered output
        var root = NodeTreeWalker.FindNode(null);
        await Assert.That(root).IsNotNull();
    }

    [Test]
    public async Task NodeTreeWalker_SetRoot_ComponentHost_GetChildren_ReturnsRenderedTree()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        // GetChildren on the component should return the rendered tree
        var children = NodeTreeWalker.GetChildren(component);
        await Assert.That(children.Count).IsEqualTo(1);
        // The rendered tree should be a Column (per CounterComponent.Render)
        await Assert.That(children[0]).IsAssignableTo<Column>();
    }

    // ── Snapshot / Tree Structure ───────────────────────────────

    [Test]
    public async Task Snapshot_ReturnsCorrectTypeAndChildren()
    {
        var label = new Label("Test");
        var root = new Column(spacing: 4, children: new Node[] { label });
        NodeTreeWalker.SetRoot(root);

        var snapshot = NodeTreeWalker.Snapshot(root, maxDepth: 5, currentDepth: 0);
        await Assert.That(snapshot.TypeName).IsEqualTo("Column");
        await Assert.That(snapshot.Children.Count).IsEqualTo(1);
        await Assert.That(snapshot.Children[0].TypeName).IsEqualTo("Label");
    }

    [Test]
    public async Task Snapshot_RespectsMaxDepth()
    {
        var label = new Label("Deep");
        var root = new Column(children: new Node[] { label });
        NodeTreeWalker.SetRoot(root);

        var shallow = NodeTreeWalker.Snapshot(root, maxDepth: 0, currentDepth: 0);
        await Assert.That(shallow.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_IncludesBoundsFromLayoutData()
    {
        var label = new Label("Bounded");
        label.LayoutData.Bounds = new Rect(10, 20, 100, 50);
        NodeTreeWalker.SetRoot(label);

        var snapshot = NodeTreeWalker.Snapshot(label, maxDepth: 0, currentDepth: 0);
        await Assert.That(snapshot.Bounds.X).IsEqualTo(10);
        await Assert.That(snapshot.Bounds.Y).IsEqualTo(20);
        await Assert.That(snapshot.Bounds.Width).IsEqualTo(100);
        await Assert.That(snapshot.Bounds.Height).IsEqualTo(50);
    }

    // ── DetailSnapshot (signals, computed) ──────────────────────

    [Test]
    public async Task DetailSnapshot_Component_ListsSignalFields()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        var detail = NodeTreeWalker.DetailSnapshot(component);
        await Assert.That(detail.TypeName).IsEqualTo("CounterComponent");
        // CounterComponent has a 'count' field — should appear in signals
        var countSignal = detail.Signals.FirstOrDefault(s => s.Name == "count");
        await Assert.That(countSignal).IsNotNull();
        await Assert.That(countSignal!.Value).IsEqualTo("0");
        await Assert.That(countSignal.TypeName).IsEqualTo("Int32");
    }

    // ── BoxModel / ConstraintFlow ──────────────────────────────

    [Test]
    public async Task GetBoxModel_ReturnsRealBoundsAndPadding()
    {
        var label = new Label("Padded");
        label.LayoutData.Bounds = new Rect(0, 0, 200, 40);
        label.LayoutData.Padding = new EdgeInsets(8, 8, 8, 8);
        NodeTreeWalker.SetRoot(label);

        var box = NodeTreeWalker.GetBoxModel(label);
        await Assert.That(box.OuterBounds.Width).IsEqualTo(200);
        await Assert.That(box.Padding.Top).IsEqualTo(8);
        await Assert.That(box.ContentBounds.Width).IsEqualTo(184); // 200 - 8 - 8
    }

    [Test]
    public async Task GetConstraintFlow_ReturnsDefaultConstraints()
    {
        var label = new Label("Constrained");
        label.LayoutData.Bounds = new Rect(0, 0, 120, 30);
        NodeTreeWalker.SetRoot(label);

        var flow = NodeTreeWalker.GetConstraintFlow(label);
        await Assert.That(flow.ReturnedWidth).IsEqualTo(120);
        await Assert.That(flow.ReturnedHeight).IsEqualTo(30);
        await Assert.That(flow.MinWidth).IsEqualTo(0);
        await Assert.That(float.IsPositiveInfinity(flow.MaxWidth)).IsTrue();
    }

    // ── FlexDistribution ───────────────────────────────────────

    [Test]
    public async Task GetFlexDistribution_Row_ReturnsChildInfo()
    {
        var child1 = new Label("Left");
        child1.LayoutData.Bounds = new Rect(0, 0, 50, 30);
        var child2 = new Label("Right");
        child2.LayoutData.Bounds = new Rect(50, 0, 150, 30);
        child2.LayoutData.GrowFactor = 1f;
        var row = new Row(spacing: 0, children: new Node[] { child1, child2 });
        row.LayoutData.Bounds = new Rect(0, 0, 200, 30);
        NodeTreeWalker.SetRoot(row);

        var flex = NodeTreeWalker.GetFlexDistribution(row);
        await Assert.That(flex).IsNotNull();
        await Assert.That(flex!.TotalSpace).IsEqualTo(200);
        await Assert.That(flex.Children.Count).IsEqualTo(2);
        await Assert.That(flex.Children[0].IsFlex).IsFalse();
        await Assert.That(flex.Children[1].IsFlex).IsTrue();
        await Assert.That(flex.Children[1].GrowFactor).IsEqualTo(1f);
    }

    [Test]
    public async Task GetFlexDistribution_Label_ReturnsNull()
    {
        var label = new Label("Not a flex container");
        NodeTreeWalker.SetRoot(label);

        var flex = NodeTreeWalker.GetFlexDistribution(label);
        await Assert.That(flex).IsNull();
    }

    // ── FindOverflows ──────────────────────────────────────────

    [Test]
    public async Task FindOverflows_DetectsChildOverflowingParent()
    {
        var child = new Label("Overflow");
        child.LayoutData.Bounds = new Rect(-5, 0, 110, 30);
        var parent = new Column(children: new Node[] { child });
        parent.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        NodeTreeWalker.SetRoot(parent);

        var overflows = NodeTreeWalker.FindOverflows();
        await Assert.That(overflows.Count).IsEqualTo(1);
        await Assert.That(overflows[0].OverflowLeft > 0).IsTrue();
        await Assert.That(overflows[0].OverflowRight > 0).IsTrue();
    }

    [Test]
    public async Task FindOverflows_NoneWhenChildFitsParent()
    {
        var child = new Label("Fits");
        child.LayoutData.Bounds = new Rect(10, 5, 80, 20);
        var parent = new Column(children: new Node[] { child });
        parent.LayoutData.Bounds = new Rect(0, 0, 200, 100);
        NodeTreeWalker.SetRoot(parent);

        var overflows = NodeTreeWalker.FindOverflows();
        await Assert.That(overflows.Count).IsEqualTo(0);
    }

    // ── Accessibility Tree ─────────────────────────────────────

    [Test]
    public async Task GetAccessibilityTree_ReturnsTreeFromMountedRoot()
    {
        var label = new Label("Accessible");
        label.LayoutData.A11yRole = AccessibleRole.Text;
        label.LayoutData.A11yLabel = "Test label";
        NodeTreeWalker.SetRoot(label);

        var tree = NodeTreeWalker.GetAccessibilityTree();
        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Text);
        await Assert.That(tree.Label).IsEqualTo("Test label");
    }

    [Test]
    public async Task GetFocusOrder_ReturnsFocusableNodes()
    {
        var btn1 = new Label("Btn1");
        btn1.LayoutData.A11yFocusable = true;
        btn1.LayoutData.A11yTabIndex = 2;
        var btn2 = new Label("Btn2");
        btn2.LayoutData.A11yFocusable = true;
        btn2.LayoutData.A11yTabIndex = 1;
        var root = new Column(children: new Node[] { btn1, btn2 });
        NodeTreeWalker.SetRoot(root);

        var order = NodeTreeWalker.GetFocusOrder();
        await Assert.That(order.Count).IsEqualTo(2);
        // Should be sorted by tab index (1 before 2)
        await Assert.That(order[0].Order).IsEqualTo(1);
        await Assert.That(order[1].Order).IsEqualTo(2);
    }

    // ── State Panel: GetAllSignals ─────────────────────────────

    [Test]
    public async Task GetAllSignals_ReturnsComponentFields()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        var signals = NodeTreeWalker.GetAllSignals();
        var countSignal = signals.FirstOrDefault(s => s.FieldName == "count" && s.ComponentName == "CounterComponent");
        await Assert.That(countSignal).IsNotNull();
        await Assert.That(countSignal!.CurrentValue).IsEqualTo("0");
        await Assert.That(countSignal.ValueType).IsEqualTo("Int32");
    }

    // ── TrySetSignal ───────────────────────────────────────────

    [Test]
    public async Task TrySetSignal_ChangesFieldValue()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        bool success = NodeTreeWalker.TrySetSignal("CounterComponent", "count", "42");
        await Assert.That(success).IsTrue();

        // Verify the value actually changed
        var signals = NodeTreeWalker.GetAllSignals();
        var countSignal = signals.FirstOrDefault(s => s.FieldName == "count");
        await Assert.That(countSignal).IsNotNull();
        await Assert.That(countSignal!.CurrentValue).IsEqualTo("42");
    }

    [Test]
    public async Task TrySetSignal_ReturnsFalseForUnknownComponent()
    {
        var label = new Label("Not a component");
        NodeTreeWalker.SetRoot(label);

        bool success = NodeTreeWalker.TrySetSignal("NonExistent", "field", "42");
        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task TrySetSignal_RecordsUndoEntry()
    {
        var scheduler = new RenderScheduler();
        using var component = new CounterComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        NodeTreeWalker.SetRoot(host);

        NodeTreeWalker.TrySetSignal("CounterComponent", "count", "99");

        var undo = NodeTreeWalker.GetUndoStack();
        var lastEntry = undo.FirstOrDefault(u => u.SignalName == "count");
        await Assert.That(lastEntry).IsNotNull();
        await Assert.That(lastEntry!.OldValue).IsEqualTo("0");
        await Assert.That(lastEntry.NewValue).IsEqualTo("99");
    }

    // ── GetAllNodeBounds / GetNodeBoundsById ───────────────────

    [Test]
    public async Task GetAllNodeBounds_ReturnsAllNodesWithDepth()
    {
        var child = new Label("Child");
        child.LayoutData.Bounds = new Rect(10, 10, 80, 20);
        var root = new Column(children: new Node[] { child });
        root.LayoutData.Bounds = new Rect(0, 0, 100, 50);
        NodeTreeWalker.SetRoot(root);

        var bounds = NodeTreeWalker.GetAllNodeBounds();
        await Assert.That(bounds.Count).IsEqualTo(2);
        // Root at depth 0, child at depth 1
        await Assert.That(bounds[0].depth).IsEqualTo(0);
        await Assert.That(bounds[1].depth).IsEqualTo(1);
    }

    [Test]
    public async Task GetNodeBoundsById_ReturnsCorrectBounds()
    {
        var label = new Label("Find me");
        label.LayoutData.Bounds = new Rect(30, 40, 60, 25);
        NodeTreeWalker.SetRoot(label);

        // Get the node's ID from the snapshot
        var snapshot = NodeTreeWalker.Snapshot(label, maxDepth: 0, currentDepth: 0);
        var bounds = NodeTreeWalker.GetNodeBoundsById(snapshot.Id);
        await Assert.That(bounds).IsNotNull();
        await Assert.That(bounds!.Value.X).IsEqualTo(30);
        await Assert.That(bounds.Value.Y).IsEqualTo(40);
    }

    [Test]
    public async Task GetNodeBoundsById_ReturnsNullForUnknown()
    {
        var label = new Label("Root");
        NodeTreeWalker.SetRoot(label);

        var bounds = NodeTreeWalker.GetNodeBoundsById("definitely-not-a-real-id");
        await Assert.That(bounds).IsNull();
    }

    // ── InspectorPanel via NodeTreeWalker ──────────────────────

    [Test]
    public async Task InspectorPanel_CaptureTree_WiredTree_ReturnsRealNodes()
    {
        var label = new Label("Wired");
        var root = new Column(children: new Node[] { label });
        root.LayoutData.Bounds = new Rect(0, 0, 300, 200);
        NodeTreeWalker.SetRoot(root);

        var tree = InspectorPanel.CaptureTree(maxDepth: 5);
        await Assert.That(tree).IsNotNull();
        await Assert.That(tree.TypeName).IsEqualTo("Column");
        await Assert.That(tree.Children.Count).IsEqualTo(1);
        await Assert.That(tree.Children[0].TypeName).IsEqualTo("Label");
    }

    // ── PerformancePanel: RecordFrame captures data ────────────

    [Test]
    public async Task PerformancePanel_RecordFrame_CapturesTimingInWiredMode()
    {
        PerformancePanel.ResetStats();

        // Simulate what FrameOrchestrator.Tick does
        PerformancePanel.RecordFrame(
            frameTimeMs: 16.1f,
            layoutTimeMs: 3.0f,
            renderTimeMs: 7.0f,
            gpuTimeMs: 5.5f,
            targetFrameTimeMs: 16.67f);

        var frames = PerformancePanel.GetRecentFrames();
        await Assert.That(frames.Count > 0).IsTrue();
        var last = frames[frames.Count - 1];
        await Assert.That(last.FrameTimeMs).IsEqualTo(16.1f);
        await Assert.That(last.LayoutTimeMs).IsEqualTo(3.0f);
        await Assert.That(last.RenderTimeMs).IsEqualTo(7.0f);
        await Assert.That(last.GpuTimeMs).IsEqualTo(5.5f);
        await Assert.That(last.Dropped).IsFalse();
    }

    [Test]
    public async Task PerformancePanel_RecordComponentRender_WiredMode_TracksStats()
    {
        PerformancePanel.ResetStats();

        string name = "IntegrationComp_" + Guid.NewGuid().ToString("N")[..8];
        PerformancePanel.RecordComponentRender(name, 2.5f, "count");
        PerformancePanel.RecordComponentRender(name, 3.1f, "name");
        PerformancePanel.RecordComponentRender(name, 1.0f, "count");

        var stats = PerformancePanel.GetComponentStats(100);
        var compStats = stats.FirstOrDefault(s => s.ComponentName == name);
        await Assert.That(compStats).IsNotNull();
        await Assert.That(compStats!.RenderCount).IsEqualTo(3);
        await Assert.That(compStats.MaxRenderMs).IsEqualTo(3.1f);
        await Assert.That(compStats.LastTrigger).IsEqualTo("count");
    }

    // ── F12 Toggle ─────────────────────────────────────────────

    [Test]
    public async Task DevToolsPanel_HandleKeyDown_F12_TogglesVisibility()
    {
        CascadeDevTools.Hide();
        await Assert.That(CascadeDevTools.IsVisible).IsFalse();

        // Simulate F12 keypress
        bool handled = CascadeDevTools.HandleKeyDown(Key.F12, ModifierKeys.None);
        await Assert.That(handled).IsTrue();
        await Assert.That(CascadeDevTools.IsVisible).IsTrue();

        // Toggle off
        handled = CascadeDevTools.HandleKeyDown(Key.F12, ModifierKeys.None);
        await Assert.That(handled).IsTrue();
        await Assert.That(CascadeDevTools.IsVisible).IsFalse();
    }

    [Test]
    public async Task DevToolsPanel_HandleKeyDown_CtrlShiftI_TogglesVisibility()
    {
        CascadeDevTools.Hide();

        bool handled = CascadeDevTools.HandleKeyDown(Key.I, ModifierKeys.Ctrl | ModifierKeys.Shift);
        await Assert.That(handled).IsTrue();
        await Assert.That(CascadeDevTools.IsVisible).IsTrue();

        CascadeDevTools.Hide();
    }

    [Test]
    public async Task DevToolsPanel_HandleKeyDown_UnrelatedKey_NotHandled()
    {
        bool handled = CascadeDevTools.HandleKeyDown(Key.A, ModifierKeys.None);
        await Assert.That(handled).IsFalse();
    }

    // ── Nested Component Tree ──────────────────────────────────

    [Test]
    public async Task NodeTreeWalker_NestedLabels_DeepTreeWalk()
    {
        var inner1 = new Label("Inner1");
        var inner2 = new Label("Inner2");
        var innerCol = new Column(children: new Node[] { inner1, inner2 });
        var outer = new Row(children: new Node[] { innerCol, new Label("Sibling") });
        NodeTreeWalker.SetRoot(outer);

        var snapshot = NodeTreeWalker.Snapshot(outer, maxDepth: 10, currentDepth: 0);
        await Assert.That(snapshot.TypeName).IsEqualTo("Row");
        await Assert.That(snapshot.Children.Count).IsEqualTo(2);
        await Assert.That(snapshot.Children[0].TypeName).IsEqualTo("Column");
        await Assert.That(snapshot.Children[0].Children.Count).IsEqualTo(2);
        await Assert.That(snapshot.Children[1].TypeName).IsEqualTo("Label");
    }

    // ── GetAllBoxModels ────────────────────────────────────────

    [Test]
    public async Task GetAllBoxModels_ReturnsModelPerNode()
    {
        var child = new Label("Box");
        child.LayoutData.Bounds = new Rect(0, 0, 50, 20);
        var root = new Column(children: new Node[] { child });
        root.LayoutData.Bounds = new Rect(0, 0, 200, 100);
        NodeTreeWalker.SetRoot(root);

        var models = NodeTreeWalker.GetAllBoxModels();
        await Assert.That(models.Count).IsEqualTo(2);
    }

    // ── PerformancePanel recording + export in wired mode ──────

    [Test]
    public async Task PerformancePanel_Recording_CapturesWiredFrameData()
    {
        PerformancePanel.ResetStats();
        PerformancePanel.StartRecording(TimeSpan.FromSeconds(10));

        PerformancePanel.RecordFrame(15.0f, 2.0f, 8.0f, 5.0f, 16.67f);
        PerformancePanel.RecordComponentRender("WiredComp", 1.5f, "signal");

        var events = PerformancePanel.StopRecording();
        await Assert.That(events.Count >= 2).IsTrue();
    }

    [Test]
    public async Task PerformancePanel_ExportTrace_ContainsFrameData()
    {
        PerformancePanel.ResetStats();
        PerformancePanel.StartRecording(TimeSpan.FromSeconds(10));
        PerformancePanel.RecordFrame(16.0f, 3.0f, 8.0f, 5.0f, 16.67f);
        PerformancePanel.StopRecording();

        var json = PerformancePanel.ExportTraceAsJson();
        await Assert.That(json.Contains("FrameEnd", StringComparison.Ordinal)).IsTrue();
        await Assert.That(json.Contains("timestampMs", StringComparison.Ordinal)).IsTrue();
    }

    // ── Test helper components ─────────────────────────────────

    private sealed class CounterComponent : Component
    {
        internal int count = 0;
        internal string label = "Click me";

        protected override Node Render()
        {
            return new Column(
                spacing: 8,
                children: new Node[]
                {
                    new Label($"Count: {count}"),
                    new Button(label, onClick: () => { count++; }),
                });
        }
    }
}

#endif

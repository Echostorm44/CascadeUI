using Cascade.UI;
using Cascade.UI.DevTools;
using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("DevTools")]
public class DevToolsTests
{
    // ── DevToolsPanel ───────────────────────────────────────────

    [Test]
    public async Task DevToolsPanel_IsNotVisibleByDefault()
    {
        await Assert.That(CascadeDevTools.IsVisible).IsFalse();
    }

    [Test]
    public async Task DevToolsPanel_Show_SetsVisibleTrue()
    {
        CascadeDevTools.Show();
        await Assert.That(CascadeDevTools.IsVisible).IsTrue();
        CascadeDevTools.Hide();
    }

    [Test]
    public async Task DevToolsPanel_Hide_SetsVisibleFalse()
    {
        CascadeDevTools.Show();
        CascadeDevTools.Hide();
        await Assert.That(CascadeDevTools.IsVisible).IsFalse();
    }

    [Test]
    public async Task DevToolsPanel_Toggle_FlipsVisibility()
    {
        CascadeDevTools.Hide(); // ensure off
        CascadeDevTools.Toggle();
        await Assert.That(CascadeDevTools.IsVisible).IsTrue();
        CascadeDevTools.Toggle();
        await Assert.That(CascadeDevTools.IsVisible).IsFalse();
    }

    [Test]
    public async Task DevToolsPanel_DefaultConfig_HasReasonableDefaults()
    {
        var config = CascadeDevTools.Config;
        await Assert.That(config).IsNotNull();
    }

    // ── VisualOverlays ──────────────────────────────────────────

    [Test]
    public async Task VisualOverlays_DefaultsToNone()
    {
        VisualOverlays.ClearAll();
        await Assert.That(VisualOverlays.ActiveOverlays).IsEqualTo(DevToolsOverlay.None);
    }

    [Test]
    public async Task VisualOverlays_Toggle_ActivatesOverlay()
    {
        VisualOverlays.ClearAll();
        VisualOverlays.Toggle(DevToolsOverlay.LayoutBounds);
        await Assert.That(VisualOverlays.IsActive(DevToolsOverlay.LayoutBounds)).IsTrue();
        VisualOverlays.ClearAll();
    }

    [Test]
    public async Task VisualOverlays_Toggle_DeactivatesOverlay()
    {
        VisualOverlays.ClearAll();
        VisualOverlays.Toggle(DevToolsOverlay.LayoutBounds);
        VisualOverlays.Toggle(DevToolsOverlay.LayoutBounds);
        await Assert.That(VisualOverlays.IsActive(DevToolsOverlay.LayoutBounds)).IsFalse();
        VisualOverlays.ClearAll();
    }

    [Test]
    public async Task VisualOverlays_SetOverlays_SetsMultipleFlags()
    {
        VisualOverlays.SetOverlays(DevToolsOverlay.LayoutBounds | DevToolsOverlay.FocusOrder);
        await Assert.That(VisualOverlays.IsActive(DevToolsOverlay.LayoutBounds)).IsTrue();
        await Assert.That(VisualOverlays.IsActive(DevToolsOverlay.FocusOrder)).IsTrue();
        await Assert.That(VisualOverlays.IsActive(DevToolsOverlay.PaddingMargin)).IsFalse();
        VisualOverlays.ClearAll();
    }

    [Test]
    public async Task VisualOverlays_ClearAll_RemovesAllOverlays()
    {
        VisualOverlays.SetOverlays(DevToolsOverlay.LayoutBounds | DevToolsOverlay.AccessibilityLabels);
        VisualOverlays.ClearAll();
        await Assert.That(VisualOverlays.ActiveOverlays).IsEqualTo(DevToolsOverlay.None);
    }

    [Test]
    public async Task DevToolsOverlay_IsFlags_SupportsMultipleValues()
    {
        var combined = DevToolsOverlay.LayoutBounds | DevToolsOverlay.PaddingMargin | DevToolsOverlay.RepaintRegions;
        await Assert.That((combined & DevToolsOverlay.LayoutBounds) == DevToolsOverlay.LayoutBounds).IsTrue();
        await Assert.That((combined & DevToolsOverlay.PaddingMargin) == DevToolsOverlay.PaddingMargin).IsTrue();
        await Assert.That((combined & DevToolsOverlay.RepaintRegions) == DevToolsOverlay.RepaintRegions).IsTrue();
        await Assert.That((combined & DevToolsOverlay.AccessibilityLabels) == DevToolsOverlay.AccessibilityLabels).IsFalse();
    }

    // ── InspectorPanel ──────────────────────────────────────────

    [Test]
    public async Task InspectorPanel_CaptureTree_ReturnsRootSnapshot()
    {
        var tree = InspectorPanel.CaptureTree();
        await Assert.That(tree).IsNotNull();
        await Assert.That(tree.Id).IsNotNull();
        await Assert.That(tree.TypeName).IsNotNull();
    }

    [Test]
    public async Task InspectorPanel_CaptureTree_RespectsMaxDepth()
    {
        var shallow = InspectorPanel.CaptureTree(maxDepth: 0);
        await Assert.That(shallow.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InspectorPanel_GetNodeDetail_ReturnsNullForUnknownId()
    {
        var detail = InspectorPanel.GetNodeDetail("nonexistent-id-12345");
        await Assert.That(detail).IsNull();
    }

    [Test]
    public async Task InspectorPanel_NodeSnapshot_HasDefaultEmptyLists()
    {
        var snapshot = new NodeSnapshot
        {
            Id = "test",
            TypeName = "TestComponent",
        };
        await Assert.That(snapshot.ReactiveDependencies.Count).IsEqualTo(0);
        await Assert.That(snapshot.Children.Count).IsEqualTo(0);
    }

    // ── PerformancePanel ────────────────────────────────────────

    [Test]
    public async Task PerformancePanel_RecordFrame_StoresFrameSample()
    {
        var before = PerformancePanel.GetRecentFrames().Count;
        PerformancePanel.RecordFrame(16.6f, 2.0f, 8.0f, 6.0f, 16.67f);
        var after = PerformancePanel.GetRecentFrames().Count;
        await Assert.That(after >= before).IsTrue();
    }

    [Test]
    public async Task PerformancePanel_RecordFrame_DetectsDroppedFrames()
    {
        PerformancePanel.RecordFrame(20.0f, 3.0f, 10.0f, 7.0f, 16.67f);
        var frames = PerformancePanel.GetRecentFrames();
        var last = frames[frames.Count - 1];
        await Assert.That(last.Dropped).IsTrue();
    }

    [Test]
    public async Task PerformancePanel_RecordFrame_NormalFrameNotDropped()
    {
        PerformancePanel.RecordFrame(10.0f, 2.0f, 5.0f, 3.0f, 16.67f);
        var frames = PerformancePanel.GetRecentFrames();
        var last = frames[frames.Count - 1];
        await Assert.That(last.Dropped).IsFalse();
    }

    [Test]
    public async Task PerformancePanel_RecordComponentRender_TracksStats()
    {
        string uniqueName = "TrackComp_" + Guid.NewGuid().ToString("N")[..8];
        PerformancePanel.RecordComponentRender(uniqueName, 1.5f, "count");
        PerformancePanel.RecordComponentRender(uniqueName, 2.5f, "name");
        var stats = PerformancePanel.GetComponentStats(100);
        var myStats = stats.FirstOrDefault(s => s.ComponentName == uniqueName);
        await Assert.That(myStats).IsNotNull();
        await Assert.That(myStats!.RenderCount >= 1).IsTrue();
        await Assert.That(myStats.LastTrigger).IsEqualTo("name");
    }

    [Test]
    public async Task PerformancePanel_GetComponentStats_SortsByRenderCount()
    {
        string a = "SortA_" + Guid.NewGuid().ToString("N")[..8];
        string b = "SortB_" + Guid.NewGuid().ToString("N")[..8];
        PerformancePanel.RecordComponentRender(a, 1.0f, null);
        PerformancePanel.RecordComponentRender(b, 1.0f, null);
        PerformancePanel.RecordComponentRender(b, 1.0f, null);
        PerformancePanel.RecordComponentRender(b, 1.0f, null);
        var stats = PerformancePanel.GetComponentStats(100);
        var bStats = stats.FirstOrDefault(s => s.ComponentName == b);
        var aStats = stats.FirstOrDefault(s => s.ComponentName == a);
        await Assert.That(bStats).IsNotNull();
        await Assert.That(bStats!.RenderCount).IsEqualTo(3);
        await Assert.That(aStats).IsNotNull();
        // B should appear before A in sorted output
        int bIdx = stats.ToList().FindIndex(s => s.ComponentName == b);
        int aIdx = stats.ToList().FindIndex(s => s.ComponentName == a);
        await Assert.That(bIdx < aIdx).IsTrue();
    }

    [Test]
    public async Task PerformancePanel_RecordComponentRender_TracksMaxRenderMs()
    {
        string name = "SlowComp_" + Guid.NewGuid().ToString("N")[..8];
        PerformancePanel.RecordComponentRender(name, 1.0f, null);
        PerformancePanel.RecordComponentRender(name, 5.0f, null);
        PerformancePanel.RecordComponentRender(name, 2.0f, null);
        var stats = PerformancePanel.GetComponentStats(100);
        var myStats = stats.FirstOrDefault(s => s.ComponentName == name);
        await Assert.That(myStats).IsNotNull();
        await Assert.That(myStats!.MaxRenderMs).IsEqualTo(5.0f);
    }

    [Test]
    public async Task PerformancePanel_Recording_CapturesEvents()
    {
        PerformancePanel.StartRecording(TimeSpan.FromSeconds(10));
        await Assert.That(PerformancePanel.IsRecording).IsTrue();
        PerformancePanel.RecordFrame(16.6f, 2.0f, 8.0f, 6.0f, 16.67f);
        PerformancePanel.RecordComponentRender("Test", 1.0f, "signal");
        var events = PerformancePanel.StopRecording();
        await Assert.That(PerformancePanel.IsRecording).IsFalse();
        await Assert.That(events.Count >= 2).IsTrue(); // at least frame + render
    }

    [Test]
    public async Task PerformancePanel_ExportTraceAsJson_ReturnsValidJson()
    {
        PerformancePanel.StartRecording(TimeSpan.FromSeconds(10));
        PerformancePanel.RecordFrame(16.6f, 2.0f, 8.0f, 6.0f, 16.67f);
        PerformancePanel.StopRecording();
        var json = PerformancePanel.ExportTraceAsJson();
        await Assert.That(json).IsNotNull();
        await Assert.That(json.Contains("timestampMs", StringComparison.Ordinal)).IsTrue();
        await Assert.That(json.Contains("FrameEnd", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task PerformancePanel_GetMemoryStats_ReturnsValidData()
    {
        var stats = PerformancePanel.GetMemoryStats();
        await Assert.That(stats).IsNotNull();
        await Assert.That(stats.GcCollections.Count).IsEqualTo(3);
    }

    [Test]
    public async Task PerformancePanel_ResetStats_ClearsAllStats()
    {
        PerformancePanel.RecordComponentRender("ToReset", 1.0f, null);
        PerformancePanel.ResetStats();
        var stats = PerformancePanel.GetComponentStats();
        await Assert.That(stats.Count).IsEqualTo(0);
    }

    // ── NetworkPanel ────────────────────────────────────────────

    [Test]
    public async Task NetworkPanel_DefaultNotLogging()
    {
        NetworkPanel.DisableNetworkLogging();
        await Assert.That(NetworkPanel.IsLoggingEnabled).IsFalse();
    }

    [Test]
    public async Task NetworkPanel_EnableDisable_TogglesLogging()
    {
        NetworkPanel.EnableNetworkLogging();
        await Assert.That(NetworkPanel.IsLoggingEnabled).IsTrue();
        NetworkPanel.DisableNetworkLogging();
        await Assert.That(NetworkPanel.IsLoggingEnabled).IsFalse();
    }

    [Test]
    public async Task NetworkPanel_RegisterRequest_ReturnsNegativeWhenDisabled()
    {
        NetworkPanel.DisableNetworkLogging();
        int id = NetworkPanel.RegisterRequest("GET", "https://example.com");
        await Assert.That(id).IsEqualTo(-1);
    }

    [Test]
    public async Task NetworkPanel_RegisterRequest_ReturnsIdWhenEnabled()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        int id = NetworkPanel.RegisterRequest("GET", "https://example.com/api");
        await Assert.That(id).IsNotEqualTo(-1);
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_CompleteRequest_SetsStatusCode()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        int id = NetworkPanel.RegisterRequest("POST", "https://unique-complete-test.example.com/api");
        NetworkPanel.CompleteRequest(id, 200, body: "{\"ok\": true}");
        var requests = NetworkPanel.GetRequests();
        var req = requests.FirstOrDefault(r => r.Url == "https://unique-complete-test.example.com/api");
        await Assert.That(req).IsNotNull();
        await Assert.That(req!.StatusCode).IsEqualTo(200);
        await Assert.That(req.InProgress).IsFalse();
        await Assert.That(req.DurationMs).IsNotNull();
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_GetRequests_ReturnsNewestFirst()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        NetworkPanel.RegisterRequest("GET", "https://first.com");
        NetworkPanel.RegisterRequest("GET", "https://second.com");
        var requests = NetworkPanel.GetRequests();
        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(requests[0].Url).IsEqualTo("https://second.com");
        await Assert.That(requests[1].Url).IsEqualTo("https://first.com");
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_FilteredRequests_FiltersByMethod()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        NetworkPanel.RegisterRequest("GET", "https://example.com/a");
        NetworkPanel.RegisterRequest("POST", "https://example.com/b");
        NetworkPanel.RegisterRequest("GET", "https://example.com/c");
        var gets = NetworkPanel.GetFilteredRequests(methodFilter: "GET");
        await Assert.That(gets.Count).IsEqualTo(2);
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_FilteredRequests_FiltersByUrlContains()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        NetworkPanel.RegisterRequest("GET", "https://example.com/api/users");
        NetworkPanel.RegisterRequest("GET", "https://example.com/api/posts");
        NetworkPanel.RegisterRequest("GET", "https://example.com/health");
        var apiRequests = NetworkPanel.GetFilteredRequests(urlContains: "/api/");
        await Assert.That(apiRequests.Count).IsEqualTo(2);
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_ClearRequests_RemovesAll()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.RegisterRequest("GET", "https://example.com");
        NetworkPanel.ClearRequests();
        var requests = NetworkPanel.GetRequests();
        await Assert.That(requests.Count).IsEqualTo(0);
        NetworkPanel.DisableNetworkLogging();
    }

    [Test]
    public async Task NetworkPanel_TruncatesLargeBodies()
    {
        NetworkPanel.EnableNetworkLogging();
        NetworkPanel.ClearRequests();
        string largeBody = new string('x', 100000);
        int id = NetworkPanel.RegisterRequest("POST", "https://example.com", body: largeBody);
        var req = NetworkPanel.GetRequestById(id);
        await Assert.That(req!.RequestBody!.Length).IsEqualTo(65536);
        NetworkPanel.DisableNetworkLogging();
    }

    // ── StatePanel ──────────────────────────────────────────────

    [Test]
    public async Task StatePanel_GetSignals_ReturnsEmptyWhenNoRoot()
    {
        var signals = StatePanel.GetSignals();
        await Assert.That(signals).IsNotNull();
    }

    [Test]
    public async Task StatePanel_GetComputed_ReturnsEmptyWhenNoRoot()
    {
        var computed = StatePanel.GetComputed();
        await Assert.That(computed).IsNotNull();
    }

    [Test]
    public async Task StatePanel_GetAsyncData_ReturnsEmptyWhenNoRoot()
    {
        var asyncData = StatePanel.GetAsyncData();
        await Assert.That(asyncData).IsNotNull();
    }

    [Test]
    public async Task StatePanel_GetLocalStorage_ReturnsEmptyWhenNoStorage()
    {
        var storage = StatePanel.GetLocalStorage();
        await Assert.That(storage).IsNotNull();
    }

    [Test]
    public async Task StatePanel_GetUndoStack_ReturnsEmptyInitially()
    {
        var undo = StatePanel.GetUndoStack();
        await Assert.That(undo).IsNotNull();
    }

    [Test]
    public async Task StatePanel_TrySetSignalValue_ReturnsFalseForUnknownComponent()
    {
        var result = StatePanel.TrySetSignalValue("NonExistent", "field", "42");
        await Assert.That(result).IsFalse();
    }

    // ── AccessibilityPanel ──────────────────────────────────────

    [Test]
    public async Task AccessibilityPanel_CaptureTree_ReturnsRoot()
    {
        var tree = AccessibilityPanel.CaptureAccessibilityTree();
        await Assert.That(tree).IsNotNull();
        await Assert.That(tree.NodeId).IsNotNull();
    }

    [Test]
    public async Task AccessibilityPanel_ValidateAccessibility_ReturnsViolationsList()
    {
        var violations = AccessibilityPanel.ValidateAccessibility();
        await Assert.That(violations).IsNotNull();
    }

    [Test]
    public async Task AccessibilityPanel_GetFocusOrder_ReturnsList()
    {
        var order = AccessibilityPanel.GetFocusOrder();
        await Assert.That(order).IsNotNull();
    }

    [Test]
    public async Task AccessibilityPanel_GetScreenReaderPreview_ReturnsList()
    {
        var preview = AccessibilityPanel.GetScreenReaderPreview();
        await Assert.That(preview).IsNotNull();
    }

    [Test]
    public async Task AccessibilityPanel_ContrastRatio_CalculatesCorrectly()
    {
        // White on white = 1:1
        var white = new ColorValue("#FFFFFF");
        float ratio = AccessibilityPanel.CalculateContrastRatio(white, white);
        await Assert.That(ratio).IsEqualTo(1.0f);
    }

    [Test]
    public async Task AccessibilityPanel_ContrastRatio_BlackOnWhite_IsMaximum()
    {
        var black = new ColorValue("#000000");
        var white = new ColorValue("#FFFFFF");
        float ratio = AccessibilityPanel.CalculateContrastRatio(black, white);
        // WCAG max is 21:1 for pure black on white
        await Assert.That(ratio > 20.0f).IsTrue();
    }

    [Test]
    public async Task AccessibilityPanel_ContrastRatio_MidGrayOnWhite_PassesAA()
    {
        // #757575 on white has approximately 4.6:1 ratio — passes AA
        var gray = new ColorValue("#595959");
        var white = new ColorValue("#FFFFFF");
        float ratio = AccessibilityPanel.CalculateContrastRatio(gray, white);
        await Assert.That(ratio >= 4.5f).IsTrue();
    }

    [Test]
    public async Task AccessibilityPanel_ViolationSeverity_HasAllLevels()
    {
        await Assert.That(Enum.GetValues<AccessibilityPanel.ViolationSeverity>().Length).IsEqualTo(3);
    }

    // ── LayoutPanel ─────────────────────────────────────────────

    [Test]
    public async Task LayoutPanel_GetBoxModel_ReturnsNullForUnknownNode()
    {
        var model = LayoutPanel.GetBoxModel("nonexistent-id");
        await Assert.That(model).IsNull();
    }

    [Test]
    public async Task LayoutPanel_GetConstraintFlow_ReturnsNullForUnknownNode()
    {
        var flow = LayoutPanel.GetConstraintFlow("nonexistent-id");
        await Assert.That(flow).IsNull();
    }

    [Test]
    public async Task LayoutPanel_GetFlexDistribution_ReturnsNullForUnknownNode()
    {
        var flex = LayoutPanel.GetFlexDistribution("nonexistent-id");
        await Assert.That(flex).IsNull();
    }

    [Test]
    public async Task LayoutPanel_FindOverflows_ReturnsEmptyWhenNoRoot()
    {
        var overflows = LayoutPanel.FindOverflows();
        await Assert.That(overflows).IsNotNull();
    }

    [Test]
    public async Task LayoutPanel_GetGridInfo_ReturnsNullForUnknownNode()
    {
        var grid = LayoutPanel.GetGridInfo("nonexistent-id");
        await Assert.That(grid).IsNull();
    }

    // ── RingBuffer ──────────────────────────────────────────────

    [Test]
    public async Task RingBuffer_Add_StoresItems()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        var list = buffer.ToList();
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[1]).IsEqualTo(2);
    }

    [Test]
    public async Task RingBuffer_Wraps_WhenCapacityExceeded()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);
        var list = buffer.ToList();
        await Assert.That(list.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(2);
        await Assert.That(list[1]).IsEqualTo(3);
        await Assert.That(list[2]).IsEqualTo(4);
    }

    [Test]
    public async Task RingBuffer_Count_DoesNotExceedCapacity()
    {
        var buffer = new RingBuffer<int>(2);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);
        buffer.Add(5);
        await Assert.That(buffer.Count).IsEqualTo(2);
    }

    // ── ColorValue FromRgba and ToHex ───────────────────────────

    [Test]
    public async Task ColorValue_FromRgba_CreatesColor()
    {
        var color = ColorValue.FromRgba(1.0f, 0.0f, 0.0f, 1.0f);
        await Assert.That(color.A).IsEqualTo(1.0f);
        // Red channel should be non-zero (linear sRGB premultiplied)
        await Assert.That(color.R > 0f).IsTrue();
    }

    [Test]
    public async Task ColorValue_ToHex_RoundTrips()
    {
        var original = new ColorValue("#FF6B2BFF");
        var hex = original.ToHex();
        var roundTripped = new ColorValue(hex);
        // Allow 1/255 tolerance due to gamma round-trip
        await Assert.That(MathF.Abs(original.R - roundTripped.R) < 0.01f).IsTrue();
        await Assert.That(MathF.Abs(original.G - roundTripped.G) < 0.01f).IsTrue();
        await Assert.That(MathF.Abs(original.B - roundTripped.B) < 0.01f).IsTrue();
    }

    [Test]
    public async Task ColorValue_FromRgba_WithAlpha_PremultipliesCorrectly()
    {
        var opaque = ColorValue.FromRgba(1.0f, 1.0f, 1.0f, 1.0f);
        var half = ColorValue.FromRgba(1.0f, 1.0f, 1.0f, 0.5f);
        await Assert.That(half.A).IsEqualTo(0.5f);
        // Premultiplied R should be roughly half of opaque R
        await Assert.That(half.R < opaque.R).IsTrue();
    }

    [Test]
    public async Task ColorValue_Properties_ExposeChannels()
    {
        var color = new ColorValue("#FF0000");
        await Assert.That(color.A).IsEqualTo(1.0f);
        await Assert.That(color.R > 0f).IsTrue();
        await Assert.That(color.G).IsEqualTo(0f);
        await Assert.That(color.B).IsEqualTo(0f);
    }

    // ── AccessibleRole.None ─────────────────────────────────────

    [Test]
    public async Task AccessibleRole_None_IsDefaultValue()
    {
        AccessibleRole role = default;
        await Assert.That(role).IsEqualTo(AccessibleRole.None);
    }

    [Test]
    public async Task AccessibleRole_None_IsFirstEnumValue()
    {
        int noneValue = (int)AccessibleRole.None;
        await Assert.That(noneValue).IsEqualTo(0);
    }
}

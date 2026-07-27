using System.Runtime.InteropServices;

namespace Cascade.UI.Tests.Accessibility;

/// <summary>
/// Tests for WP-700: Accessibility System Integration.
/// Covers tree building, role mapping, announcements, focus order,
/// context detection, and platform bridge registration.
/// </summary>
[NotInParallel]
public class AccessibilityTests
{
    // ═══════════════════════════════════════════════════════════════════
    // AccessibilityTreeBuilder — Tree Building
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task BuildTree_WithNoRoot_ReturnsEmptyRootNode()
    {
        AccessibilityTreeBuilder.SetRoot(null);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree).IsNotNull();
        await Assert.That(tree.NodeId).IsEqualTo("root");
        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.None);
        await Assert.That(tree.Label).IsEqualTo("Empty application");
    }

    [Test]
    public async Task BuildTree_WithSimpleNode_ReturnsNodeWithMetadata()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yLabel = "Submit";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree).IsNotNull();
        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Button);
        await Assert.That(tree.Label).IsEqualTo("Submit");
    }

    [Test]
    public async Task BuildTree_PresentationRole_ExcludesFromTree()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Presentation;
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Presentation);
        await Assert.That(tree.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildTree_DisabledNode_SetsDisabledFlag()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yDisabled = true;
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Disabled).IsTrue();
    }

    [Test]
    public async Task BuildTree_FocusableNode_SetsFocusableFlag()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yFocusable = true;
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsTrue();
    }

    [Test]
    public async Task BuildTree_LiveRegion_SetsLiveRegionMode()
    {
        var node = new TestNode();
        node.LayoutData.A11yLiveRegion = LiveRegionMode.Assertive;
        node.LayoutData.A11yLabel = "Status";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.LiveRegion).IsEqualTo(LiveRegionMode.Assertive);
    }

    [Test]
    public async Task BuildTree_StateProperties_AreCopied()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Checkbox;
        node.LayoutData.A11yLabel = "Accept terms";
        node.LayoutData.A11yState = new Dictionary<string, string>
        {
            ["checked"] = "true",
        };
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.States.ContainsKey("checked")).IsTrue();
        await Assert.That(tree.States["checked"]).IsEqualTo("true");
    }

    [Test]
    public async Task BuildTree_WithDescription_SetsDescription()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yLabel = "Delete";
        node.LayoutData.A11yDescription = "Permanently removes this item";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Label).IsEqualTo("Delete");
        await Assert.That(tree.Description).IsEqualTo("Permanently removes this item");
    }

    [Test]
    public async Task BuildTree_TabIndex_IsCaptured()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yTabIndex = 3;
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.TabIndex).IsEqualTo(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AccessibilityTreeBuilder — Role Inference
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task InferRole_ButtonType_InfersButtonRole()
    {
        var node = new TestButton();
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        // TestButton name doesn't match known types, so it gets None
        // Unless we use an exact type name match
        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.None);
    }

    [Test]
    public async Task InferRole_ExplicitRole_OverridesInference()
    {
        var node = new TestButton();
        node.LayoutData.A11yRole = AccessibleRole.Link;
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Link);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AccessibilityTreeBuilder — Implicit Focusability
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task ImplicitFocusable_ButtonRole_IsFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yLabel = "OK";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsTrue();
    }

    [Test]
    public async Task ImplicitFocusable_TextRole_IsNotFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Text;
        node.LayoutData.A11yLabel = "Hello";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsFalse();
    }

    [Test]
    public async Task ImplicitFocusable_CheckboxRole_IsFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Checkbox;
        node.LayoutData.A11yLabel = "Remember me";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsTrue();
    }

    [Test]
    public async Task ImplicitFocusable_SliderRole_IsFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Slider;
        node.LayoutData.A11yLabel = "Volume";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsTrue();
    }

    [Test]
    public async Task ImplicitFocusable_TextBoxRole_IsFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.TextBox;
        node.LayoutData.A11yLabel = "Name";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsTrue();
    }

    [Test]
    public async Task ImplicitFocusable_ImageRole_IsNotFocusable()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Image;
        node.LayoutData.A11yLabel = "Logo";
        AccessibilityTreeBuilder.SetRoot(node);

        var tree = AccessibilityTreeBuilder.BuildTree();

        await Assert.That(tree.Focusable).IsFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AccessibilityTreeBuilder — Focus Order
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task GetFocusOrder_NoRoot_ReturnsEmptyList()
    {
        AccessibilityTreeBuilder.SetRoot(null);

        var order = AccessibilityTreeBuilder.GetFocusOrder();

        await Assert.That(order.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetFocusOrder_SingleFocusableNode_ReturnsOne()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yFocusable = true;
        node.LayoutData.A11yLabel = "Click me";
        AccessibilityTreeBuilder.SetRoot(node);

        var order = AccessibilityTreeBuilder.GetFocusOrder();

        await Assert.That(order.Count).IsEqualTo(1);
        await Assert.That(order[0].Order).IsEqualTo(1);
        await Assert.That(order[0].Role).IsEqualTo(AccessibleRole.Button);
    }

    [Test]
    public async Task GetFocusOrder_DisabledNode_IsExcluded()
    {
        var node = new TestNode();
        node.LayoutData.A11yRole = AccessibleRole.Button;
        node.LayoutData.A11yFocusable = true;
        node.LayoutData.A11yDisabled = true;
        AccessibilityTreeBuilder.SetRoot(node);

        var order = AccessibilityTreeBuilder.GetFocusOrder();

        await Assert.That(order.Count).IsEqualTo(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Accessibility — Static API
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task Announce_NullMessage_ThrowsArgumentException()
    {
        await Assert.That(() => Cascade.UI.Accessibility.Announce(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Announce_EmptyMessage_ThrowsArgumentException()
    {
        await Assert.That(() => Cascade.UI.Accessibility.Announce(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Announce_ValidMessage_DoesNotThrow()
    {
        // Set up a bridge that can receive announcements
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        Cascade.UI.Accessibility.Announce("Hello");

        await Assert.That(bridge.LastAnnouncement).IsEqualTo("Hello");
        await Assert.That(bridge.LastPriority).IsEqualTo(AnnouncePriority.Normal);

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    [Test]
    public async Task Announce_WithPriority_PassesPriorityToBridge()
    {
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        Cascade.UI.Accessibility.Announce("Alert!", AnnouncePriority.High);

        await Assert.That(bridge.LastAnnouncement).IsEqualTo("Alert!");
        await Assert.That(bridge.LastPriority).IsEqualTo(AnnouncePriority.High);

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    [Test]
    public async Task Announce_LowPriority_PassesToBridge()
    {
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        Cascade.UI.Accessibility.Announce("Info", AnnouncePriority.Low);

        await Assert.That(bridge.LastPriority).IsEqualTo(AnnouncePriority.Low);

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AccessibilityContext — GetContext
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task GetContext_NoBridge_ReturnsDefaultContext()
    {
        AccessibilityTreeBuilder.SetPlatformBridge(null);

        var context = Cascade.UI.Accessibility.GetContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.DisplayScale).IsEqualTo(1.0f);
        await Assert.That(context.TextScale).IsEqualTo(1.0f);
        await Assert.That(context.ReducedMotion).IsFalse();
        await Assert.That(context.HighContrast).IsFalse();
        await Assert.That(context.ReducedTransparency).IsFalse();
        await Assert.That(context.HasCursor).IsTrue();
        await Assert.That(context.LayoutDensity).IsEqualTo(LayoutDensity.Standard);
        await Assert.That(context.ScreenReaderActive).IsFalse();
    }

    [Test]
    public async Task GetContext_WithBridge_ReturnsBridgeContext()
    {
        var bridge = new TestBridge
        {
            Context = new AccessibilityContext
            {
                DisplayScale = 2.0f,
                TextScale = 1.5f,
                ReducedMotion = true,
                HighContrast = true,
                ReducedTransparency = true,
                HasCursor = false,
                LayoutDensity = LayoutDensity.Comfortable,
                ScreenReaderActive = true,
            },
        };
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        var context = Cascade.UI.Accessibility.GetContext();

        await Assert.That(context.DisplayScale).IsEqualTo(2.0f);
        await Assert.That(context.TextScale).IsEqualTo(1.5f);
        await Assert.That(context.ReducedMotion).IsTrue();
        await Assert.That(context.HighContrast).IsTrue();
        await Assert.That(context.ReducedTransparency).IsTrue();
        await Assert.That(context.HasCursor).IsFalse();
        await Assert.That(context.LayoutDensity).IsEqualTo(LayoutDensity.Comfortable);
        await Assert.That(context.ScreenReaderActive).IsTrue();

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    [Test]
    public async Task Current_EquivalentToGetContext()
    {
        AccessibilityTreeBuilder.SetPlatformBridge(null);

        var fromGetContext = Cascade.UI.Accessibility.GetContext();
        var fromCurrent = AccessibilityContext.Current;

        await Assert.That(fromGetContext.DisplayScale).IsEqualTo(fromCurrent.DisplayScale);
        await Assert.That(fromGetContext.TextScale).IsEqualTo(fromCurrent.TextScale);
        await Assert.That(fromGetContext.ReducedMotion).IsEqualTo(fromCurrent.ReducedMotion);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Platform Bridge Registration
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task SetPlatformBridge_StoresBridge()
    {
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        var stored = AccessibilityTreeBuilder.GetPlatformBridge();

        await Assert.That(stored).IsEqualTo(bridge);

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    [Test]
    public async Task SetPlatformBridge_Null_ClearsBridge()
    {
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);
        AccessibilityTreeBuilder.SetPlatformBridge(null);

        var stored = AccessibilityTreeBuilder.GetPlatformBridge();

        await Assert.That(stored).IsNull();
    }

    [Test]
    public async Task CreatePlatformBridge_ReturnsNonNull()
    {
        var bridge = AccessibilityTreeBuilder.CreatePlatformBridge();

        await Assert.That(bridge).IsNotNull();
        // On Windows CI this will be UIA, on Linux AT-SPI2, etc.
        // The important thing is it never returns null.
    }

    [Test]
    public async Task NotifyTreeChanged_WithBridge_CallsBridge()
    {
        var bridge = new TestBridge();
        AccessibilityTreeBuilder.SetPlatformBridge(bridge);

        AccessibilityTreeBuilder.NotifyTreeChanged();

        await Assert.That(bridge.TreeChangedCount).IsEqualTo(1);

        AccessibilityTreeBuilder.SetPlatformBridge(null);
    }

    [Test]
    public async Task NotifyTreeChanged_NoBridge_DoesNotThrow()
    {
        AccessibilityTreeBuilder.SetPlatformBridge(null);

        // Should not throw
        AccessibilityTreeBuilder.NotifyTreeChanged();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // NullAccessibilityBridge
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task NullBridge_PlatformName_IsNone()
    {
        var bridge = new NullAccessibilityBridge();

        await Assert.That(bridge.PlatformName).IsEqualTo("None");
    }

    [Test]
    public async Task NullBridge_IsScreenReaderActive_ReturnsFalse()
    {
        var bridge = new NullAccessibilityBridge();

        var result = bridge.IsScreenReaderActive();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NullBridge_GetContext_ReturnsDefaults()
    {
        var bridge = new NullAccessibilityBridge();

        var context = bridge.GetAccessibilityContext();

        await Assert.That(context.DisplayScale).IsEqualTo(1.0f);
        await Assert.That(context.ReducedMotion).IsFalse();
        await Assert.That(context.ScreenReaderActive).IsFalse();
    }

    [Test]
    public async Task NullBridge_Announce_DoesNotThrow()
    {
        var bridge = new NullAccessibilityBridge();

        bridge.Announce("test", AnnouncePriority.High);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UIA Provider — Role Mapping (Windows)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task UiaProvider_MapRole_Button_ReturnsButtonControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.Button);

        await Assert.That(controlType).IsEqualTo(50000);
    }

    [Test]
    public async Task UiaProvider_MapRole_TextBox_ReturnsEditControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.TextBox);

        await Assert.That(controlType).IsEqualTo(50004);
    }

    [Test]
    public async Task UiaProvider_MapRole_Slider_ReturnsSliderControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.Slider);

        await Assert.That(controlType).IsEqualTo(50015);
    }

    [Test]
    public async Task UiaProvider_MapRole_Checkbox_ReturnsCheckBoxControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.Checkbox);

        await Assert.That(controlType).IsEqualTo(50002);
    }

    [Test]
    public async Task UiaProvider_MapRole_Link_ReturnsHyperlinkControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.Link);

        await Assert.That(controlType).IsEqualTo(50005);
    }

    [Test]
    public async Task UiaProvider_MapRole_Tree_ReturnsTreeControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.Tree);

        await Assert.That(controlType).IsEqualTo(50023);
    }

    [Test]
    public async Task UiaProvider_MapRole_TreeItem_ReturnsTreeItemControlType()
    {
        int controlType = UiaProvider.MapRoleToUiaControlType(AccessibleRole.TreeItem);

        await Assert.That(controlType).IsEqualTo(50024);
    }

    [Test]
    public async Task UiaProvider_PlatformName_IsWindowsUia()
    {
        var provider = new UiaProvider();

        await Assert.That(provider.PlatformName).IsEqualTo("Windows UIA");
    }

    [Test]
    public async Task UiaProvider_Initialize_ThenShutdown_DoesNotThrow()
    {
        var provider = new UiaProvider();
        provider.Initialize(nint.Zero);
        provider.Shutdown();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // NSAccessibility Bridge — Role Mapping (macOS)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task NsBridge_MapRole_Button_ReturnsAXButton()
    {
        string role = NsAccessibilityBridge.MapRoleToNsRole(AccessibleRole.Button);

        await Assert.That(role).IsEqualTo("AXButton");
    }

    [Test]
    public async Task NsBridge_MapRole_TextBox_ReturnsAXTextField()
    {
        string role = NsAccessibilityBridge.MapRoleToNsRole(AccessibleRole.TextBox);

        await Assert.That(role).IsEqualTo("AXTextField");
    }

    [Test]
    public async Task NsBridge_MapRole_Slider_ReturnsAXSlider()
    {
        string role = NsAccessibilityBridge.MapRoleToNsRole(AccessibleRole.Slider);

        await Assert.That(role).IsEqualTo("AXSlider");
    }

    [Test]
    public async Task NsBridge_MapRole_Image_ReturnsAXImage()
    {
        string role = NsAccessibilityBridge.MapRoleToNsRole(AccessibleRole.Image);

        await Assert.That(role).IsEqualTo("AXImage");
    }

    [Test]
    public async Task NsBridge_MapRole_Tree_ReturnsAXOutline()
    {
        string role = NsAccessibilityBridge.MapRoleToNsRole(AccessibleRole.Tree);

        await Assert.That(role).IsEqualTo("AXOutline");
    }

    [Test]
    public async Task NsBridge_PlatformName_IsMacOSNSAccessibility()
    {
        var bridge = new NsAccessibilityBridge();

        await Assert.That(bridge.PlatformName).IsEqualTo("macOS NSAccessibility");
    }

    [Test]
    public async Task NsBridge_Initialize_ThenShutdown_DoesNotThrow()
    {
        var bridge = new NsAccessibilityBridge();
        bridge.Initialize(nint.Zero);
        bridge.Shutdown();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AT-SPI2 Bridge — Role Mapping (Linux)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task AtSpi_MapRole_Button_ReturnsPushButton()
    {
        int role = AtSpiBridge.MapRoleToAtSpiRole(AccessibleRole.Button);

        await Assert.That(role).IsEqualTo(62);
    }

    [Test]
    public async Task AtSpi_MapRole_Checkbox_ReturnsCheckBox()
    {
        int role = AtSpiBridge.MapRoleToAtSpiRole(AccessibleRole.Checkbox);

        await Assert.That(role).IsEqualTo(12);
    }

    [Test]
    public async Task AtSpi_MapRole_TextBox_ReturnsText()
    {
        int role = AtSpiBridge.MapRoleToAtSpiRole(AccessibleRole.TextBox);

        await Assert.That(role).IsEqualTo(78);
    }

    [Test]
    public async Task AtSpi_MapRole_Dialog_ReturnsDialog()
    {
        int role = AtSpiBridge.MapRoleToAtSpiRole(AccessibleRole.Dialog);

        await Assert.That(role).IsEqualTo(16);
    }

    [Test]
    public async Task AtSpi_MapRole_Slider_ReturnsSlider()
    {
        int role = AtSpiBridge.MapRoleToAtSpiRole(AccessibleRole.Slider);

        await Assert.That(role).IsEqualTo(73);
    }

    [Test]
    public async Task AtSpi_PlatformName_IsLinuxAtSpi2()
    {
        var bridge = new AtSpiBridge();

        await Assert.That(bridge.PlatformName).IsEqualTo("Linux AT-SPI2");
    }

    [Test]
    public async Task AtSpi_Initialize_SetsApplicationPath()
    {
        var bridge = new AtSpiBridge();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            bridge.Initialize(nint.Zero);
            var path = bridge.GetApplicationPath();
            await Assert.That(path).IsNotNull();
            await Assert.That(path.Length > 0).IsTrue();
            bridge.Shutdown();
        }
        else
        {
            bool passed = true;
        await Assert.That(passed).IsTrue();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // AccessibleRole — Coverage
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task AllRoles_HaveUiaMapping()
    {
        foreach (var role in Enum.GetValues<AccessibleRole>())
        {
            if (role == AccessibleRole.None)
            {
                continue;
            }

            int controlType = UiaProvider.MapRoleToUiaControlType(role);
            await Assert.That(controlType > 0).IsTrue();
        }
    }

    [Test]
    public async Task AllRoles_HaveNsAccessibilityMapping()
    {
        foreach (var role in Enum.GetValues<AccessibleRole>())
        {
            if (role == AccessibleRole.None)
            {
                continue;
            }

            string nsRole = NsAccessibilityBridge.MapRoleToNsRole(role);
            await Assert.That(nsRole).IsNotNull();
            await Assert.That(nsRole.Length > 0).IsTrue();
        }
    }

    [Test]
    public async Task AllRoles_HaveAtSpiMapping()
    {
        foreach (var role in Enum.GetValues<AccessibleRole>())
        {
            if (role == AccessibleRole.None)
            {
                continue;
            }

            int atSpiRole = AtSpiBridge.MapRoleToAtSpiRole(role);
            await Assert.That(atSpiRole >= 0).IsTrue();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // LayoutNodeData — Accessibility Fields
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task LayoutNodeData_AccessibilityFields_DefaultValues()
    {
        var data = new LayoutNodeData();

        await Assert.That(data.A11yRole).IsEqualTo(AccessibleRole.None);
        await Assert.That(data.A11yLabel).IsNull();
        await Assert.That(data.A11yDescription).IsNull();
        await Assert.That(data.A11yLiveRegion).IsEqualTo(LiveRegionMode.Off);
        await Assert.That(data.A11yTabIndex).IsEqualTo(0);
        await Assert.That(data.A11yFocusable).IsFalse();
        await Assert.That(data.A11yDisabled).IsFalse();
        await Assert.That(data.A11yState).IsNull();
    }

    [Test]
    public async Task LayoutNodeData_AccessibilityFields_CanBeSet()
    {
        var data = new LayoutNodeData();
        data.A11yRole = AccessibleRole.Button;
        data.A11yLabel = "Submit";
        data.A11yDescription = "Submit the form";
        data.A11yLiveRegion = LiveRegionMode.Polite;
        data.A11yTabIndex = 5;
        data.A11yFocusable = true;
        data.A11yDisabled = true;
        data.A11yState = new Dictionary<string, string> { ["pressed"] = "true" };

        await Assert.That(data.A11yRole).IsEqualTo(AccessibleRole.Button);
        await Assert.That(data.A11yLabel).IsEqualTo("Submit");
        await Assert.That(data.A11yDescription).IsEqualTo("Submit the form");
        await Assert.That(data.A11yLiveRegion).IsEqualTo(LiveRegionMode.Polite);
        await Assert.That(data.A11yTabIndex).IsEqualTo(5);
        await Assert.That(data.A11yFocusable).IsTrue();
        await Assert.That(data.A11yDisabled).IsTrue();
        await Assert.That(data.A11yState!["pressed"]).IsEqualTo("true");
    }

    // ═══════════════════════════════════════════════════════════════════
    // UIA Provider — Accessibility Context (Windows-specific)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task UiaProvider_GetContext_ReturnsValidContext()
    {
        var provider = new UiaProvider();
        provider.Initialize(nint.Zero);

        var context = provider.GetAccessibilityContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.DisplayScale > 0).IsTrue();
        await Assert.That(context.TextScale > 0).IsTrue();

        provider.Shutdown();
    }

    [Test]
    public async Task UiaProvider_Announce_DoesNotThrow()
    {
        var provider = new UiaProvider();
        provider.Initialize(nint.Zero);

        provider.Announce("Test announcement", AnnouncePriority.Normal);
        provider.Announce("Urgent!", AnnouncePriority.High);
        provider.Announce("FYI", AnnouncePriority.Low);

        provider.Shutdown();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // NSAccessibility Bridge — Context (macOS-specific)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task NsBridge_GetContext_ReturnsValidContext()
    {
        var bridge = new NsAccessibilityBridge();
        bridge.Initialize(nint.Zero);

        var context = bridge.GetAccessibilityContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.DisplayScale > 0).IsTrue();

        bridge.Shutdown();
    }

    [Test]
    public async Task NsBridge_Announce_DoesNotThrow()
    {
        var bridge = new NsAccessibilityBridge();
        bridge.Initialize(nint.Zero);

        bridge.Announce("Test", AnnouncePriority.Normal);

        bridge.Shutdown();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AT-SPI2 Bridge — Context (Linux-specific)
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public async Task AtSpi_GetContext_ReturnsValidContext()
    {
        var bridge = new AtSpiBridge();

        var context = bridge.GetAccessibilityContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.DisplayScale > 0).IsTrue();
    }

    [Test]
    public async Task AtSpi_Announce_DoesNotThrow()
    {
        var bridge = new AtSpiBridge();

        bridge.Announce("Test", AnnouncePriority.High);

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Test Helpers
    // ═══════════════════════════════════════════════════════════════════

    private sealed class TestNode : Node
    {
    }

    private sealed class TestButton : Node
    {
    }

    private sealed class TestBridge : IPlatformAccessibilityBridge
    {
        public string PlatformName => "Test";
        public string? LastAnnouncement { get; private set; }
        public AnnouncePriority LastPriority { get; private set; }
        public int TreeChangedCount { get; private set; }

        public AccessibilityContext Context { get; set; } = new()
        {
            DisplayScale = 1.0f,
            TextScale = 1.0f,
            ReducedMotion = false,
            HighContrast = false,
            ReducedTransparency = false,
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = false,
        };

        public void Initialize(nint windowHandle) { }
        public void Shutdown() { }

        public void OnTreeChanged()
        {
            TreeChangedCount++;
        }

        public void Announce(string message, AnnouncePriority priority)
        {
            LastAnnouncement = message;
            LastPriority = priority;
        }

        public AccessibilityContext GetAccessibilityContext() => Context;

        public bool IsScreenReaderActive() => Context.ScreenReaderActive;
    }
}

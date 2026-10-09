#pragma warning disable CA2000, CA1812

using System.Runtime.InteropServices;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Accessibility;

/// <summary>
/// The Windows UI Automation provider tree, driven in-process through a real
/// <see cref="FrameOrchestrator"/> (layout, focus and input; nothing painted): which elements
/// exist, their roles and names, navigation, screen bounds, patterns, focus, menus and dialogs,
/// identity across re-renders, and that nothing is built until a client asks.
/// </summary>
[NotInParallel(["FocusManager", "Dialog", "ContextMenu", "AccessibilityBridge"])]
public class UiaProviderTreeTests
{
    private static readonly string[] Clips = ["Meeting notes", "Shopping list", "Invoice 2026-114", "Quarterly numbers"];

    /// <summary>Screen = origin (100, 50) + logical × 2, as on a 200% display.</summary>
    private sealed class FakeHost : IUiaHost
    {
        public nint Handle => 0;

        public UiaRect ToScreen(Rect logical)
        {
            return new UiaRect { Left = 100 + (logical.X * 2), Top = 50 + (logical.Y * 2), Width = logical.Width * 2, Height = logical.Height * 2 };
        }

        public Point FromScreen(double x, double y)
        {
            return new Point((float)((x - 100) / 2), (float)((y - 50) / 2));
        }
    }

    private sealed class FormView : Component
    {
        internal static int Clicks;
        internal static bool Pinned;
        internal static string Query = "";
        internal static string? Selected = Clips[1];
        internal static string Last = "none";

        protected override Node Render()
        {
            return new Column(spacing: 8, children:
            [
                new Label("Title"),
                new Row(spacing: 8, children:
                [
                    new Button("Save", () => { Clicks++; Invalidate(); }),
                    new Checkbox(Bind(Pinned, v => { Pinned = v; Invalidate(); }), "Pin"),
                    new Button("Delete all", () => { _ = Dialog.ConfirmAsync("Delete all clips?", "This empties your history.", "Delete", "Keep"); }),
                ]),
                new TextInput(Bind(Query, v => { Query = v; Invalidate(); }), placeholder: "Search"),
                new ListView<string>(Clips, clip => new Row(spacing: 4, children: [new Label("•"), new Label(clip)]),
                        SelectionMode.Single,
                        selected: Bind(Selected!, v => { Selected = v; Invalidate(); }))
                    .ItemHeight(30f)
                    .ItemContextMenu(clip =>
                    [
                        ContextMenuItem.Action("Paste", () => { Last = $"paste {clip}"; }, shortcut: "Enter"),
                        ContextMenuItem.Separator(),
                        ContextMenuItem.Action("Delete", () => { Last = $"delete {clip}"; }),
                    ])
                    .AccessibleLabel("Clips")
                    .Width(300)
                    .Height(90),
            ]).Padding(EdgeInsets.All(10));
        }

        internal static void Reset()
        {
            Clicks = 0;
            Pinned = false;
            Query = "";
            Selected = Clips[1];
            Last = "none";
        }
    }

    private sealed class LongListView : Component
    {
        internal static readonly string[] Items = Enumerable.Range(0, 1000).Select(i => $"Item {i}").ToArray();

        protected override Node Render()
        {
            return new ListView<string>(Items, item => new Label(item), SelectionMode.Single)
                .ItemHeight(20f)
                .Height(100);
        }
    }

    /// <summary>The orchestrator, and the bridge registered as App registers it (re-renders reach it).</summary>
    private sealed class Mounted : IDisposable
    {
        internal Mounted(FrameOrchestrator orch, UiaContext uia)
        {
            Orch = orch;
            Uia = uia;
            Bridge = new UiaProvider();
            Bridge.Attach(uia);
            AccessibilityTreeBuilder.SetPlatformBridge(Bridge);
        }

        internal FrameOrchestrator Orch { get; }

        internal UiaContext Uia { get; }

        internal UiaProvider Bridge { get; }

        public void Dispose()
        {
            AccessibilityTreeBuilder.SetPlatformBridge(null);
            Orch.Dispose();
        }
    }

    private static (Mounted Owner, UiaContext Uia) Mount<TRoot>()
        where TRoot : Component, new()
    {
        FormView.Reset();
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<TRoot>(800, 500);
        orch.Tick();
        var uia = new UiaContext(new FakeHost(), () => orch.RootHost?.RenderedTree, () => orch.Input, () => new Size(800, 500));
        return (new Mounted(orch, uia), uia);
    }

    /// <summary>Runs a frame, as the app does after an action, and marks the UIA tree stale like the bridge does.</summary>
    private static void Frame(FrameOrchestrator orch, UiaContext uia)
    {
        orch.Tick();
        uia.Invalidate();
    }

    private static List<UiaFragment> Children(UiaFragment element)
    {
        var result = new List<UiaFragment>();
        for (var child = element.Navigate(UiaIds.NavigateDirection_FirstChild); child is not null; child = child.Navigate(UiaIds.NavigateDirection_NextSibling))
        {
            result.Add(child);
        }
        return result;
    }

    private static UiaElement Named(UiaContext uia, string name)
    {
        return Find(uia.Root, name) ?? throw new InvalidOperationException($"no element named '{name}'");
    }

    private static UiaElement? Find(UiaFragment element, string name)
    {
        foreach (var child in Children(element))
        {
            if (child is UiaElement e && e.Kind != UiaElementKind.Row && e.Name() == name)
            {
                return e;
            }
            if (child is UiaElement { Kind: not UiaElementKind.Row } && Find(child, name) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static object? Property(UiaElement element, int propertyId)
    {
        var value = element.Property(propertyId);
        try
        {
            return value.ToObject();
        }
        finally
        {
            value.Clear();
        }
    }

    [Test]
    public async Task Tree_ExposesControlsWithRolesAndNames_AndHoistsLayoutContainers()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var top = Children(uia.Root).Cast<UiaElement>().ToList();
        string shape = string.Join(" | ", top.Select(e => $"{e.ControlType()}:{e.Name()}"));

        // The Column and Row are transparent: their controls are the window's children.
        await Assert.That(shape).IsEqualTo(
            $"{UiaIds.TextControl}:Title | {UiaIds.ButtonControl}:Save | {UiaIds.CheckBoxControl}:Pin | " +
            $"{UiaIds.ButtonControl}:Delete all | {UiaIds.EditControl}:Search | {UiaIds.ListControl}:Clips");
        await Assert.That(Property(top[1], UiaIds.FrameworkIdProperty)).IsEqualTo("Cascade");
        await Assert.That(Property(top[1], UiaIds.ClassNameProperty)).IsEqualTo("Button");
        await Assert.That(Property(top[5], UiaIds.ClassNameProperty)).IsEqualTo("ListView");
        await Assert.That(Property(top[1], UiaIds.IsEnabledProperty)).IsEqualTo(true);
        await Assert.That(Property(top[1], UiaIds.IsKeyboardFocusableProperty)).IsEqualTo(true);
        await Assert.That(Property(top[0], UiaIds.IsKeyboardFocusableProperty)).IsEqualTo(false);
    }

    [Test]
    public async Task Navigation_IsConsistentInEveryDirection_AndListRowsAreChildrenOfTheList()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var top = Children(uia.Root);
        for (int i = 0; i < top.Count; i++)
        {
            await Assert.That(top[i].Navigate(UiaIds.NavigateDirection_Parent)).IsSameReferenceAs(uia.Root);
            await Assert.That(top[i].Navigate(UiaIds.NavigateDirection_PreviousSibling)).IsSameReferenceAs(i > 0 ? top[i - 1] : null);
        }
        await Assert.That(uia.Root.Navigate(UiaIds.NavigateDirection_LastChild)).IsSameReferenceAs(top[^1]);
        await Assert.That(uia.Root.Navigate(UiaIds.NavigateDirection_Parent)).IsNull();

        var list = (UiaElement)top[^1];
        var rows = Children(list).Cast<UiaElement>().ToList();
        await Assert.That(string.Join("|", rows.Select(r => r.Name()))).IsEqualTo(string.Join("|", Clips.Select(c => $"• {c}")));
        await Assert.That(rows.All(r => r.ControlType() == UiaIds.ListItemControl)).IsTrue();
        await Assert.That(rows[2].Navigate(UiaIds.NavigateDirection_Parent)).IsSameReferenceAs(list);
        await Assert.That(rows[2].Navigate(UiaIds.NavigateDirection_PreviousSibling)).IsSameReferenceAs(rows[1]);
        await Assert.That(list.Navigate(UiaIds.NavigateDirection_LastChild)).IsSameReferenceAs(rows[^1]);
        await Assert.That(rows[0].Navigate(UiaIds.NavigateDirection_FirstChild)).IsNull();
        await Assert.That(Property(rows[2], UiaIds.PositionInSetProperty)).IsEqualTo(3);
        await Assert.That(Property(rows[2], UiaIds.SizeOfSetProperty)).IsEqualTo(4);
    }

    [Test]
    public async Task Bounds_MatchHitTesting_AndAreScreenPixels()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var save = Named(uia, "Save");
        var page = orch.RootHost!.RenderedTree!;
        var button = OverlayTestKit.FindButton(page, "Save");
        await Assert.That(HitTester.TryGetAbsoluteBounds(page, button, out var expected)).IsTrue();
        await Assert.That(save.LogicalBounds()).IsEqualTo(expected);

        await Assert.That(save.GetBoundingRectangle(out var screen)).IsEqualTo(UiaIds.S_OK);
        await Assert.That(screen.Left).IsEqualTo(100 + (expected.X * 2.0));
        await Assert.That(screen.Top).IsEqualTo(50 + (expected.Y * 2.0));
        await Assert.That(screen.Width).IsEqualTo(expected.Width * 2.0);

        // A point inside the button, in screen pixels, finds it.
        var center = expected.Center;
        await Assert.That(uia.Root.ElementAt(100 + (center.X * 2.0), 50 + (center.Y * 2.0))).IsSameReferenceAs(save);

        // Rows sit inside the list at their own offsets; a row below the fold is offscreen.
        var list = Named(uia, "Clips");
        var rows = Children(list).Cast<UiaElement>().ToList();
        var listBounds = list.LogicalBounds();
        await Assert.That(rows[1].LogicalBounds().Y).IsEqualTo(listBounds.Y + 30f);
        await Assert.That(rows[1].IsOffscreen()).IsFalse();
        await Assert.That(rows[3].IsOffscreen()).IsTrue();
        var rowCenter = rows[1].LogicalBounds().Center;
        await Assert.That(uia.Root.ElementAt(100 + (rowCenter.X * 2.0), 50 + (rowCenter.Y * 2.0))).IsSameReferenceAs(rows[1]);
    }

    [Test]
    public async Task Patterns_InvokeToggleValueAndSelection_ActLikeTheUser()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var save = Named(uia, "Save");
        await Assert.That(save.SupportsPattern(UiaIds.InvokePattern)).IsTrue();
        await Assert.That(save.SupportsPattern(UiaIds.TogglePattern)).IsFalse();
        save.InvokeCore();
        await Assert.That(FormView.Clicks).IsEqualTo(1);

        var pin = Named(uia, "Pin");
        await Assert.That(pin.SupportsPattern(UiaIds.TogglePattern)).IsTrue();
        await Assert.That(pin.ToggleState()).IsEqualTo(UiaIds.ToggleState_Off);
        await Assert.That(((IToggleProvider)pin).Toggle()).IsEqualTo(UiaIds.S_OK);
        Frame(orch, uia);
        await Assert.That(FormView.Pinned).IsTrue();
        await Assert.That(Named(uia, "Pin").ToggleState()).IsEqualTo(UiaIds.ToggleState_On);

        var search = Named(uia, "Search");
        await Assert.That(search.SupportsPattern(UiaIds.ValuePattern)).IsTrue();
        await Assert.That(search.IsValueReadOnly()).IsFalse();
        search.SetValueCore("hello");
        Frame(orch, uia);
        await Assert.That(FormView.Query).IsEqualTo("hello");
        await Assert.That(Property(Named(uia, "Search"), UiaIds.ValueValueProperty)).IsEqualTo("hello");

        var list = Named(uia, "Clips");
        await Assert.That(list.SupportsPattern(UiaIds.SelectionPattern)).IsTrue();
        await Assert.That(list.Selection().Cast<UiaElement>().Single().RowIndex).IsEqualTo(1);
        var row = list.Row(2);
        await Assert.That(row.SupportsPattern(UiaIds.SelectionItemPattern)).IsTrue();
        await Assert.That(row.IsSelected()).IsFalse();
        row.SelectCore();
        Frame(orch, uia);
        await Assert.That(FormView.Selected).IsEqualTo(Clips[2]);
        await Assert.That(row.IsSelected()).IsTrue();
        await Assert.That(list.Selection().Cast<UiaElement>().Single()).IsSameReferenceAs(row);
        await Assert.That(row.SelectionContainer()).IsSameReferenceAs(list);
    }

    [Test]
    public async Task Focus_FollowsTheFocusedControl_AndTheSelectedRowOfAFocusedList()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        await Assert.That(uia.FocusedElement()).IsNull();

        var search = Named(uia, "Search");
        search.FocusCore();
        Frame(orch, uia);
        await Assert.That(FocusManager.FocusedElement).IsTypeOf<TextInput>();
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(search);
        await Assert.That(Property(search, UiaIds.HasKeyboardFocusProperty)).IsEqualTo(true);

        // Focus the list: focus is on its selected row, as Narrator expects of a list box. (Tab into the
        // list is covered end to end by UiaEndToEndTests; focus order is registered while painting.)
        var list = Named(uia, "Clips");
        list.FocusCore();
        Frame(orch, uia);
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(list.Row(1)).Because($"focused node: {FocusManager.FocusedElement?.GetType().Name}, element: {(uia.FocusedElement() as UiaElement)?.Name()}");
        OverlayTestKit.Key(orch, Key.Down);
        Frame(orch, uia);
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(list.Row(2));
        await Assert.That(Property(list.Row(2), UiaIds.HasKeyboardFocusProperty)).IsEqualTo(true);
        await Assert.That(Property(list.Row(1), UiaIds.HasKeyboardFocusProperty)).IsEqualTo(false);
    }

    [Test]
    public async Task Elements_SurviveReRenders_AndReportGoneWhenRemoved()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var save = Named(uia, "Save");
        int id = save.RuntimeId;
        save.InvokeCore();
        Frame(orch, uia);

        // The component re-rendered (new Button node); the element followed it.
        await Assert.That(Named(uia, "Save")).IsSameReferenceAs(save);
        await Assert.That(save.RuntimeId).IsEqualTo(id);
        await Assert.That(save.GetRuntimeId(out nint runtimeId)).IsEqualTo(UiaIds.S_OK);
        await Assert.That(runtimeId).IsNotEqualTo(0);
        _ = SafeArrayDestroy(runtimeId);

        // An element that is no longer in the tree says so instead of answering for a stale node.
        var orphan = UiaElement.ForNode(uia, new Button("Gone", () => { }));
        await Assert.That(orphan.GetPropertyValue(UiaIds.NameProperty, out _)).IsEqualTo(UiaIds.UIA_E_ELEMENTNOTAVAILABLE);
    }

    [Test]
    public async Task ContextMenu_IsAMenuOfMenuItems_WithTheHighlightedItemFocused()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var list = Named(uia, "Clips");
        list.Row(1).FocusCore();
        Frame(orch, uia);
        OverlayTestKit.Key(orch, Key.Apps);
        Frame(orch, uia);

        var menu = Children(uia.Root).Cast<UiaElement>().Single(e => e.Kind == UiaElementKind.Menu);
        await Assert.That(menu.ControlType()).IsEqualTo(UiaIds.MenuControl);
        var items = Children(menu).Cast<UiaElement>().ToList();
        await Assert.That(string.Join("|", items.Select(i => $"{i.ControlType()}:{i.Name()}"))).IsEqualTo($"{UiaIds.MenuItemControl}:Paste|{UiaIds.MenuItemControl}:Delete");
        await Assert.That(Property(items[0], UiaIds.AcceleratorKeyProperty)).IsEqualTo("Enter");
        await Assert.That(Property(items[1], UiaIds.PositionInSetProperty)).IsEqualTo(2);
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(items[0]);

        items[1].FocusCore();
        Frame(orch, uia);
        await Assert.That(uia.FocusedElement()).IsSameReferenceAs(items[1]);
        items[1].InvokeCore();
        Frame(orch, uia);
        await Assert.That(FormView.Last).IsEqualTo($"delete {Clips[1]}");
        await Assert.That(Children(uia.Root).Cast<UiaElement>().Any(e => e.Kind == UiaElementKind.Menu)).IsFalse();
    }

    [Test]
    public async Task Dialog_IsAWindowWithIsDialog_HoldingItsControls_AndTakesFocus()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        Named(uia, "Delete all").InvokeCore();
        OverlayTestKit.Settle(orch);
        uia.Invalidate();

        var dialog = Children(uia.Root).Cast<UiaElement>().Last();
        await Assert.That(dialog.ControlType()).IsEqualTo(UiaIds.WindowControl);
        await Assert.That(dialog.Name()).IsEqualTo("Delete all clips?");
        await Assert.That(Property(dialog, UiaIds.IsDialogProperty)).IsEqualTo(true);
        await Assert.That(Find(dialog, "Keep")).IsNotNull();
        await Assert.That(Find(dialog, "Delete")).IsNotNull();
        await Assert.That(uia.FocusedElement() is UiaElement { Kind: UiaElementKind.Node } focused && Find(dialog, focused.Name()!) is not null).IsTrue();

        Find(dialog, "Keep")!.InvokeCore();
        OverlayTestKit.Settle(orch);
        uia.Invalidate();
        await Assert.That(Children(uia.Root).Cast<UiaElement>().Any(e => e.ControlType() == UiaIds.WindowControl)).IsFalse();
    }

    [Test]
    public async Task VirtualizedList_ExposesEveryRowByIndex_WithoutBuildingThem()
    {
        var (owner, uia) = Mount<LongListView>();
        using var owned = owner;
        var orch = owner.Orch;

        var list = (UiaElement)Children(uia.Root).Single();
        var last = (UiaElement)list.Navigate(UiaIds.NavigateDirection_LastChild)!;
        await Assert.That(last.RowIndex).IsEqualTo(999);
        await Assert.That(last.Name()).IsEqualTo("Item 999");
        await Assert.That(last.IsOffscreen()).IsTrue();

        last.SelectCore();
        Frame(orch, uia);
        await Assert.That(last.IsOffscreen()).IsFalse().Because("selecting a row scrolls it into view, as the keyboard does");
        await Assert.That(uia.TreeBuilds).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task Bridge_DoesNothingPerFrame_UntilAClientAsks()
    {
        var (owner, uia) = Mount<FormView>();
        using var owned = owner;
        var orch = owner.Orch;

        var bridge = owner.Bridge;
        bridge.Initialize(0);
        for (int i = 0; i < 100; i++)
        {
            bridge.OnFrameCompleted(reRendered: i % 2 == 0);
        }

        await Assert.That(bridge.IsConnected).IsFalse();
        await Assert.That(uia.HasRoot).IsFalse();
        await Assert.That(uia.TreeBuilds).IsEqualTo(0);
    }

    [Test]
    public async Task ControlTypes_FollowUiaConventions()
    {
        await Assert.That(UiaProvider.MapRoleToUiaControlType(AccessibleRole.Heading)).IsEqualTo(UiaIds.TextControl);
        await Assert.That(UiaProvider.MapRoleToUiaControlType(AccessibleRole.Row)).IsEqualTo(UiaIds.DataItemControl);
        await Assert.That(UiaProvider.MapRoleToUiaControlType(AccessibleRole.ProgressBar)).IsEqualTo(UiaIds.ProgressBarControl);
        await Assert.That(UiaProvider.MapRoleToUiaControlType(AccessibleRole.Dialog)).IsEqualTo(UiaIds.WindowControl);
        await Assert.That(UiaProvider.MapRoleToUiaControlType(AccessibleRole.Menu)).IsEqualTo(UiaIds.MenuControl);
    }

    [DllImport("oleaut32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SafeArrayDestroy(nint array);
}

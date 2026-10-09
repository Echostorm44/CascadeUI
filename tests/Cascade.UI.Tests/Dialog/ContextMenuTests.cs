#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// Context menus end to end through the input dispatcher: opening from a right-click (generic
/// <c>.OnContextMenu</c>), the mouse and keyboard while open, dismissal, and the window's
/// geometry. Layout runs through a real <see cref="FrameOrchestrator"/>; nothing is painted, so
/// these exercise the same geometry the painter draws and hit-testing reads.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager"])]
public sealed class ContextMenuTests
{
    private static readonly List<string> Log = [];

    private static IReadOnlyList<ContextMenuItem> Items() =>
    [
        ContextMenuItem.Action("Paste", () => { Log.Add("paste"); }, shortcut: "Enter"),
        ContextMenuItem.Action("Copy", () => { Log.Add("copy"); }, shortcut: "Ctrl+C"),
        ContextMenuItem.Separator(),
        ContextMenuItem.Action("Pin", () => { Log.Add("pin"); }, disabled: true),
        ContextMenuItem.Submenu("Move to",
        [
            ContextMenuItem.Action("Archive", () => { Log.Add("archive"); }),
            ContextMenuItem.Action("Trash", () => { Log.Add("trash"); }),
        ]),
        ContextMenuItem.Action("Delete", () => { Log.Add("delete"); }, style: MenuItemStyle.Destructive),
    ];

    /// <summary>A target that opens the menu from its right-click handler, plus a button beside it.</summary>
    private sealed class MenuHostView : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 0, children:
            [
                new Label("Right-click me")
                    .Width(300).Height(100)
                    .OnContextMenu(() => { ContextMenu.Show(Items()); }),
                new Button("Other", () => { Log.Add("button"); }).Width(200).Height(40),
            ]);
        }
    }

    private static FrameOrchestrator Mount(float width = 800f, float height = 500f)
    {
        Log.Clear();
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<MenuHostView>(width, height);
        orch.Tick();
        return orch;
    }

    private static void Mouse(InputDispatcher input, NativeMouseEventType type, float x, float y, NativeMouseButton button = NativeMouseButton.Left)
    {
        input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = type, Button = button });
    }

    private static void RightClick(InputDispatcher input, float x, float y)
    {
        Mouse(input, NativeMouseEventType.MouseDown, x, y, NativeMouseButton.Right);
        Mouse(input, NativeMouseEventType.MouseUp, x, y, NativeMouseButton.Right);
    }

    private static void Click(InputDispatcher input, float x, float y)
    {
        Mouse(input, NativeMouseEventType.MouseDown, x, y);
        Mouse(input, NativeMouseEventType.MouseUp, x, y);
    }

    private static void Key(InputDispatcher input, Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    private static void Type(InputDispatcher input, char c)
    {
        input.HandleKeyEvent(new NativeKeyEvent { Key = Cascade.UI.Key.None, Character = c, Type = NativeKeyEventType.KeyDown });
    }

    private static Point CenterOf(MenuLevel level, int item)
    {
        return new Point(level.Bounds.X + 30f, level.ItemTop(item) + (level.ItemHeights[item] / 2f));
    }

    [Test]
    public async Task RightClick_OpensTheMenuAtThePointer()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        await Assert.That(ContextMenu.IsOpen).IsTrue();
        var root = orch.Input.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(50f);
        await Assert.That(root.Bounds.Y).IsEqualTo(40f);
        await Assert.That(root.Highlighted).IsEqualTo(-1); // pointer-opened: nothing pre-selected
    }

    [Test]
    public async Task ReleaseOfTheOpeningClick_DoesNotActivate()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        await Assert.That(ContextMenu.IsOpen).IsTrue();
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task ClickingAnItem_RunsItsHandler_AndCloses()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var root = orch.Input.Menu!.Levels[0];
        var copy = CenterOf(root, 1);

        Mouse(orch.Input, NativeMouseEventType.MouseMove, copy.X, copy.Y);
        await Assert.That(root.Highlighted).IsEqualTo(1);
        Click(orch.Input, copy.X, copy.Y);

        await Assert.That(string.Join(",", Log)).IsEqualTo("copy");
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task PressDragRelease_ActivatesTheItemUnderTheRelease()
    {
        using var orch = Mount();
        Mouse(orch.Input, NativeMouseEventType.MouseDown, 50, 40, NativeMouseButton.Right);
        var root = orch.Input.Menu!.Levels[0];
        var paste = CenterOf(root, 0);

        Mouse(orch.Input, NativeMouseEventType.MouseMove, paste.X, paste.Y, NativeMouseButton.Right);
        Mouse(orch.Input, NativeMouseEventType.MouseUp, paste.X, paste.Y, NativeMouseButton.Right);

        await Assert.That(string.Join(",", Log)).IsEqualTo("paste");
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task ClickingADisabledItemOrSeparator_DoesNothing()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var root = orch.Input.Menu!.Levels[0];

        var pin = CenterOf(root, 3);
        Click(orch.Input, pin.X, pin.Y);
        var separator = CenterOf(root, 2);
        Click(orch.Input, separator.X, separator.Y);

        await Assert.That(Log).IsEmpty();
        await Assert.That(ContextMenu.IsOpen).IsTrue();
    }

    [Test]
    public async Task ClickOutside_Closes_AndDoesNotReachTheContentBeneath()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        // The button sits at y 100–140, x 0–200, outside the menu (which starts at x 50, y 40
        // but the click at x 20 is left of it).
        Click(orch.Input, 20, 120);

        await Assert.That(ContextMenu.IsOpen).IsFalse();
        await Assert.That(Log).IsEmpty();

        // The next click reaches the button normally.
        Click(orch.Input, 20, 120);
        await Assert.That(string.Join(",", Log)).IsEqualTo("button");
    }

    [Test]
    public async Task RightClickElsewhere_ReopensTheMenuThere()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        RightClick(orch.Input, 10, 10);

        await Assert.That(ContextMenu.IsOpen).IsTrue();
        await Assert.That(orch.Input.Menu!.Levels[0].Bounds.X).IsEqualTo(10f);
    }

    [Test]
    public async Task Keyboard_NavigatesSkippingSeparatorsAndDisabled_EnterActivates()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var root = orch.Input.Menu!.Levels[0];

        Key(orch.Input, Cascade.UI.Key.Down);
        await Assert.That(root.Highlighted).IsEqualTo(0);
        Key(orch.Input, Cascade.UI.Key.Down);
        Key(orch.Input, Cascade.UI.Key.Down);
        await Assert.That(root.Highlighted).IsEqualTo(4); // Copy → (separator, disabled Pin skipped) → Move to
        Key(orch.Input, Cascade.UI.Key.Down);
        await Assert.That(root.Highlighted).IsEqualTo(5);
        Key(orch.Input, Cascade.UI.Key.Enter);

        await Assert.That(string.Join(",", Log)).IsEqualTo("delete");
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task Keyboard_RightOpensSubmenu_LeftClosesIt_EnterRunsTheChild()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var menu = orch.Input.Menu!;

        Key(orch.Input, Cascade.UI.Key.Up);
        Key(orch.Input, Cascade.UI.Key.Up);
        await Assert.That(menu.Levels[0].Highlighted).IsEqualTo(4);

        Key(orch.Input, Cascade.UI.Key.Right);
        await Assert.That(menu.Levels.Count).IsEqualTo(2);
        await Assert.That(menu.Levels[1].Highlighted).IsEqualTo(0);

        Key(orch.Input, Cascade.UI.Key.Left);
        await Assert.That(menu.Levels.Count).IsEqualTo(1);

        Key(orch.Input, Cascade.UI.Key.Enter); // Enter on a submenu item opens it, highlighting the first child
        Key(orch.Input, Cascade.UI.Key.Down);
        Key(orch.Input, Cascade.UI.Key.Enter);

        await Assert.That(string.Join(",", Log)).IsEqualTo("trash");
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task Hover_OpensSubmenu_AndClickingAChildRunsIt()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var menu = orch.Input.Menu!;
        var moveTo = CenterOf(menu.Levels[0], 4);

        Mouse(orch.Input, NativeMouseEventType.MouseMove, moveTo.X, moveTo.Y);
        await Assert.That(menu.Levels.Count).IsEqualTo(2);

        var archive = CenterOf(menu.Levels[1], 0);
        Mouse(orch.Input, NativeMouseEventType.MouseMove, archive.X, archive.Y);
        Click(orch.Input, archive.X, archive.Y);

        await Assert.That(string.Join(",", Log)).IsEqualTo("archive");
    }

    [Test]
    public async Task Escape_ClosesInnermostPanelFirst()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);
        var menu = orch.Input.Menu!;
        menu.OpenSubmenu(0, 4, highlightFirst: true);

        Key(orch.Input, Cascade.UI.Key.Escape);
        await Assert.That(menu.Levels.Count).IsEqualTo(1);
        Key(orch.Input, Cascade.UI.Key.Escape);
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task TypingALetter_JumpsToTheItem()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        Type(orch.Input, 'd');
        await Assert.That(orch.Input.Menu!.Levels[0].Highlighted).IsEqualTo(5);
    }

    [Test]
    public async Task KeysWhileOpen_DoNotReachTheFocusedControl()
    {
        using var orch = Mount();
        var button = FindButton(orch);
        FocusManager.RequestFocus(button);
        RightClick(orch.Input, 50, 40);
        FocusManager.RequestFocus(button); // the right-click focused nothing new; keep the button focused

        Key(orch.Input, Cascade.UI.Key.Space); // would click the focused button if it leaked
        Key(orch.Input, Cascade.UI.Key.Escape);

        await Assert.That(Log).IsEmpty();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(button);
    }

    [Test]
    public async Task WindowDeactivation_ClosesTheMenu()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        orch.Input.HandleWindowDeactivated();
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task WindowResize_ClosesTheMenu()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        orch.HandleResize(600, 400);
        await Assert.That(ContextMenu.IsOpen).IsFalse();
    }

    [Test]
    public async Task NearTheWindowCorner_TheMenuStaysInside()
    {
        using var orch = Mount(800, 500);
        RightClick(orch.Input, 40, 95); // inside the target, then ask for it again near the corner
        ContextMenu.Show(new Point(795, 495), Items());

        var b = orch.Input.Menu!.Levels[0].Bounds;
        await Assert.That(b.X).IsGreaterThanOrEqualTo(MenuOverlay.EdgeMargin);
        await Assert.That(b.Y).IsGreaterThanOrEqualTo(MenuOverlay.EdgeMargin);
        await Assert.That(b.Right).IsLessThanOrEqualTo(800f - MenuOverlay.EdgeMargin);
        await Assert.That(b.Bottom).IsLessThanOrEqualTo(500f - MenuOverlay.EdgeMargin);
    }

    [Test]
    public async Task ShowAnchoredToANode_OpensBelowIt()
    {
        using var orch = Mount();
        var button = FindButton(orch);

        ContextMenu.Show(button, Items());

        var b = orch.Input.Menu!.Levels[0].Bounds;
        await Assert.That(b.X).IsEqualTo(0f + MenuOverlay.EdgeMargin);
        await Assert.That(b.Y).IsEqualTo(140f + MenuOverlay.AnchorGap);
    }

    [Test]
    public async Task ShowAnchoredToANodeNotInTheTree_Throws()
    {
        using var orch = Mount();

        await Assert.That(() => ContextMenu.Show(new Label("detached"), Items())).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ContextMenuKey_RunsTheFocusedNodesHandler_BelowIt()
    {
        using var orch = Mount();
        var target = FindLabel(orch);
        FocusManager.RequestFocus(target);

        Key(orch.Input, Cascade.UI.Key.Apps);

        await Assert.That(ContextMenu.IsOpen).IsTrue();
        var root = orch.Input.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(100f + MenuOverlay.AnchorGap);
        await Assert.That(root.Highlighted).IsEqualTo(0); // keyboard-opened: first item highlighted

        Key(orch.Input, Cascade.UI.Key.Escape);
        Key(orch.Input, Cascade.UI.Key.F10, ModifierKeys.Shift);
        await Assert.That(ContextMenu.IsOpen).IsTrue();
    }

    [Test]
    public async Task WheelWhileOpen_IsTakenByTheMenu()
    {
        using var orch = Mount();
        RightClick(orch.Input, 50, 40);

        orch.Input.HandleScrollEvent(new NativeScrollEvent { X = 10, Y = 10, DeltaY = -1f });

        await Assert.That(ContextMenu.IsOpen).IsTrue();
    }

    private static Button FindButton(FrameOrchestrator orch)
    {
        var column = (Column)orch.RootHost!.RenderedTree!;
        return (Button)column.Children[1];
    }

    private static Label FindLabel(FrameOrchestrator orch)
    {
        var column = (Column)orch.RootHost!.RenderedTree!;
        return (Label)column.Children[0];
    }
}

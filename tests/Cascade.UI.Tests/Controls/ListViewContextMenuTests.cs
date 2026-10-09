#pragma warning disable CA2000

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <see cref="ListView{T}.ItemContextMenu"/>: a right-click selects the row and opens that row's
/// menu at the pointer; the context-menu key / Shift+F10 opens the selected row's menu below the
/// row; <see cref="ListView{T}.ShowContextMenu"/> does the same from app code. Geometry the
/// painter normally stamps (the list's screen bounds, viewport) is set directly, as in
/// <see cref="ListViewSelectionTests"/>.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager"])]
public class ListViewContextMenuTests
{
    private const float RowHeight = 30f;
    private InputDispatcher dispatcher = null!;
    private string? selected;
    private readonly List<string> log = [];

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher { ViewportSize = new Size(800, 500) };
        FocusManager.Reset();
        selected = null;
        log.Clear();
    }

    private ListView<string> BuildList(int count = 10, float top = 50f, float viewport = 300f, string? initiallySelected = null)
    {
        selected = initiallySelected ?? selected;
        var items = Enumerable.Range(0, count).Select(i => $"item{i}").ToList();
        var list = new ListView<string>(items, s => new Label(s), SelectionMode.Single,
                selected: new Bindable<string>(selected!, v => selected = v))
            .ItemHeight(RowHeight)
            .ItemContextMenu(item =>
            [
                ContextMenuItem.Action("Paste", () => { log.Add($"paste:{item}"); }, shortcut: "Enter"),
                ContextMenuItem.Separator(),
                ContextMenuItem.Action("Delete", () => { log.Add($"delete:{item}"); }, style: MenuItemStyle.Destructive),
            ]);
        IListViewNode node = list;
        node.ReorderBounds = new Rect(20, top, 400, viewport);
        node.ViewportHeight = viewport;
        node.MaxY = Math.Max(0, (count * RowHeight) - viewport);
        dispatcher.SetRoot(list);
        return list;
    }

    private void Mouse(NativeMouseEventType type, float x, float y, NativeMouseButton button)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = type, Button = button });
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    [Test]
    public async Task RightClickOnARow_SelectsIt_OpensItsMenu_AndTheChosenItemRunsForThatRow()
    {
        var list = BuildList();
        float y = 50 + (2 * RowHeight) + 10;

        Mouse(NativeMouseEventType.MouseDown, 100, y, NativeMouseButton.Right);
        Mouse(NativeMouseEventType.MouseUp, 100, y, NativeMouseButton.Right);

        await Assert.That(selected).IsEqualTo("item2");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(list);
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(100f);
        await Assert.That(root.Bounds.Y).IsEqualTo(y);

        var delete = new Point(root.Bounds.X + 30, root.ItemTop(2) + 5);
        Mouse(NativeMouseEventType.MouseMove, delete.X, delete.Y, NativeMouseButton.None);
        Mouse(NativeMouseEventType.MouseDown, delete.X, delete.Y, NativeMouseButton.Left);
        Mouse(NativeMouseEventType.MouseUp, delete.X, delete.Y, NativeMouseButton.Left);

        await Assert.That(string.Join(",", log)).IsEqualTo("delete:item2");
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(list);
    }

    [Test]
    public async Task RightClick_WithFocusOnClickOff_FocusStaysInTheSearchBox_BeforeAndAfterTheMenu()
    {
        var list = BuildList().FocusOnClick(false);
        var search = new TextInput(new Bindable<string>("", _ => { }));
        dispatcher.SetRoot(new Column(children: [search, list]));
        FocusManager.RequestFocus(search);
        float y = 50 + RowHeight + 10;

        Mouse(NativeMouseEventType.MouseDown, 100, y, NativeMouseButton.Right);
        Mouse(NativeMouseEventType.MouseUp, 100, y, NativeMouseButton.Right);

        await Assert.That(selected).IsEqualTo("item1");
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(search);

        Press(Key.Escape);

        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(search);
    }

    [Test]
    public async Task RightClickBelowTheLastRow_OpensNothing()
    {
        BuildList(count: 3);

        Mouse(NativeMouseEventType.MouseDown, 100, 50 + (5 * RowHeight), NativeMouseButton.Right);

        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(selected).IsNull();
    }

    [Test]
    public async Task ContextMenuKey_OpensBelowTheSelectedRow_WithTheFirstItemHighlighted()
    {
        var list = BuildList();
        Mouse(NativeMouseEventType.MouseDown, 100, 50 + RowHeight + 5, NativeMouseButton.Left);
        Mouse(NativeMouseEventType.MouseUp, 100, 50 + RowHeight + 5, NativeMouseButton.Left);
        await Assert.That(selected).IsEqualTo("item1");

        Press(Key.Apps);

        await Assert.That(dispatcher.IsMenuOpen).IsTrue();
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(20f);
        await Assert.That(root.Bounds.Y).IsEqualTo(50 + (2 * RowHeight) + MenuOverlay.AnchorGap);
        await Assert.That(root.Highlighted).IsEqualTo(0);

        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("paste:item1");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(list);
    }

    [Test]
    public async Task ShiftF10_OpensTheSelectedRowsMenu_EscapeClosesIt()
    {
        BuildList();
        Mouse(NativeMouseEventType.MouseDown, 100, 55, NativeMouseButton.Left);
        Mouse(NativeMouseEventType.MouseUp, 100, 55, NativeMouseButton.Left);

        Press(Key.F10, ModifierKeys.Shift);
        await Assert.That(dispatcher.IsMenuOpen).IsTrue();

        Press(Key.Escape);
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task ShowContextMenu_FromAppCode_OpensForTheSelectedItem()
    {
        var list = BuildList(initiallySelected: "item3");

        await Assert.That(list.ShowContextMenu()).IsTrue();
        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(50 + (4 * RowHeight) + MenuOverlay.AnchorGap);

        Press(Key.Down);
        Press(Key.Enter);
        await Assert.That(string.Join(",", log)).IsEqualTo("delete:item3");
    }

    [Test]
    public async Task ShowContextMenu_WithNothingSelected_ReturnsFalse()
    {
        var list = BuildList();

        await Assert.That(list.ShowContextMenu()).IsFalse();
        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
    }

    [Test]
    public async Task ScrolledList_AnchorsToTheRowOnScreen()
    {
        var list = BuildList(viewport: 120f, initiallySelected: "item6");
        ((IListViewNode)list).OffsetY = 5 * RowHeight;

        list.ShowContextMenu();

        var root = dispatcher.Menu!.Levels[0];
        await Assert.That(root.Bounds.Y).IsEqualTo(50 + RowHeight + RowHeight + MenuOverlay.AnchorGap); // row 6 is 2nd visible
    }

    [Test]
    public async Task WheelWhileOpen_DoesNotScrollTheList()
    {
        var list = BuildList();
        Mouse(NativeMouseEventType.MouseDown, 100, 60, NativeMouseButton.Right);
        Mouse(NativeMouseEventType.MouseUp, 100, 60, NativeMouseButton.Right);

        dispatcher.HandleScrollEvent(new NativeScrollEvent { X = 30, Y = 200, DeltaY = -3f });

        await Assert.That(((IListViewNode)list).OffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task EmptyItemMenu_OpensNothing()
    {
        var items = new List<string> { "a" };
        var list = new ListView<string>(items, s => new Label(s), SelectionMode.Single)
            .ItemHeight(RowHeight)
            .ItemContextMenu(_ => []);
        ((IListViewNode)list).ReorderBounds = new Rect(0, 0, 200, 100);
        ((IListViewNode)list).ViewportHeight = 100;
        dispatcher.SetRoot(list);

        Mouse(NativeMouseEventType.MouseDown, 10, 10, NativeMouseButton.Right);

        await Assert.That(dispatcher.IsMenuOpen).IsFalse();
    }
}

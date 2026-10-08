namespace Cascade.UI.Tests.Controls;

/// <summary>
/// ListView selection: click selects and focuses, arrow keys move the selection and scroll it into
/// view, Enter and double-click activate. Geometry that layout and paint normally provide (the
/// list's screen bounds, viewport) is set directly.
/// </summary>
public class ListViewSelectionTests
{
    private const float RowHeight = 30f;
    private InputDispatcher dispatcher = null!;
    private string? selected;
    private readonly List<string> activated = [];

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher();
        FocusManager.Reset();
        selected = null;
        activated.Clear();
    }

    private ListView<string> BuildList(int count, float viewport = 90f)
    {
        var items = Enumerable.Range(0, count).Select(i => $"item{i}").ToList();
        var list = new ListView<string>(items, s => new Label(s), SelectionMode.Single,
                selected: new Bindable<string>(selected!, v => selected = v))
            .ItemHeight(RowHeight)
            .OnActivate(activated.Add);
        IListViewNode node = list;
        node.ReorderBounds = new Rect(0, 0, 200, viewport);
        node.ViewportHeight = viewport;
        node.MaxY = Math.Max(0, count * RowHeight - viewport);
        dispatcher.SetRoot(list);
        return list;
    }

    [Test]
    public async Task Click_SelectsRowAndFocusesList()
    {
        var list = BuildList(10);

        Click(y: 2 * RowHeight + 5);

        await Assert.That(selected).IsEqualTo("item2");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(list);
    }

    [Test]
    public async Task Click_WithFocusOnClickOff_SelectsRow_AndFocusStaysInTheSearchBox()
    {
        // A search-driven list (Raycast, a launcher): typing must keep filtering after a row is clicked.
        var list = BuildList(10).FocusOnClick(false);
        var search = new TextInput(new Bindable<string>("", _ => { }));
        dispatcher.SetRoot(new Column(children: [search, list]));
        FocusManager.RequestFocus(search);

        Click(y: 2 * RowHeight + 5);

        await Assert.That(selected).IsEqualTo("item2");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(search);
    }

    [Test]
    public async Task ArrowKeys_MoveSelection_AndScrollItIntoView()
    {
        var list = BuildList(10);
        Click(y: 5);

        // Several presses before any re-render keep advancing from the node's own selection.
        Press(Key.Down);
        Press(Key.Down);
        Press(Key.Down);

        await Assert.That(selected).IsEqualTo("item3");
        // Row 3 spans 90–120; the 90 px viewport scrolls to 30 to show it fully.
        await Assert.That(((IListViewNode)list).OffsetY).IsEqualTo(30f);

        Press(Key.End);
        await Assert.That(selected).IsEqualTo("item9");
        Press(Key.Home);
        await Assert.That(selected).IsEqualTo("item0");
        await Assert.That(((IListViewNode)list).OffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task Enter_And_DoubleClick_Activate()
    {
        BuildList(5);
        Click(y: RowHeight + 5);
        Press(Key.Enter);
        Click(y: 2 * RowHeight + 5, clickCount: 2);

        await Assert.That(string.Join(",", activated)).IsEqualTo("item1,item2");
    }

    [Test]
    public async Task ScrollToReveal_MovesTheMinimum()
    {
        await Assert.That(LayoutSolver.ScrollToReveal(offset: 50, rowTop: 60, rowHeight: 30, viewport: 100)).IsEqualTo(50f);
        await Assert.That(LayoutSolver.ScrollToReveal(offset: 50, rowTop: 20, rowHeight: 30, viewport: 100)).IsEqualTo(20f);
        await Assert.That(LayoutSolver.ScrollToReveal(offset: 50, rowTop: 140, rowHeight: 30, viewport: 100)).IsEqualTo(70f);
    }

    private void Click(float y, int clickCount = 1)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = 20, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left, ClickCount = clickCount });
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = 20, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
    }

    private void Press(Key key)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = ModifierKeys.None });
    }

    [Test]
    public async Task Plain_DropsCardChrome_DefaultKeepsIt()
    {
        IListViewNode card = new ListView<string>(["a"], s => new Label(s));
        IListViewNode plain = new ListView<string>(["a"], s => new Label(s)).Plain();

        await Assert.That(card.IsPlain).IsFalse();
        await Assert.That(plain.IsPlain).IsTrue();
    }
}

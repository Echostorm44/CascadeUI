#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ListViewTests
{
    private static readonly string[] sampleItems = ["Alpha", "Bravo", "Charlie"];

    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task FlatListStoresItems()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty);

        var count = list.Items.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task FlatListStoresRenderFunc()
    {
        Func<string, Node> render = item => Node.Empty;
        var list = new ListView<string>(sampleItems, render);

        var storedRender = list.Render;
        await Assert.That(storedRender).IsEqualTo(render);
    }

    [Test]
    public async Task FlatListDefaultSelectionModeIsNone()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty);

        var mode = list.SelectionMode;
        await Assert.That(mode).IsEqualTo(SelectionMode.None);
    }

    [Test]
    public async Task FlatListSectionsIsNullForFlatList()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty);

        var sections = list.Sections;
        await Assert.That(sections).IsNull();
    }

    // ── Sectioned list ──────────────────────────────────────────────

    [Test]
    public async Task SectionedListStoresSections()
    {
        var sections = new[]
        {
            new ListSection<string>("Group1", ["A", "B"]),
            new ListSection<string>("Group2", ["C"]),
        };
        var list = new ListView<string>(sections, item => Node.Empty, s => Node.Empty);

        var sectionCount = list.Sections!.Count;
        await Assert.That(sectionCount).IsEqualTo(2);
    }

    [Test]
    public async Task SectionedListStoresHeaderRenderer()
    {
        var sections = new[] { new ListSection<string>("G", ["X"]) };
        Func<ListSection<string>, Node> headerRender = s => Node.Empty;
        var list = new ListView<string>(sections, item => Node.Empty, headerRender);

        var storedHeader = list.RenderHeader;
        await Assert.That(storedHeader).IsEqualTo(headerRender);
    }

    // ── Fixed item height ───────────────────────────────────────────

    [Test]
    public async Task ItemHeightFloatStoresValue()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .ItemHeight(42f);

        var height = list.fixedItemHeight;
        await Assert.That(height).IsEqualTo(42f);
    }

    // ── Estimated item height ───────────────────────────────────────

    [Test]
    public async Task ItemHeightEstimatedStoresStrategy()
    {
        var strategy = ItemHeight.Estimated(60f);
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .ItemHeight(strategy);

        var stored = list.itemHeightStrategy;
        await Assert.That(stored).IsEqualTo(strategy);

        var estimated = strategy.estimatedHeight;
        await Assert.That(estimated).IsEqualTo(60f);
    }

    [Test]
    public async Task ItemHeightDynamicIsDynamic()
    {
        var dynamic = ItemHeight.Dynamic;

        var isDynamic = dynamic.isDynamic;
        await Assert.That(isDynamic).IsTrue();
    }

    // ── Sticky headers ──────────────────────────────────────────────

    [Test]
    public async Task StickyHeadersStoresValue()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .StickyHeaders(true);

        var sticky = list.stickyHeadersEnabled;
        await Assert.That(sticky).IsTrue();
    }

    // ── Reorderable ─────────────────────────────────────────────────

    [Test]
    public async Task ReorderableStoresValue()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .Reorderable(true);

        var reorderable = list.reorderableEnabled;
        await Assert.That(reorderable).IsTrue();
    }

    [Test]
    public async Task OnReorderStoresCallback()
    {
        int fromCapture = -1;
        int toCapture = -1;
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .OnReorder((from, to) => { fromCapture = from; toCapture = to; });

        list.onReorderHandler!(1, 2);

        await Assert.That(fromCapture).IsEqualTo(1);
        await Assert.That(toCapture).IsEqualTo(2);
    }

    // ── Drag handle ─────────────────────────────────────────────────

    [Test]
    public async Task DragHandleStoresSelector()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .DragHandle(_ => DragHandleMode.Full);

        var mode = list.dragHandleSelector!("test");
        await Assert.That(mode).IsEqualTo(DragHandleMode.Full);
    }

    // ── Empty state ─────────────────────────────────────────────────

    [Test]
    public async Task EmptyStateDefaultIsNodeEmpty()
    {
        var list = new ListView<string>([], item => Node.Empty);

        var empty = list.emptyStateNode;
        await Assert.That(empty).IsEqualTo(Node.Empty);
    }

    [Test]
    public async Task EmptyStateStoresNode()
    {
        var placeholder = Node.Empty;
        var list = new ListView<string>([], item => Node.Empty)
            .EmptyState(placeholder);

        var stored = list.emptyStateNode;
        await Assert.That(stored).IsEqualTo(placeholder);
    }

    // ── Pull to refresh ─────────────────────────────────────────────

    [Test]
    public async Task PullToRefreshStoresHandler()
    {
        bool refreshed = false;
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .PullToRefresh(() => { refreshed = true; return Task.CompletedTask; });

        await list.pullToRefreshHandler!();

        await Assert.That(refreshed).IsTrue();
    }

    // ── Infinite scroll ─────────────────────────────────────────────

    [Test]
    public async Task OnEndReachedStoresThresholdAndHandler()
    {
        bool reached = false;
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .OnEndReached(0.8f, () => { reached = true; return Task.CompletedTask; });

        var threshold = list.endReachedThreshold;
        await Assert.That(threshold).IsEqualTo(0.8f);

        await list.onEndReachedHandler!();
        await Assert.That(reached).IsTrue();
    }

    // ── Fluent chaining returns same instance ───────────────────────

    [Test]
    public async Task FluentChainingReturnsSameInstance()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty);
        var chained = list
            .ItemHeight(40f)
            .StickyHeaders(true)
            .Reorderable(true)
            .EmptyState(Node.Empty);

        var same = ReferenceEquals(list, chained);
        await Assert.That(same).IsTrue();
    }

    // ── ListSection ─────────────────────────────────────────────────

    [Test]
    public async Task ListSectionStoresKeyAndItems()
    {
        var section = new ListSection<int>("Numbers", [1, 2, 3]);

        var key = section.Key;
        var count = section.Items.Count;
        await Assert.That(key).IsEqualTo("Numbers");
        await Assert.That(count).IsEqualTo(3);
    }

    // ── Swipe actions ───────────────────────────────────────────────

    [Test]
    public async Task ItemSwipeActionsStoresFactory()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .ItemSwipeActions(_ => null);

        var factory = list.swipeActionsFactory;
        await Assert.That(factory).IsNotNull();
    }

    // ── Virtualization ──────────────────────────────────────────────

    [Test]
    public async Task VirtualizationBuildsOnlyVisibleSlice()
    {
        int renderCalls = 0;
        var items = Enumerable.Range(0, 10_000).ToArray();
        var list = new ListView<int>(items, _ => { renderCalls++; return Node.Empty; })
            .ItemHeight(30f);

        var lvn = (IListViewNode)list;
        lvn.ViewportHeight = 300f; // 10 rows visible
        lvn.OffsetY = 0f;
        lvn.InvalidateContent();
        lvn.GetContentNode();

        // 10 visible + a small buffer — nowhere near 10,000.
        await Assert.That(renderCalls).IsGreaterThan(0);
        await Assert.That(renderCalls).IsLessThan(20);
    }

    [Test]
    public async Task VirtualizationRendersScrolledSlice()
    {
        var rendered = new List<int>();
        var items = Enumerable.Range(0, 10_000).ToArray();
        var list = new ListView<int>(items, i => { rendered.Add(i); return Node.Empty; })
            .ItemHeight(30f);

        var lvn = (IListViewNode)list;
        lvn.ViewportHeight = 300f;
        lvn.OffsetY = 3000f; // scrolled to row 100
        lvn.InvalidateContent();
        lvn.GetContentNode();

        // Rows around index 100 are built; the top of the list is not.
        await Assert.That(rendered).Contains(100);
        await Assert.That(rendered).DoesNotContain(0);
        await Assert.That(rendered.Count).IsLessThan(20);
    }

    [Test]
    public async Task VirtualizationContentOffsetAlignsSliceToScroll()
    {
        var items = Enumerable.Range(0, 10_000).ToArray();
        var list = new ListView<int>(items, _ => Node.Empty).ItemHeight(30f);

        var lvn = (IListViewNode)list;
        lvn.ViewportHeight = 300f;
        lvn.OffsetY = 3000f; // row 100; first = 100 - 3 buffer = 97
        lvn.InvalidateContent();
        lvn.GetContentNode();

        // ContentOffsetY = first*ih - offsetY = 97*30 - 3000 = -90.
        await Assert.That(lvn.ContentOffsetY).IsEqualTo(-90f);
    }

    [Test]
    public async Task VirtualizationDisabledWithoutFixedHeight()
    {
        var list = new ListView<int>([1, 2, 3], _ => Node.Empty);

        var lvn = (IListViewNode)list;
        await Assert.That(lvn.CanVirtualize).IsFalse();
    }

    [Test]
    public async Task NonVirtualizedBuildsAllRows()
    {
        int renderCalls = 0;
        var items = Enumerable.Range(0, 500).ToArray();
        // No ItemHeight → not virtualizable; every row is built.
        var list = new ListView<int>(items, _ => { renderCalls++; return Node.Empty; });

        var lvn = (IListViewNode)list;
        lvn.InvalidateContent();
        lvn.GetContentNode();

        await Assert.That(renderCalls).IsEqualTo(500);
    }

    // ── Swipe actions (control-level behavior) ──────────────────────

    private static readonly Icon swipeIcon = new("m6 9 6 6 6-6", new Size(24, 24), 24f, "Test");

    [Test]
    public async Task SwipeActionsAreDetectedAndCounted()
    {
        var set = new SwipeActionSet(
            leading: new SwipeAction("Pin", swipeIcon, new ColorValue("#3B82F6"), () => { }),
            trailing: new SwipeAction("Delete", swipeIcon, new ColorValue("#EF4444"), () => { }));
        var list = new ListView<string>(sampleItems, _ => Node.Empty)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        await Assert.That(lvn.HasSwipeActions).IsTrue();
        await Assert.That(lvn.TrailingActionCount(0)).IsEqualTo(1);
        await Assert.That(lvn.LeadingActionCount(0)).IsEqualTo(1);
    }

    [Test]
    public async Task SwipeInvokeTrailingActionFiresHandler()
    {
        bool fired = false;
        var set = new SwipeActionSet(
            trailing: new SwipeAction("Delete", swipeIcon, new ColorValue("#EF4444"), () => fired = true));
        var list = new ListView<string>(sampleItems, _ => Node.Empty)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        lvn.InvokeTrailingAction(0, 0);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SwipeInvokeLeadingActionFiresHandler()
    {
        bool fired = false;
        var set = new SwipeActionSet(
            leading: new SwipeAction("Pin", swipeIcon, new ColorValue("#3B82F6"), () => fired = true));
        var list = new ListView<string>(sampleItems, _ => Node.Empty)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        lvn.InvokeLeadingAction(0, 0);

        await Assert.That(fired).IsTrue();
    }

    [Test]
    public async Task SwipeFullSwipeFlagReported()
    {
        var set = new SwipeActionSet(
            trailing: new SwipeAction("Delete", swipeIcon, new ColorValue("#EF4444"), () => { }, fullSwipe: true));
        var list = new ListView<string>(sampleItems, _ => Node.Empty)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        await Assert.That(lvn.TrailingIsFullSwipe(0)).IsTrue();
        await Assert.That(lvn.LeadingIsFullSwipe(0)).IsFalse();
    }

    [Test]
    public async Task SwipeClosedRowIsNotWrapped()
    {
        var set = new SwipeActionSet(
            trailing: new SwipeAction("Delete", swipeIcon, new ColorValue("#EF4444"), () => { }));
        // A marker row so we can tell the raw content from a swipe wrapper.
        var list = new ListView<int>(Enumerable.Range(0, 5).ToArray(), _ => new Label("row"))
            .ItemHeight(40f)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        lvn.ViewportHeight = 200f;
        lvn.ContentWidth = 300f;
        lvn.InvalidateContent();
        var content = (Column)lvn.GetContentNode();

        // Closed: each row is the plain rendered content (with a height wrapper), not a Row composite.
        await Assert.That(content.Children[0] is Row).IsFalse();
    }

    [Test]
    public async Task SwipeOpenRowIsWrappedInSlidingComposite()
    {
        var set = new SwipeActionSet(
            trailing: new SwipeAction("Delete", swipeIcon, new ColorValue("#EF4444"), () => { }));
        var list = new ListView<int>(Enumerable.Range(0, 5).ToArray(), _ => new Label("row"))
            .ItemHeight(40f)
            .ItemSwipeActions(_ => set);

        var lvn = (IListViewNode)list;
        lvn.ViewportHeight = 200f;
        lvn.ContentWidth = 300f;
        lvn.SwipeRowIndex = 2;
        lvn.SwipeOffsetX = -72f; // trailing open
        lvn.InvalidateContent();
        var content = (Column)lvn.GetContentNode();

        // Only the open row (index 2) becomes a sliding Row composite (content + button).
        await Assert.That(content.Children[2] is Row).IsTrue();
        await Assert.That(content.Children[0] is Row).IsFalse();
    }

    // ── Context menu ────────────────────────────────────────────────

    [Test]
    public async Task ItemContextMenuStoresFactory()
    {
        var list = new ListView<string>(sampleItems, item => Node.Empty)
            .ItemContextMenu(_ => []);

        var factory = list.contextMenuFactory;
        await Assert.That(factory).IsNotNull();
    }
}

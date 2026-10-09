#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Core;

/// <summary>
/// <see cref="InputDispatcher.LastScroll"/> names the view a wheel event actually scrolled, with that
/// view's own offset. The DevTools <c>scroll</c> verb reports it: it used to report the page
/// ScrollView's offset (0/0) while a DataGrid, DataTable or ListView — which own their offsets —
/// was the thing that moved.
/// </summary>
public class ScrollOutcomeTests
{
    private const float RowHeight = 30f;

    private sealed record Item(string Name);

    private static (InputDispatcher Dispatcher, DataTable<Item> Table) BuildTable(int rows)
    {
        var dispatcher = new InputDispatcher { ViewportSize = new Size(800, 600) };
        var table = new DataTable<Item>(
            Enumerable.Range(0, rows).Select(i => new Item($"item{i}")).ToList(),
            [DataColumn<Item>.Text("Name", i => i.Name)])
            .RowHeight(RowHeight);
        table.LayoutData.Bounds = new Rect(0, 0, 400, 300);
        table.LayoutData.IsVisible = true;
        ITabularDataNode tdn = table;
        tdn.AbsoluteBounds = new Rect(0, 0, 400, 300);
        tdn.ViewportHeight = 300 - TabularRowGeometry.DataTop(tdn);
        dispatcher.SetRoot(table);
        return (dispatcher, table);
    }

    private static void Wheel(InputDispatcher dispatcher, float x, float y, float notchesDown)
    {
        dispatcher.HandleScrollEvent(new NativeScrollEvent { X = x, Y = y, DeltaY = -notchesDown });
    }

    [Test]
    public async Task WheelOverATable_ReportsTheTableAndItsOwnOffset()
    {
        var (dispatcher, table) = BuildTable(rows: 40);
        ITabularDataNode tdn = table;

        Wheel(dispatcher, 100, 150, notchesDown: 2);

        var outcome = dispatcher.LastScroll;
        await Assert.That(outcome.Kind).IsEqualTo(ScrollTargetKind.Table);
        await Assert.That(outcome.Target).IsSameReferenceAs(table);
        await Assert.That(outcome.OffsetY).IsEqualTo(96f);
        await Assert.That(outcome.MaxY).IsEqualTo(tdn.MaxScrollOffsetY);
        await Assert.That(outcome.Moved).IsTrue();
    }

    [Test]
    public async Task WheelPastTheEnd_ReportsTheMaximum_AndThatNothingMoved()
    {
        var (dispatcher, table) = BuildTable(rows: 40);
        ITabularDataNode tdn = table;

        Wheel(dispatcher, 100, 150, notchesDown: 100);
        await Assert.That(dispatcher.LastScroll.OffsetY).IsEqualTo(tdn.MaxScrollOffsetY);
        await Assert.That(dispatcher.LastScroll.Moved).IsTrue();

        Wheel(dispatcher, 100, 150, notchesDown: 1);
        await Assert.That(dispatcher.LastScroll.Kind).IsEqualTo(ScrollTargetKind.Table);
        await Assert.That(dispatcher.LastScroll.Moved).IsFalse();
    }

    [Test]
    public async Task WheelOverNothingScrollable_ReportsNone()
    {
        var (dispatcher, _) = BuildTable(rows: 3);

        // Three rows fit: the table cannot scroll, and nothing else is under the point.
        Wheel(dispatcher, 100, 150, notchesDown: 1);
        await Assert.That(dispatcher.LastScroll.Kind).IsEqualTo(ScrollTargetKind.None);

        Wheel(dispatcher, 700, 500, notchesDown: 1);
        await Assert.That(dispatcher.LastScroll).IsEqualTo(ScrollOutcome.Nothing);
    }
}

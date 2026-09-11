#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DataTableTests
{
    private sealed class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public bool InStock { get; set; }
    }

    private static DataTable<Product> CreateTable(IReadOnlyList<Product>? items = null)
    {
        var data = items ?? new[]
        {
            new Product { Name = "Widget", Price = 9.99m, InStock = true },
            new Product { Name = "Gadget", Price = 24.50m, InStock = false },
        };
        var columns = new[]
        {
            DataColumn<Product>.Text("Name", p => p.Name),
            DataColumn<Product>.Number("Price", p => p.Price, format: "C2"),
            DataColumn<Product>.Bool("In Stock", p => p.InStock),
        };
        return new DataTable<Product>(data, columns);
    }

    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task ConstructorStoresItems()
    {
        var table = CreateTable();

        var count = table.Items.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ConstructorStoresColumns()
    {
        var table = CreateTable();

        var colCount = table.Columns.Count;
        await Assert.That(colCount).IsEqualTo(3);
    }

    // ── Sorting ─────────────────────────────────────────────────────

    [Test]
    public async Task SortableStoresValue()
    {
        var table = CreateTable().Sortable(true);

        var sortable = table.sortableEnabled;
        await Assert.That(sortable).IsTrue();
    }

    [Test]
    public async Task DefaultSortStoresColumnAndDirection()
    {
        var table = CreateTable().DefaultSort("Price", SortDirection.Descending);

        var col = table.defaultSortColumn;
        var dir = table.defaultSortDirection;
        await Assert.That(col).IsEqualTo("Price");
        await Assert.That(dir).IsEqualTo(SortDirection.Descending);
    }

    [Test]
    public async Task OnSortStoresCallback()
    {
        string? capturedCol = null;
        SortDirection capturedDir = SortDirection.Ascending;
        var table = CreateTable().OnSort((c, d) => { capturedCol = c; capturedDir = d; });

        table.onSortHandler!("Name", SortDirection.Descending);

        await Assert.That(capturedCol).IsEqualTo("Name");
        await Assert.That(capturedDir).IsEqualTo(SortDirection.Descending);
    }

    // ── Filtering ───────────────────────────────────────────────────

    [Test]
    public async Task FilterRowStoresValue()
    {
        var table = CreateTable().FilterRow(true);

        var enabled = table.filterRowEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task GlobalFilterStoresBinding()
    {
        string captured = "";
        var binding = new Bindable<string>("test", v => { captured = v; });
        var table = CreateTable().GlobalFilter(binding);

        var stored = table.globalFilterBinding;
        await Assert.That(stored).IsNotNull();

        var value = stored!.Value.Value;
        await Assert.That(value).IsEqualTo("test");
    }

    // ── Selection ───────────────────────────────────────────────────

    [Test]
    public async Task SelectionModeStoresValue()
    {
        var table = CreateTable().SelectionMode(SelectionMode.Multi);

        var mode = table.selectionModeValue;
        await Assert.That(mode).IsEqualTo(SelectionMode.Multi);
    }

    [Test]
    public async Task OnSelectStoresCallback()
    {
        Product? capturedProduct = null;
        var table = CreateTable().OnSelect(p => { capturedProduct = p; });

        var product = new Product { Name = "Test" };
        table.onSelectHandler!(product);

        var name = capturedProduct!.Name;
        await Assert.That(name).IsEqualTo("Test");
    }

    // ── Appearance ──────────────────────────────────────────────────

    [Test]
    public async Task RowHeightStoresValue()
    {
        var table = CreateTable().RowHeight(48f);

        var height = table.rowHeightValue;
        await Assert.That(height).IsEqualTo(48f);
    }

    [Test]
    public async Task StripedStoresValue()
    {
        var table = CreateTable().Striped(true);

        var striped = table.stripedEnabled;
        await Assert.That(striped).IsTrue();
    }

    [Test]
    public async Task HoverHighlightStoresValue()
    {
        var table = CreateTable().HoverHighlight(true);

        var hover = table.hoverHighlightEnabled;
        await Assert.That(hover).IsTrue();
    }

    [Test]
    public async Task EmptyStateStoresNode()
    {
        var table = CreateTable().EmptyState(Node.Empty);

        var empty = table.emptyStateNode;
        await Assert.That(empty).IsEqualTo(Node.Empty);
    }

    // ── Row actions ─────────────────────────────────────────────────

    [Test]
    public async Task RowContextMenuStoresFactory()
    {
        var table = CreateTable().RowContextMenu(_ => []);

        var factory = table.rowContextMenuFactory;
        await Assert.That(factory).IsNotNull();
    }

    [Test]
    public async Task RowActionsStoresFactory()
    {
        var table = CreateTable().RowActions(_ => []);

        var factory = table.rowActionsFactory;
        await Assert.That(factory).IsNotNull();
    }

    // ── Fluent chaining ─────────────────────────────────────────────

    [Test]
    public async Task FluentChainingReturnsSameInstance()
    {
        var table = CreateTable();
        var chained = table
            .Sortable(true)
            .FilterRow(true)
            .Striped(true)
            .HoverHighlight(true)
            .RowHeight(36f);

        var same = ReferenceEquals(table, chained);
        await Assert.That(same).IsTrue();
    }

    // ── DataColumn factory methods ──────────────────────────────────

    [Test]
    public async Task TextColumnSetsHeader()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name);

        var header = col.Header;
        await Assert.That(header).IsEqualTo("Name");
    }

    [Test]
    public async Task TextColumnStoresAccessor()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name);
        var product = new Product { Name = "Widget" };

        var value = col.textAccessor!(product);
        await Assert.That(value).IsEqualTo("Widget");
    }

    [Test]
    public async Task NumberColumnDefaultsToRightAlign()
    {
        var col = DataColumn<Product>.Number("Price", p => p.Price);

        var align = col.alignValue;
        await Assert.That(align).IsEqualTo(ColumnAlignment.Right);
    }

    [Test]
    public async Task NumberColumnStoresFormat()
    {
        var col = DataColumn<Product>.Number("Price", p => p.Price, format: "C2");

        var format = col.formatString;
        await Assert.That(format).IsEqualTo("C2");
    }

    [Test]
    public async Task BoolColumnDefaultsToCenterAlign()
    {
        var col = DataColumn<Product>.Bool("In Stock", p => p.InStock);

        var align = col.alignValue;
        await Assert.That(align).IsEqualTo(ColumnAlignment.Center);
    }

    [Test]
    public async Task DateColumnStoresAccessor()
    {
        var col = DataColumn<Product>.Date("Created", _ => DateTime.Now);

        var accessor = col.dateAccessor;
        await Assert.That(accessor).IsNotNull();
    }

    [Test]
    public async Task EnumColumnStoresAccessor()
    {
        var col = DataColumn<Product>.Enum("Status", _ => "Active");

        var accessor = col.enumAccessor;
        await Assert.That(accessor).IsNotNull();
    }

    [Test]
    public async Task CustomColumnStoresRenderer()
    {
        var col = DataColumn<Product>.Custom("Custom", _ => Node.Empty);

        var renderer = col.customRenderer;
        await Assert.That(renderer).IsNotNull();
    }

    // ── DataColumn fluent modifiers ─────────────────────────────────

    [Test]
    public async Task ColumnWidthFloatStoresValue()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name).Width(200f);

        var width = col.widthValue;
        await Assert.That(width).IsEqualTo(200f);
    }

    [Test]
    public async Task ColumnWidthStrategyStoresValue()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name).Width(DataColumnWidth.Fill);

        var strategy = col.widthStrategy;
        await Assert.That(strategy).IsEqualTo(DataColumnWidth.Fill);
    }

    [Test]
    public async Task ColumnMinMaxWidthStoresValues()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name)
            .MinWidth(50f)
            .MaxWidth(300f);

        var min = col.minWidthValue;
        var max = col.maxWidthValue;
        await Assert.That(min).IsEqualTo(50f);
        await Assert.That(max).IsEqualTo(300f);
    }

    [Test]
    public async Task ColumnSortableStoresValue()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name).Sortable(false);

        var sortable = col.sortableValue;
        await Assert.That(sortable).IsEqualTo(false);
    }

    [Test]
    public async Task ColumnPinnedStoresValue()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name).Pinned(ColumnPin.Left);

        var pin = col.pinValue;
        await Assert.That(pin).IsEqualTo(ColumnPin.Left);
    }

    [Test]
    public async Task ColumnTooltipStoresFactory()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name)
            .Tooltip(p => $"Product: {p.Name}");
        var product = new Product { Name = "Widget" };

        var tooltip = col.tooltipFactory!(product);
        await Assert.That(tooltip).IsEqualTo("Product: Widget");
    }

    [Test]
    public async Task ColumnFluentChainingReturnsSameInstance()
    {
        var col = DataColumn<Product>.Text("Name", p => p.Name);
        var chained = col
            .Width(100f)
            .MinWidth(50f)
            .MaxWidth(200f)
            .Sortable(true)
            .Resizable(true)
            .Align(ColumnAlignment.Center);

        var same = ReferenceEquals(col, chained);
        await Assert.That(same).IsTrue();
    }

    // ── Column sizing (CONTROLS-001) ─────────────────────────────────
    //
    // Before CONTROLS-001 every column without explicit pixels resolved to
    // availableWidth / columnCount, so Fill/MinWidth/MaxWidth compiled and did nothing.

    private static DataTable<Product> TableWithColumns(params DataColumn<Product>[] columns) =>
        new(
            [new Product { Name = "Widget", Price = 9.99m, InStock = true }],
            columns);

    [Test]
    public async Task FillColumnTakesSpaceLeftByFixedColumns()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(100f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill),
            DataColumn<Product>.Text("C", p => p.Name).Width(100f));

        var width = ((ITabularDataNode)table).GetColumnWidth(1, 500f);
        await Assert.That(width).IsEqualTo(300f);
    }

    [Test]
    public async Task MultipleFillColumnsSplitRemainingSpaceEvenly()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(100f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill),
            DataColumn<Product>.Text("C", p => p.Name).Width(DataColumnWidth.Fill));

        var tdn = (ITabularDataNode)table;
        var first = tdn.GetColumnWidth(1, 500f);
        var second = tdn.GetColumnWidth(2, 500f);

        await Assert.That(first).IsEqualTo(200f);
        await Assert.That(second).IsEqualTo(200f);
    }

    [Test]
    public async Task FillColumnRespectsMinWidth()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(380f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill).MinWidth(250f));

        var width = ((ITabularDataNode)table).GetColumnWidth(1, 500f);
        await Assert.That(width).IsEqualTo(250f);
    }

    [Test]
    public async Task FillColumnRespectsMaxWidth()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(200f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill).MaxWidth(150f));

        var width = ((ITabularDataNode)table).GetColumnWidth(1, 500f);
        await Assert.That(width).IsEqualTo(150f);
    }

    [Test]
    public async Task OverflowingFixedColumnsCollapseFillToZeroNotNegative()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(400f),
            DataColumn<Product>.Text("B", p => p.Name).Width(400f),
            DataColumn<Product>.Text("C", p => p.Name).Width(DataColumnWidth.Fill));

        var width = ((ITabularDataNode)table).GetColumnWidth(2, 500f);
        await Assert.That(width).IsEqualTo(0f);
    }

    [Test]
    public async Task FixedWidthColumnIsUnaffectedBySiblings()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(120f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill));

        var width = ((ITabularDataNode)table).GetColumnWidth(0, 500f);
        await Assert.That(width).IsEqualTo(120f);
    }

    // ── Column resize (CONTROLS-003) ─────────────────────────────────
    //
    // IsColumnResizable returned a hard false and SetColumnWidth was an empty body, so
    // DataTable columns could never be dragged however the column was configured.

    [Test]
    public async Task ColumnsAreResizableByDefault()
    {
        var table = CreateTable();

        var resizable = ((ITabularDataNode)table).IsColumnResizable(0);
        await Assert.That(resizable).IsTrue();
    }

    [Test]
    public async Task ColumnOptedOutOfResizeIsNotResizable()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Resizable(false));

        var resizable = ((ITabularDataNode)table).IsColumnResizable(0);
        await Assert.That(resizable).IsFalse();
    }

    [Test]
    public async Task SetColumnWidthChangesResolvedWidth()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(100f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill));

        var tdn = (ITabularDataNode)table;
        tdn.SetColumnWidth(0, 250f);

        var width = tdn.GetColumnWidth(0, 500f);
        await Assert.That(width).IsEqualTo(250f);
    }

    [Test]
    public async Task SetColumnWidthClampsToMinAndMax()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).MinWidth(80f).MaxWidth(300f));

        var tdn = (ITabularDataNode)table;

        tdn.SetColumnWidth(0, 10f);
        var floored = tdn.GetColumnWidth(0, 500f);

        tdn.SetColumnWidth(0, 900f);
        var capped = tdn.GetColumnWidth(0, 500f);

        await Assert.That(floored).IsEqualTo(80f);
        await Assert.That(capped).IsEqualTo(300f);
    }

    [Test]
    public async Task SetColumnWidthDoesNotMutateCallerColumn()
    {
        var column = DataColumn<Product>.Text("A", p => p.Name).Width(100f);
        var table = TableWithColumns(column);

        ((ITabularDataNode)table).SetColumnWidth(0, 250f);

        // The caller's definition must be untouched: Render() rebuilds these every pass, and a
        // hoisted static column list would otherwise leak the width into every table using it.
        await Assert.That(column.widthValue).IsEqualTo(100f);
    }

    [Test]
    public async Task ResizeIsIgnoredForOutOfRangeColumn()
    {
        var table = CreateTable();
        var tdn = (ITabularDataNode)table;

        tdn.SetColumnWidth(99, 250f);

        var width = tdn.GetColumnWidth(0, 600f);
        await Assert.That(width).IsEqualTo(200f);
    }

    // ── Interaction state across reconcile (RENDER-007) ──────────────
    //
    // Render() returns a fresh tree, so any Invalidate() replaced the table node and dropped
    // the user's selection, sort and resized widths.

    [Test]
    public async Task SelectionSurvivesReconcileAgainstSameItems()
    {
        var items = new[]
        {
            new Product { Name = "Widget" },
            new Product { Name = "Gadget" },
            new Product { Name = "Doohickey" },
        };
        var columns = new[] { DataColumn<Product>.Text("Name", p => p.Name) };

        var oldTable = (ITabularDataNode)new DataTable<Product>(items, columns);
        oldTable.SelectRow(2, ctrl: false, shift: false);

        var newTable = (ITabularDataNode)new DataTable<Product>(items, columns);
        newTable.RestoreInteractionState(oldTable.CaptureInteractionState());

        var index = newTable.SelectedRowIndex;
        var selected = newTable.IsRowSelected(2);

        await Assert.That(index).IsEqualTo(2);
        await Assert.That(selected).IsTrue();
    }

    [Test]
    public async Task SortSurvivesReconcileWithoutFlippingDirection()
    {
        var items = new[]
        {
            new Product { Name = "Widget" },
            new Product { Name = "Gadget" },
        };
        var columns = new[] { DataColumn<Product>.Text("Name", p => p.Name) };

        var oldTable = (ITabularDataNode)new DataTable<Product>(items, columns).Sortable(true);
        oldTable.ApplySort(0);
        oldTable.ApplySort(0);   // second click → descending

        var newTable = (ITabularDataNode)new DataTable<Product>(items, columns).Sortable(true);
        newTable.RestoreInteractionState(oldTable.CaptureInteractionState());

        var column = newTable.SortColumnIndex;
        var direction = newTable.SortDirectionValue;
        var firstCell = newTable.GetCellText(0, 0);

        await Assert.That(column).IsEqualTo(0);
        await Assert.That(direction).IsEqualTo(SortDirection.Descending);
        await Assert.That(firstCell).IsEqualTo("Widget");
    }

    [Test]
    public async Task SelectionPastEndOfShorterListIsDropped()
    {
        var columns = new[] { DataColumn<Product>.Text("Name", p => p.Name) };
        var longList = new[]
        {
            new Product { Name = "Widget" },
            new Product { Name = "Gadget" },
            new Product { Name = "Doohickey" },
        };

        var oldTable = (ITabularDataNode)new DataTable<Product>(longList, columns);
        oldTable.SelectRow(2, ctrl: false, shift: false);

        var shortTable = (ITabularDataNode)new DataTable<Product>(longList[..1], columns);
        shortTable.RestoreInteractionState(oldTable.CaptureInteractionState());

        var index = shortTable.SelectedRowIndex;
        await Assert.That(index).IsEqualTo(-1);
    }

    [Test]
    public async Task ResizedWidthTransfersToReplacementNode()
    {
        var items = new[] { new Product { Name = "Widget" } };

        var oldTable = (ITabularDataNode)new DataTable<Product>(
            items,
            [DataColumn<Product>.Text("A", p => p.Name), DataColumn<Product>.Text("B", p => p.Name)]);
        oldTable.SetColumnWidth(0, 275f);

        var newTable = (ITabularDataNode)new DataTable<Product>(
            items,
            [DataColumn<Product>.Text("A", p => p.Name), DataColumn<Product>.Text("B", p => p.Name)]);
        newTable.ColumnWidthOverrides = oldTable.ColumnWidthOverrides;

        var width = newTable.GetColumnWidth(0, 600f);
        await Assert.That(width).IsEqualTo(275f);
    }

    // ── Scrolling (CONTROLS-005) ─────────────────────────────────────
    //
    // Every virtualization member used to be a no-op stub, so a table taller than its box
    // clipped the overflow: no scrollbar, and the wheel did nothing because InputDispatcher
    // gates wheel handling on MaxScrollOffsetY > 0.

    private static ITabularDataNode ScrollableTable(int rowCount, float viewportHeight)
    {
        var items = new Product[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            items[i] = new Product { Name = $"Row {i}" };
        }

        var table = new DataTable<Product>(
            items,
            [DataColumn<Product>.Text("Name", p => p.Name)]).RowHeight(20f);

        var tdn = (ITabularDataNode)table;
        tdn.ViewportHeight = viewportHeight;
        return tdn;
    }

    [Test]
    public async Task TotalContentHeightIsRowCountTimesRowHeight()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);

        var height = tdn.TotalContentHeight;
        await Assert.That(height).IsEqualTo(1000f);
    }

    [Test]
    public async Task MaxScrollOffsetIsContentMinusViewport()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);

        var max = tdn.MaxScrollOffsetY;
        await Assert.That(max).IsEqualTo(800f);
    }

    [Test]
    public async Task ContentShorterThanViewportDoesNotScroll()
    {
        var tdn = ScrollableTable(rowCount: 3, viewportHeight: 200f);

        var max = tdn.MaxScrollOffsetY;
        await Assert.That(max).IsEqualTo(0f);
    }

    [Test]
    public async Task ScrollOffsetIsClampedToRange()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);

        tdn.ScrollOffsetY = 5000f;
        var clampedHigh = tdn.ScrollOffsetY;

        tdn.ScrollOffsetY = -50f;
        var clampedLow = tdn.ScrollOffsetY;

        await Assert.That(clampedHigh).IsEqualTo(800f);
        await Assert.That(clampedLow).IsEqualTo(0f);
    }

    [Test]
    public async Task ScrollIntoViewScrollsDownToReachALaterRow()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);

        // Row 30 spans 600..620; the viewport shows 0..200, so the minimum scroll that reveals
        // its bottom edge is 620 - 200.
        tdn.ScrollIntoView(30);

        var offset = tdn.ScrollOffsetY;
        await Assert.That(offset).IsEqualTo(420f);
    }

    [Test]
    public async Task ScrollIntoViewScrollsUpToReachAnEarlierRow()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);
        tdn.ScrollOffsetY = 500f;

        tdn.ScrollIntoView(5);

        var offset = tdn.ScrollOffsetY;
        await Assert.That(offset).IsEqualTo(100f);
    }

    [Test]
    public async Task ScrollIntoViewLeavesAnAlreadyVisibleRowAlone()
    {
        var tdn = ScrollableTable(rowCount: 50, viewportHeight: 200f);
        tdn.ScrollOffsetY = 400f;

        tdn.ScrollIntoView(25);   // spans 500..520, inside 400..600

        var offset = tdn.ScrollOffsetY;
        await Assert.That(offset).IsEqualTo(400f);
    }

    [Test]
    public async Task ScrollOffsetSurvivesReconcileBeforeTheViewportIsKnown()
    {
        var items = new Product[50];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Product { Name = $"Row {i}" };
        }
        var columns = new[] { DataColumn<Product>.Text("Name", p => p.Name) };

        var oldTable = (ITabularDataNode)new DataTable<Product>(items, columns).RowHeight(20f);
        oldTable.ViewportHeight = 200f;
        oldTable.ScrollOffsetY = 400f;

        // The replacement has not been painted, so its viewport is still 0. Restoring through the
        // clamping setter would drive the offset to zero and snap the table back to the top.
        var newTable = (ITabularDataNode)new DataTable<Product>(items, columns).RowHeight(20f);
        newTable.RestoreInteractionState(oldTable.CaptureInteractionState());

        var offset = newTable.ScrollOffsetY;
        await Assert.That(offset).IsEqualTo(400f);
    }

    // ── Auto columns (CONTROLS-002) ──────────────────────────────────
    //
    // Auto needs font metrics, which live in the painter, so the painter measures and publishes
    // the widths through AutoColumnWidths and the resolver reads them back. Until it has, an Auto
    // column falls back to fill rather than collapsing.

    [Test]
    public async Task AutoColumnIsFlaggedInItsSizing()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(DataColumnWidth.Auto),
            DataColumn<Product>.Text("B", p => p.Name).Width(100f));

        var sizing = ((ITabularDataNode)table).GetColumnSizing(0);

        await Assert.That(sizing.Auto).IsTrue();
        await Assert.That(((ITabularDataNode)table).HasAutoColumns).IsTrue();
    }

    [Test]
    public async Task TableWithoutAutoColumnsReportsNone()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(100f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill));

        var hasAuto = ((ITabularDataNode)table).HasAutoColumns;
        await Assert.That(hasAuto).IsFalse();
    }

    [Test]
    public async Task MeasuredAutoColumnUsesItsContentWidth()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(DataColumnWidth.Auto),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Fill));

        var tdn = (ITabularDataNode)table;
        tdn.AutoColumnWidths = [180f, null];

        var auto = tdn.GetColumnWidth(0, 500f);
        var fill = tdn.GetColumnWidth(1, 500f);

        await Assert.That(auto).IsEqualTo(180f);

        // The fill column takes what the measured Auto column left, exactly as it would for a
        // fixed-width sibling.
        await Assert.That(fill).IsEqualTo(320f);
    }

    [Test]
    public async Task MeasuredAutoColumnStillObeysMaxWidth()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(DataColumnWidth.Auto).MaxWidth(120f));

        var tdn = (ITabularDataNode)table;
        tdn.AutoColumnWidths = [400f];

        var width = tdn.GetColumnWidth(0, 500f);
        await Assert.That(width).IsEqualTo(120f);
    }

    [Test]
    public async Task UnmeasuredAutoColumnFallsBackToFill()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(100f),
            DataColumn<Product>.Text("B", p => p.Name).Width(DataColumnWidth.Auto));

        // No AutoColumnWidths yet — before the first paint. It must take the remaining space
        // rather than collapsing to nothing.
        var width = ((ITabularDataNode)table).GetColumnWidth(1, 500f);
        await Assert.That(width).IsEqualTo(400f);
    }

    // ── Selected(Bindable<T>) (CONTROLS-008) ──────────────────────────
    //
    // The binding was stored and never read in either direction: a selection set in code could
    // not be drawn, and a click never reached the bound field. The painter now syncs from it
    // before drawing rows, and SelectRow pushes through it.

    private static Product[] ThreeProducts() =>
    [
        new() { Name = "Widget" },
        new() { Name = "Gadget" },
        new() { Name = "Doohickey" },
    ];

    [Test]
    public async Task BoundValueBecomesTheSelectionOnSync()
    {
        var items = ThreeProducts();
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .Selected(new Bindable<Product>(items[2], _ => { }));
        var tdn = (ITabularDataNode)table;

        tdn.SyncSelectionFromBinding();

        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(2);
        await Assert.That(tdn.IsRowSelected(2)).IsTrue();
    }

    [Test]
    public async Task ClickPushesTheItemThroughTheBinding()
    {
        var items = ThreeProducts();
        Product? received = null;
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .Selected(new Bindable<Product>(items[0], v => received = v));

        ((ITabularDataNode)table).SelectRow(1, ctrl: false, shift: false);

        await Assert.That(received).IsSameReferenceAs(items[1]);
    }

    [Test]
    public async Task NullBoundValueClearsTheSelection()
    {
        var items = ThreeProducts();
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .Selected(new Bindable<Product>(null!, _ => { }));
        var tdn = (ITabularDataNode)table;
        tdn.SelectRow(1, ctrl: false, shift: false);

        tdn.SyncSelectionFromBinding();

        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(-1);
        await Assert.That(tdn.IsRowSelected(1)).IsFalse();
    }

    [Test]
    public async Task BoundValueNotInTheListLeavesTheSelectionAlone()
    {
        var items = ThreeProducts();
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .Selected(new Bindable<Product>(new Product { Name = "Elsewhere" }, _ => { }));
        var tdn = (ITabularDataNode)table;
        tdn.SelectRow(1, ctrl: false, shift: false);

        tdn.SyncSelectionFromBinding();

        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(1);
    }

    [Test]
    public async Task BoundValueMapsThroughTheSortOrder()
    {
        var items = ThreeProducts();   // Widget, Gadget, Doohickey
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .Sortable(true)
            .Selected(new Bindable<Product>(items[0], _ => { }));   // Widget
        var tdn = (ITabularDataNode)table;
        tdn.ApplySort(0);   // ascending: Doohickey, Gadget, Widget

        tdn.SyncSelectionFromBinding();

        // Widget is data index 0 but display row 2 once sorted.
        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(2);
        await Assert.That(tdn.GetCellText(tdn.SelectedRowIndex, 0)).IsEqualTo("Widget");
    }

    [Test]
    public async Task SyncLeavesAMultiSelectionAloneWhenItsAnchorMatches()
    {
        var items = ThreeProducts();
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)])
            .SelectionMode(SelectionMode.Multi)
            .Selected(new Bindable<Product>(items[2], _ => { }));
        var tdn = (ITabularDataNode)table;
        tdn.SelectRow(0, ctrl: false, shift: false);
        tdn.SelectRow(2, ctrl: true, shift: false);   // anchor now 2, rows {0, 2}

        tdn.SyncSelectionFromBinding();

        await Assert.That(tdn.IsRowSelected(0)).IsTrue();
        await Assert.That(tdn.IsRowSelected(2)).IsTrue();
    }

    [Test]
    public async Task BindingWinsOverARestoredSelectionThatDisagrees()
    {
        // RENDER-007 carries the old node's selection across; CONTROLS-008 then applies the
        // binding. When they disagree the binding is the caller's stated intent and must win.
        var items = ThreeProducts();
        var columns = new[] { DataColumn<Product>.Text("Name", p => p.Name) };

        var oldTable = (ITabularDataNode)new DataTable<Product>(items, columns);
        oldTable.SelectRow(1, ctrl: false, shift: false);

        var newTable = (ITabularDataNode)new DataTable<Product>(items, columns)
            .Selected(new Bindable<Product>(items[2], _ => { }));
        newTable.RestoreInteractionState(oldTable.CaptureInteractionState());
        newTable.SyncSelectionFromBinding();

        await Assert.That(newTable.SelectedRowIndex).IsEqualTo(2);
    }

    [Test]
    public async Task NoBindingMeansSyncIsANoOp()
    {
        var items = ThreeProducts();
        var table = new DataTable<Product>(items, [DataColumn<Product>.Text("Name", p => p.Name)]);
        var tdn = (ITabularDataNode)table;
        tdn.SelectRow(1, ctrl: false, shift: false);

        tdn.SyncSelectionFromBinding();

        await Assert.That(tdn.SelectedRowIndex).IsEqualTo(1);
    }

    [Test]
    public async Task StaleAutoWidthsForADifferentColumnCountAreIgnored()
    {
        var table = TableWithColumns(
            DataColumn<Product>.Text("A", p => p.Name).Width(DataColumnWidth.Auto),
            DataColumn<Product>.Text("B", p => p.Name).Width(100f));

        var tdn = (ITabularDataNode)table;
        tdn.AutoColumnWidths = [180f];   // three columns' worth of nothing — wrong shape

        var width = tdn.GetColumnWidth(0, 500f);
        await Assert.That(width).IsEqualTo(400f);   // falls back to fill
    }
}

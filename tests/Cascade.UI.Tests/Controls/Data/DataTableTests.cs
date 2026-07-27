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
}

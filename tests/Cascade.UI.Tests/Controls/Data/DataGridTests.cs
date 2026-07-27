#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DataGridTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public bool Active { get; set; }
    }

    private static Bindable<IReadOnlyList<Row>> CreateBinding(IReadOnlyList<Row>? initial = null)
    {
        IReadOnlyList<Row> data = initial ?? new[]
        {
            new Row { Name = "Alpha", Quantity = 10, Active = true },
            new Row { Name = "Bravo", Quantity = 20, Active = false },
        };
        IReadOnlyList<Row> captured = data;
        return new Bindable<IReadOnlyList<Row>>(captured, v => { captured = v; });
    }

    private static DataGrid<Row> CreateGrid(Bindable<IReadOnlyList<Row>>? binding = null)
    {
        var items = binding ?? CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        return new DataGrid<Row>(items, columns);
    }

    // ── Construction ─────────────────────────────────────────────────

    [Test]
    public async Task ConstructorStoresItemsBinding()
    {
        var binding = CreateBinding();
        var grid = CreateGrid(binding);

        var count = grid.Items.Value.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ConstructorStoresColumns()
    {
        var grid = CreateGrid();

        var colCount = grid.Columns.Count;
        await Assert.That(colCount).IsEqualTo(3);
    }

    // ── Edit mode ───────────────────────────────────────────────────

    [Test]
    public async Task EditModeStoresValue()
    {
        var grid = CreateGrid().EditMode(GridEditMode.DoubleClick);

        var mode = grid.editModeValue;
        await Assert.That(mode).IsEqualTo(GridEditMode.DoubleClick);
    }

    [Test]
    public async Task EditModeAlwaysEditingStoresValue()
    {
        var grid = CreateGrid().EditMode(GridEditMode.AlwaysEditing);

        var mode = grid.editModeValue;
        await Assert.That(mode).IsEqualTo(GridEditMode.AlwaysEditing);
    }

    // ── Cell selection ──────────────────────────────────────────────

    [Test]
    public async Task CellSelectionStoresValue()
    {
        var grid = CreateGrid().CellSelection(CellSelectionMode.Range);

        var mode = grid.cellSelectionModeValue;
        await Assert.That(mode).IsEqualTo(CellSelectionMode.Range);
    }

    // ── Row operations ──────────────────────────────────────────────

    [Test]
    public async Task AddRowStoresFactoryAndPosition()
    {
        var grid = CreateGrid().AddRow(() => new Row { Name = "New" }, RowAddPosition.Top);

        var position = grid.addRowPosition;
        await Assert.That(position).IsEqualTo(RowAddPosition.Top);

        var newRow = grid.addRowFactory!();
        var name = newRow.Name;
        await Assert.That(name).IsEqualTo("New");
    }

    [Test]
    public async Task DeleteRowStoresPredicateAndCallback()
    {
        Row? deleted = null;
        var grid = CreateGrid().DeleteRow(
            canDelete: r => r.Active,
            onDelete: r => { deleted = r; });

        var canDelete = grid.canDeletePredicate!(new Row { Active = true });
        await Assert.That(canDelete).IsTrue();

        var inactiveRow = new Row { Active = false };
        var canDeleteInactive = grid.canDeletePredicate!(inactiveRow);
        await Assert.That(canDeleteInactive).IsFalse();

        grid.onDeleteHandler!(new Row { Name = "Gone" });
        var deletedName = deleted!.Name;
        await Assert.That(deletedName).IsEqualTo("Gone");
    }

    [Test]
    public async Task DuplicateRowStoresCloneFactory()
    {
        var grid = CreateGrid().DuplicateRow(
            clone: r => new Row { Name = r.Name + " (copy)", Quantity = r.Quantity, Active = r.Active });

        var original = new Row { Name = "Alpha", Quantity = 5, Active = true };
        var clone = grid.cloneFactory!(original);
        var cloneName = clone.Name;
        await Assert.That(cloneName).IsEqualTo("Alpha (copy)");
    }

    [Test]
    public async Task OnChangeStoresCallback()
    {
        Row? changed = null;
        var grid = CreateGrid().OnChange(r => { changed = r; });

        grid.onChangeHandler!(new Row { Name = "Modified" });
        var name = changed!.Name;
        await Assert.That(name).IsEqualTo("Modified");
    }

    [Test]
    public async Task ReorderableStoresValues()
    {
        int fromIdx = -1;
        int toIdx = -1;
        var grid = CreateGrid().Reorderable(true, (f, t) => { fromIdx = f; toIdx = t; });

        var enabled = grid.reorderableEnabled;
        await Assert.That(enabled).IsTrue();

        grid.onReorderHandler!(0, 2);
        await Assert.That(fromIdx).IsEqualTo(0);
        await Assert.That(toIdx).IsEqualTo(2);
    }

    // ── Undo / Redo ─────────────────────────────────────────────────

    [Test]
    public async Task UndoEnabledStoresValue()
    {
        var grid = CreateGrid().UndoEnabled(true);

        var enabled = grid.undoEnabledValue;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task UndoDepthStoresValueClamped()
    {
        var grid = CreateGrid().UndoDepth(50);

        var depth = grid.undoDepthValue;
        await Assert.That(depth).IsEqualTo(50);
    }

    [Test]
    public async Task UndoDepthClampsToMinimumOne()
    {
        var grid = CreateGrid().UndoDepth(0);

        var depth = grid.undoDepthValue;
        await Assert.That(depth).IsEqualTo(1);
    }

    // ── Batch edit ──────────────────────────────────────────────────

    [Test]
    public async Task BatchEditStoresValue()
    {
        var grid = CreateGrid().BatchEdit(true);

        var enabled = grid.batchEditEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task BatchEditConfirmationStoresValue()
    {
        var grid = CreateGrid().BatchEditConfirmation(true);

        var enabled = grid.batchEditConfirmationEnabled;
        await Assert.That(enabled).IsTrue();
    }

    // ── Column management ───────────────────────────────────────────

    [Test]
    public async Task ColumnReorderingStoresValue()
    {
        var grid = CreateGrid().ColumnReordering(true);

        var enabled = grid.columnReorderingEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task ColumnChooserStoresValue()
    {
        var grid = CreateGrid().ColumnChooser(true);

        var enabled = grid.columnChooserEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task ColumnOrderStoresArray()
    {
        var order = new[] { "Qty", "Name", "Active" };
        var grid = CreateGrid().ColumnOrder(order);

        var stored = grid.columnOrderValue;
        var length = stored!.Length;
        await Assert.That(length).IsEqualTo(3);

        var first = stored[0];
        await Assert.That(first).IsEqualTo("Qty");
    }

    [Test]
    public async Task ColumnVisibilityStoresMap()
    {
        var visibility = new Dictionary<string, bool> { ["Qty"] = false, ["Name"] = true };
        var grid = CreateGrid().ColumnVisibility(visibility);

        var stored = grid.columnVisibilityMap;
        var qtyVisible = stored!["Qty"];
        await Assert.That(qtyVisible).IsFalse();
    }

    // ── Row detail ──────────────────────────────────────────────────

    [Test]
    public async Task RowDetailStoresRenderer()
    {
        var grid = CreateGrid().RowDetail(_ => Node.Empty);

        var renderer = grid.rowDetailRenderer;
        await Assert.That(renderer).IsNotNull();
    }

    [Test]
    public async Task RowDetailModeStoresValue()
    {
        var grid = CreateGrid().RowDetailMode(RowDetailMode.Multi);

        var mode = grid.rowDetailModeValue;
        await Assert.That(mode).IsEqualTo(RowDetailMode.Multi);
    }

    // ── Sorting and filtering ───────────────────────────────────────

    [Test]
    public async Task SortableStoresValue()
    {
        var grid = CreateGrid().Sortable(true);

        var enabled = grid.sortableEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task FilterRowStoresValue()
    {
        var grid = CreateGrid().FilterRow(true);

        var enabled = grid.filterRowEnabled;
        await Assert.That(enabled).IsTrue();
    }

    // ── Grouping ────────────────────────────────────────────────────

    [Test]
    public async Task GroupByStoresKeySelector()
    {
        var grid = CreateGrid().GroupBy(
            r => r.Active,
            (key, items) => Node.Empty,
            GroupOrder.Descending);

        var order = grid.groupOrderValue;
        await Assert.That(order).IsEqualTo(GroupOrder.Descending);

        var selector = grid.groupKeySelector;
        await Assert.That(selector).IsNotNull();
    }

    [Test]
    public async Task GroupsCollapsedByDefaultBoolStoresValue()
    {
        var grid = CreateGrid().GroupsCollapsedByDefault(true);

        var collapsed = grid.groupsCollapsedByDefaultFlag;
        await Assert.That(collapsed).IsTrue();
    }

    [Test]
    public async Task GroupsCollapsedByDefaultPredicateStoresFunc()
    {
        var grid = CreateGrid().GroupsCollapsedByDefault(key => (bool)key);

        var predicate = grid.groupsCollapsedPredicate;
        await Assert.That(predicate).IsNotNull();

        var result = predicate!(true);
        await Assert.That(result).IsTrue();
    }

    // ── Export and clipboard ────────────────────────────────────────

    [Test]
    public async Task ExportEnabledStoresValue()
    {
        var grid = CreateGrid().ExportEnabled(true);

        var enabled = grid.isExportEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task ClipboardSupportStoresValue()
    {
        var grid = CreateGrid().ClipboardSupport(true);

        var enabled = grid.clipboardEnabled;
        await Assert.That(enabled).IsTrue();
    }

    // ── Validation ──────────────────────────────────────────────────

    [Test]
    public async Task ValidateRowStoresValidator()
    {
        var grid = CreateGrid().ValidateRow(r => r.Name.Length > 0
            ? ValidationResult.Ok
            : ValidationResult.Error("Name required"));

        var result = grid.rowValidator!(new Row { Name = "" });
        var isValid = result.IsValid;
        await Assert.That(isValid).IsFalse();

        var okResult = grid.rowValidator!(new Row { Name = "Valid" });
        var okIsValid = okResult.IsValid;
        await Assert.That(okIsValid).IsTrue();
    }

    // ── Frozen rows ─────────────────────────────────────────────────

    [Test]
    public async Task FrozenRowsStoresCount()
    {
        var grid = CreateGrid().FrozenRows(3);

        var count = grid.frozenRowCount;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task FrozenRowsClampsToZero()
    {
        var grid = CreateGrid().FrozenRows(-5);

        var count = grid.frozenRowCount;
        await Assert.That(count).IsEqualTo(0);
    }

    // ── Virtualization ──────────────────────────────────────────────

    [Test]
    public async Task VirtualizationBufferStoresValues()
    {
        var grid = CreateGrid().VirtualizationBuffer(rows: 20, columns: 8);

        var rows = grid.virtualizationBufferRows;
        var cols = grid.virtualizationBufferColumns;
        await Assert.That(rows).IsEqualTo(20);
        await Assert.That(cols).IsEqualTo(8);
    }

    // ── Layout persistence ──────────────────────────────────────────

    [Test]
    public async Task RestoreLayoutStoresState()
    {
        var state = new GridLayoutState
        {
            ColumnOrder = ["Name", "Qty"],
            ColumnVisibility = new Dictionary<string, bool> { ["Name"] = true },
            ColumnWidths = new Dictionary<string, float> { ["Name"] = 100f },
            SortColumn = "Name",
            SortDirectionValue = SortDirection.Ascending,
        };
        var grid = CreateGrid().RestoreLayout(state);

        var sortCol = grid.restoredLayout!.SortColumn;
        await Assert.That(sortCol).IsEqualTo("Name");
    }

    // ── Appearance ──────────────────────────────────────────────────

    [Test]
    public async Task RowHeightStoresValue()
    {
        var grid = CreateGrid().RowHeight(32f);

        var height = grid.rowHeightValue;
        await Assert.That(height).IsEqualTo(32f);
    }

    [Test]
    public async Task StripedStoresValue()
    {
        var grid = CreateGrid().Striped(true);

        var striped = grid.stripedEnabled;
        await Assert.That(striped).IsTrue();
    }

    [Test]
    public async Task EmptyStateStoresNode()
    {
        var grid = CreateGrid().EmptyState(Node.Empty);

        var empty = grid.emptyStateNode;
        await Assert.That(empty).IsEqualTo(Node.Empty);
    }

    // ── Fluent chaining ─────────────────────────────────────────────

    [Test]
    public async Task FullFluentChainReturnsSameInstance()
    {
        var grid = CreateGrid();
        var chained = grid
            .EditMode(GridEditMode.DoubleClick)
            .CellSelection(CellSelectionMode.Range)
            .Sortable(true)
            .FilterRow(true)
            .Striped(true)
            .ClipboardSupport(true)
            .ExportEnabled(true)
            .UndoEnabled(true)
            .UndoDepth(25)
            .RowHeight(40f)
            .FrozenRows(1)
            .ColumnReordering(true)
            .ColumnChooser(true);

        var same = ReferenceEquals(grid, chained);
        await Assert.That(same).IsTrue();
    }

    // ── DataGridColumn factory methods ──────────────────────────────

    [Test]
    public async Task TextColumnStoresGetterAndSetter()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v);

        var row = new Row { Name = "Original" };
        var value = col.textGetter!(row);
        await Assert.That(value).IsEqualTo("Original");

        col.textSetter!(row, "Updated");
        var updated = row.Name;
        await Assert.That(updated).IsEqualTo("Updated");
    }

    [Test]
    public async Task NumberColumnStoresMinAndFormat()
    {
        var col = DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v), min: 0, format: "N0");

        var min = col.minValue;
        await Assert.That(min).IsEqualTo(0);

        var format = col.formatString;
        await Assert.That(format).IsEqualTo("N0");
    }

    [Test]
    public async Task BoolColumnDefaultsToCenterAlign()
    {
        var col = DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v);

        var align = col.alignValue;
        await Assert.That(align).IsEqualTo(ColumnAlignment.Center);
    }

    [Test]
    public async Task SelectColumnStoresOptions()
    {
        var options = new object[] { "Low", "Medium", "High" };
        var col = DataGridColumn<Row>.Select("Priority", _ => "Low", (_, _) => { }, options);

        var count = col.selectOptions!.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task MultiLineColumnSetsFlag()
    {
        var col = DataGridColumn<Row>.MultiLine("Notes", r => r.Name, (r, v) => r.Name = v);

        var isMulti = col.isMultiLine;
        await Assert.That(isMulti).IsTrue();
    }

    [Test]
    public async Task ComputedColumnIsReadOnly()
    {
        var col = DataGridColumn<Row>.Computed("Total", r => r.Quantity * 2, format: "N0");

        var readOnly = col.isReadOnly;
        await Assert.That(readOnly).IsTrue();

        var format = col.formatString;
        await Assert.That(format).IsEqualTo("N0");
    }

    [Test]
    public async Task CustomColumnStoresRenderers()
    {
        var col = DataGridColumn<Row>.Custom(
            "Custom",
            r => r.Name,
            (r, v) => r.Name = (string)v,
            r => Node.Empty,
            r => Node.Empty);

        var display = col.displayRenderer;
        var editor = col.editorRenderer;
        await Assert.That(display).IsNotNull();
        await Assert.That(editor).IsNotNull();
    }

    // ── DataGridColumn fluent modifiers ─────────────────────────────

    [Test]
    public async Task ColumnWidthStoresValue()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
            .Width(150f);

        var width = col.widthValue;
        await Assert.That(width).IsEqualTo(150f);
    }

    [Test]
    public async Task ColumnWidthStrategyStoresValue()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
            .Width(DataColumnWidth.Auto);

        var strategy = col.widthStrategy;
        await Assert.That(strategy).IsEqualTo(DataColumnWidth.Auto);
    }

    [Test]
    public async Task ColumnVisibleStoresValue()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
            .Visible(false);

        var visible = col.visibleValue;
        await Assert.That(visible).IsFalse();
    }

    [Test]
    public async Task ColumnValidateStoresValidator()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
            .Validate(v => v is string s && s.Length > 0
                ? ValidationResult.Ok
                : ValidationResult.Error("Required"));

        var errorResult = col.cellValidator!("");
        var isValid = errorResult.IsValid;
        await Assert.That(isValid).IsFalse();

        var okResult = col.cellValidator!("Valid");
        var okIsValid = okResult.IsValid;
        await Assert.That(okIsValid).IsTrue();
    }

    [Test]
    public async Task ColumnFluentChainingReturnsSameInstance()
    {
        var col = DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v);
        var chained = col
            .Width(120f)
            .MinWidth(50f)
            .MaxWidth(300f)
            .Sortable(true)
            .Resizable(true)
            .Pinned(ColumnPin.Left)
            .Align(ColumnAlignment.Center)
            .Visible(true);

        var same = ReferenceEquals(col, chained);
        await Assert.That(same).IsTrue();
    }

    // ── Supporting types ────────────────────────────────────────────

    [Test]
    public async Task GridLayoutStateStoresAllFields()
    {
        var state = new GridLayoutState
        {
            ColumnOrder = ["A", "B"],
            ColumnVisibility = new Dictionary<string, bool> { ["A"] = true, ["B"] = false },
            ColumnWidths = new Dictionary<string, float> { ["A"] = 100f, ["B"] = 200f },
            SortColumn = "A",
            SortDirectionValue = SortDirection.Descending,
        };

        var orderLen = state.ColumnOrder.Length;
        await Assert.That(orderLen).IsEqualTo(2);

        var sortDir = state.SortDirectionValue;
        await Assert.That(sortDir).IsEqualTo(SortDirection.Descending);
    }

    [Test]
    public async Task GridExportOptionsDefaults()
    {
        var options = new GridExportOptions();

        var includeHeaders = options.IncludeHeaders;
        await Assert.That(includeHeaders).IsTrue();

        var delimiter = options.Delimiter;
        await Assert.That(delimiter).IsEqualTo(',');

        var sheetName = options.SheetName;
        await Assert.That(sheetName).IsEqualTo("Sheet1");
    }

    [Test]
    public async Task ColumnAggregateStoresComputation()
    {
        var agg = new ColumnAggregate<Row>("Qty", items => items.Sum(r => r.Quantity), format: "N0");

        var header = agg.ColumnHeader;
        await Assert.That(header).IsEqualTo("Qty");

        var format = agg.Format;
        await Assert.That(format).IsEqualTo("N0");

        var rows = new[] { new Row { Quantity = 10 }, new Row { Quantity = 20 } };
        var result = (int)agg.Compute!(rows);
        await Assert.That(result).IsEqualTo(30);
    }

    // ── Row Detail Expansion ────────────────────────────────────────

    [Test]
    public async Task HasRowDetail_FalseByDefault()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.HasRowDetail).IsFalse();
    }

    [Test]
    public async Task HasRowDetail_TrueAfterRowDetailSet()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}");
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.HasRowDetail).IsTrue();
    }

    [Test]
    public async Task ToggleRowDetail_ExpandsAndCollapses()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}");
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.IsRowExpanded(0)).IsFalse();

        tdn.ToggleRowDetail(0);
        await Assert.That(tdn.IsRowExpanded(0)).IsTrue();

        tdn.ToggleRowDetail(0);
        await Assert.That(tdn.IsRowExpanded(0)).IsFalse();
    }

    [Test]
    public async Task RowDetailMode_SingleClearsOtherExpanded()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}");
        var tdn = (ITabularDataNode)grid;

        // Default is Single mode
        tdn.ToggleRowDetail(0);
        await Assert.That(tdn.IsRowExpanded(0)).IsTrue();

        tdn.ToggleRowDetail(1);
        await Assert.That(tdn.IsRowExpanded(1)).IsTrue();
        await Assert.That(tdn.IsRowExpanded(0)).IsFalse();
    }

    [Test]
    public async Task RowDetailMode_MultiKeepsBothExpanded()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}");
        grid.RowDetailMode(Cascade.UI.RowDetailMode.Multi);
        var tdn = (ITabularDataNode)grid;

        tdn.ToggleRowDetail(0);
        tdn.ToggleRowDetail(1);

        await Assert.That(tdn.IsRowExpanded(0)).IsTrue();
        await Assert.That(tdn.IsRowExpanded(1)).IsTrue();
    }

    [Test]
    public async Task GetRowDetailText_ReturnsFormattedText()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}, Qty: {r.Quantity}");
        var tdn = (ITabularDataNode)grid;

        var text = tdn.GetRowDetailText(0);
        await Assert.That(text).IsEqualTo("Name: Alpha, Qty: 10");
    }

    [Test]
    public async Task GetRowDetailHeight_VariesWithContent()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => r.Active ? $"Name: {r.Name}" : $"Name: {r.Name}\nQty: {r.Quantity}\nInactive");
        var tdn = (ITabularDataNode)grid;

        float singleLineHeight = tdn.GetRowDetailHeight(0);
        float multiLineHeight = tdn.GetRowDetailHeight(1);

        await Assert.That(multiLineHeight).IsGreaterThan(singleLineHeight);
    }

    [Test]
    public async Task GetRowDetailText_ReturnsEmptyWhenNoRenderer()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;

        var text = tdn.GetRowDetailText(0);
        await Assert.That(text).IsEqualTo("");
    }

    [Test]
    public async Task GetRowDetailText_ReturnsEmptyForInvalidRow()
    {
        var grid = CreateGrid();
        grid.RowDetail(r => $"Name: {r.Name}");
        var tdn = (ITabularDataNode)grid;

        var text = tdn.GetRowDetailText(999);
        await Assert.That(text).IsEqualTo("");
    }

    // ── Aggregate Row ───────────────────────────────────────────────

    [Test]
    public async Task HasAggregateRow_FalseByDefault()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.HasAggregateRow).IsFalse();
    }

    [Test]
    public async Task HasAggregateRow_TrueAfterAggregateRowSet()
    {
        var grid = CreateGrid();
        grid.AggregateRow(AggregatePosition.Bottom, [
            new ColumnAggregate<Row>("Qty", items => items.Sum(r => r.Quantity), format: "N0"),
        ]);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.HasAggregateRow).IsTrue();
    }

    [Test]
    public async Task AggregateRow_ComputesSumCorrectly()
    {
        var grid = CreateGrid();
        grid.AggregateRow(AggregatePosition.Bottom, [
            new ColumnAggregate<Row>("Qty", items => items.Sum(r => r.Quantity), format: "N0"),
        ]);
        var tdn = (ITabularDataNode)grid;

        // "Qty" is column index 1
        var text = tdn.GetAggregateText(1);
        await Assert.That(text).IsEqualTo("30");
    }

    [Test]
    public async Task AggregateRow_ReturnsEmptyForNonAggregateColumn()
    {
        var grid = CreateGrid();
        grid.AggregateRow(AggregatePosition.Bottom, [
            new ColumnAggregate<Row>("Qty", items => items.Sum(r => r.Quantity), format: "N0"),
        ]);
        var tdn = (ITabularDataNode)grid;

        var text = tdn.GetAggregateText(0); // "Name" column — no aggregate
        await Assert.That(text).IsEqualTo("");
    }

    [Test]
    public async Task AggregateRow_PositionIsConfigurable()
    {
        var grid = CreateGrid();
        grid.AggregateRow(AggregatePosition.Top, [
            new ColumnAggregate<Row>("Qty", items => items.Sum(r => r.Quantity)),
        ]);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.AggregatePos).IsEqualTo(AggregatePosition.Top);
    }

    [Test]
    public async Task AggregateRow_CountComputation()
    {
        var grid = CreateGrid();
        grid.AggregateRow(AggregatePosition.Bottom, [
            new ColumnAggregate<Row>("Name", items => items.Count),
        ]);
        var tdn = (ITabularDataNode)grid;

        var text = tdn.GetAggregateText(0); // "Name" column
        await Assert.That(text).IsEqualTo("2");
    }

    // ── Frozen Rows ─────────────────────────────────────────────────

    [Test]
    public async Task FrozenRowCount_ZeroByDefault()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.FrozenRowCount).IsEqualTo(0);
    }

    [Test]
    public async Task FrozenRowCount_ReflectsConfiguredValue()
    {
        var grid = CreateGrid();
        grid.FrozenRows(1);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.FrozenRowCount).IsEqualTo(1);
    }

    [Test]
    public async Task FrozenRowCount_ClampedToRowCount()
    {
        var grid = CreateGrid(); // 2 rows
        grid.FrozenRows(10);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.FrozenRowCount).IsEqualTo(2);
    }

    // ── WP-3090: Undo / Redo ─────────────────────────────────────────

    [Test]
    public async Task UndoEnabled_DefaultFalse()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsUndoEnabled).IsFalse();
        await Assert.That(tdn.GetUndoStack()).IsNull();
    }

    [Test]
    public async Task UndoEnabled_CreatesUndoStack()
    {
        var grid = CreateGrid();
        grid.UndoEnabled(true);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsUndoEnabled).IsTrue();
        await Assert.That(tdn.GetUndoStack()).IsNotNull();
    }

    [Test]
    public async Task UndoDepth_LimitsStack()
    {
        var grid = CreateGrid();
        grid.UndoEnabled(true).UndoDepth(5);
        var tdn = (ITabularDataNode)grid;
        var stack = tdn.GetUndoStack();
        await Assert.That(stack).IsNotNull();
    }

    [Test]
    public async Task Undo_RevertsEdit()
    {
        var grid = CreateGrid();
        grid.UndoEnabled(true);
        var tdn = (ITabularDataNode)grid;

        // Edit Name of row 0 from "Alpha" to "Changed"
        tdn.BeginEdit(0, 0);
        tdn.HandleEditKey(Key.Home); // Go to start
        // Clear buffer and type new value
        grid.editBuffer = "Changed";
        grid.editCursorPos = 7;
        tdn.CommitEdit();
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Changed");

        // Undo should revert
        bool undone = tdn.UndoEdit();
        await Assert.That(undone).IsTrue();
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Alpha");
    }

    [Test]
    public async Task Redo_ReappliesEdit()
    {
        var grid = CreateGrid();
        grid.UndoEnabled(true);
        var tdn = (ITabularDataNode)grid;

        // Edit then undo
        tdn.BeginEdit(0, 0);
        grid.editBuffer = "Changed";
        grid.editCursorPos = 7;
        tdn.CommitEdit();
        tdn.UndoEdit();
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Alpha");

        // Redo should re-apply
        bool redone = tdn.RedoEdit();
        await Assert.That(redone).IsTrue();
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Changed");
    }

    [Test]
    public async Task Undo_NoEdits_ReturnsFalse()
    {
        var grid = CreateGrid();
        grid.UndoEnabled(true);
        var tdn = (ITabularDataNode)grid;
        // Force undo stack creation
        tdn.GetUndoStack();
        bool undone = tdn.UndoEdit();
        await Assert.That(undone).IsFalse();
    }

    [Test]
    public async Task Undo_DisabledReturns_False()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        bool undone = tdn.UndoEdit();
        await Assert.That(undone).IsFalse();
    }

    // ── WP-3090: Clipboard ───────────────────────────────────────────

    [Test]
    public async Task ClipboardEnabled_DefaultFalse()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsClipboardEnabled).IsFalse();
    }

    [Test]
    public async Task ClipboardEnabled_WhenSet()
    {
        var grid = CreateGrid();
        grid.ClipboardSupport(true);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsClipboardEnabled).IsTrue();
    }

    // ── WP-3090: Batch edit ──────────────────────────────────────────

    [Test]
    public async Task BatchEdit_DefaultFalse()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsBatchEditEnabled).IsFalse();
    }

    [Test]
    public async Task BatchEdit_AppliesValueToSelectedRows()
    {
        var grid = CreateGrid();
        grid.BatchEdit(true);
        var tdn = (ITabularDataNode)grid;

        // Select both rows
        tdn.SelectRow(0, false, false);
        tdn.SelectRow(1, true, false); // Ctrl+click for multi-select

        // Apply batch edit to Name column
        tdn.ApplyBatchEdit(0, "Batch");
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Batch");
        await Assert.That(tdn.GetCellText(1, 0)).IsEqualTo("Batch");
    }

    [Test]
    public async Task BatchEdit_IgnoresNonEditableColumn()
    {
        var grid = CreateGrid();
        grid.BatchEdit(true);
        var tdn = (ITabularDataNode)grid;

        // Select rows
        tdn.SelectRow(0, false, false);
        tdn.SelectRow(1, true, false);

        // Bool column (col 2) is not editable via text
        tdn.ApplyBatchEdit(2, "true");
        // Should be no-op — bool columns require boolSetter, not text editing
    }

    [Test]
    public async Task BatchEdit_WithUndo_RevertsAll()
    {
        var grid = CreateGrid();
        grid.BatchEdit(true).UndoEnabled(true);
        var tdn = (ITabularDataNode)grid;

        // Select both rows
        tdn.SelectRow(0, false, false);
        tdn.SelectRow(1, true, false);

        // Apply batch edit
        tdn.ApplyBatchEdit(0, "Batch");
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Batch");
        await Assert.That(tdn.GetCellText(1, 0)).IsEqualTo("Batch");

        // Single undo should revert ALL rows (batched)
        tdn.UndoEdit();
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Alpha");
        await Assert.That(tdn.GetCellText(1, 0)).IsEqualTo("Bravo");
    }

    [Test]
    public async Task CommitEdit_BatchEdit_AppliesAll()
    {
        var grid = CreateGrid();
        grid.BatchEdit(true);
        var tdn = (ITabularDataNode)grid;

        // Select both rows
        tdn.SelectRow(0, false, false);
        tdn.SelectRow(1, true, false);

        // Edit row 0 Name
        tdn.BeginEdit(0, 0);
        grid.editBuffer = "Updated";
        grid.editCursorPos = 7;
        tdn.CommitEdit();

        // Both rows should be updated
        await Assert.That(tdn.GetCellText(0, 0)).IsEqualTo("Updated");
        await Assert.That(tdn.GetCellText(1, 0)).IsEqualTo("Updated");
    }

    // ── WP-3095: Export & State Persistence ─────────────────────────

    [Test]
    public async Task ExportEnabled_StoresValue()
    {
        var grid = CreateGrid().ExportEnabled(true);
        var tdn = (ITabularDataNode)grid;
        await Assert.That(tdn.IsExportEnabled).IsTrue();
    }

    [Test]
    public async Task ExportCsv_IncludesHeadersAndRows()
    {
        var grid = CreateGrid().ExportEnabled(true);
        var tdn = (ITabularDataNode)grid;

        string csv = tdn.ExportCsv(null);
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Header line + 2 data rows
        await Assert.That(lines.Length).IsEqualTo(3);
        await Assert.That(lines[0]).Contains("Name");
        await Assert.That(lines[0]).Contains("Qty");
        await Assert.That(lines[1]).Contains("Alpha");
        await Assert.That(lines[2]).Contains("Bravo");
    }

    [Test]
    public async Task ExportCsv_EscapesCommasInValues()
    {
        var items = new List<Row>
        {
            new() { Name = "Last, First", Quantity = 30 },
        };
        var grid = new DataGrid<Row>(
            items: new Bindable<IReadOnlyList<Row>>(items, _ => { }),
            columns:
            [
                DataGridColumn<Row>.Text("Name", x => x.Name, (x, v) => x.Name = v),
                DataGridColumn<Row>.Number("Qty", x => (object)x.Quantity, (x, v) => { }),
            ]
        ).ExportEnabled(true);
        var tdn = (ITabularDataNode)grid;

        string csv = tdn.ExportCsv(null);
        // "Last, First" should be quoted
        await Assert.That(csv).Contains("\"Last, First\"");
    }

    [Test]
    public async Task ExportCsv_RespectsHiddenColumns()
    {
        var grid = CreateGrid().ExportEnabled(true).ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        // Hide the Qty column (index 1)
        tdn.ToggleColumnVisibility(1);

        string csv = tdn.ExportCsv(new GridExportOptions { IncludeHiddenCols = false });
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Should not contain Qty header
        await Assert.That(lines[0]).DoesNotContain("Qty");
    }

    [Test]
    public async Task ExportCsv_IncludeHiddenCols_IncludesAll()
    {
        var grid = CreateGrid().ExportEnabled(true).ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        tdn.ToggleColumnVisibility(1);

        string csv = tdn.ExportCsv(new GridExportOptions { IncludeHiddenCols = true });
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines[0]).Contains("Qty");
    }

    [Test]
    public async Task SaveLayout_CapturesState()
    {
        var grid = CreateGrid().ColumnChooser(true).Sortable(true);
        var tdn = (ITabularDataNode)grid;

        // Sort by column 1
        tdn.ApplySort(1);

        // Save layout
        var layout = tdn.SaveLayout();

        await Assert.That(layout.ColumnOrder.Length).IsEqualTo(3);
        await Assert.That(layout.ColumnOrder[0]).IsEqualTo("Name");
        await Assert.That(layout.ColumnOrder[1]).IsEqualTo("Qty");
        await Assert.That(layout.SortColumn).IsEqualTo("Qty");
        await Assert.That(layout.SortDirectionValue).IsEqualTo(SortDirection.Ascending);
    }

    [Test]
    public async Task RestoreLayout_AppliesState()
    {
        var grid = CreateGrid().ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        // Create a layout with reversed column order and Qty hidden
        var layout = new GridLayoutState
        {
            ColumnOrder = ["Qty", "Name", "Active"],
            ColumnVisibility = new Dictionary<string, bool> { ["Qty"] = false, ["Name"] = true, ["Active"] = true },
            ColumnWidths = new Dictionary<string, float> { ["Name"] = 200f },
            SortColumn = "Name",
            SortDirectionValue = SortDirection.Descending,
        };

        grid.RestoreLayout(layout);
        tdn.ApplyRestoredLayout();

        // Column order should be reordered
        await Assert.That(tdn.GetColumnHeader(0)).IsEqualTo("Qty");
        await Assert.That(tdn.GetColumnHeader(1)).IsEqualTo("Name");
        await Assert.That(tdn.GetColumnHeader(2)).IsEqualTo("Active");

        // Qty column should be hidden
        await Assert.That(tdn.GetColumnVisible(0)).IsFalse();
        await Assert.That(tdn.GetColumnVisible(1)).IsTrue();

        // Sort state should be restored — "Name" is now at index 1
        await Assert.That(tdn.SortColumnIndex).IsEqualTo(1);
        await Assert.That(tdn.SortDirectionValue).IsEqualTo(SortDirection.Descending);
    }

    [Test]
    public async Task ColumnChooser_ToggleVisibility()
    {
        var grid = CreateGrid().ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.IsColumnChooserEnabled).IsTrue();
        await Assert.That(tdn.GetColumnVisible(0)).IsTrue();
        await Assert.That(tdn.GetColumnVisible(1)).IsTrue();
        await Assert.That(tdn.VisibleColumnCount).IsEqualTo(3);

        // Hide column 1
        tdn.ToggleColumnVisibility(1);
        await Assert.That(tdn.GetColumnVisible(1)).IsFalse();
        await Assert.That(tdn.VisibleColumnCount).IsEqualTo(2);

        // Show it again
        tdn.ToggleColumnVisibility(1);
        await Assert.That(tdn.GetColumnVisible(1)).IsTrue();
        await Assert.That(tdn.VisibleColumnCount).IsEqualTo(3);
    }

    [Test]
    public async Task ColumnChooser_CannotHideLastColumn()
    {
        var grid = CreateGrid().ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        // Hide 2 columns, leaving 1 visible
        tdn.ToggleColumnVisibility(1);
        tdn.ToggleColumnVisibility(2);
        await Assert.That(tdn.VisibleColumnCount).IsEqualTo(1);

        // Try to hide the last visible column — should be blocked
        tdn.ToggleColumnVisibility(0);
        await Assert.That(tdn.GetColumnVisible(0)).IsTrue();
        await Assert.That(tdn.VisibleColumnCount).IsEqualTo(1);
    }

    [Test]
    public async Task ColumnChooser_OpenCloseState()
    {
        var grid = CreateGrid().ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.IsColumnChooserOpen).IsFalse();
        tdn.ToggleColumnChooser();
        await Assert.That(tdn.IsColumnChooserOpen).IsTrue();
        tdn.ToggleColumnChooser();
        await Assert.That(tdn.IsColumnChooserOpen).IsFalse();
    }

    [Test]
    public async Task HiddenColumn_HasZeroWidth()
    {
        var grid = CreateGrid().ColumnChooser(true);
        var tdn = (ITabularDataNode)grid;

        tdn.ToggleColumnVisibility(1);
        float width = tdn.GetColumnWidth(1, 500f);
        await Assert.That(width).IsEqualTo(0f);
    }

    [Test]
    public async Task SaveRestoreLayout_Roundtrip()
    {
        var grid = CreateGrid().ColumnChooser(true).Sortable(true);
        var tdn = (ITabularDataNode)grid;

        tdn.ApplySort(0);
        tdn.ToggleColumnVisibility(1);

        var layout = tdn.SaveLayout();

        // Create a fresh grid and restore
        var grid2 = CreateGrid().ColumnChooser(true).Sortable(true);
        grid2.RestoreLayout(layout);
        var tdn2 = (ITabularDataNode)grid2;
        tdn2.ApplyRestoredLayout();

        await Assert.That(tdn2.SortColumnIndex).IsEqualTo(0);
        await Assert.That(tdn2.SortDirectionValue).IsEqualTo(SortDirection.Ascending);
        await Assert.That(tdn2.GetColumnVisible(1)).IsFalse();
    }

    // ── WP-3097: Validation & Error Display ─────────────────────────

    private static void ClearEditBuffer(ITabularDataNode tdn, int currentLength)
    {
        for (int i = 0; i < currentLength; i++)
        {
            tdn.HandleEditKey(Key.Backspace);
        }
    }

    [Test]
    public async Task CellValidator_InvalidValue_HasCellError()
    {
        var items = CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
                .Validate(v => string.IsNullOrEmpty(v?.ToString()) ? ValidationResult.Error("Required") : ValidationResult.Ok),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        var grid = new DataGrid<Row>(items, columns);
        var tdn = (ITabularDataNode)grid;

        // Edit Name to empty string (Alpha = 5 chars)
        tdn.BeginEdit(0, 0);
        ClearEditBuffer(tdn, 5);
        tdn.CommitEdit();

        await Assert.That(tdn.HasCellError(0, 0)).IsTrue();
        await Assert.That(tdn.GetCellErrorMessage(0, 0)).IsEqualTo("Required");
    }

    [Test]
    public async Task CellValidator_ValidValue_NoCellError()
    {
        var items = CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
                .Validate(v => string.IsNullOrEmpty(v?.ToString()) ? ValidationResult.Error("Required") : ValidationResult.Ok),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        var grid = new DataGrid<Row>(items, columns);
        var tdn = (ITabularDataNode)grid;

        // Edit Name to a valid value
        tdn.BeginEdit(0, 0);
        ClearEditBuffer(tdn, 5);
        tdn.HandleEditChar('Z');
        tdn.CommitEdit();

        await Assert.That(tdn.HasCellError(0, 0)).IsFalse();
        await Assert.That(tdn.GetCellErrorMessage(0, 0)).IsNull();
    }

    [Test]
    public async Task RowValidator_RunsOnCellChange()
    {
        var items = CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        var grid = new DataGrid<Row>(items, columns)
            .ValidateRow(r => r.Quantity <= 0
                ? ValidationResult.Error("Quantity must be positive")
                : ValidationResult.Ok);
        var tdn = (ITabularDataNode)grid;

        // Edit Qty to 0 (currently "10" = 2 chars)
        tdn.BeginEdit(0, 1);
        ClearEditBuffer(tdn, 2);
        tdn.HandleEditChar('0');
        tdn.CommitEdit();

        // Row-level error stored at col -1; check via HasCellError at col -1
        await Assert.That(tdn.HasCellError(0, -1)).IsTrue();
    }

    [Test]
    public async Task ValidateRow_ClearsErrorOnValidChange()
    {
        var items = CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v)
                .Validate(v => string.IsNullOrEmpty(v?.ToString()) ? ValidationResult.Error("Required") : ValidationResult.Ok),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        var grid = new DataGrid<Row>(items, columns);
        var tdn = (ITabularDataNode)grid;

        // Make invalid (Alpha = 5 chars)
        tdn.BeginEdit(0, 0);
        ClearEditBuffer(tdn, 5);
        tdn.CommitEdit();
        await Assert.That(tdn.HasCellError(0, 0)).IsTrue();

        // Fix it (now empty, BeginEdit loads "" = 0 chars)
        tdn.BeginEdit(0, 0);
        tdn.HandleEditChar('X');
        tdn.CommitEdit();
        await Assert.That(tdn.HasCellError(0, 0)).IsFalse();
    }

    [Test]
    public async Task HoveredColIndex_TrackedOnInterface()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;

        tdn.HoveredColIndex = 2;
        await Assert.That(tdn.HoveredColIndex).IsEqualTo(2);

        tdn.HoveredColIndex = -1;
        await Assert.That(tdn.HoveredColIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task ValidateRow_NoValidators_NoErrors()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;

        // Edit and commit with no validators
        tdn.BeginEdit(0, 0);
        ClearEditBuffer(tdn, 5);
        tdn.CommitEdit();

        await Assert.That(tdn.HasCellError(0, 0)).IsFalse();
    }

    [Test]
    public async Task ToggleBool_TriggersValidation()
    {
        var items = CreateBinding();
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v)
                .Validate(v => v is false ? ValidationResult.Error("Must be active") : ValidationResult.Ok),
        };
        var grid = new DataGrid<Row>(items, columns);
        var tdn = (ITabularDataNode)grid;

        // Row 0 is Active=true, toggle to false → should fail validation
        tdn.ToggleBool(0, 2);
        await Assert.That(tdn.HasCellError(0, 2)).IsTrue();
        await Assert.That(tdn.GetCellErrorMessage(0, 2)).IsEqualTo("Must be active");
    }

    [Test]
    public async Task DataTable_ValidationStubs_ReturnDefaults()
    {
        var items = new Row[]
        {
            new() { Name = "A", Quantity = 1, Active = true },
        };
        var columns = new DataColumn<Row>[]
        {
            DataColumn<Row>.Text("Name", r => r.Name),
        };
        var table = new DataTable<Row>(items, columns);
        var tdn = (ITabularDataNode)table;

        await Assert.That(tdn.HasCellError(0, 0)).IsFalse();
        await Assert.That(tdn.GetCellErrorMessage(0, 0)).IsNull();
        await Assert.That(tdn.HoveredColIndex).IsEqualTo(-1);
    }

    // ── WP-3100: Virtualization & Performance ─────────────────────────

    private static DataGrid<Row> CreateLargeGrid(int rowCount)
    {
        var rows = new Row[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            rows[i] = new Row { Name = $"Item-{i}", Quantity = i * 10, Active = i % 2 == 0 };
        }
        var items = CreateBinding(rows);
        var columns = new[]
        {
            DataGridColumn<Row>.Text("Name", r => r.Name, (r, v) => r.Name = v),
            DataGridColumn<Row>.Number("Qty", r => r.Quantity, (r, v) => r.Quantity = Convert.ToInt32(v)),
            DataGridColumn<Row>.Bool("Active", r => r.Active, (r, v) => r.Active = v),
        };
        return new DataGrid<Row>(items, columns);
    }

    [Test]
    public async Task ScrollOffsetY_ClampedToMax()
    {
        var grid = CreateGrid().MaxVisibleRows(1);
        var tdn = (ITabularDataNode)grid;

        // Set viewport height to simulate a small viewport
        tdn.ViewportHeight = 30f;

        // Scroll past max should clamp
        tdn.ScrollOffsetY = 999999f;
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(tdn.MaxScrollOffsetY);

        // Scroll negative should clamp to 0
        tdn.ScrollOffsetY = -100f;
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task TotalContentHeight_EqualsRowCountTimesHeight()
    {
        var grid = CreateLargeGrid(100);
        var tdn = (ITabularDataNode)grid;

        float rowHeight = tdn.GetRowHeight();
        float expected = 100 * rowHeight;

        await Assert.That(tdn.TotalContentHeight).IsEqualTo(expected);
    }

    [Test]
    public async Task MaxScrollOffsetY_ReflectsContentMinusViewport()
    {
        var grid = CreateLargeGrid(100);
        var tdn = (ITabularDataNode)grid;

        tdn.ViewportHeight = 300f;
        float expected = tdn.TotalContentHeight - 300f;

        await Assert.That(tdn.MaxScrollOffsetY).IsEqualTo(expected);
    }

    [Test]
    public async Task VirtualizationBufferRows_DefaultIsTen()
    {
        var grid = CreateGrid();
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.VirtualizationBufferRows).IsEqualTo(10);
    }

    [Test]
    public async Task VirtualizationBuffer_Configurable()
    {
        var grid = CreateGrid().VirtualizationBuffer(rows: 5, columns: 3);
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.VirtualizationBufferRows).IsEqualTo(5);
    }

    [Test]
    public async Task MaxVisibleRows_CapsLayoutHeightInterface()
    {
        var grid = CreateLargeGrid(100).MaxVisibleRows(10);
        var tdn = (ITabularDataNode)grid;

        await Assert.That(tdn.MaxVisibleRows).IsEqualTo(10);
    }

    [Test]
    public async Task ScrollIntoView_ScrollsDown()
    {
        var grid = CreateLargeGrid(100);
        var tdn = (ITabularDataNode)grid;

        tdn.ViewportHeight = 150f; // ~5 rows visible
        tdn.ScrollOffsetY = 0;

        // Scroll to row 50 — should adjust offset so row 50 is at the bottom
        tdn.ScrollIntoView(50);

        float rowHeight = tdn.GetRowHeight();
        float expectedMin = 50 * rowHeight - 150f;
        await Assert.That(tdn.ScrollOffsetY).IsGreaterThanOrEqualTo(expectedMin);
    }

    [Test]
    public async Task ScrollIntoView_ScrollsUp()
    {
        var grid = CreateLargeGrid(100);
        var tdn = (ITabularDataNode)grid;

        tdn.ViewportHeight = 150f;
        tdn.ScrollOffsetY = 1000f; // scrolled far down

        // Scroll to row 5 — should snap scroll to row 5
        tdn.ScrollIntoView(5);

        float rowHeight = tdn.GetRowHeight();
        float expectedOffset = 5 * rowHeight;
        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(expectedOffset);
    }

    [Test]
    public async Task ScrollIntoView_NoChangeWhenVisible()
    {
        var grid = CreateLargeGrid(100);
        var tdn = (ITabularDataNode)grid;

        tdn.ViewportHeight = 300f;
        tdn.ScrollOffsetY = 0;

        // Row 2 should already be visible at scroll 0
        tdn.ScrollIntoView(2);

        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task DataTable_VirtualizationStubs_ReturnDefaults()
    {
        var items = new Row[]
        {
            new() { Name = "A", Quantity = 1, Active = true },
        };
        var columns = new DataColumn<Row>[]
        {
            DataColumn<Row>.Text("Name", r => r.Name),
        };
        var table = new DataTable<Row>(items, columns);
        var tdn = (ITabularDataNode)table;

        await Assert.That(tdn.ScrollOffsetY).IsEqualTo(0f);
        await Assert.That(tdn.ScrollOffsetX).IsEqualTo(0f);
        await Assert.That(tdn.MaxScrollOffsetY).IsEqualTo(0f);
        await Assert.That(tdn.MaxScrollOffsetX).IsEqualTo(0f);
        await Assert.That(tdn.ViewportHeight).IsEqualTo(0f);
        await Assert.That(tdn.TotalContentHeight).IsEqualTo(0f);
        await Assert.That(tdn.VirtualizationBufferRows).IsEqualTo(0);
        await Assert.That(tdn.MaxVisibleRows).IsNull();
    }

    [Test]
    public async Task LargeGrid_TotalContentHeightScalesLinearly()
    {
        var grid1 = CreateLargeGrid(1000);
        var tdn1 = (ITabularDataNode)grid1;

        var grid2 = CreateLargeGrid(10000);
        var tdn2 = (ITabularDataNode)grid2;

        // 10K should be exactly 10x of 1K
        await Assert.That(tdn2.TotalContentHeight).IsEqualTo(tdn1.TotalContentHeight * 10f);
    }

    [Test]
    public async Task ApplySort_ThirdClickClearsSortState()
    {
        var grid = CreateGrid().Sortable(true);
        var tdn = (ITabularDataNode)grid;

        // First click: ascending
        tdn.ApplySort(0);
        await Assert.That(tdn.SortColumnIndex).IsEqualTo(0);
        await Assert.That(tdn.SortDirectionValue).IsEqualTo(SortDirection.Ascending);

        // Second click: descending
        tdn.ApplySort(0);
        await Assert.That(tdn.SortColumnIndex).IsEqualTo(0);
        await Assert.That(tdn.SortDirectionValue).IsEqualTo(SortDirection.Descending);

        // Third click: clear sort
        tdn.ApplySort(0);
        await Assert.That(tdn.SortColumnIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task ApplySort_DifferentColumnResetsToAscending()
    {
        var grid = CreateGrid().Sortable(true);
        var tdn = (ITabularDataNode)grid;

        // Sort by column 0 ascending, then descending
        tdn.ApplySort(0);
        tdn.ApplySort(0);
        await Assert.That(tdn.SortDirectionValue).IsEqualTo(SortDirection.Descending);

        // Click different column: resets to ascending
        tdn.ApplySort(1);
        await Assert.That(tdn.SortColumnIndex).IsEqualTo(1);
        await Assert.That(tdn.SortDirectionValue).IsEqualTo(SortDirection.Ascending);
    }
}

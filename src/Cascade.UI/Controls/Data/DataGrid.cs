namespace Cascade.UI;

/// <summary>
/// Editable, virtualized data grid with cell editing, row add/delete, clipboard
/// support, cell-level selection, undo/redo, batch editing, export, and
/// validation. Extends <see cref="DataTable{T}"/> concepts with inline editing
/// capabilities.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class DataGrid<T> : Node, ITabularDataNode
{
    private readonly List<DataGridColumn<T>> columns;

    /// <summary>
    /// Creates an editable data grid.
    /// </summary>
    /// <param name="items">Two-way binding to the data source.</param>
    /// <param name="columns">Column definitions.</param>
    public DataGrid(
        Bindable<IReadOnlyList<T>> items,
        IReadOnlyList<DataGridColumn<T>> columns)
    {
        Items = items;
        this.columns = new List<DataGridColumn<T>>(columns);
    }

    /// <summary>Two-way binding to the data source.</summary>
    public Bindable<IReadOnlyList<T>> Items { get; }

    /// <summary>Column definitions.</summary>
    public IReadOnlyList<DataGridColumn<T>> Columns => columns;

    // ── Internal state ────────────────────────────────────────────────

    internal GridEditMode editModeValue;
    internal CellSelectionMode cellSelectionModeValue;
    internal Func<T>? addRowFactory;
    internal RowAddPosition addRowPosition;
    internal Func<T, bool>? canDeletePredicate;
    internal Action<T>? onDeleteHandler;
    internal Func<T, bool>? canDuplicatePredicate;
    internal Func<T, T>? cloneFactory;
    internal Action<T>? onChangeHandler;
    internal bool reorderableEnabled;
    internal Action<int, int>? onReorderHandler;
    internal bool undoEnabledValue;
    internal int undoDepthValue = 100;
    internal bool batchEditEnabled;
    internal bool batchEditConfirmationEnabled;
    internal Func<IReadOnlyList<T>, IReadOnlyList<ContextMenuItem>>? batchActionsFactory;
    internal bool columnReorderingEnabled;
    internal bool columnChooserEnabled;
    internal string[]? columnOrderValue;
    internal IReadOnlyDictionary<string, bool>? columnVisibilityMap;
    internal Func<T, Node>? rowDetailRenderer;
    internal RowDetailMode rowDetailModeValue;
    internal bool sortableEnabled;
    internal bool filterRowEnabled;
    internal Bindable<string>? globalFilterBinding;
    internal Func<T, object>? groupKeySelector;
    internal Func<object, IReadOnlyList<T>, Node>? groupHeaderRenderer;
    internal GroupOrder groupOrderValue;
    internal bool groupsCollapsedByDefaultFlag;
    internal Func<object, bool>? groupsCollapsedPredicate;
    internal AggregatePosition? aggregatePosition;
    internal IReadOnlyList<ColumnAggregate<T>>? aggregates;
    internal bool isExportEnabled;
    internal bool clipboardEnabled;
    internal Func<T, ValidationResult>? rowValidator;
    internal int frozenRowCount;
    internal int virtualizationBufferRows = 10;
    internal int virtualizationBufferColumns = 5;
    internal int? maxVisibleRows;
    internal GridLayoutState? restoredLayout;
    internal float? rowHeightValue;
    internal bool stripedEnabled;
    internal Node emptyStateNode = Node.Empty;
    internal Func<T, IReadOnlyList<ContextMenuItem>>? rowContextMenuFactory;

    // ── Column resize / reorder runtime state ─────────────────────────

    private int resizingColumnIndex = -1;
    private float resizeStartWidth;
    private float resizeStartMouseX;
    private int reorderDragIndex = -1;
    private int reorderDropIndex = -1;
    private float reorderDragX;
    private float reorderDragWidth;
    private float reorderHeaderY;
    private float reorderHeaderHeight;
    private int hoveredHeaderCol = -1;
    private bool isNearColumnBorder;

    // ── Grouping runtime state ────────────────────────────────────────

    private GroupedSection[]? groupedSections;
    private readonly Dictionary<string, bool> groupCollapsed = new();

    // ── Filtering runtime state ──────────────────────────────────────

    private string[]? columnFilters;
    private int[]? filteredIndices;
    private int activeFilterCol = -1;
    private int filterCursorPos;

    // ── Row detail runtime state ─────────────────────────────────────

    private readonly HashSet<int> expandedRows = new();
    internal Func<T, string>? rowDetailTextGetter;

    // ── Undo runtime state ───────────────────────────────────────────

    private UndoStack? undoStack;

    // ── Column chooser runtime state ─────────────────────────────────

    private bool columnChooserOpen;
    private Rect columnChooserBounds;
    private int columnChooserHoverIndex = -1;
    private Rect columnChooserButtonBounds;
    private Dictionary<int, bool>? runtimeColumnVisibility;

    // ── Validation runtime state ──────────────────────────────────────

    private readonly Dictionary<(int dataRow, int col), ValidationResult> validationErrors = new();
    private int hoveredColIndex = -1;

    // ── Virtualization runtime state ──────────────────────────────────

    private float scrollOffsetY;
    private float scrollOffsetX;
    private float viewportHeight;

    internal readonly struct GroupedSection
    {
        public readonly string Key;
        public readonly int[] RowIndices;

        public GroupedSection(string key, int[] rowIndices)
        {
            Key = key;
            RowIndices = rowIndices;
        }
    }

    // ── Edit mode ─────────────────────────────────────────────────────

    /// <summary>Sets how cells enter edit mode.</summary>
    public DataGrid<T> EditMode(GridEditMode mode)
    {
        editModeValue = mode;
        return this;
    }

    // ── Cell selection ────────────────────────────────────────────────

    /// <summary>Sets the cell selection mode.</summary>
    public DataGrid<T> CellSelection(CellSelectionMode mode)
    {
        cellSelectionModeValue = mode;
        return this;
    }

    // ── Row operations ────────────────────────────────────────────────

    /// <summary>Enables adding new rows via a factory function.</summary>
    public DataGrid<T> AddRow(Func<T> factory, RowAddPosition position = RowAddPosition.Bottom)
    {
        addRowFactory = factory;
        addRowPosition = position;
        return this;
    }

    /// <summary>Enables row deletion with an optional predicate.</summary>
    public DataGrid<T> DeleteRow(Func<T, bool>? canDelete = null, Action<T>? onDelete = null)
    {
        canDeletePredicate = canDelete;
        onDeleteHandler = onDelete;
        return this;
    }

    /// <summary>Enables row duplication.</summary>
    public DataGrid<T> DuplicateRow(Func<T, bool>? canDuplicate = null, Func<T, T>? clone = null)
    {
        canDuplicatePredicate = canDuplicate;
        cloneFactory = clone;
        return this;
    }

    /// <summary>Callback when any cell value changes.</summary>
    public DataGrid<T> OnChange(Action<T> onChange)
    {
        onChangeHandler = onChange;
        return this;
    }

    /// <summary>Enables drag-to-reorder rows.</summary>
    public DataGrid<T> Reorderable(bool enabled, Action<int, int>? onReorder = null)
    {
        reorderableEnabled = enabled;
        onReorderHandler = onReorder;
        return this;
    }

    // ── Undo / Redo ───────────────────────────────────────────────────

    /// <summary>Enables or disables undo/redo.</summary>
    public DataGrid<T> UndoEnabled(bool enabled)
    {
        undoEnabledValue = enabled;
        return this;
    }

    /// <summary>Sets the maximum undo depth.</summary>
    public DataGrid<T> UndoDepth(int maxSteps)
    {
        undoDepthValue = Math.Max(1, maxSteps);
        return this;
    }

    // ── Batch edit ────────────────────────────────────────────────────

    /// <summary>Enables batch editing of selected rows.</summary>
    public DataGrid<T> BatchEdit(bool enabled)
    {
        batchEditEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables the batch edit confirmation prompt.</summary>
    public DataGrid<T> BatchEditConfirmation(bool enabled)
    {
        batchEditConfirmationEnabled = enabled;
        return this;
    }

    /// <summary>Adds custom batch actions to the context menu.</summary>
    public DataGrid<T> BatchActions(Func<IReadOnlyList<T>, IReadOnlyList<ContextMenuItem>> factory)
    {
        batchActionsFactory = factory;
        return this;
    }

    // ── Column management ─────────────────────────────────────────────

    /// <summary>Enables drag-to-reorder columns.</summary>
    public DataGrid<T> ColumnReordering(bool enabled)
    {
        columnReorderingEnabled = enabled;
        return this;
    }

    /// <summary>Shows a column chooser dropdown in the header.</summary>
    public DataGrid<T> ColumnChooser(bool enabled)
    {
        columnChooserEnabled = enabled;
        return this;
    }

    /// <summary>Restores a saved column order.</summary>
    public DataGrid<T> ColumnOrder(string[] order)
    {
        columnOrderValue = order;
        return this;
    }

    /// <summary>Restores saved column visibility.</summary>
    public DataGrid<T> ColumnVisibility(IReadOnlyDictionary<string, bool> visibility)
    {
        columnVisibilityMap = visibility;
        return this;
    }

    // ── Row detail ────────────────────────────────────────────────────

    /// <summary>Enables expandable row detail panels.</summary>
    public DataGrid<T> RowDetail(Func<T, Node> detailRenderer)
    {
        rowDetailRenderer = detailRenderer;
        return this;
    }

    /// <summary>Enables expandable row detail panels with text content.</summary>
    public DataGrid<T> RowDetail(Func<T, string> textRenderer)
    {
        rowDetailTextGetter = textRenderer;
        return this;
    }

    /// <summary>Sets whether one or many rows can be expanded simultaneously.</summary>
    public DataGrid<T> RowDetailMode(RowDetailMode mode)
    {
        rowDetailModeValue = mode;
        return this;
    }

    // ── Sorting and filtering ─────────────────────────────────────────

    /// <summary>Enables or disables column sorting.</summary>
    public DataGrid<T> Sortable(bool enabled)
    {
        sortableEnabled = enabled;
        return this;
    }

    /// <summary>Shows a filter input row below the header.</summary>
    public DataGrid<T> FilterRow(bool enabled)
    {
        filterRowEnabled = enabled;
        return this;
    }

    /// <summary>Binds a global filter query.</summary>
    public DataGrid<T> GlobalFilter(Bindable<string> query)
    {
        globalFilterBinding = query;
        return this;
    }

    // ── Grouping ──────────────────────────────────────────────────────

    /// <summary>Groups rows by a key selector with a custom header renderer.</summary>
    public DataGrid<T> GroupBy(Func<T, object> keySelector, Func<object, IReadOnlyList<T>, Node>? headerRender = null, GroupOrder groupOrder = GroupOrder.Ascending)
    {
        groupKeySelector = keySelector;
        groupHeaderRenderer = headerRender;
        groupOrderValue = groupOrder;
        return this;
    }

    /// <summary>Controls whether groups start collapsed.</summary>
    public DataGrid<T> GroupsCollapsedByDefault(bool collapsed)
    {
        groupsCollapsedByDefaultFlag = collapsed;
        groupsCollapsedPredicate = null;
        return this;
    }

    /// <summary>Controls whether groups start collapsed using a per-group predicate.</summary>
    public DataGrid<T> GroupsCollapsedByDefault(Func<object, bool> predicate)
    {
        groupsCollapsedPredicate = predicate;
        return this;
    }

    // ── Aggregation ───────────────────────────────────────────────────

    /// <summary>Adds an aggregate (summary) row.</summary>
    public DataGrid<T> AggregateRow(AggregatePosition position, IReadOnlyList<ColumnAggregate<T>> aggregates)
    {
        aggregatePosition = position;
        this.aggregates = aggregates;
        return this;
    }

    // ── Export ─────────────────────────────────────────────────────────

    /// <summary>Enables the export toolbar button.</summary>
    public DataGrid<T> ExportEnabled(bool enabled)
    {
        isExportEnabled = enabled;
        return this;
    }

    // ── Clipboard ─────────────────────────────────────────────────────

    /// <summary>Enables clipboard support (Ctrl+C/V/X).</summary>
    public DataGrid<T> ClipboardSupport(bool enabled)
    {
        clipboardEnabled = enabled;
        return this;
    }

    // ── Validation ────────────────────────────────────────────────────

    /// <summary>Adds a per-row cross-column validation rule.</summary>
    public DataGrid<T> ValidateRow(Func<T, ValidationResult> validator)
    {
        rowValidator = validator;
        return this;
    }

    // ── Frozen rows ───────────────────────────────────────────────────

    /// <summary>Freezes the first N rows at the top of the grid.</summary>
    public DataGrid<T> FrozenRows(int count)
    {
        frozenRowCount = Math.Max(0, count);
        return this;
    }

    // ── Virtualization ────────────────────────────────────────────────

    /// <summary>Sets the off-screen buffer for row and column virtualization.</summary>
    public DataGrid<T> VirtualizationBuffer(int rows = 10, int columns = 5)
    {
        virtualizationBufferRows = Math.Max(0, rows);
        virtualizationBufferColumns = Math.Max(0, columns);
        return this;
    }

    /// <summary>
    /// Sets the maximum number of visible rows before the grid scrolls internally.
    /// When set, the grid caps its layout height and enables internal scroll with virtualization.
    /// </summary>
    public DataGrid<T> MaxVisibleRows(int rows)
    {
        maxVisibleRows = Math.Max(1, rows);
        return this;
    }

    // ── State persistence ─────────────────────────────────────────────

    /// <summary>Restores a previously saved grid layout state.</summary>
    public DataGrid<T> RestoreLayout(GridLayoutState state)
    {
        restoredLayout = state;
        return this;
    }

    // ── Appearance ────────────────────────────────────────────────────

    /// <summary>Sets fixed row height in logical pixels.</summary>
    public DataGrid<T> RowHeight(float height)
    {
        rowHeightValue = height;
        return this;
    }

    /// <summary>Enables alternating row backgrounds.</summary>
    public DataGrid<T> Striped(bool enabled)
    {
        stripedEnabled = enabled;
        return this;
    }

    /// <summary>Sets the empty state displayed when the grid has no rows.</summary>
    public DataGrid<T> EmptyState(Node emptyState)
    {
        emptyStateNode = emptyState;
        return this;
    }

    /// <summary>Configures a right-click context menu per row.</summary>
    public DataGrid<T> RowContextMenu(Func<T, IReadOnlyList<ContextMenuItem>> factory)
    {
        rowContextMenuFactory = factory;
        return this;
    }

    // ── Runtime interaction state ────────────────────────────────────

    internal int sortColumnIdx = -1;
    internal SortDirection currentSortDirection;
    internal int selectedRowIdx = -1;
    internal int hoveredRowIdx = -1;
    internal int[]? sortedIndices;
    internal HashSet<int> selectedRows = new();
    internal int anchorRow = -1;
    internal int visibleRowCount;

    // ── Cell editing state ────────────────────────────────────────────

    internal int editingRow = -1;
    internal int editingCol = -1;
    internal string editBuffer = "";
    internal int editCursorPos;

    // ── Select dropdown overlay state ─────────────────────────────────

    internal bool selectDropdownOpen;
    internal int selectDropdownRow = -1;
    internal int selectDropdownCol = -1;
    internal int selectDropdownHoverIndex = -1;
    internal Rect selectDropdownBounds;
    internal Rect selectDropdownCellBounds;

    // ── Date popup overlay state ──────────────────────────────────────

    internal DatePicker? datePopupPicker;
    internal int datePopupRow = -1;
    internal int datePopupCol = -1;
    internal Rect datePopupCellBounds;

    // ── ITabularDataNode implementation ───────────────────────────────

    private int MapRow(int displayRow)
    {
        if (sortedIndices != null)
        {
            return sortedIndices[displayRow];
        }
        if (filteredIndices != null)
        {
            return filteredIndices[displayRow];
        }
        return displayRow;
    }

    int ITabularDataNode.RowCount => filteredIndices?.Length ?? Items.Value.Count;
    int ITabularDataNode.ColumnCount => Columns.Count;
    string ITabularDataNode.GetColumnHeader(int col) => Columns[col].Header;
    bool ITabularDataNode.IsStriped => stripedEnabled;
    float ITabularDataNode.GetRowHeight() => rowHeightValue ?? 36f;

    bool ITabularDataNode.IsBoolColumn(int col) => Columns[col].boolGetter != null;

    bool ITabularDataNode.GetBoolValue(int row, int col)
    {
        var getter = Columns[col].boolGetter;
        return getter != null && getter(Items.Value[MapRow(row)]);
    }

    bool ITabularDataNode.IsCustomColumn(int col) => false;

    Node? ITabularDataNode.GetCustomCellNode(int row, int col) => null;

    bool ITabularDataNode.IsSortable => sortableEnabled;

    bool ITabularDataNode.IsColumnSortable(int col)
    {
        return sortableEnabled && (Columns[col].sortableValue ?? true);
    }

    int ITabularDataNode.SortColumnIndex => sortColumnIdx;
    SortDirection ITabularDataNode.SortDirectionValue => currentSortDirection;
    int ITabularDataNode.SelectedRowIndex => selectedRowIdx;

    bool ITabularDataNode.IsRowSelected(int row) => selectedRows.Contains(row);

    int ITabularDataNode.HoveredRowIndex
    {
        get => hoveredRowIdx;
        set => hoveredRowIdx = value;
    }

    bool ITabularDataNode.IsHoverHighlightEnabled => true;

    int ITabularDataNode.VisibleRowCount
    {
        get => visibleRowCount;
        set => visibleRowCount = value;
    }

    Rect ITabularDataNode.AbsoluteBounds { get; set; }

    // ── Cell editing implementation ───────────────────────────────────

    bool ITabularDataNode.IsColumnEditable(int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return false;
        }
        var column = Columns[col];
        // Computed columns are never editable
        if (column.isReadOnly || column.computeFunc != null)
        {
            return false;
        }
        // Must have at least one setter
        return column.textSetter != null || column.objectSetter != null ||
               column.boolSetter != null;
    }

    bool ITabularDataNode.IsEditing => editingRow >= 0 && editingCol >= 0;
    int ITabularDataNode.EditingRow => editingRow;
    int ITabularDataNode.EditingCol => editingCol;
    string ITabularDataNode.EditBuffer => editBuffer;
    int ITabularDataNode.EditCursorPos
    {
        get => editCursorPos;
        set => editCursorPos = value;
    }
    GridEditMode ITabularDataNode.EditModeValue => editModeValue;

    bool ITabularDataNode.BeginEdit(int row, int col)
    {
        if (!((ITabularDataNode)this).IsColumnEditable(col))
        {
            return false;
        }
        if (row < 0 || row >= Items.Value.Count)
        {
            return false;
        }

        // Bool columns don't enter edit mode — they toggle directly
        if (Columns[col].boolGetter != null)
        {
            return false;
        }

        // Select columns open a dropdown overlay instead of text buffer
        if (Columns[col].kind == DataColumnKind.Select)
        {
            ((ITabularDataNode)this).OpenSelectDropdown(row, col);
            return true;
        }

        // Date columns open a calendar popup instead of text buffer
        if (Columns[col].kind == DataColumnKind.Date)
        {
            ((ITabularDataNode)this).OpenDatePopup(row, col);
            return true;
        }

        editingRow = row;
        editingCol = col;

        // For Number columns, show the raw numeric value (no currency/format symbols)
        var editCol = Columns[col];
        if (editCol.kind == DataColumnKind.Number && editCol.objectGetter != null)
        {
            int dataRowIdx = MapRow(row);
            var item = Items.Value[dataRowIdx];
            editBuffer = editCol.objectGetter(item)?.ToString() ?? "";
        }
        else
        {
            editBuffer = ((ITabularDataNode)this).GetCellText(row, col);
        }
        editCursorPos = editBuffer.Length;
        return true;
    }

    bool ITabularDataNode.CommitEdit()
    {
        if (editingRow < 0 || editingCol < 0)
        {
            return false;
        }

        int dataRow = MapRow(editingRow);
        var items = Items.Value;
        if (dataRow < 0 || dataRow >= items.Count)
        {
            ((ITabularDataNode)this).CancelEdit();
            return false;
        }

        var item = items[dataRow];
        var column = Columns[editingCol];
        string newValue = editBuffer;

        if (undoEnabledValue)
        {
            EnsureUndoStack();

            // Capture old value before applying
            string oldValue = column.textGetter != null
                ? column.textGetter(item) ?? ""
                : column.objectGetter != null
                    ? column.objectGetter(item)?.ToString() ?? ""
                    : "";

            int capturedDataRow = dataRow;
            int capturedCol = editingCol;

            // Execute applies the new value and pushes onto undo stack.
            // Invalidate cache inside each lambda so undo/redo refresh text.
            undoStack!.Execute(UndoCommand.Create(
                $"Edit {column.Header}",
                () => { ApplyCellValue(items[capturedDataRow], Columns[capturedCol], newValue); InvalidateCellCache(capturedDataRow); },
                () => { ApplyCellValue(items[capturedDataRow], Columns[capturedCol], oldValue); InvalidateCellCache(capturedDataRow); }));
        }
        else
        {
            ApplyCellValue(item, column, newValue);
        }

        // Apply batch edit to all other selected rows
        if (batchEditEnabled && selectedRows.Count > 1)
        {
            int currentEditRow = editingRow;
            foreach (int selRow in selectedRows)
            {
                if (selRow == currentEditRow)
                {
                    continue;
                }
                int selDataRow = MapRow(selRow);
                if (selDataRow >= 0 && selDataRow < items.Count)
                {
                    ApplyCellValue(items[selDataRow], column, newValue);
                    onChangeHandler?.Invoke(items[selDataRow]);
                    InvalidateCellCache(selDataRow);
                }
            }
        }

        onChangeHandler?.Invoke(item);
        InvalidateCellCache(dataRow);

        // Run validation on the edited row
        int committedRow = editingRow;
        editingRow = -1;
        editingCol = -1;
        editBuffer = "";
        editCursorPos = 0;

        ((ITabularDataNode)this).ValidateRow(committedRow);

        return true;
    }

    void ITabularDataNode.CancelEdit()
    {
        editingRow = -1;
        editingCol = -1;
        editBuffer = "";
        editCursorPos = 0;
    }

    void ITabularDataNode.HandleEditChar(char ch)
    {
        if (editingRow < 0)
        {
            return;
        }
        editBuffer = editBuffer.Insert(editCursorPos, ch.ToString());
        editCursorPos++;
    }

    void ITabularDataNode.HandleEditKey(Key key)
    {
        if (editingRow < 0)
        {
            return;
        }

        switch (key)
        {
            case global::Cascade.UI.Key.Backspace:
                if (editCursorPos > 0)
                {
                    editBuffer = editBuffer.Remove(editCursorPos - 1, 1);
                    editCursorPos--;
                }
                break;
            case global::Cascade.UI.Key.Delete:
                if (editCursorPos < editBuffer.Length)
                {
                    editBuffer = editBuffer.Remove(editCursorPos, 1);
                }
                break;
            case global::Cascade.UI.Key.Left:
                if (editCursorPos > 0)
                {
                    editCursorPos--;
                }
                break;
            case global::Cascade.UI.Key.Right:
                if (editCursorPos < editBuffer.Length)
                {
                    editCursorPos++;
                }
                break;
            case global::Cascade.UI.Key.Home:
                editCursorPos = 0;
                break;
            case global::Cascade.UI.Key.End:
                editCursorPos = editBuffer.Length;
                break;
        }
    }

    void ITabularDataNode.ToggleBool(int row, int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }
        var column = Columns[col];
        if (column.boolGetter == null || column.boolSetter == null)
        {
            return;
        }
        int dataRow = MapRow(row);
        var items = Items.Value;
        if (dataRow < 0 || dataRow >= items.Count)
        {
            return;
        }
        var item = items[dataRow];
        column.boolSetter(item, !column.boolGetter(item));
        onChangeHandler?.Invoke(item);
        ((ITabularDataNode)this).ValidateRow(row);
    }

    // ── Column type detection ─────────────────────────────────────────

    bool ITabularDataNode.IsSelectColumn(int col)
    {
        return col >= 0 && col < Columns.Count && Columns[col].kind == DataColumnKind.Select;
    }

    bool ITabularDataNode.IsDateColumn(int col)
    {
        return col >= 0 && col < Columns.Count && Columns[col].kind == DataColumnKind.Date;
    }

    IReadOnlyList<object>? ITabularDataNode.GetSelectOptions(int col)
    {
        return col >= 0 && col < Columns.Count ? Columns[col].selectOptions : null;
    }

    // ── Select dropdown overlay ───────────────────────────────────────

    bool ITabularDataNode.IsSelectDropdownOpen => selectDropdownOpen;
    int ITabularDataNode.SelectDropdownRow => selectDropdownRow;
    int ITabularDataNode.SelectDropdownCol => selectDropdownCol;

    int ITabularDataNode.SelectDropdownHoverIndex
    {
        get => selectDropdownHoverIndex;
        set => selectDropdownHoverIndex = value;
    }

    Rect ITabularDataNode.SelectDropdownBounds
    {
        get => selectDropdownBounds;
        set => selectDropdownBounds = value;
    }

    Rect ITabularDataNode.SelectDropdownCellBounds
    {
        get => selectDropdownCellBounds;
        set => selectDropdownCellBounds = value;
    }

    void ITabularDataNode.OpenSelectDropdown(int row, int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }
        var column = Columns[col];
        if (column.selectOptions == null || column.selectOptions.Count == 0)
        {
            return;
        }

        // Close any existing overlay first
        ((ITabularDataNode)this).CloseOverlay();

        selectDropdownOpen = true;
        selectDropdownRow = row;
        selectDropdownCol = col;

        // Pre-select the current value in the hover
        string currentText = ((ITabularDataNode)this).GetCellText(row, col);
        selectDropdownHoverIndex = -1;
        for (int i = 0; i < column.selectOptions.Count; i++)
        {
            if (string.Equals(column.selectOptions[i]?.ToString(), currentText, StringComparison.Ordinal))
            {
                selectDropdownHoverIndex = i;
                break;
            }
        }
    }

    void ITabularDataNode.CommitSelectOption(int index)
    {
        if (!selectDropdownOpen || selectDropdownCol < 0)
        {
            return;
        }
        var column = Columns[selectDropdownCol];
        if (column.selectOptions == null || index < 0 || index >= column.selectOptions.Count)
        {
            return;
        }

        int dataRow = MapRow(selectDropdownRow);
        var items = Items.Value;
        int capturedRow = selectDropdownRow;
        if (dataRow >= 0 && dataRow < items.Count && column.objectSetter != null)
        {
            column.objectSetter(items[dataRow], column.selectOptions[index]);
            onChangeHandler?.Invoke(items[dataRow]);
            // Drop the row's cached cell text so the new selection is displayed —
            // Cascade has no INotifyPropertyChanged, so the flat cell cache is stale
            // otherwise (same reason CommitEdit invalidates it).
            InvalidateCellCache(dataRow);
        }

        selectDropdownOpen = false;
        selectDropdownRow = -1;
        selectDropdownCol = -1;
        selectDropdownHoverIndex = -1;
        selectDropdownBounds = default;

        ((ITabularDataNode)this).ValidateRow(capturedRow);
    }

    void ITabularDataNode.CloseSelectDropdown()
    {
        selectDropdownOpen = false;
        selectDropdownRow = -1;
        selectDropdownCol = -1;
        selectDropdownHoverIndex = -1;
        selectDropdownBounds = default;
    }

    // ── Date popup overlay ────────────────────────────────────────────

    bool ITabularDataNode.IsDatePopupOpen => datePopupPicker != null;
    int ITabularDataNode.DatePopupRow => datePopupRow;
    int ITabularDataNode.DatePopupCol => datePopupCol;

    Rect ITabularDataNode.DatePopupCellBounds
    {
        get => datePopupCellBounds;
        set => datePopupCellBounds = value;
    }

    DatePicker? ITabularDataNode.DatePopupPicker => datePopupPicker;

    void ITabularDataNode.OpenDatePopup(int row, int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }

        // Close any existing overlay first
        ((ITabularDataNode)this).CloseOverlay();

        int dataRow = MapRow(row);
        var items = Items.Value;
        if (dataRow < 0 || dataRow >= items.Count)
        {
            return;
        }

        var column = Columns[col];
        var currentValue = column.objectGetter?.Invoke(items[dataRow]);
        DateOnly? dateValue = currentValue switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            _ => null
        };

        // Create a temporary DatePicker for calendar state
        datePopupPicker = new DatePicker(
            new Bindable<DateOnly?>(dateValue, _ => { }));
        datePopupPicker.OpenCalendar();
        datePopupRow = row;
        datePopupCol = col;
    }

    void ITabularDataNode.CommitDateValue(DateOnly date)
    {
        if (datePopupPicker == null || datePopupCol < 0)
        {
            return;
        }

        int dataRow = MapRow(datePopupRow);
        var items = Items.Value;
        var column = Columns[datePopupCol];
        int capturedRow = datePopupRow;

        if (dataRow >= 0 && dataRow < items.Count && column.objectSetter != null)
        {
            column.objectSetter(items[dataRow], date);
            onChangeHandler?.Invoke(items[dataRow]);
            // Drop the row's cached cell text so the new date is displayed — the
            // flat cell cache is otherwise stale (same reason CommitEdit does this).
            InvalidateCellCache(dataRow);
        }

        datePopupPicker.CloseCalendar();
        datePopupPicker = null;
        datePopupRow = -1;
        datePopupCol = -1;
        datePopupCellBounds = default;

        ((ITabularDataNode)this).ValidateRow(capturedRow);
    }

    void ITabularDataNode.CloseDatePopup()
    {
        if (datePopupPicker != null)
        {
            datePopupPicker.CloseCalendar();
            datePopupPicker = null;
        }
        datePopupRow = -1;
        datePopupCol = -1;
        datePopupCellBounds = default;
    }

    void ITabularDataNode.CloseOverlay()
    {
        ((ITabularDataNode)this).CloseSelectDropdown();
        ((ITabularDataNode)this).CloseDatePopup();
        if (columnChooserOpen)
        {
            columnChooserOpen = false;
            columnChooserHoverIndex = -1;
        }
    }

    // ── Column resize / reorder / pin ─────────────────────────────────

    bool ITabularDataNode.IsResizingEnabled
    {
        get
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].resizableValue != false)
                {
                    return true;
                }
            }
            return false;
        }
    }

    bool ITabularDataNode.IsReorderingEnabled => columnReorderingEnabled;

    int ITabularDataNode.ResizingColumnIndex
    {
        get => resizingColumnIndex;
        set => resizingColumnIndex = value;
    }

    float ITabularDataNode.ResizeStartWidth
    {
        get => resizeStartWidth;
        set => resizeStartWidth = value;
    }

    float ITabularDataNode.ResizeStartMouseX
    {
        get => resizeStartMouseX;
        set => resizeStartMouseX = value;
    }

    bool ITabularDataNode.IsColumnResizable(int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return false;
        }
        // Default to resizable unless explicitly set to false
        return Columns[col].resizableValue != false;
    }

    void ITabularDataNode.SetColumnWidth(int col, float width)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }
        float minW = Columns[col].minWidthValue ?? 40f;
        float maxW = Columns[col].maxWidthValue ?? float.MaxValue;
        Columns[col].widthValue = Math.Clamp(width, minW, maxW);
    }

    int ITabularDataNode.ReorderDragIndex
    {
        get => reorderDragIndex;
        set => reorderDragIndex = value;
    }

    int ITabularDataNode.ReorderDropIndex
    {
        get => reorderDropIndex;
        set => reorderDropIndex = value;
    }

    float ITabularDataNode.ReorderDragX
    {
        get => reorderDragX;
        set => reorderDragX = value;
    }

    float ITabularDataNode.ReorderDragWidth
    {
        get => reorderDragWidth;
        set => reorderDragWidth = value;
    }

    float ITabularDataNode.ReorderHeaderY
    {
        get => reorderHeaderY;
        set => reorderHeaderY = value;
    }

    float ITabularDataNode.ReorderHeaderHeight
    {
        get => reorderHeaderHeight;
        set => reorderHeaderHeight = value;
    }

    void ITabularDataNode.ReorderColumn(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 ||
            fromIndex >= columns.Count || toIndex >= columns.Count)
        {
            return;
        }
        var col = columns[fromIndex];
        columns.RemoveAt(fromIndex);
        columns.Insert(toIndex, col);

        // The cell-text cache is flat-indexed by *display column*, so a bare
        // reorder leaves every cached string bound to the column that used to
        // sit at that index — the header moves but the values don't. Drop the
        // cache; GetCellText repopulates it lazily against the new order.
        cellTextCache = null;

        // Runtime visibility is likewise keyed by column index and must be
        // permuted the same way the list was, or a hidden column's flag stays
        // pinned to a position instead of following the column.
        if (runtimeColumnVisibility is { Count: > 0 })
        {
            var remapped = new Dictionary<int, bool>(runtimeColumnVisibility.Count);
            foreach (var (oldIdx, vis) in runtimeColumnVisibility)
            {
                remapped[RemapReorderedIndex(oldIdx, fromIndex, toIndex)] = vis;
            }
            runtimeColumnVisibility = remapped;
        }

        // Keep the sort indicator tracking the same logical column.
        sortColumnIdx = RemapReorderedIndex(sortColumnIdx, fromIndex, toIndex);
    }

    /// <summary>
    /// Maps a column index through a move of the column at <paramref name="from"/>
    /// to <paramref name="to"/> — the same permutation the columns list undergoes
    /// (remove at <paramref name="from"/>, insert at <paramref name="to"/>). A
    /// negative index (e.g. "no sort", -1) is returned unchanged.
    /// </summary>
    static int RemapReorderedIndex(int index, int from, int to)
    {
        if (index < 0)
        {
            return index;
        }
        if (index == from)
        {
            return to;
        }
        if (from < to)
        {
            return index > from && index <= to ? index - 1 : index;
        }
        return index >= to && index < from ? index + 1 : index;
    }

    ColumnPin? ITabularDataNode.GetColumnPin(int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return null;
        }
        return Columns[col].pinValue;
    }

    bool ITabularDataNode.HasLeftPinnedColumns
    {
        get
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].pinValue == ColumnPin.Left)
                {
                    return true;
                }
            }
            return false;
        }
    }

    bool ITabularDataNode.HasRightPinnedColumns
    {
        get
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].pinValue == ColumnPin.Right)
                {
                    return true;
                }
            }
            return false;
        }
    }

    int ITabularDataNode.HoveredHeaderCol
    {
        get => hoveredHeaderCol;
        set => hoveredHeaderCol = value;
    }

    bool ITabularDataNode.IsNearColumnBorder
    {
        get => isNearColumnBorder;
        set => isNearColumnBorder = value;
    }

    // ── Grouping ──────────────────────────────────────────────────────

    bool ITabularDataNode.IsGrouped
    {
        get
        {
            if (groupKeySelector == null)
            {
                return false;
            }
            if (groupedSections == null)
            {
                RebuildGroupedRows();
            }
            return groupedSections != null && groupedSections.Length > 0;
        }
    }
    int ITabularDataNode.GroupCount => groupedSections?.Length ?? 0;

    string ITabularDataNode.GetGroupKey(int groupIndex)
    {
        if (groupedSections == null || groupIndex < 0 || groupIndex >= groupedSections.Length)
        {
            return "";
        }
        return groupedSections[groupIndex].Key;
    }

    int ITabularDataNode.GetGroupRowCount(int groupIndex)
    {
        if (groupedSections == null || groupIndex < 0 || groupIndex >= groupedSections.Length)
        {
            return 0;
        }
        return groupedSections[groupIndex].RowIndices.Length;
    }

    bool ITabularDataNode.IsGroupCollapsed(int groupIndex)
    {
        if (groupedSections == null || groupIndex < 0 || groupIndex >= groupedSections.Length)
        {
            return false;
        }
        string key = groupedSections[groupIndex].Key;
        if (groupCollapsed.TryGetValue(key, out bool collapsed))
        {
            return collapsed;
        }
        // Check per-group predicate or global default
        if (groupsCollapsedPredicate != null)
        {
            return groupsCollapsedPredicate(key);
        }
        return groupsCollapsedByDefaultFlag;
    }

    int ITabularDataNode.GetGroupDataRowIndex(int groupIndex, int rowInGroup)
    {
        if (groupedSections == null || groupIndex < 0 || groupIndex >= groupedSections.Length)
        {
            return 0;
        }
        var section = groupedSections[groupIndex];
        if (rowInGroup < 0 || rowInGroup >= section.RowIndices.Length)
        {
            return 0;
        }
        return section.RowIndices[rowInGroup];
    }

    void ITabularDataNode.ToggleGroupCollapse(int groupIndex)
    {
        if (groupedSections == null || groupIndex < 0 || groupIndex >= groupedSections.Length)
        {
            return;
        }
        string key = groupedSections[groupIndex].Key;
        bool current = ((ITabularDataNode)this).IsGroupCollapsed(groupIndex);
        groupCollapsed[key] = !current;
    }

    /// <summary>
    /// Rebuilds the grouped sections from the current data + sort state.
    /// Called after sort changes or when data is first laid out.
    /// </summary>
    internal void RebuildGroupedRows()
    {
        if (groupKeySelector == null)
        {
            groupedSections = null;
            return;
        }

        var items = Items.Value;
        // When filtering is active, use filtered row count instead of total
        int count = filteredIndices != null ? filteredIndices.Length
                  : sortedIndices != null ? sortedIndices.Length
                  : items.Count;
        if (count == 0)
        {
            groupedSections = Array.Empty<GroupedSection>();
            return;
        }

        // Build index mapping (respects current sort and filter order)
        var groups = new Dictionary<string, List<int>>();
        var groupOrder = new List<string>();
        for (int i = 0; i < count; i++)
        {
            int dataIdx = sortedIndices != null ? sortedIndices[i]
                        : filteredIndices != null ? filteredIndices[i]
                        : i;
            string key = groupKeySelector(items[dataIdx])?.ToString() ?? "(null)";
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<int>();
                groups[key] = list;
                groupOrder.Add(key);
            }
            // Store display-order index (i), not data index — GetCellText uses MapRow
            list.Add(i);
        }

        // Sort group keys if requested
        if (groupOrderValue == GroupOrder.Ascending)
        {
            groupOrder.Sort(StringComparer.OrdinalIgnoreCase);
        }
        else if (groupOrderValue == GroupOrder.Descending)
        {
            groupOrder.Sort(StringComparer.OrdinalIgnoreCase);
            groupOrder.Reverse();
        }
        // GroupOrder.Preserve keeps insertion order

        var sections = new GroupedSection[groupOrder.Count];
        for (int i = 0; i < groupOrder.Count; i++)
        {
            sections[i] = new GroupedSection(groupOrder[i], groups[groupOrder[i]].ToArray());
        }
        groupedSections = sections;
    }

    // ── Filtering ─────────────────────────────────────────────────────

    bool ITabularDataNode.HasFilterRow => filterRowEnabled;

    string ITabularDataNode.GetColumnFilter(int col)
    {
        if (columnFilters == null || col < 0 || col >= columnFilters.Length)
        {
            return "";
        }
        return columnFilters[col];
    }

    void ITabularDataNode.SetColumnFilter(int col, string value)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }
        if (columnFilters == null)
        {
            columnFilters = new string[Columns.Count];
            Array.Fill(columnFilters, "");
        }
        columnFilters[col] = value ?? "";
        RebuildFilteredIndices();
    }

    int ITabularDataNode.ActiveFilterCol
    {
        get => activeFilterCol;
        set => activeFilterCol = value;
    }

    int ITabularDataNode.FilterCursorPos
    {
        get => filterCursorPos;
        set => filterCursorPos = value;
    }

    int ITabularDataNode.FilteredRowCount => filteredIndices?.Length ?? Items.Value.Count;

    bool ITabularDataNode.HasActiveFilter
    {
        get
        {
            if (columnFilters != null)
            {
                for (int i = 0; i < columnFilters.Length; i++)
                {
                    if (columnFilters[i].Length > 0)
                    {
                        return true;
                    }
                }
            }
            string globalText = globalFilterBinding?.Value ?? "";
            return globalText.Length > 0;
        }
    }

    string ITabularDataNode.GlobalFilterText => globalFilterBinding?.Value ?? "";

    // ── Row detail ────────────────────────────────────────────────────

    bool ITabularDataNode.HasRowDetail => rowDetailRenderer != null || rowDetailTextGetter != null;

    RowDetailMode ITabularDataNode.RowDetailModeValue => rowDetailModeValue;

    bool ITabularDataNode.IsRowExpanded(int row) => expandedRows.Contains(row);

    float ITabularDataNode.GetRowDetailHeight(int row)
    {
        string text = ((ITabularDataNode)this).GetRowDetailText(row);
        int lineCount = Math.Max(1, text.Split('\n').Length);
        return 16f + lineCount * 18f;
    }

    void ITabularDataNode.ToggleRowDetail(int row)
    {
        if (!expandedRows.Remove(row))
        {
            if (rowDetailModeValue == Cascade.UI.RowDetailMode.Single)
            {
                expandedRows.Clear();
            }
            expandedRows.Add(row);
        }
    }

    string ITabularDataNode.GetRowDetailText(int row)
    {
        int dataRow = MapRow(row);
        var items = Items.Value;
        if (dataRow < 0 || dataRow >= items.Count)
        {
            return "";
        }
        var item = items[dataRow];
        if (rowDetailTextGetter != null)
        {
            return rowDetailTextGetter(item) ?? "";
        }
        if (rowDetailRenderer != null)
        {
            var node = rowDetailRenderer(item);
            return node?.ToString() ?? "";
        }
        return "";
    }

    // ── Aggregate row ────────────────────────────────────────────────

    bool ITabularDataNode.HasAggregateRow => aggregatePosition.HasValue && aggregates != null && aggregates.Count > 0;

    AggregatePosition ITabularDataNode.AggregatePos => aggregatePosition ?? AggregatePosition.Bottom;

    float ITabularDataNode.GetAggregateRowHeight() => ((ITabularDataNode)this).GetRowHeight() + 4f;

    string ITabularDataNode.GetAggregateText(int col)
    {
        if (aggregates == null || col < 0 || col >= Columns.Count)
        {
            return "";
        }

        string colHeader = Columns[col].Header;
        foreach (var agg in aggregates)
        {
            if (string.Equals(agg.ColumnHeader, colHeader, StringComparison.Ordinal) && agg.Compute != null)
            {
                var items = Items.Value;
                // Use filtered data if filters are active
                IReadOnlyList<T> dataSource;
                if (filteredIndices != null)
                {
                    var filtered = new List<T>(filteredIndices.Length);
                    for (int i = 0; i < filteredIndices.Length; i++)
                    {
                        filtered.Add(items[filteredIndices[i]]);
                    }
                    dataSource = filtered;
                }
                else
                {
                    dataSource = items;
                }

                try
                {
                    object result = agg.Compute(dataSource);
                    if (agg.Format != null)
                    {
                        return string.Format($"{{0:{agg.Format}}}", result);
                    }
                    return result?.ToString() ?? "";
                }
                catch
                {
                    return "ERR";
                }
            }
        }
        return "";
    }

    // ── Frozen rows ──────────────────────────────────────────────────

    int ITabularDataNode.FrozenRowCount => Math.Min(frozenRowCount, ((ITabularDataNode)this).RowCount);

    // ── Undo / Redo ──────────────────────────────────────────────────

    bool ITabularDataNode.IsUndoEnabled => undoEnabledValue;

    UndoStack? ITabularDataNode.GetUndoStack()
    {
        if (!undoEnabledValue)
        {
            return null;
        }
        EnsureUndoStack();
        return undoStack;
    }

    bool ITabularDataNode.UndoEdit()
    {
        if (!undoEnabledValue || undoStack == null || !undoStack.CanUndo)
        {
            return false;
        }
        undoStack.Undo();
        return true;
    }

    bool ITabularDataNode.RedoEdit()
    {
        if (!undoEnabledValue || undoStack == null || !undoStack.CanRedo)
        {
            return false;
        }
        undoStack.Redo();
        return true;
    }

    private void EnsureUndoStack()
    {
        undoStack ??= new UndoStack(undoDepthValue);
    }

    private static void ApplyCellValue(T item, DataGridColumn<T> column, string value)
    {
        if (column.textSetter != null)
        {
            column.textSetter(item, value);
        }
        else if (column.objectSetter != null)
        {
            object? parsed = ParseEditValue(value, column);
            if (parsed != null)
            {
                column.objectSetter(item, parsed);
            }
        }
    }

    // ── Clipboard ────────────────────────────────────────────────────

    bool ITabularDataNode.IsClipboardEnabled => clipboardEnabled;

    async Task ITabularDataNode.CopyCellsAsync()
    {
        if (!clipboardEnabled)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        var tdn = (ITabularDataNode)this;

        if (selectedRows.Count > 0)
        {
            // Copy selected rows, sorted by display order
            var sorted = new List<int>(selectedRows);
            sorted.Sort();
            foreach (int row in sorted)
            {
                for (int col = 0; col < tdn.ColumnCount; col++)
                {
                    if (col > 0)
                    {
                        sb.Append('\t');
                    }
                    sb.Append(tdn.GetCellText(row, col));
                }
                sb.AppendLine();
            }
        }
        else if (tdn.SelectedRowIndex >= 0)
        {
            // Copy the single selected row
            int row = tdn.SelectedRowIndex;
            for (int col = 0; col < tdn.ColumnCount; col++)
            {
                if (col > 0)
                {
                    sb.Append('\t');
                }
                sb.Append(tdn.GetCellText(row, col));
            }
            sb.AppendLine();
        }

        if (sb.Length > 0)
        {
            await Clipboard.WriteTextAsync(sb.ToString());
        }
    }

    async Task<bool> ITabularDataNode.CutCellsAsync()
    {
        if (!clipboardEnabled)
        {
            return false;
        }

        // Copy first
        await ((ITabularDataNode)this).CopyCellsAsync();

        // Clear selected cells
        var items = Items.Value;
        var rowsToEdit = selectedRows.Count > 0
            ? new List<int>(selectedRows)
            : (selectedRowIdx >= 0 ? new List<int> { selectedRowIdx } : new List<int>());

        foreach (int row in rowsToEdit)
        {
            int dataRow = MapRow(row);
            if (dataRow < 0 || dataRow >= items.Count)
            {
                continue;
            }
            var item = items[dataRow];
            for (int col = 0; col < Columns.Count; col++)
            {
                if (((ITabularDataNode)this).IsColumnEditable(col) && !((ITabularDataNode)this).IsBoolColumn(col))
                {
                    ApplyCellValue(item, Columns[col], "");
                    onChangeHandler?.Invoke(item);
                }
            }
        }

        return rowsToEdit.Count > 0;
    }

    async Task<bool> ITabularDataNode.PasteCellsAsync()
    {
        if (!clipboardEnabled)
        {
            return false;
        }

        var content = await Clipboard.GetContentAsync();
        if (!content.HasText)
        {
            return false;
        }

        string? text = await content.GetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Parse tab-separated lines
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        var tdn = (ITabularDataNode)this;
        int startRow = tdn.SelectedRowIndex;
        if (startRow < 0)
        {
            startRow = 0;
        }

        var items = Items.Value;
        bool anyPasted = false;

        IDisposable? batch = null;
        if (undoEnabledValue)
        {
            EnsureUndoStack();
            batch = undoStack!.BeginBatch("Paste");
        }

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            int targetRow = startRow + lineIdx;
            if (targetRow >= tdn.RowCount)
            {
                break;
            }

            int dataRow = MapRow(targetRow);
            if (dataRow < 0 || dataRow >= items.Count)
            {
                continue;
            }

            string[] cells = lines[lineIdx].TrimEnd('\r').Split('\t');
            var item = items[dataRow];

            for (int cellIdx = 0; cellIdx < cells.Length && cellIdx < tdn.ColumnCount; cellIdx++)
            {
                if (!tdn.IsColumnEditable(cellIdx) || tdn.IsBoolColumn(cellIdx))
                {
                    continue;
                }

                string newVal = cells[cellIdx];
                var column = Columns[cellIdx];

                if (undoEnabledValue)
                {
                    string oldVal = column.textGetter != null
                        ? column.textGetter(item) ?? ""
                        : column.objectGetter != null
                            ? column.objectGetter(item)?.ToString() ?? ""
                            : "";

                    int capturedDataRow = dataRow;
                    int capturedCol = cellIdx;

                    // Invalidate the row's cached text in each lambda so the apply,
                    // undo, and redo all refresh GetCellText — same reason as
                    // CommitEdit/ApplyBatchEdit (no INotifyPropertyChanged).
                    undoStack!.Execute(UndoCommand.Create(
                        $"Paste {column.Header}",
                        () => { ApplyCellValue(items[capturedDataRow], Columns[capturedCol], newVal); InvalidateCellCache(capturedDataRow); },
                        () => { ApplyCellValue(items[capturedDataRow], Columns[capturedCol], oldVal); InvalidateCellCache(capturedDataRow); }));
                }
                else
                {
                    ApplyCellValue(item, column, newVal);
                    InvalidateCellCache(dataRow);
                }

                anyPasted = true;
            }

            onChangeHandler?.Invoke(item);
        }

        batch?.Dispose();

        return anyPasted;
    }

    // ── Batch edit ───────────────────────────────────────────────────

    bool ITabularDataNode.IsBatchEditEnabled => batchEditEnabled;

    void ITabularDataNode.ApplyBatchEdit(int col, string value)
    {
        if (!batchEditEnabled || col < 0 || col >= Columns.Count)
        {
            return;
        }
        if (!((ITabularDataNode)this).IsColumnEditable(col))
        {
            return;
        }

        var items = Items.Value;
        var column = Columns[col];

        IDisposable? batch = null;
        if (undoEnabledValue)
        {
            EnsureUndoStack();
            batch = undoStack!.BeginBatch($"Batch edit {column.Header}");
        }

        foreach (int row in selectedRows)
        {
            int dataRow = MapRow(row);
            if (dataRow < 0 || dataRow >= items.Count)
            {
                continue;
            }

            var item = items[dataRow];

            if (undoEnabledValue)
            {
                string oldVal = column.textGetter != null
                    ? column.textGetter(item) ?? ""
                    : column.objectGetter != null
                        ? column.objectGetter(item)?.ToString() ?? ""
                        : "";

                int capturedDataRow = dataRow;

                // Invalidate the row's cached text inside each lambda so the
                // initial apply, undo, and redo all refresh GetCellText (Cascade
                // does not use INotifyPropertyChanged, so the cache is stale
                // otherwise — this is the same pattern CommitEdit uses).
                undoStack!.Execute(UndoCommand.Create(
                    $"Batch {column.Header}",
                    () => { ApplyCellValue(items[capturedDataRow], column, value); InvalidateCellCache(capturedDataRow); },
                    () => { ApplyCellValue(items[capturedDataRow], column, oldVal); InvalidateCellCache(capturedDataRow); }));
            }
            else
            {
                ApplyCellValue(item, column, value);
                InvalidateCellCache(dataRow);
            }

            onChangeHandler?.Invoke(item);
        }

        batch?.Dispose();
    }

    // ── Export ───────────────────────────────────────────────────────

    bool ITabularDataNode.IsExportEnabled => isExportEnabled;

    string ITabularDataNode.ExportCsv(GridExportOptions? options)
    {
        if (!isExportEnabled)
        {
            return "";
        }

        var opts = options ?? new GridExportOptions();
        var sb = new System.Text.StringBuilder();
        var items = Items.Value;
        ITabularDataNode tdn = this;

        // Headers
        if (opts.IncludeHeaders)
        {
            bool first = true;
            for (int c = 0; c < Columns.Count; c++)
            {
                if (!opts.IncludeHiddenCols && !tdn.GetColumnVisible(c))
                {
                    continue;
                }
                if (!first)
                {
                    sb.Append(opts.Delimiter);
                }
                first = false;
                AppendCsvField(sb, Columns[c].Header, opts.Delimiter);
            }
            sb.AppendLine();
        }

        // Data rows — respect current sort/filter order
        int rowCount = filteredIndices?.Length ?? items.Count;
        for (int r = 0; r < rowCount; r++)
        {
            int dataRow = MapRow(r);
            if (dataRow < 0 || dataRow >= items.Count)
            {
                continue;
            }

            bool first = true;
            for (int c = 0; c < Columns.Count; c++)
            {
                if (!opts.IncludeHiddenCols && !tdn.GetColumnVisible(c))
                {
                    continue;
                }
                if (!first)
                {
                    sb.Append(opts.Delimiter);
                }
                first = false;
                AppendCsvField(sb, tdn.GetCellText(r, c), opts.Delimiter);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendCsvField(System.Text.StringBuilder sb, string value, char delimiter)
    {
        string delimStr = delimiter.ToString();
        bool needsQuoting = value.Contains(delimStr, StringComparison.Ordinal) ||
                            value.Contains('"', StringComparison.Ordinal) ||
                            value.Contains('\n', StringComparison.Ordinal) ||
                            value.Contains('\r', StringComparison.Ordinal);
        if (needsQuoting)
        {
            sb.Append('"');
            sb.Append(value.Replace("\"", "\"\"", StringComparison.Ordinal));
            sb.Append('"');
        }
        else
        {
            sb.Append(value);
        }
    }

    // ── Layout persistence ──────────────────────────────────────────

    GridLayoutState ITabularDataNode.SaveLayout()
    {
        var order = new string[Columns.Count];
        var widths = new Dictionary<string, float>();
        var visibility = new Dictionary<string, bool>();

        for (int c = 0; c < Columns.Count; c++)
        {
            var col = Columns[c];
            order[c] = col.Header;
            if (col.widthValue is { } w)
            {
                widths[col.Header] = w;
            }
            visibility[col.Header] = ((ITabularDataNode)this).GetColumnVisible(c);
        }

        return new GridLayoutState
        {
            ColumnOrder = order,
            ColumnWidths = widths,
            ColumnVisibility = visibility,
            SortColumn = sortColumnIdx >= 0 && sortColumnIdx < Columns.Count
                ? Columns[sortColumnIdx].Header
                : null,
            SortDirectionValue = sortColumnIdx >= 0 ? currentSortDirection : null,
        };
    }

    bool ITabularDataNode.HasPendingLayoutRestore => restoredLayout != null;

    void ITabularDataNode.ApplyRestoredLayout()
    {
        if (restoredLayout == null)
        {
            return;
        }

        var layout = restoredLayout;
        restoredLayout = null;

        // Restore column order by reordering columns list to match saved order
        var headerToCol = new Dictionary<string, DataGridColumn<T>>();
        foreach (var col in Columns)
        {
            headerToCol[col.Header] = col;
        }

        var reordered = new List<DataGridColumn<T>>();
        foreach (string header in layout.ColumnOrder)
        {
            if (headerToCol.TryGetValue(header, out var col))
            {
                reordered.Add(col);
                headerToCol.Remove(header);
            }
        }
        // Append any columns not in the saved order (new columns added since save)
        foreach (var remaining in headerToCol.Values)
        {
            reordered.Add(remaining);
        }

        columns.Clear();
        columns.AddRange(reordered);

        // Restore column widths
        for (int c = 0; c < Columns.Count; c++)
        {
            if (layout.ColumnWidths.TryGetValue(Columns[c].Header, out float w))
            {
                Columns[c].widthValue = w;
            }
        }

        // Restore column visibility
        runtimeColumnVisibility ??= new Dictionary<int, bool>();
        for (int c = 0; c < Columns.Count; c++)
        {
            if (layout.ColumnVisibility.TryGetValue(Columns[c].Header, out bool vis))
            {
                runtimeColumnVisibility[c] = vis;
            }
        }

        // Restore sort state
        if (layout.SortColumn != null && layout.SortDirectionValue.HasValue)
        {
            for (int c = 0; c < Columns.Count; c++)
            {
                if (Columns[c].Header == layout.SortColumn)
                {
                    sortColumnIdx = c;
                    currentSortDirection = layout.SortDirectionValue.Value;
                    break;
                }
            }
        }
    }

    // ── Column chooser ──────────────────────────────────────────────

    bool ITabularDataNode.IsColumnChooserEnabled => columnChooserEnabled;
    bool ITabularDataNode.IsColumnChooserOpen => columnChooserOpen;

    void ITabularDataNode.ToggleColumnChooser()
    {
        columnChooserOpen = !columnChooserOpen;
        columnChooserHoverIndex = -1;
    }

    bool ITabularDataNode.GetColumnVisible(int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return false;
        }
        // Runtime visibility overrides take priority
        if (runtimeColumnVisibility != null && runtimeColumnVisibility.TryGetValue(col, out bool vis))
        {
            return vis;
        }
        // Then check the configuration-time visibility map
        if (columnVisibilityMap != null && columnVisibilityMap.TryGetValue(Columns[col].Header, out bool mapVis))
        {
            return mapVis;
        }
        // Fall back to the column's default
        return Columns[col].visibleValue;
    }

    void ITabularDataNode.ToggleColumnVisibility(int col)
    {
        if (col < 0 || col >= Columns.Count)
        {
            return;
        }
        runtimeColumnVisibility ??= new Dictionary<int, bool>();
        bool current = ((ITabularDataNode)this).GetColumnVisible(col);
        // Don't allow hiding the last visible column
        if (current && ((ITabularDataNode)this).VisibleColumnCount <= 1)
        {
            return;
        }
        runtimeColumnVisibility[col] = !current;
    }

    int ITabularDataNode.VisibleColumnCount
    {
        get
        {
            int count = 0;
            for (int c = 0; c < Columns.Count; c++)
            {
                if (((ITabularDataNode)this).GetColumnVisible(c))
                {
                    count++;
                }
            }
            return count;
        }
    }

    Rect ITabularDataNode.ColumnChooserBounds
    {
        get => columnChooserBounds;
        set => columnChooserBounds = value;
    }

    int ITabularDataNode.ColumnChooserHoverIndex
    {
        get => columnChooserHoverIndex;
        set => columnChooserHoverIndex = value;
    }

    Rect ITabularDataNode.ColumnChooserButtonBounds
    {
        get => columnChooserButtonBounds;
        set => columnChooserButtonBounds = value;
    }

    // ── Validation ────────────────────────────────────────────────────

    bool ITabularDataNode.HasCellError(int row, int col)
    {
        int dataRow = MapRow(row);
        return validationErrors.ContainsKey((dataRow, col));
    }

    string? ITabularDataNode.GetCellErrorMessage(int row, int col)
    {
        int dataRow = MapRow(row);
        if (validationErrors.TryGetValue((dataRow, col), out var result))
        {
            return result.Message;
        }
        return null;
    }

    int ITabularDataNode.HoveredColIndex
    {
        get => hoveredColIndex;
        set => hoveredColIndex = value;
    }

    void ITabularDataNode.ValidateRow(int displayRow)
    {
        int dataRow = MapRow(displayRow);
        var items = Items.Value;
        if (dataRow < 0 || dataRow >= items.Count)
        {
            return;
        }

        var item = items[dataRow];

        // Run per-cell validators
        for (int c = 0; c < Columns.Count; c++)
        {
            var column = Columns[c];
            if (column.cellValidator != null)
            {
                object? cellValue = column.objectGetter != null
                    ? column.objectGetter(item)
                    : column.textGetter != null
                        ? column.textGetter(item)
                        : column.boolGetter != null
                            ? (object)column.boolGetter(item)
                            : null;

                var result = column.cellValidator(cellValue!);
                if (result.Status == ValidationStatus.Error)
                {
                    validationErrors[(dataRow, c)] = result;
                }
                else
                {
                    validationErrors.Remove((dataRow, c));
                }
            }
        }

        // Run row-level validator (produces errors keyed to col -1)
        if (rowValidator != null)
        {
            var rowResult = rowValidator(item);
            if (rowResult.Status == ValidationStatus.Error)
            {
                validationErrors[(dataRow, -1)] = rowResult;
            }
            else
            {
                validationErrors.Remove((dataRow, -1));
            }
        }
    }

    // ── Virtualization & Scroll ───────────────────────────────────────

    float ITabularDataNode.ScrollOffsetY
    {
        get => scrollOffsetY;
        set => scrollOffsetY = Math.Clamp(value, 0, ((ITabularDataNode)this).MaxScrollOffsetY);
    }

    float ITabularDataNode.ScrollOffsetX
    {
        get => scrollOffsetX;
        set => scrollOffsetX = Math.Clamp(value, 0, ((ITabularDataNode)this).MaxScrollOffsetX);
    }

    float ITabularDataNode.MaxScrollOffsetY
    {
        get
        {
            float content = ((ITabularDataNode)this).TotalContentHeight;
            return Math.Max(0, content - viewportHeight);
        }
    }

    float ITabularDataNode.MaxScrollOffsetX => 0f;

    float ITabularDataNode.ViewportHeight
    {
        get => viewportHeight;
        set => viewportHeight = value;
    }

    float ITabularDataNode.TotalContentHeight => ComputeTotalDataContentHeight();

    int ITabularDataNode.VirtualizationBufferRows => virtualizationBufferRows;

    int? ITabularDataNode.MaxVisibleRows => maxVisibleRows;

    void ITabularDataNode.ScrollIntoView(int displayRow)
    {
        var tdn = (ITabularDataNode)this;
        float rowHeight = tdn.GetRowHeight();

        // Compute the Y offset of this row within the data content area
        float rowTop;
        if (tdn.IsGrouped)
        {
            rowTop = ComputeGroupedRowOffset(displayRow, rowHeight);
        }
        else
        {
            rowTop = ComputeFlatRowOffset(displayRow, rowHeight);
        }

        float rowBottom = rowTop + rowHeight;

        if (rowTop < scrollOffsetY)
        {
            tdn.ScrollOffsetY = rowTop;
        }
        else if (rowBottom > scrollOffsetY + viewportHeight)
        {
            tdn.ScrollOffsetY = rowBottom - viewportHeight;
        }
    }

    private float ComputeTotalDataContentHeight()
    {
        var tdn = (ITabularDataNode)this;
        float rowHeight = tdn.GetRowHeight();
        const float groupHeaderHeight = 32f;
        float total = 0;

        if (tdn.IsGrouped)
        {
            for (int g = 0; g < tdn.GroupCount; g++)
            {
                total += groupHeaderHeight;
                if (!tdn.IsGroupCollapsed(g))
                {
                    int groupRowCount = tdn.GetGroupRowCount(g);
                    total += groupRowCount * rowHeight;

                    if (tdn.HasRowDetail)
                    {
                        for (int r = 0; r < groupRowCount; r++)
                        {
                            int dataRow = tdn.GetGroupDataRowIndex(g, r);
                            if (tdn.IsRowExpanded(dataRow))
                            {
                                total += tdn.GetRowDetailHeight(dataRow);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            total = tdn.RowCount * rowHeight;

            if (tdn.HasRowDetail)
            {
                for (int r = 0; r < tdn.RowCount; r++)
                {
                    if (tdn.IsRowExpanded(r))
                    {
                        total += tdn.GetRowDetailHeight(r);
                    }
                }
            }
        }

        return total;
    }

    private float ComputeFlatRowOffset(int displayRow, float rowHeight)
    {
        var tdn = (ITabularDataNode)this;
        float offset = displayRow * rowHeight;

        if (tdn.HasRowDetail)
        {
            for (int r = 0; r < displayRow; r++)
            {
                if (tdn.IsRowExpanded(r))
                {
                    offset += tdn.GetRowDetailHeight(r);
                }
            }
        }

        return offset;
    }

    private float ComputeGroupedRowOffset(int displayRow, float rowHeight)
    {
        var tdn = (ITabularDataNode)this;
        const float groupHeaderHeight = 32f;
        float offset = 0;

        for (int g = 0; g < tdn.GroupCount; g++)
        {
            offset += groupHeaderHeight;
            if (!tdn.IsGroupCollapsed(g))
            {
                int groupRowCount = tdn.GetGroupRowCount(g);
                for (int r = 0; r < groupRowCount; r++)
                {
                    int dataRow = tdn.GetGroupDataRowIndex(g, r);
                    if (dataRow == displayRow)
                    {
                        return offset;
                    }
                    offset += rowHeight;
                    if (tdn.HasRowDetail && tdn.IsRowExpanded(dataRow))
                    {
                        offset += tdn.GetRowDetailHeight(dataRow);
                    }
                }
            }
        }

        return offset;
    }

    /// <summary>
    /// Rebuilds filtered indices from the current column filters and global filter.
    /// Called when any filter text changes. Also rebuilds sort and groups.
    /// </summary>
    internal void RebuildFilteredIndices()
    {
        var items = Items.Value;
        int dataCount = items.Count;
        string globalText = globalFilterBinding?.Value ?? "";
        bool hasAnyFilter = globalText.Length > 0;

        if (!hasAnyFilter && columnFilters != null)
        {
            for (int i = 0; i < columnFilters.Length; i++)
            {
                if (columnFilters[i].Length > 0)
                {
                    hasAnyFilter = true;
                    break;
                }
            }
        }

        if (!hasAnyFilter)
        {
            filteredIndices = null;
        }
        else
        {
            var passing = new List<int>(dataCount);
            for (int r = 0; r < dataCount; r++)
            {
                if (RowPassesFilter(items[r], r, globalText))
                {
                    passing.Add(r);
                }
            }
            filteredIndices = passing.ToArray();
        }

        // Rebuild sort from new filtered set
        if (sortColumnIdx >= 0 && sortableEnabled)
        {
            var items2 = Items.Value;
            int count;
            if (filteredIndices != null)
            {
                count = filteredIndices.Length;
                sortedIndices = new int[count];
                Array.Copy(filteredIndices, sortedIndices, count);
            }
            else
            {
                count = items2.Count;
                sortedIndices = new int[count];
                for (int i = 0; i < count; i++)
                {
                    sortedIndices[i] = i;
                }
            }
            var column = Columns[sortColumnIdx];
            Array.Sort(sortedIndices, (a, b) =>
            {
                int cmp = CompareSortValues(items2[a], items2[b], column);
                return currentSortDirection == SortDirection.Ascending ? cmp : -cmp;
            });
        }
        else
        {
            sortedIndices = null;
        }

        // Rebuild groups
        RebuildGroupedRows();

        // Clear selection
        selectedRowIdx = -1;
        selectedRows.Clear();
        anchorRow = -1;
    }

    private bool RowPassesFilter(T item, int dataRow, string globalText)
    {
        // Check per-column filters
        if (columnFilters != null)
        {
            for (int c = 0; c < Columns.Count && c < columnFilters.Length; c++)
            {
                string filter = columnFilters[c];
                if (filter.Length == 0)
                {
                    continue;
                }
                string cellText = GetRawCellText(item, c);
                if (!cellText.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        // Check global filter
        if (globalText.Length > 0)
        {
            bool found = false;
            for (int c = 0; c < Columns.Count; c++)
            {
                string cellText = GetRawCellText(item, c);
                if (cellText.Contains(globalText, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private string GetRawCellText(T item, int col)
    {
        var column = Columns[col];
        if (column.textGetter != null)
        {
            return column.textGetter(item) ?? "";
        }
        if (column.objectGetter != null)
        {
            object? val = column.objectGetter(item);
            if (column.formatString != null && val is IFormattable fmt)
            {
                return fmt.ToString(column.formatString, null);
            }
            return val?.ToString() ?? "";
        }
        if (column.boolGetter != null)
        {
            return column.boolGetter(item) ? "True" : "False";
        }
        if (column.computeFunc != null)
        {
            object? val = column.computeFunc(item);
            return val?.ToString() ?? "";
        }
        return "";
    }

    private static object? ParseEditValue(string text, DataGridColumn<T> column)
    {
        if (column.objectGetter != null)
        {
            // For Number columns, try decimal first (most common in financial/business data)
            // This prevents type mismatches where "50" parses as int but setter expects decimal
            if (column.kind == DataColumnKind.Number)
            {
                if (decimal.TryParse(text, System.Globalization.NumberStyles.Currency,
                    System.Globalization.CultureInfo.CurrentCulture, out decimal decVal))
                {
                    return decVal;
                }
                if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.CurrentCulture, out double dblVal))
                {
                    return dblVal;
                }
                if (int.TryParse(text, out int intVal))
                {
                    return intVal;
                }
                return text;
            }

            // For other column types, try common numeric types
            if (int.TryParse(text, out int intVal2))
            {
                return intVal2;
            }
            if (decimal.TryParse(text, out decimal decVal2))
            {
                return decVal2;
            }
            if (double.TryParse(text, out double dblVal2))
            {
                return dblVal2;
            }
            if (DateTime.TryParse(text, out DateTime dtVal))
            {
                return dtVal;
            }
            return text;
        }
        return text;
    }

    void ITabularDataNode.ApplySort(int col)
    {
        if (!sortableEnabled)
        {
            return;
        }

        if (sortColumnIdx == col)
        {
            if (currentSortDirection == SortDirection.Ascending)
            {
                currentSortDirection = SortDirection.Descending;
            }
            else
            {
                // Third click on same column: clear sort
                sortColumnIdx = -1;
                sortedIndices = null;
                selectedRowIdx = -1;
                selectedRows.Clear();
                anchorRow = -1;
                RebuildGroupedRows();
                return;
            }
        }
        else
        {
            sortColumnIdx = col;
            currentSortDirection = SortDirection.Ascending;
        }

        // Build sorted index from filtered set (or all rows)
        var items = Items.Value;
        int count;
        if (filteredIndices != null)
        {
            count = filteredIndices.Length;
            sortedIndices = new int[count];
            Array.Copy(filteredIndices, sortedIndices, count);
        }
        else
        {
            count = items.Count;
            sortedIndices = new int[count];
            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
            }
        }

        var column = Columns[col];
        Array.Sort(sortedIndices, (a, b) =>
        {
            int cmp = CompareSortValues(items[a], items[b], column);
            return currentSortDirection == SortDirection.Ascending ? cmp : -cmp;
        });

        // Clear selection since row indices changed
        selectedRowIdx = -1;
        selectedRows.Clear();
        anchorRow = -1;

        // Rebuild groups from new sort order
        RebuildGroupedRows();
    }

    private static int CompareSortValues(T itemA, T itemB, DataGridColumn<T> column)
    {
        if (column.objectGetter != null)
        {
            var valA = column.objectGetter(itemA);
            var valB = column.objectGetter(itemB);
            if (valA is IComparable compA)
            {
                return compA.CompareTo(valB);
            }
            return string.Compare(valA?.ToString() ?? "", valB?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
        if (column.textGetter != null)
        {
            return string.Compare(column.textGetter(itemA), column.textGetter(itemB), StringComparison.OrdinalIgnoreCase);
        }
        if (column.boolGetter != null)
        {
            return column.boolGetter(itemA).CompareTo(column.boolGetter(itemB));
        }
        if (column.computeFunc != null)
        {
            var valA = column.computeFunc(itemA);
            var valB = column.computeFunc(itemB);
            if (valA is IComparable compA)
            {
                return compA.CompareTo(valB);
            }
            return string.Compare(valA?.ToString() ?? "", valB?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
        return 0;
    }

    void ITabularDataNode.SelectRow(int row, bool ctrl, bool shift)
    {
        var items = Items.Value;
        if (row < 0 || row >= items.Count)
        {
            return;
        }

        if (shift && anchorRow >= 0)
        {
            selectedRows.Clear();
            int lo = Math.Min(anchorRow, row);
            int hi = Math.Max(anchorRow, row);
            for (int i = lo; i <= hi; i++)
            {
                selectedRows.Add(i);
            }
            selectedRowIdx = row;
        }
        else if (ctrl)
        {
            if (!selectedRows.Add(row))
            {
                selectedRows.Remove(row);
            }
            selectedRowIdx = row;
            anchorRow = row;
        }
        else
        {
            selectedRows.Clear();
            selectedRows.Add(row);
            selectedRowIdx = row;
            anchorRow = row;
        }
    }

    void ITabularDataNode.MoveSelection(int delta)
    {
        int newRow = Math.Clamp(selectedRowIdx + delta, 0, Items.Value.Count - 1);
        ((ITabularDataNode)this).SelectRow(newRow, false, false);
    }

    void ITabularDataNode.SelectFirst()
    {
        if (Items.Value.Count > 0)
        {
            ((ITabularDataNode)this).SelectRow(0, false, false);
        }
    }

    void ITabularDataNode.SelectLast()
    {
        if (Items.Value.Count > 0)
        {
            ((ITabularDataNode)this).SelectRow(Items.Value.Count - 1, false, false);
        }
    }

    ColumnAlignment ITabularDataNode.GetColumnAlignment(int col)
    {
        return Columns[col].alignValue ?? ColumnAlignment.Left;
    }

    float ITabularDataNode.GetColumnWidth(int col, float availableWidth)
    {
        // Hidden columns have zero width
        if (!((ITabularDataNode)this).GetColumnVisible(col))
        {
            return 0f;
        }

        var column = Columns[col];
        if (column.widthValue.HasValue)
        {
            return column.widthValue.Value;
        }

        // Fill column: distribute remaining space after fixed-width columns
        float fixedTotal = 0f;
        int fillCount = 0;
        var self = (ITabularDataNode)this;
        for (int c = 0; c < Columns.Count; c++)
        {
            if (!self.GetColumnVisible(c))
            {
                continue;
            }
            if (Columns[c].widthValue is { } w)
            {
                fixedTotal += w;
            }
            else
            {
                fillCount++;
            }
        }

        float remaining = availableWidth - fixedTotal;
        if (remaining < 0f)
        {
            remaining = 0f;
        }

        float fillWidth = fillCount > 0 ? remaining / fillCount : 0f;

        // Respect MinWidth constraint
        if (column.minWidthValue.HasValue && fillWidth < column.minWidthValue.Value)
        {
            fillWidth = column.minWidthValue.Value;
        }

        return fillWidth;
    }

    string ITabularDataNode.GetCellText(int row, int col)
    {
        int dataRow = MapRow(row);
        int colCount = Columns.Count;
        int idx = dataRow * colCount + col;

        var cache = cellTextCache;
        if (cache != null && (uint)idx < (uint)cache.Length)
        {
            var hit = cache[idx];
            if (hit != null)
            {
                return hit;
            }
        }

        var items = Items.Value;
        var item = items[dataRow];
        var column = Columns[col];
        string text;

        if (column.textGetter != null)
        {
            text = column.textGetter(item);
        }
        else if (column.objectGetter != null)
        {
            var val = column.objectGetter(item);
            text = column.formatString != null
                ? string.Format(column.ComposedFormat, val)
                : val?.ToString() ?? "";
        }
        else if (column.boolGetter != null)
        {
            text = column.boolGetter(item) ? "true" : "false";
        }
        else if (column.computeFunc != null)
        {
            var val = column.computeFunc(item);
            text = column.formatString != null
                ? string.Format(column.ComposedFormat, val)
                : val?.ToString() ?? "";
        }
        else
        {
            text = "";
        }

        // Allocate or resize cache on first miss. Row count changes for a
        // given data-source identity (add/delete rows) force a resize +
        // full invalidate; this is rare and acceptable.
        int needed = items.Count * colCount;
        if (cache == null || cache.Length != needed)
        {
            cache = new string?[needed];
            cellTextCache = cache;
        }
        if ((uint)idx < (uint)cache.Length)
        {
            cache[idx] = text;
        }
        return text;
    }

    /// <summary>
    /// Backing flat cell text cache. Populated lazily in GetCellText;
    /// transferred across re-renders by the reconciler when Items reference
    /// is unchanged. Invalidated on edit commit, add/delete row.
    /// </summary>
    internal string?[]? cellTextCache;

    string?[]? ITabularDataNode.CellTextCache
    {
        get => cellTextCache;
        set => cellTextCache = value;
    }

    object? ITabularDataNode.CellTextCacheKey => Items.Value;

    /// <summary>
    /// Invalidates the entire cell text cache. Call this after mutating
    /// properties on row items that would change their displayed text,
    /// since Cascade does not use INotifyPropertyChanged.
    /// </summary>
    public void InvalidateCellCache()
    {
        cellTextCache.AsSpan().Clear();
    }

    /// <summary>
    /// Invalidates cached text for all cells in a single data-row (the index
    /// into the underlying Items list, not the display row).
    /// </summary>
    /// <param name="dataRow">Index into <see cref="Items"/>'s value list.</param>
    public void InvalidateCellCache(int dataRow)
    {
        var cache = cellTextCache;
        if (cache == null)
        {
            return;
        }
        int colCount = Columns.Count;
        int start = dataRow * colCount;
        if ((uint)start >= (uint)cache.Length)
        {
            return;
        }
        cache.AsSpan(start, colCount).Clear();
    }
}

/// <summary>
/// A column definition for <see cref="DataGrid{T}"/>.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class DataGridColumn<T>
{
    private DataGridColumn(string header)
    {
        Header = header;
    }

    /// <summary>Column header text.</summary>
    public string Header { get; }

    // ── Internal state ────────────────────────────────────────────────

    internal DataColumnKind kind;
    internal Func<T, string>? textGetter;
    internal Action<T, string>? textSetter;
    internal Func<T, object>? objectGetter;
    internal Action<T, object>? objectSetter;
    internal Func<T, bool>? boolGetter;
    internal Action<T, bool>? boolSetter;
    internal Func<T, object>? computeFunc;
    internal Func<T, Node>? displayRenderer;
    internal Func<T, Node>? editorRenderer;
    internal IReadOnlyList<object>? selectOptions;
    internal object? minValue;
    internal string? formatString;
    // Cached "{0:format}" template, built lazily once per column. Avoids
    // allocating a fresh interpolation string on every GetCellText call
    // (DataGrid's paint loop hits this once per visible cell per paint).
    private string? composedFormat;
    internal string ComposedFormat => composedFormat ??= formatString is null
        ? "{0}"
        : string.Concat("{0:", formatString, "}");
    internal bool isReadOnly;
    internal bool isMultiLine;
    internal float? widthValue;
    internal DataColumnWidth? widthStrategy;
    internal float? minWidthValue;
    internal float? maxWidthValue;
    internal bool? sortableValue;
    internal bool? resizableValue;
    internal ColumnPin? pinValue;
    internal ColumnAlignment? alignValue;
    internal bool visibleValue = true;
    internal Func<object, ValidationResult>? cellValidator;

    // ── Factory methods ───────────────────────────────────────────────

    /// <summary>Creates an editable text column.</summary>
    public static DataGridColumn<T> Text(string header, Func<T, string> get, Action<T, string> set)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Text,
            textGetter = get,
            textSetter = set,
            alignValue = ColumnAlignment.Left,
        };
    }

    /// <summary>Creates an editable numeric column.</summary>
    public static DataGridColumn<T> Number(string header, Func<T, object> get, Action<T, object> set, object? min = null, string? format = null)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Number,
            objectGetter = get,
            objectSetter = set,
            minValue = min,
            formatString = format,
            alignValue = ColumnAlignment.Right,
        };
    }

    /// <summary>Creates an editable date column.</summary>
    public static DataGridColumn<T> Date(string header, Func<T, object> get, Action<T, object> set)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Date,
            objectGetter = get,
            objectSetter = set,
        };
    }

    /// <summary>Creates an editable select (dropdown) column.</summary>
    public static DataGridColumn<T> Select(string header, Func<T, object> get, Action<T, object> set, IReadOnlyList<object> options)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Select,
            objectGetter = get,
            objectSetter = set,
            selectOptions = options,
        };
    }

    /// <summary>Creates an editable boolean (toggle) column.</summary>
    public static DataGridColumn<T> Bool(string header, Func<T, bool> get, Action<T, bool> set)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Bool,
            boolGetter = get,
            boolSetter = set,
            alignValue = ColumnAlignment.Center,
        };
    }

    /// <summary>Creates an editable multi-line text column.</summary>
    public static DataGridColumn<T> MultiLine(string header, Func<T, string> get, Action<T, string> set)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Text,
            textGetter = get,
            textSetter = set,
            isMultiLine = true,
            alignValue = ColumnAlignment.Left,
        };
    }

    /// <summary>Creates a read-only computed column.</summary>
    public static DataGridColumn<T> Computed(string header, Func<T, object> compute, string? format = null)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Computed,
            computeFunc = compute,
            formatString = format,
            isReadOnly = true,
        };
    }

    /// <summary>Creates a custom column with separate display and edit renderers.</summary>
    public static DataGridColumn<T> Custom(string header, Func<T, object> get, Action<T, object> set, Func<T, Node> render, Func<T, Node> editor)
    {
        return new DataGridColumn<T>(header)
        {
            kind = DataColumnKind.Custom,
            objectGetter = get,
            objectSetter = set,
            displayRenderer = render,
            editorRenderer = editor,
        };
    }

    // ── Column options (fluent) ───────────────────────────────────────

    /// <summary>Sets fixed column width in logical pixels.</summary>
    public DataGridColumn<T> Width(float width)
    {
        widthValue = width;
        widthStrategy = null;
        return this;
    }

    /// <summary>Sets column width strategy (Fill or Auto).</summary>
    public DataGridColumn<T> Width(DataColumnWidth width)
    {
        widthStrategy = width;
        widthValue = null;
        return this;
    }

    /// <summary>Sets minimum column width.</summary>
    public DataGridColumn<T> MinWidth(float minWidth)
    {
        minWidthValue = minWidth;
        return this;
    }

    /// <summary>Sets maximum column width.</summary>
    public DataGridColumn<T> MaxWidth(float maxWidth)
    {
        maxWidthValue = maxWidth;
        return this;
    }

    /// <summary>Enables or disables sorting on this column.</summary>
    public DataGridColumn<T> Sortable(bool enabled)
    {
        sortableValue = enabled;
        return this;
    }

    /// <summary>Enables or disables column resizing.</summary>
    public DataGridColumn<T> Resizable(bool enabled)
    {
        resizableValue = enabled;
        return this;
    }

    /// <summary>Pins (freezes) the column to the left or right edge.</summary>
    public DataGridColumn<T> Pinned(ColumnPin pin)
    {
        pinValue = pin;
        return this;
    }

    /// <summary>Sets cell content alignment.</summary>
    public DataGridColumn<T> Align(ColumnAlignment alignment)
    {
        alignValue = alignment;
        return this;
    }

    /// <summary>Sets initial column visibility.</summary>
    public DataGridColumn<T> Visible(bool visible)
    {
        visibleValue = visible;
        return this;
    }

    /// <summary>Adds per-cell validation.</summary>
    public DataGridColumn<T> Validate(Func<object, ValidationResult> validator)
    {
        cellValidator = validator;
        return this;
    }
}

/// <summary>
/// Identifies the type of data a <see cref="DataGridColumn{T}"/> holds.
/// </summary>
internal enum DataColumnKind
{
    Text,
    Number,
    Date,
    Select,
    Bool,
    Computed,
    Custom,
}

/// <summary>
/// How cells enter edit mode in a <see cref="DataGrid{T}"/>.
/// </summary>
public enum GridEditMode
{
    /// <summary>Single click enters edit mode (default).</summary>
    ClickToEdit,

    /// <summary>Double click enters edit mode.</summary>
    DoubleClick,

    /// <summary>All cells show editors at all times (spreadsheet style).</summary>
    AlwaysEditing
}

/// <summary>
/// Cell selection mode for <see cref="DataGrid{T}"/>.
/// </summary>
public enum CellSelectionMode
{
    /// <summary>One cell at a time (default).</summary>
    Single,

    /// <summary>Click and drag or Shift+click to select a rectangular block.</summary>
    Range,

    /// <summary>Ctrl+click to add non-contiguous ranges (Excel-style).</summary>
    MultiRange,

    /// <summary>Entire rows only — disables cell selection.</summary>
    RowOnly
}

/// <summary>
/// Position for new rows added to a <see cref="DataGrid{T}"/>.
/// </summary>
public enum RowAddPosition
{
    /// <summary>New row added at the bottom.</summary>
    Bottom,

    /// <summary>New row added at the top.</summary>
    Top
}

/// <summary>
/// Expand behavior for row detail panels.
/// </summary>
public enum RowDetailMode
{
    /// <summary>Only one row expanded at a time (default).</summary>
    Single,

    /// <summary>Multiple rows can be expanded simultaneously.</summary>
    Multi
}

/// <summary>
/// Export format for data grid export.
/// </summary>
public enum GridExportFormat
{
    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>Open XML spreadsheet format.</summary>
    Xlsx
}

/// <summary>
/// Scope of rows included in a data grid export.
/// </summary>
public enum GridExportScope
{
    /// <summary>All rows (respecting current filters).</summary>
    All,

    /// <summary>Only selected rows.</summary>
    SelectedRows,

    /// <summary>Current page (server-side pagination).</summary>
    CurrentPage
}

/// <summary>
/// Position of an aggregate (summary) row.
/// </summary>
public enum AggregatePosition
{
    /// <summary>Aggregate row at the top of the grid.</summary>
    Top,

    /// <summary>Aggregate row at the bottom of the grid.</summary>
    Bottom
}

/// <summary>
/// Sort order for grouped rows in a <see cref="DataGrid{T}"/>.
/// </summary>
public enum GroupOrder
{
    /// <summary>Groups sorted ascending by key.</summary>
    Ascending,

    /// <summary>Groups sorted descending by key.</summary>
    Descending,

    /// <summary>Groups presented in data source order.</summary>
    Preserve
}

/// <summary>
/// Position of per-group aggregate rows.
/// </summary>
public enum GroupAggregatePosition
{
    /// <summary>Aggregate row at the top of the group.</summary>
    Top,

    /// <summary>Aggregate row at the bottom of the group.</summary>
    Bottom
}

/// <summary>
/// An aggregate computation for a single column.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class ColumnAggregate<T>
{
    /// <summary>Creates a column aggregate.</summary>
    /// <param name="columnHeader">The header of the column to aggregate.</param>
    /// <param name="compute">
    /// Aggregate function, or null to skip this column in the aggregate row.
    /// </param>
    /// <param name="format">Display format string for the result.</param>
    public ColumnAggregate(string columnHeader, Func<IReadOnlyList<T>, object>? compute, string? format = null)
    {
        ColumnHeader = columnHeader;
        Compute = compute;
        Format = format;
    }

    /// <summary>The header of the column.</summary>
    public string ColumnHeader { get; }

    /// <summary>The aggregate function.</summary>
    public Func<IReadOnlyList<T>, object>? Compute { get; }

    /// <summary>Display format string.</summary>
    public string? Format { get; }
}

/// <summary>
/// Export options for data grid CSV/XLSX export.
/// </summary>
public sealed class GridExportOptions
{
    /// <summary>Include column headers in the export. Default: true.</summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>Include hidden columns. Default: false.</summary>
    public bool IncludeHiddenCols { get; init; }

    /// <summary>Include group headers. Default: true.</summary>
    public bool IncludeGroupHeaders { get; init; } = true;

    /// <summary>CSV delimiter character. Default: comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>XLSX sheet name. Default: "Sheet1".</summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>Freeze the header row in XLSX. Default: true.</summary>
    public bool FreezeHeaderRow { get; init; } = true;

    /// <summary>Auto-fit column widths in XLSX. Default: true.</summary>
    public bool AutoFitColumns { get; init; } = true;

    /// <summary>Apply column format strings as number formats. Default: true.</summary>
    public bool NumberFormats { get; init; } = true;
}

/// <summary>
/// Serializable grid layout state for persistence of column order, widths,
/// visibility, sort state, and group configuration.
/// </summary>
public sealed class GridLayoutState
{
    /// <summary>Column order as an array of column header strings.</summary>
    public required string[] ColumnOrder { get; init; }

    /// <summary>Column visibility map.</summary>
    public required IReadOnlyDictionary<string, bool> ColumnVisibility { get; init; }

    /// <summary>Column widths map.</summary>
    public required IReadOnlyDictionary<string, float> ColumnWidths { get; init; }

    /// <summary>Current sort column header, or null if unsorted.</summary>
    public string? SortColumn { get; init; }

    /// <summary>Current sort direction.</summary>
    public SortDirection? SortDirectionValue { get; init; }
}

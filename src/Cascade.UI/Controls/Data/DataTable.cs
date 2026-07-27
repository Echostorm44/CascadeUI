namespace Cascade.UI;

/// <summary>
/// Non-generic interface for data table/grid rendering. Enables the layout solver
/// and painter to work with generic DataTable{T} and DataGrid{T} without knowing T.
/// </summary>
internal interface ITabularDataNode
{
    int RowCount { get; }
    int ColumnCount { get; }
    string GetColumnHeader(int col);
    string GetCellText(int row, int col);
    ColumnAlignment GetColumnAlignment(int col);
    float GetColumnWidth(int col, float availableWidth);
    bool IsStriped { get; }
    float GetRowHeight();

    // ── Interactive state ─────────────────────────────────────────────

    /// <summary>Whether this column uses a boolean accessor (rendered as circle indicator).</summary>
    bool IsBoolColumn(int col);

    /// <summary>Gets the raw boolean value for a bool column cell.</summary>
    bool GetBoolValue(int row, int col);

    /// <summary>Whether this column uses a custom render callback (Node-based content).</summary>
    bool IsCustomColumn(int col);

    /// <summary>Gets the rendered Node for a custom column cell.</summary>
    Node? GetCustomCellNode(int row, int col);

    /// <summary>Whether the table supports sorting.</summary>
    bool IsSortable { get; }

    /// <summary>Whether a specific column is sortable.</summary>
    bool IsColumnSortable(int col);

    /// <summary>Current sort column index, or -1 if unsorted.</summary>
    int SortColumnIndex { get; }

    /// <summary>Current sort direction.</summary>
    SortDirection SortDirectionValue { get; }

    /// <summary>Currently selected row index (primary/anchor), or -1 if no selection.</summary>
    int SelectedRowIndex { get; }

    /// <summary>Returns true if the given display row is part of the current selection.</summary>
    bool IsRowSelected(int row);

    /// <summary>Currently hovered row index, or -1 if no hover.</summary>
    int HoveredRowIndex { get; set; }

    /// <summary>Applies a sort on the given column.</summary>
    void ApplySort(int col);

    /// <summary>Selects the given row with optional modifier key behavior.</summary>
    void SelectRow(int row, bool ctrl, bool shift);

    /// <summary>Moves the selection by the given delta (positive = down, negative = up).</summary>
    void MoveSelection(int delta);

    /// <summary>Selects the first row.</summary>
    void SelectFirst();

    /// <summary>Selects the last row.</summary>
    void SelectLast();

    /// <summary>Whether hover highlighting is enabled.</summary>
    bool IsHoverHighlightEnabled { get; }

    /// <summary>Number of visible rows in the current viewport, set by the painter.</summary>
    int VisibleRowCount { get; set; }

    /// <summary>Absolute bounds in window coordinates, set by the painter for hit-testing.</summary>
    Rect AbsoluteBounds { get; set; }

    // ── Cell text cache (perf) ─────────────────────────────────────────
    // The cache avoids re-running GetCellText's accessor + formatting path
    // (which allocates a fresh string per call for value-type cells) on every
    // paint. The reconciler transfers the cache across re-renders when the
    // Items reference is unchanged; see Reconciler.TransferInteractiveState.

    /// <summary>
    /// Backing flat cache indexed as [dataRow * ColumnCount + col]. A flat
    /// array avoids the hashing/bucket overhead of a Dictionary on every
    /// cell read during paint. Reconciler transfers this across re-renders
    /// when CellTextCacheKey is reference-equal between old and new instances.
    /// </summary>
    string?[]? CellTextCache { get; set; }

    /// <summary>
    /// Identity of the data source the cache was built against. Reconciler
    /// uses reference-equality against this value to decide whether cached
    /// entries are still valid for the new node instance.
    /// </summary>
    object? CellTextCacheKey { get; }

    // ── Cell editing ──────────────────────────────────────────────────

    /// <summary>Whether this column is editable (has a setter and is not read-only).</summary>
    bool IsColumnEditable(int col);

    /// <summary>Whether the grid is currently in edit mode for a cell.</summary>
    bool IsEditing { get; }

    /// <summary>The row currently being edited, or -1.</summary>
    int EditingRow { get; }

    /// <summary>The column currently being edited, or -1.</summary>
    int EditingCol { get; }

    /// <summary>The current edit buffer text.</summary>
    string EditBuffer { get; }

    /// <summary>The cursor position within the edit buffer.</summary>
    int EditCursorPos { get; set; }

    /// <summary>The edit mode (ClickToEdit, DoubleClick, AlwaysEditing).</summary>
    GridEditMode EditModeValue { get; }

    /// <summary>Begins editing a cell. Returns true if editing started.</summary>
    bool BeginEdit(int row, int col);

    /// <summary>Commits the current edit, calling the column setter.</summary>
    bool CommitEdit();

    /// <summary>Cancels the current edit, discarding changes.</summary>
    void CancelEdit();

    /// <summary>Handles a character typed during editing.</summary>
    void HandleEditChar(char ch);

    /// <summary>Handles a special key during editing (Backspace, Delete, Left, Right).</summary>
    void HandleEditKey(Key key);

    /// <summary>Toggles a bool column value directly (no edit buffer needed).</summary>
    void ToggleBool(int row, int col);

    // ── Column type detection ─────────────────────────────────────────

    /// <summary>Whether this column is a select (dropdown) column.</summary>
    bool IsSelectColumn(int col);

    /// <summary>Whether this column is a date column.</summary>
    bool IsDateColumn(int col);

    /// <summary>Gets the available options for a select column.</summary>
    IReadOnlyList<object>? GetSelectOptions(int col);

    // ── Select dropdown overlay state ─────────────────────────────────

    /// <summary>Whether the select dropdown overlay is currently visible.</summary>
    bool IsSelectDropdownOpen { get; }

    /// <summary>The row being edited via select dropdown.</summary>
    int SelectDropdownRow { get; }

    /// <summary>The column being edited via select dropdown.</summary>
    int SelectDropdownCol { get; }

    /// <summary>Currently hovered option index in the select dropdown (-1 = none).</summary>
    int SelectDropdownHoverIndex { get; set; }

    /// <summary>Absolute bounds of the select dropdown, set by painter for hit testing.</summary>
    Rect SelectDropdownBounds { get; set; }

    /// <summary>Absolute bounds of the cell that triggered the select dropdown.</summary>
    Rect SelectDropdownCellBounds { get; set; }

    /// <summary>Opens the select dropdown for the specified cell.</summary>
    void OpenSelectDropdown(int row, int col);

    /// <summary>Commits the selected option by index, closing the dropdown.</summary>
    void CommitSelectOption(int index);

    /// <summary>Closes the select dropdown without committing.</summary>
    void CloseSelectDropdown();

    // ── Date popup overlay state ──────────────────────────────────────

    /// <summary>Whether the date popup overlay is currently visible.</summary>
    bool IsDatePopupOpen { get; }

    /// <summary>The row being edited via date popup.</summary>
    int DatePopupRow { get; }

    /// <summary>The column being edited via date popup.</summary>
    int DatePopupCol { get; }

    /// <summary>Absolute bounds of the cell that triggered the date popup.</summary>
    Rect DatePopupCellBounds { get; set; }

    /// <summary>The DatePicker used for calendar popup state (null if not open).</summary>
    DatePicker? DatePopupPicker { get; }

    /// <summary>Opens the date popup for the specified cell.</summary>
    void OpenDatePopup(int row, int col);

    /// <summary>Commits a selected date, closing the popup.</summary>
    void CommitDateValue(DateOnly date);

    /// <summary>Closes the date popup without committing.</summary>
    void CloseDatePopup();

    /// <summary>Closes any open overlay (select dropdown or date popup).</summary>
    void CloseOverlay();

    // ── Column resize / reorder / pin ─────────────────────────────────

    /// <summary>Whether column resizing is enabled (any column has resizable set).</summary>
    bool IsResizingEnabled { get; }

    /// <summary>Whether column reordering is enabled.</summary>
    bool IsReorderingEnabled { get; }

    /// <summary>Column index currently being resized, or -1.</summary>
    int ResizingColumnIndex { get; set; }

    /// <summary>The starting width of the column being resized.</summary>
    float ResizeStartWidth { get; set; }

    /// <summary>The mouse X at drag start for resize.</summary>
    float ResizeStartMouseX { get; set; }

    /// <summary>Whether a specific column is resizable.</summary>
    bool IsColumnResizable(int col);

    /// <summary>Sets the runtime width of a column (used during/after resize).</summary>
    void SetColumnWidth(int col, float width);

    /// <summary>Column index currently being dragged for reorder, or -1.</summary>
    int ReorderDragIndex { get; set; }

    /// <summary>The current drop target index for reorder (-1 = none).</summary>
    int ReorderDropIndex { get; set; }

    /// <summary>The absolute X position of the dragged column header during reorder.</summary>
    float ReorderDragX { get; set; }

    /// <summary>The width of the column being reordered (for ghost rendering).</summary>
    float ReorderDragWidth { get; set; }

    /// <summary>The absolute Y of the header row (for reorder ghost rendering).</summary>
    float ReorderHeaderY { get; set; }

    /// <summary>The height of the header row.</summary>
    float ReorderHeaderHeight { get; set; }

    /// <summary>Commits the column reorder, moving a column from one position to another.</summary>
    void ReorderColumn(int fromIndex, int toIndex);

    /// <summary>Gets the pin position for a column, or null if unpinned.</summary>
    ColumnPin? GetColumnPin(int col);

    /// <summary>Whether any column is pinned left.</summary>
    bool HasLeftPinnedColumns { get; }

    /// <summary>Whether any column is pinned right.</summary>
    bool HasRightPinnedColumns { get; }

    /// <summary>The hovered column header index, or -1.</summary>
    int HoveredHeaderCol { get; set; }

    /// <summary>Whether the mouse is near a column border (for resize cursor).</summary>
    bool IsNearColumnBorder { get; set; }

    // ── Grouping ──────────────────────────────────────────────────────

    /// <summary>Whether the data is currently grouped.</summary>
    bool IsGrouped { get; }

    /// <summary>The number of groups.</summary>
    int GroupCount { get; }

    /// <summary>Gets the display text for a group header.</summary>
    string GetGroupKey(int groupIndex);

    /// <summary>Gets the number of data rows in a group.</summary>
    int GetGroupRowCount(int groupIndex);

    /// <summary>Whether a group is currently collapsed.</summary>
    bool IsGroupCollapsed(int groupIndex);

    /// <summary>Maps a (groupIndex, rowInGroup) pair to a data row index suitable for GetCellText etc.</summary>
    int GetGroupDataRowIndex(int groupIndex, int rowInGroup);

    /// <summary>Toggles a group's collapsed state.</summary>
    void ToggleGroupCollapse(int groupIndex);

    // ── Filtering ─────────────────────────────────────────────────────

    /// <summary>Whether the filter row is shown below the header.</summary>
    bool HasFilterRow { get; }

    /// <summary>Gets the current filter text for a column (empty string = no filter).</summary>
    string GetColumnFilter(int col);

    /// <summary>Sets the filter text for a column. Triggers a rebuild of filtered indices.</summary>
    void SetColumnFilter(int col, string value);

    /// <summary>The column index of the currently focused filter cell, or -1.</summary>
    int ActiveFilterCol { get; set; }

    /// <summary>Cursor position within the active filter cell text.</summary>
    int FilterCursorPos { get; set; }

    /// <summary>The number of rows after all filters are applied.</summary>
    int FilteredRowCount { get; }

    /// <summary>Whether any filter is currently active (column or global).</summary>
    bool HasActiveFilter { get; }

    /// <summary>The current global filter text, or empty.</summary>
    string GlobalFilterText { get; }

    // ── Row detail expansion ──────────────────────────────────────────

    /// <summary>Whether row detail expansion is enabled.</summary>
    bool HasRowDetail { get; }

    /// <summary>Whether the specified display row is currently expanded to show detail.</summary>
    bool IsRowExpanded(int row);

    /// <summary>Gets the height of the detail panel for an expanded row.</summary>
    float GetRowDetailHeight(int row);

    /// <summary>Toggles the expanded state of a row, respecting RowDetailMode.</summary>
    void ToggleRowDetail(int row);

    /// <summary>Whether Single (one at a time) or Multi (many) row detail expansion.</summary>
    RowDetailMode RowDetailModeValue { get; }

    /// <summary>Gets the text content for a row's detail panel.</summary>
    string GetRowDetailText(int row);

    // ── Aggregate row ───────────────────────────────────────────────

    /// <summary>Whether an aggregate (summary) row is configured.</summary>
    bool HasAggregateRow { get; }

    /// <summary>Where the aggregate row is positioned (Top or Bottom).</summary>
    AggregatePosition AggregatePos { get; }

    /// <summary>Gets the formatted aggregate value for the specified column, or empty string.</summary>
    string GetAggregateText(int col);

    /// <summary>Gets the height of the aggregate row.</summary>
    float GetAggregateRowHeight();

    // ── Frozen rows ─────────────────────────────────────────────────

    /// <summary>Number of data rows frozen (pinned) at the top of the scroll region.</summary>
    int FrozenRowCount { get; }

    // ── Undo / Redo ─────────────────────────────────────────────────

    /// <summary>Whether undo/redo is enabled for cell edits.</summary>
    bool IsUndoEnabled { get; }

    /// <summary>Gets the UndoStack for this grid, or null if undo is not enabled.</summary>
    UndoStack? GetUndoStack();

    /// <summary>Undoes the last cell edit. Returns true if an undo was performed.</summary>
    bool UndoEdit();

    /// <summary>Redoes the last undone edit. Returns true if a redo was performed.</summary>
    bool RedoEdit();

    // ── Clipboard ───────────────────────────────────────────────────

    /// <summary>Whether clipboard support (Ctrl+C/V/X) is enabled.</summary>
    bool IsClipboardEnabled { get; }

    /// <summary>Copies the selected cells/rows to the clipboard as tab-separated text.</summary>
    Task CopyCellsAsync();

    /// <summary>Cuts the selected cells (copy then clear). Returns true if cut was performed.</summary>
    Task<bool> CutCellsAsync();

    /// <summary>Pastes tab-separated text from clipboard into selected cell range.</summary>
    Task<bool> PasteCellsAsync();

    // ── Batch edit ──────────────────────────────────────────────────

    /// <summary>Whether batch editing of selected rows is enabled.</summary>
    bool IsBatchEditEnabled { get; }

    /// <summary>
    /// Applies a cell value change to all selected rows for the same column.
    /// The value is taken from the current edit cell.
    /// </summary>
    void ApplyBatchEdit(int col, string value);

    // ── Export ───────────────────────────────────────────────────────

    /// <summary>Whether export functionality is enabled.</summary>
    bool IsExportEnabled { get; }

    /// <summary>Generates a CSV string from the visible data.</summary>
    string ExportCsv(GridExportOptions? options);

    // ── Layout persistence ──────────────────────────────────────────

    /// <summary>Captures current column order, widths, visibility, and sort state.</summary>
    GridLayoutState SaveLayout();

    /// <summary>Whether a restored layout is pending application.</summary>
    bool HasPendingLayoutRestore { get; }

    /// <summary>Applies the pending restored layout state to runtime columns.</summary>
    void ApplyRestoredLayout();

    // ── Column chooser ──────────────────────────────────────────────

    /// <summary>Whether the column chooser dropdown is enabled.</summary>
    bool IsColumnChooserEnabled { get; }

    /// <summary>Whether the column chooser dropdown is currently open.</summary>
    bool IsColumnChooserOpen { get; }

    /// <summary>Toggles the column chooser dropdown open/closed.</summary>
    void ToggleColumnChooser();

    /// <summary>Whether the specified column is currently visible.</summary>
    bool GetColumnVisible(int col);

    /// <summary>Toggles visibility of the specified column.</summary>
    void ToggleColumnVisibility(int col);

    /// <summary>Number of currently visible columns.</summary>
    int VisibleColumnCount { get; }

    /// <summary>Absolute bounds of the column chooser dropdown, set by the painter.</summary>
    Rect ColumnChooserBounds { get; set; }

    /// <summary>Currently hovered item index in the column chooser dropdown (-1 = none).</summary>
    int ColumnChooserHoverIndex { get; set; }

    /// <summary>Absolute bounds of the column chooser button, set by the painter.</summary>
    Rect ColumnChooserButtonBounds { get; set; }

    // ── Validation ─────────────────────────────────────────────────────

    /// <summary>Whether the specified cell has a validation error.</summary>
    bool HasCellError(int row, int col);

    /// <summary>Gets the validation error message for the specified cell, or null if valid.</summary>
    string? GetCellErrorMessage(int row, int col);

    /// <summary>Currently hovered column index within data rows, or -1 if none.</summary>
    int HoveredColIndex { get; set; }

    /// <summary>Runs validation on the specified display row and caches results.</summary>
    void ValidateRow(int row);

    // ── Virtualization & Scroll ───────────────────────────────────────

    /// <summary>Current vertical scroll offset in pixels within the data area.</summary>
    float ScrollOffsetY { get; set; }

    /// <summary>Current horizontal scroll offset in pixels.</summary>
    float ScrollOffsetX { get; set; }

    /// <summary>Maximum vertical scroll offset (TotalContentHeight - ViewportHeight).</summary>
    float MaxScrollOffsetY { get; }

    /// <summary>Maximum horizontal scroll offset.</summary>
    float MaxScrollOffsetX { get; }

    /// <summary>Height of the visible data area (excluding header, filter, aggregate), set by the painter.</summary>
    float ViewportHeight { get; set; }

    /// <summary>Total height of all data content (rows + details + groups) for scroll range.</summary>
    float TotalContentHeight { get; }

    /// <summary>Number of extra rows to render above/below the viewport for smooth scrolling.</summary>
    int VirtualizationBufferRows { get; }

    /// <summary>Scrolls the viewport to ensure the given display row is visible.</summary>
    void ScrollIntoView(int displayRow);

    /// <summary>Maximum visible rows before the grid caps its height and scrolls internally, or null for unbounded.</summary>
    int? MaxVisibleRows { get; }
}

/// <summary>
/// A read-only, virtualized, sortable, filterable table for displaying
/// structured data. No inline editing. For editable grids, use
/// <see cref="DataGrid{T}"/>.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class DataTable<T> : Node, ITabularDataNode
{
    /// <summary>
    /// Creates a data table with the specified items and column definitions.
    /// </summary>
    /// <param name="items">The data source.</param>
    /// <param name="columns">Column definitions.</param>
    public DataTable(
        IReadOnlyList<T> items,
        IReadOnlyList<DataColumn<T>> columns)
    {
        Items = items;
        Columns = columns;
    }

    /// <summary>The data source.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Column definitions.</summary>
    public IReadOnlyList<DataColumn<T>> Columns { get; }

    // ── Internal state ────────────────────────────────────────────────

    internal bool sortableEnabled;
    internal string? defaultSortColumn;
    internal SortDirection defaultSortDirection;
    internal Action<string, SortDirection>? onSortHandler;
    internal bool filterRowEnabled;
    internal Bindable<string>? globalFilterBinding;
    internal SelectionMode selectionModeValue;
    internal Bindable<T>? selectedBinding;
    internal Action<T>? onSelectHandler;
    internal Func<T, IReadOnlyList<ContextMenuItem>>? rowContextMenuFactory;
    internal Func<T, IReadOnlyList<Node>>? rowActionsFactory;
    internal float? rowHeightValue;
    internal bool stripedEnabled;
    internal bool hoverHighlightEnabled;
    internal Node emptyStateNode = Node.Empty;

    // ── Sorting ───────────────────────────────────────────────────────

    /// <summary>Enables or disables column sorting.</summary>
    public DataTable<T> Sortable(bool enabled)
    {
        sortableEnabled = enabled;
        return this;
    }

    /// <summary>Sets the default sort column and direction.</summary>
    public DataTable<T> DefaultSort(string columnHeader, SortDirection direction)
    {
        defaultSortColumn = columnHeader;
        defaultSortDirection = direction;
        return this;
    }

    /// <summary>Callback for server-side sorting.</summary>
    public DataTable<T> OnSort(Action<string, SortDirection> onSort)
    {
        onSortHandler = onSort;
        return this;
    }

    // ── Filtering ─────────────────────────────────────────────────────

    /// <summary>Shows a filter input row below the header.</summary>
    public DataTable<T> FilterRow(bool enabled)
    {
        filterRowEnabled = enabled;
        return this;
    }

    /// <summary>Binds a global filter query that filters all text columns.</summary>
    public DataTable<T> GlobalFilter(Bindable<string> query)
    {
        globalFilterBinding = query;
        return this;
    }

    // ── Selection ─────────────────────────────────────────────────────

    /// <summary>Sets the row selection mode.</summary>
    public DataTable<T> SelectionMode(SelectionMode mode)
    {
        selectionModeValue = mode;
        return this;
    }

    /// <summary>Binds the selected item(s).</summary>
    public DataTable<T> Selected(Bindable<T> selected)
    {
        selectedBinding = selected;
        return this;
    }

    /// <summary>Callback when the selection changes.</summary>
    public DataTable<T> OnSelect(Action<T> onSelect)
    {
        onSelectHandler = onSelect;
        return this;
    }

    // ── Row actions ───────────────────────────────────────────────────

    /// <summary>Configures a right-click context menu per row.</summary>
    public DataTable<T> RowContextMenu(Func<T, IReadOnlyList<ContextMenuItem>> factory)
    {
        rowContextMenuFactory = factory;
        return this;
    }

    /// <summary>Configures inline action buttons in the last column.</summary>
    public DataTable<T> RowActions(Func<T, IReadOnlyList<Node>> factory)
    {
        rowActionsFactory = factory;
        return this;
    }

    // ── Appearance ────────────────────────────────────────────────────

    /// <summary>Sets fixed row height in logical pixels.</summary>
    public DataTable<T> RowHeight(float height)
    {
        rowHeightValue = height;
        return this;
    }

    /// <summary>Enables alternating row backgrounds.</summary>
    public DataTable<T> Striped(bool enabled)
    {
        stripedEnabled = enabled;
        return this;
    }

    /// <summary>Enables hover highlighting on rows.</summary>
    public DataTable<T> HoverHighlight(bool enabled)
    {
        hoverHighlightEnabled = enabled;
        return this;
    }

    /// <summary>Sets the empty state displayed when the table has no rows.</summary>
    public DataTable<T> EmptyState(Node emptyState)
    {
        emptyStateNode = emptyState;
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

    // ── ITabularDataNode implementation ───────────────────────────────

    private int MapRow(int displayRow) => sortedIndices != null ? sortedIndices[displayRow] : displayRow;

    int ITabularDataNode.RowCount => Items.Count;
    int ITabularDataNode.ColumnCount => Columns.Count;
    string ITabularDataNode.GetColumnHeader(int col) => Columns[col].Header;
    bool ITabularDataNode.IsStriped => stripedEnabled;
    float ITabularDataNode.GetRowHeight() => rowHeightValue ?? 36f;

    bool ITabularDataNode.IsBoolColumn(int col) => Columns[col].boolAccessor != null;

    bool ITabularDataNode.GetBoolValue(int row, int col)
    {
        var accessor = Columns[col].boolAccessor;
        return accessor != null && accessor(Items[MapRow(row)]);
    }

    bool ITabularDataNode.IsCustomColumn(int col) => Columns[col].customRenderer != null;

    Node? ITabularDataNode.GetCustomCellNode(int row, int col)
    {
        var renderer = Columns[col].customRenderer;
        if (renderer is null)
        {
            return null;
        }
        return renderer(Items[MapRow(row)]);
    }

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

    bool ITabularDataNode.IsHoverHighlightEnabled => hoverHighlightEnabled;

    int ITabularDataNode.VisibleRowCount
    {
        get => visibleRowCount;
        set => visibleRowCount = value;
    }

    Rect ITabularDataNode.AbsoluteBounds { get; set; }

    // DataTable is read-only — editing stubs return false/no-op
    bool ITabularDataNode.IsColumnEditable(int col) => false;
    bool ITabularDataNode.IsEditing => false;
    int ITabularDataNode.EditingRow => -1;
    int ITabularDataNode.EditingCol => -1;
    string ITabularDataNode.EditBuffer => "";
    int ITabularDataNode.EditCursorPos { get => 0; set { } }
    GridEditMode ITabularDataNode.EditModeValue => GridEditMode.ClickToEdit;
    bool ITabularDataNode.BeginEdit(int row, int col) => false;
    bool ITabularDataNode.CommitEdit() => false;
    void ITabularDataNode.CancelEdit() { }
    void ITabularDataNode.HandleEditChar(char ch) { }
    void ITabularDataNode.HandleEditKey(Key key) { }
    void ITabularDataNode.ToggleBool(int row, int col) { }

    // ── Overlay no-ops (DataTable is read-only) ───────────────────────

    bool ITabularDataNode.IsSelectColumn(int col) => false;
    bool ITabularDataNode.IsDateColumn(int col) => false;
    IReadOnlyList<object>? ITabularDataNode.GetSelectOptions(int col) => null;
    bool ITabularDataNode.IsSelectDropdownOpen => false;
    int ITabularDataNode.SelectDropdownRow => -1;
    int ITabularDataNode.SelectDropdownCol => -1;
    int ITabularDataNode.SelectDropdownHoverIndex { get => -1; set { } }
    Rect ITabularDataNode.SelectDropdownBounds { get => default; set { } }
    Rect ITabularDataNode.SelectDropdownCellBounds { get => default; set { } }
    void ITabularDataNode.OpenSelectDropdown(int row, int col) { }
    void ITabularDataNode.CommitSelectOption(int index) { }
    void ITabularDataNode.CloseSelectDropdown() { }
    bool ITabularDataNode.IsDatePopupOpen => false;
    int ITabularDataNode.DatePopupRow => -1;
    int ITabularDataNode.DatePopupCol => -1;
    Rect ITabularDataNode.DatePopupCellBounds { get => default; set { } }
    DatePicker? ITabularDataNode.DatePopupPicker => null;
    void ITabularDataNode.OpenDatePopup(int row, int col) { }
    void ITabularDataNode.CommitDateValue(DateOnly date) { }
    void ITabularDataNode.CloseDatePopup() { }
    void ITabularDataNode.CloseOverlay() { }

    // ── Column resize / reorder / pin (no-op defaults for read-only DataTable) ──

    bool ITabularDataNode.IsResizingEnabled => false;
    bool ITabularDataNode.IsReorderingEnabled => false;
    int ITabularDataNode.ResizingColumnIndex { get; set; } = -1;
    float ITabularDataNode.ResizeStartWidth { get; set; }
    float ITabularDataNode.ResizeStartMouseX { get; set; }
    bool ITabularDataNode.IsColumnResizable(int col) => false;
    void ITabularDataNode.SetColumnWidth(int col, float width) { }
    int ITabularDataNode.ReorderDragIndex { get; set; } = -1;
    int ITabularDataNode.ReorderDropIndex { get; set; } = -1;
    float ITabularDataNode.ReorderDragX { get; set; }
    float ITabularDataNode.ReorderDragWidth { get; set; }
    float ITabularDataNode.ReorderHeaderY { get; set; }
    float ITabularDataNode.ReorderHeaderHeight { get; set; }
    void ITabularDataNode.ReorderColumn(int fromIndex, int toIndex) { }
    ColumnPin? ITabularDataNode.GetColumnPin(int col) => null;
    bool ITabularDataNode.HasLeftPinnedColumns => false;
    bool ITabularDataNode.HasRightPinnedColumns => false;
    int ITabularDataNode.HoveredHeaderCol { get; set; } = -1;
    bool ITabularDataNode.IsNearColumnBorder { get; set; }

    // ── Grouping no-ops (DataTable does not support grouping) ─────────

    bool ITabularDataNode.IsGrouped => false;
    int ITabularDataNode.GroupCount => 0;
    string ITabularDataNode.GetGroupKey(int groupIndex) => "";
    int ITabularDataNode.GetGroupRowCount(int groupIndex) => 0;
    bool ITabularDataNode.IsGroupCollapsed(int groupIndex) => false;
    int ITabularDataNode.GetGroupDataRowIndex(int groupIndex, int rowInGroup) => rowInGroup;
    void ITabularDataNode.ToggleGroupCollapse(int groupIndex) { }

    // ── Filtering no-ops ─────────────────────────────────────────────
    bool ITabularDataNode.HasFilterRow => filterRowEnabled;
    string ITabularDataNode.GetColumnFilter(int col) => "";
    void ITabularDataNode.SetColumnFilter(int col, string value) { }
    int ITabularDataNode.ActiveFilterCol { get => -1; set { } }
    int ITabularDataNode.FilterCursorPos { get => 0; set { } }
    int ITabularDataNode.FilteredRowCount => Items.Count;
    bool ITabularDataNode.HasActiveFilter => false;
    string ITabularDataNode.GlobalFilterText => globalFilterBinding?.Value ?? "";

    // ── Row detail no-ops (DataTable is read-only, no detail expansion) ──
    bool ITabularDataNode.HasRowDetail => false;
    bool ITabularDataNode.IsRowExpanded(int row) => false;
    float ITabularDataNode.GetRowDetailHeight(int row) => 0f;
    void ITabularDataNode.ToggleRowDetail(int row) { }
    RowDetailMode ITabularDataNode.RowDetailModeValue => RowDetailMode.Single;
    string ITabularDataNode.GetRowDetailText(int row) => "";

    // ── Aggregate / frozen row no-ops ───────────────────────────────
    bool ITabularDataNode.HasAggregateRow => false;
    AggregatePosition ITabularDataNode.AggregatePos => AggregatePosition.Bottom;
    string ITabularDataNode.GetAggregateText(int col) => "";
    float ITabularDataNode.GetAggregateRowHeight() => 0f;
    int ITabularDataNode.FrozenRowCount => 0;

    // ── Undo / clipboard / batch no-ops (DataTable is read-only) ────
    bool ITabularDataNode.IsUndoEnabled => false;
    UndoStack? ITabularDataNode.GetUndoStack() => null;
    bool ITabularDataNode.UndoEdit() => false;
    bool ITabularDataNode.RedoEdit() => false;
    bool ITabularDataNode.IsClipboardEnabled => false;
    Task ITabularDataNode.CopyCellsAsync() => Task.CompletedTask;
    Task<bool> ITabularDataNode.CutCellsAsync() => Task.FromResult(false);
    Task<bool> ITabularDataNode.PasteCellsAsync() => Task.FromResult(false);
    bool ITabularDataNode.IsBatchEditEnabled => false;
    void ITabularDataNode.ApplyBatchEdit(int col, string value) { }

    // ── Export / layout / column chooser no-ops ──────────────────────
    bool ITabularDataNode.IsExportEnabled => false;
    string ITabularDataNode.ExportCsv(GridExportOptions? options) => "";
    GridLayoutState ITabularDataNode.SaveLayout() => new()
    {
        ColumnOrder = [],
        ColumnVisibility = new Dictionary<string, bool>(),
        ColumnWidths = new Dictionary<string, float>(),
    };
    bool ITabularDataNode.HasPendingLayoutRestore => false;
    void ITabularDataNode.ApplyRestoredLayout() { }
    bool ITabularDataNode.IsColumnChooserEnabled => false;
    bool ITabularDataNode.IsColumnChooserOpen => false;
    void ITabularDataNode.ToggleColumnChooser() { }
    bool ITabularDataNode.GetColumnVisible(int col) => true;
    void ITabularDataNode.ToggleColumnVisibility(int col) { }
    int ITabularDataNode.VisibleColumnCount => Columns.Count;
    Rect ITabularDataNode.ColumnChooserBounds { get => default; set { } }
    int ITabularDataNode.ColumnChooserHoverIndex { get => -1; set { } }
    Rect ITabularDataNode.ColumnChooserButtonBounds { get => default; set { } }

    // ── Validation no-ops (DataTable is read-only, no validation) ────
    bool ITabularDataNode.HasCellError(int row, int col) => false;
    string? ITabularDataNode.GetCellErrorMessage(int row, int col) => null;
    int ITabularDataNode.HoveredColIndex { get => -1; set { } }
    void ITabularDataNode.ValidateRow(int row) { }

    // ── Virtualization no-ops (DataTable does not scroll internally) ──
    float ITabularDataNode.ScrollOffsetY { get => 0; set { } }
    float ITabularDataNode.ScrollOffsetX { get => 0; set { } }
    float ITabularDataNode.MaxScrollOffsetY => 0;
    float ITabularDataNode.MaxScrollOffsetX => 0;
    float ITabularDataNode.ViewportHeight { get => 0; set { } }
    float ITabularDataNode.TotalContentHeight => 0;
    int ITabularDataNode.VirtualizationBufferRows => 0;
    void ITabularDataNode.ScrollIntoView(int displayRow) { }
    int? ITabularDataNode.MaxVisibleRows => null;

    void ITabularDataNode.ApplySort(int col)
    {
        if (!sortableEnabled)
        {
            return;
        }

        if (sortColumnIdx == col)
        {
            currentSortDirection = currentSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            sortColumnIdx = col;
            currentSortDirection = SortDirection.Ascending;
        }

        // Build sorted index
        int count = Items.Count;
        sortedIndices = new int[count];
        for (int i = 0; i < count; i++)
        {
            sortedIndices[i] = i;
        }

        var column = Columns[col];
        Array.Sort(sortedIndices, (a, b) =>
        {
            string textA = GetSortKey(Items[a], column);
            string textB = GetSortKey(Items[b], column);
            int cmp = string.Compare(textA, textB, StringComparison.OrdinalIgnoreCase);
            return currentSortDirection == SortDirection.Ascending ? cmp : -cmp;
        });

        selectedRowIdx = -1;
        selectedRows.Clear();
        anchorRow = -1;
        onSortHandler?.Invoke(Columns[col].Header, currentSortDirection);
    }

    private static string GetSortKey(T item, DataColumn<T> column)
    {
        if (column.textAccessor != null)
        {
            return column.textAccessor(item);
        }
        if (column.numberAccessor != null)
        {
            return column.numberAccessor(item)?.ToString() ?? "";
        }
        if (column.dateAccessor != null)
        {
            return column.dateAccessor(item)?.ToString() ?? "";
        }
        if (column.boolAccessor != null)
        {
            return column.boolAccessor(item) ? "1" : "0";
        }
        if (column.enumAccessor != null)
        {
            return column.enumAccessor(item)?.ToString() ?? "";
        }
        return "";
    }

    void ITabularDataNode.SelectRow(int row, bool ctrl, bool shift)
    {
        if (row < 0 || row >= Items.Count)
        {
            return;
        }

        if (shift && anchorRow >= 0)
        {
            // Range select from anchor to row
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
            // Toggle this row in selection
            if (!selectedRows.Add(row))
            {
                selectedRows.Remove(row);
            }
            selectedRowIdx = row;
            anchorRow = row;
        }
        else
        {
            // Single select — clear others
            selectedRows.Clear();
            selectedRows.Add(row);
            selectedRowIdx = row;
            anchorRow = row;
        }

        int dataRow = MapRow(row);
        if (dataRow >= 0 && dataRow < Items.Count)
        {
            onSelectHandler?.Invoke(Items[dataRow]);
        }
    }

    void ITabularDataNode.MoveSelection(int delta)
    {
        int newRow = Math.Clamp(selectedRowIdx + delta, 0, Items.Count - 1);
        ((ITabularDataNode)this).SelectRow(newRow, false, false);
    }

    void ITabularDataNode.SelectFirst()
    {
        if (Items.Count > 0)
        {
            ((ITabularDataNode)this).SelectRow(0, false, false);
        }
    }

    void ITabularDataNode.SelectLast()
    {
        if (Items.Count > 0)
        {
            ((ITabularDataNode)this).SelectRow(Items.Count - 1, false, false);
        }
    }

    ColumnAlignment ITabularDataNode.GetColumnAlignment(int col)
    {
        return Columns[col].alignValue ?? ColumnAlignment.Left;
    }

    float ITabularDataNode.GetColumnWidth(int col, float availableWidth)
    {
        var column = Columns[col];
        if (column.widthValue.HasValue)
        {
            return column.widthValue.Value;
        }

        // Distribute evenly for auto/fill columns
        return availableWidth / Columns.Count;
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

        var item = Items[dataRow];
        var column = Columns[col];
        string text;

        if (column.textAccessor != null)
        {
            text = column.textAccessor(item);
        }
        else if (column.numberAccessor != null)
        {
            var val = column.numberAccessor(item);
            text = column.formatString != null
                ? string.Format(column.ComposedFormat, val)
                : val?.ToString() ?? "";
        }
        else if (column.dateAccessor != null)
        {
            var val = column.dateAccessor(item);
            text = column.formatString != null
                ? string.Format(column.ComposedFormat, val)
                : val?.ToString() ?? "";
        }
        else if (column.boolAccessor != null)
        {
            text = column.boolAccessor(item) ? "✓" : "✗";
        }
        else if (column.enumAccessor != null)
        {
            text = column.enumAccessor(item)?.ToString() ?? "";
        }
        else
        {
            text = "";
        }

        // Allocate or resize cache on first miss. Items count is stable for
        // a given data-source identity (reconciler discards cache on Items
        // replace), so we size once.
        if (cache == null || cache.Length != Items.Count * colCount)
        {
            cache = new string?[Items.Count * colCount];
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
    /// is unchanged.
    /// </summary>
    internal string?[]? cellTextCache;

    string?[]? ITabularDataNode.CellTextCache
    {
        get => cellTextCache;
        set => cellTextCache = value;
    }

    object? ITabularDataNode.CellTextCacheKey => Items;

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
    /// <param name="dataRow">Index into <see cref="Items"/>.</param>
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
/// A column definition for <see cref="DataTable{T}"/>.
/// </summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class DataColumn<T>
{
    private DataColumn(string header)
    {
        Header = header;
    }

    /// <summary>Column header text.</summary>
    public string Header { get; }

    // ── Internal state ────────────────────────────────────────────────

    internal Func<T, string>? textAccessor;
    internal Func<T, object>? numberAccessor;
    internal Func<T, object>? dateAccessor;
    internal Func<T, bool>? boolAccessor;
    internal Func<T, object>? enumAccessor;
    internal Func<T, Node>? customRenderer;
    internal Func<object, Node>? enumRenderer;
    internal string? formatString;
    // Cached "{0:format}" template, built lazily once per column. Avoids
    // allocating a fresh interpolation string on every GetCellText call.
    private string? composedFormat;
    internal string ComposedFormat => composedFormat ??= formatString is null
        ? "{0}"
        : string.Concat("{0:", formatString, "}");
    internal float? widthValue;
    internal DataColumnWidth? widthStrategy;
    internal float? minWidthValue;
    internal float? maxWidthValue;
    internal bool? sortableValue;
    internal bool? resizableValue;
    internal ColumnPin? pinValue;
    internal ColumnAlignment? alignValue;
    internal ColumnAlignment? headerAlignValue;
    internal Func<T, string>? tooltipFactory;

    /// <summary>Creates a text column (left-aligned).</summary>
    public static DataColumn<T> Text(string header, Func<T, string> accessor, float? width = null)
    {
        return new DataColumn<T>(header)
        {
            textAccessor = accessor,
            widthValue = width,
            alignValue = ColumnAlignment.Left,
        };
    }

    /// <summary>Creates a numeric column (right-aligned).</summary>
    public static DataColumn<T> Number(string header, Func<T, object> accessor, string? format = null, float? width = null)
    {
        return new DataColumn<T>(header)
        {
            numberAccessor = accessor,
            formatString = format,
            widthValue = width,
            alignValue = ColumnAlignment.Right,
        };
    }

    /// <summary>Creates a date column.</summary>
    public static DataColumn<T> Date(string header, Func<T, object> accessor, string? format = null, float? width = null)
    {
        return new DataColumn<T>(header)
        {
            dateAccessor = accessor,
            formatString = format,
            widthValue = width,
        };
    }

    /// <summary>Creates a boolean column (read-only checkbox).</summary>
    public static DataColumn<T> Bool(string header, Func<T, bool> accessor, float? width = null)
    {
        return new DataColumn<T>(header)
        {
            boolAccessor = accessor,
            widthValue = width,
            alignValue = ColumnAlignment.Center,
        };
    }

    /// <summary>Creates an enum column with optional custom rendering.</summary>
    public static DataColumn<T> Enum(string header, Func<T, object> accessor, float? width = null, Func<object, Node>? render = null)
    {
        return new DataColumn<T>(header)
        {
            enumAccessor = accessor,
            widthValue = width,
            enumRenderer = render,
        };
    }

    /// <summary>Creates a custom column with arbitrary node rendering.</summary>
    public static DataColumn<T> Custom(string header, Func<T, Node> render)
    {
        return new DataColumn<T>(header)
        {
            customRenderer = render,
        };
    }

    // ── Column options (fluent) ───────────────────────────────────────

    /// <summary>Sets fixed column width in logical pixels.</summary>
    public DataColumn<T> Width(float width)
    {
        widthValue = width;
        widthStrategy = null;
        return this;
    }

    /// <summary>Sets column width strategy (Fill or Auto).</summary>
    public DataColumn<T> Width(DataColumnWidth width)
    {
        widthStrategy = width;
        widthValue = null;
        return this;
    }

    /// <summary>Sets minimum column width.</summary>
    public DataColumn<T> MinWidth(float minWidth)
    {
        minWidthValue = minWidth;
        return this;
    }

    /// <summary>Sets maximum column width.</summary>
    public DataColumn<T> MaxWidth(float maxWidth)
    {
        maxWidthValue = maxWidth;
        return this;
    }

    /// <summary>Enables or disables sorting on this column.</summary>
    public DataColumn<T> Sortable(bool enabled)
    {
        sortableValue = enabled;
        return this;
    }

    /// <summary>Enables or disables column resizing.</summary>
    public DataColumn<T> Resizable(bool enabled)
    {
        resizableValue = enabled;
        return this;
    }

    /// <summary>Pins (freezes) the column to the left or right edge.</summary>
    public DataColumn<T> Pinned(ColumnPin pin)
    {
        pinValue = pin;
        return this;
    }

    /// <summary>Sets cell content alignment.</summary>
    public DataColumn<T> Align(ColumnAlignment alignment)
    {
        alignValue = alignment;
        return this;
    }

    /// <summary>Sets header text alignment.</summary>
    public DataColumn<T> HeaderAlign(ColumnAlignment alignment)
    {
        headerAlignValue = alignment;
        return this;
    }

    /// <summary>Sets a per-cell tooltip.</summary>
    public DataColumn<T> Tooltip(Func<T, string> tooltipFactory)
    {
        this.tooltipFactory = tooltipFactory;
        return this;
    }
}

/// <summary>
/// Column width strategy for data tables.
/// </summary>
public enum DataColumnWidth
{
    /// <summary>Takes remaining space — one fill column per table.</summary>
    Fill,

    /// <summary>Sized to content by sampling visible rows.</summary>
    Auto
}

/// <summary>
/// Column pin position for frozen columns.
/// </summary>
public enum ColumnPin
{
    /// <summary>Pinned to the left edge.</summary>
    Left,

    /// <summary>Pinned to the right edge.</summary>
    Right
}

/// <summary>
/// Column content alignment.
/// </summary>
public enum ColumnAlignment
{
    /// <summary>Left-aligned content.</summary>
    Left,

    /// <summary>Center-aligned content.</summary>
    Center,

    /// <summary>Right-aligned content.</summary>
    Right
}

/// <summary>
/// Sort direction for table columns.
/// </summary>
public enum SortDirection
{
    /// <summary>Ascending (A→Z, 0→9).</summary>
    Ascending,

    /// <summary>Descending (Z→A, 9→0).</summary>
    Descending
}

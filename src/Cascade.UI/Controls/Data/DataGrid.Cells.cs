namespace Cascade.UI;

/// <summary>
/// Cell-level selection and keyboard support of <see cref="DataGrid{T}"/>: the current cell, cell
/// blocks for <see cref="CellSelectionMode.Range"/> / <see cref="CellSelectionMode.MultiRange"/>,
/// select-all, clearing cells, typing to edit, and the public <see cref="Selection"/> snapshot.
/// The keys themselves are routed by the input dispatcher (<c>InputDispatcher.GridCells.cs</c>).
/// </summary>
public sealed partial class DataGrid<T>
{
    /// <summary>One rectangular block of selected cells: a set of display rows (null = every row) by a column span.</summary>
    internal sealed class CellBlock
    {
        internal CellBlock(HashSet<int>? rows, int colA, int colB)
        {
            Rows = rows;
            ColA = colA;
            ColB = colB;
        }

        /// <summary>The block's display rows, or null for every row (Ctrl+A, Ctrl+Space).</summary>
        internal HashSet<int>? Rows { get; set; }

        /// <summary>The column the block is anchored at.</summary>
        internal int ColA { get; set; }

        /// <summary>The column at the block's moving edge.</summary>
        internal int ColB { get; set; }

        internal bool Contains(int row, int col)
        {
            return (Rows is null || Rows.Contains(row)) && TabularCellNavigation.InSpan(col, ColA, ColB);
        }

        internal CellBlock Clone()
        {
            return new CellBlock(Rows is null ? null : new HashSet<int>(Rows), ColA, ColB);
        }
    }

    internal int currentCol = -1;
    internal int anchorCol = -1;

    // Range modes only; the last block is the one Shift+arrows extend.
    internal readonly List<CellBlock> cellBlocks = [];

    /// <summary>
    /// The cells the user has selected, in the order they appear on screen (rows top to bottom,
    /// columns left to right). In <see cref="CellSelectionMode.Range"/> and
    /// <see cref="CellSelectionMode.MultiRange"/> these are the selected blocks; in
    /// <see cref="CellSelectionMode.Single"/> the current cell, or every visible cell of the selected
    /// rows when several rows are selected; in <see cref="CellSelectionMode.RowOnly"/> every visible
    /// cell of the selected rows. Keep a <see cref="NodeRef{T}"/> to the grid to read it.
    /// </summary>
    public GridSelection<T> Selection
    {
        get
        {
            var cells = new List<GridCell<T>>();
            CollectSelection(out var rows, out var cols, out bool cellBased);
            var items = Items.Value;
            foreach (int row in rows)
            {
                int dataRow = MapRow(row);
                if ((uint)dataRow >= (uint)items.Count)
                {
                    continue;
                }

                foreach (int col in cols)
                {
                    if (cellBased && !((ITabularCellGrid)this).IsCellSelected(row, col))
                    {
                        continue;
                    }

                    cells.Add(new GridCell<T>(items[dataRow], row, Columns[col].Header, col, CellValue(items[dataRow], Columns[col])));
                }
            }

            return new GridSelection<T>(cells);
        }
    }

    // ── ITabularCellGrid ─────────────────────────────────────────────

    bool ITabularCellGrid.CellNavigationEnabled => cellSelectionModeValue != CellSelectionMode.RowOnly;

    bool ITabularCellGrid.RangeSelectionEnabled => IsRangeMode;

    bool ITabularCellGrid.MultiRangeEnabled => cellSelectionModeValue == CellSelectionMode.MultiRange;

    int ITabularCellGrid.CurrentColumn => cellSelectionModeValue == CellSelectionMode.RowOnly ? -1 : currentCol;

    int ITabularCellGrid.AnchorColumn => anchorCol;

    bool ITabularCellGrid.HasCellBlocks => IsRangeMode && cellBlocks.Count > 0;

    private bool IsRangeMode => cellSelectionModeValue is CellSelectionMode.Range or CellSelectionMode.MultiRange;

    bool ITabularCellGrid.IsCellSelected(int row, int col)
    {
        if (!IsRangeMode || cellBlocks.Count == 0)
        {
            return selectedRows.Contains(row);
        }

        foreach (var block in cellBlocks)
        {
            if (block.Contains(row, col))
            {
                return true;
            }
        }

        return false;
    }

    void ITabularCellGrid.SetCurrentColumn(int col)
    {
        if (cellSelectionModeValue == CellSelectionMode.RowOnly || !TabularCellNavigation.IsVisibleColumn(this, col))
        {
            return;
        }

        currentCol = col;
        anchorCol = col;
    }

    void ITabularCellGrid.SelectCell(int row, int col, CellSelectKind kind)
    {
        ITabularDataNode self = this;
        if ((uint)row >= (uint)self.RowCount)
        {
            return;
        }

        int column = TabularCellNavigation.Clamp(this, col);
        bool rowChanged = row != selectedRowIdx;

        if (!IsRangeMode)
        {
            SelectRowsForCell(row, kind);
        }
        else
        {
            SelectBlockForCell(row, column, kind);
        }

        if (cellSelectionModeValue != CellSelectionMode.RowOnly)
        {
            currentCol = column;
            if (kind is CellSelectKind.Replace or CellSelectKind.Add || anchorCol < 0)
            {
                anchorCol = column;
            }
        }

        if (rowChanged)
        {
            NotifyRowSelected(row);
        }
    }

    /// <summary>Single and RowOnly modes: rows only, the way <see cref="ITabularDataNode.SelectRow"/> selects them.</summary>
    private void SelectRowsForCell(int row, CellSelectKind kind)
    {
        if (kind is CellSelectKind.Extend or CellSelectKind.AddExtend && anchorRow >= 0)
        {
            selectedRows.Clear();
            TabularNavigation.AddVisualRange(this, anchorRow, row, selectedRows);
            selectedRowIdx = row;
            return;
        }

        selectedRows.Clear();
        selectedRows.Add(row);
        selectedRowIdx = row;
        anchorRow = row;
    }

    /// <summary>Range modes: the blocks, and the selected rows as their union.</summary>
    private void SelectBlockForCell(int row, int column, CellSelectKind kind)
    {
        bool multi = cellSelectionModeValue == CellSelectionMode.MultiRange;
        bool haveAnchor = anchorRow >= 0 && anchorCol >= 0;
        switch (kind)
        {
            case CellSelectKind.Extend when haveAnchor:
            {
                if (cellBlocks.Count == 0)
                {
                    cellBlocks.Add(new CellBlock([], anchorCol, column));
                }

                var block = cellBlocks[^1];
                var rows = new HashSet<int>();
                TabularNavigation.AddVisualRange(this, anchorRow, row, rows);
                block.Rows = rows;
                block.ColA = anchorCol;
                block.ColB = column;
                selectedRowIdx = row;
                RebuildSelectedRowsFromBlocks();
                return;
            }

            case CellSelectKind.AddExtend when haveAnchor && multi:
            {
                var rows = new HashSet<int>();
                TabularNavigation.AddVisualRange(this, anchorRow, row, rows);
                cellBlocks.Add(new CellBlock(rows, anchorCol, column));
                selectedRowIdx = row;
                RebuildSelectedRowsFromBlocks();
                return;
            }

            case CellSelectKind.Add when multi:
                cellBlocks.Add(new CellBlock([row], column, column));
                selectedRows.Add(row);
                selectedRowIdx = row;
                anchorRow = row;
                anchorCol = column;
                return;

            default:
                cellBlocks.Clear();
                cellBlocks.Add(new CellBlock([row], column, column));
                selectedRows.Clear();
                selectedRows.Add(row);
                selectedRowIdx = row;
                anchorRow = row;
                anchorCol = column;
                return;
        }
    }

    private void RebuildSelectedRowsFromBlocks()
    {
        selectedRows.Clear();
        int rowCount = ((ITabularDataNode)this).RowCount;
        foreach (var block in cellBlocks)
        {
            if (block.Rows is null)
            {
                AddAllRows();
                return;
            }

            foreach (int r in block.Rows)
            {
                if ((uint)r < (uint)rowCount)
                {
                    selectedRows.Add(r);
                }
            }
        }
    }

    private void AddAllRows()
    {
        int rowCount = ((ITabularDataNode)this).RowCount;
        selectedRows.EnsureCapacity(rowCount);
        for (int r = 0; r < rowCount; r++)
        {
            selectedRows.Add(r);
        }
    }

    /// <summary>Pushes the newly selected row through the <c>Selected</c> binding and <c>OnSelect</c>, as a click does.</summary>
    private void NotifyRowSelected(int row)
    {
        int dataRow = MapRow(row);
        var items = Items.Value;
        if ((uint)dataRow >= (uint)items.Count)
        {
            return;
        }

        T item = items[dataRow];
        selectedBinding?.OnChange(item);
        onSelectHandler?.Invoke(item);
    }

    void ITabularCellGrid.SelectAllCells()
    {
        ITabularDataNode self = this;
        if (self.RowCount == 0)
        {
            return;
        }

        selectedRows.Clear();
        AddAllRows();
        if (selectedRowIdx < 0)
        {
            selectedRowIdx = TabularNavigation.First(this);
            anchorRow = selectedRowIdx;
            if (cellSelectionModeValue != CellSelectionMode.RowOnly)
            {
                currentCol = TabularCellNavigation.Clamp(this, currentCol);
                anchorCol = currentCol;
            }
        }

        cellBlocks.Clear();
        if (IsRangeMode)
        {
            cellBlocks.Add(new CellBlock(null, TabularCellNavigation.FirstColumn(this), TabularCellNavigation.LastColumn(this)));
        }
    }

    void ITabularCellGrid.SelectEntireRows()
    {
        if (!IsRangeMode || cellBlocks.Count == 0)
        {
            return;
        }

        var block = cellBlocks[^1];
        block.ColA = TabularCellNavigation.FirstColumn(this);
        block.ColB = TabularCellNavigation.LastColumn(this);
    }

    bool ITabularCellGrid.SelectEntireColumns()
    {
        if (!IsRangeMode || selectedRowIdx < 0 || currentCol < 0)
        {
            return false;
        }

        if (cellBlocks.Count == 0)
        {
            cellBlocks.Add(new CellBlock(null, anchorCol, currentCol));
        }

        cellBlocks[^1].Rows = null;
        RebuildSelectedRowsFromBlocks();
        return true;
    }

    bool ITabularCellGrid.ClearSelectedCells()
    {
        var targets = new List<(int Row, int Col)>();
        if (IsRangeMode && cellBlocks.Count > 0)
        {
            CollectSelection(out var rows, out var cols, out _);
            foreach (int row in rows)
            {
                foreach (int col in cols)
                {
                    if (((ITabularCellGrid)this).IsCellSelected(row, col))
                    {
                        targets.Add((row, col));
                    }
                }
            }
        }
        else if (selectedRowIdx >= 0 && currentCol >= 0)
        {
            targets.Add((selectedRowIdx, currentCol));
        }

        return ClearCells(targets, "Clear");
    }

    bool ITabularCellGrid.BeginEditWithText(int row, int col, string text)
    {
        if ((uint)col >= (uint)Columns.Count || !HasTextEditor(Columns[col]))
        {
            return false;
        }

        ITabularDataNode self = this;
        if (!self.BeginEdit(row, col))
        {
            return false;
        }

        editBuffer = text;
        editCursorPos = text.Length;
        return true;
    }

    /// <summary>Whether a column edits through the grid's text buffer (not a toggle, dropdown or calendar).</summary>
    private static bool HasTextEditor(DataGridColumn<T> column)
    {
        if (column.isReadOnly || column.computeFunc is not null || column.boolGetter is not null)
        {
            return false;
        }

        return column.kind switch
        {
            DataColumnKind.Text => column.textSetter is not null,
            DataColumnKind.Number => column.objectSetter is not null,
            _ => false,
        };
    }

    // ── Shared by copy, cut, clear and Selection ─────────────────────

    /// <summary>
    /// The rows and columns the selection covers, both in screen order. With
    /// <paramref name="cellBased"/> true only the cells <see cref="ITabularCellGrid.IsCellSelected"/>
    /// accepts are part of it (cell blocks); otherwise every listed column of every listed row is.
    /// Single mode with one row selected is just the current cell.
    /// </summary>
    internal void CollectSelection(out List<int> rows, out List<int> cols, out bool cellBased)
    {
        rows = [];
        cols = [];
        cellBased = IsRangeMode && cellBlocks.Count > 0;

        int singleRow = selectedRows.Count == 0 ? selectedRowIdx : -1;
        TabularNavigation.AddSelectedRowsInScreenOrder(this, selectedRows, singleRow, rows);
        if (rows.Count == 0)
        {
            return;
        }

        bool currentCellOnly = cellSelectionModeValue == CellSelectionMode.Single
            && rows.Count == 1
            && TabularCellNavigation.IsVisibleColumn(this, currentCol);
        if (currentCellOnly)
        {
            cols.Add(currentCol);
            return;
        }

        for (int c = TabularCellNavigation.FirstColumn(this); c >= 0; c = TabularCellNavigation.Next(this, c, +1))
        {
            if (!cellBased || ColumnHasSelectedCell(c))
            {
                cols.Add(c);
            }
        }
    }

    private bool ColumnHasSelectedCell(int col)
    {
        foreach (var block in cellBlocks)
        {
            if (TabularCellNavigation.InSpan(col, block.ColA, block.ColB))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears the editable cells in <paramref name="cells"/> as one undo step: text columns become
    /// empty, numeric columns zero (of the value's own type). Toggles, dropdowns, dates and custom
    /// columns have no empty value of their own and are left alone.
    /// </summary>
    private bool ClearCells(List<(int Row, int Col)> cells, string description)
    {
        var items = Items.Value;
        IDisposable? batch = null;
        if (undoEnabledValue && cells.Count > 0)
        {
            EnsureUndoStack();
            batch = undoStack!.BeginBatch(description);
        }

        bool any = false;
        var changedRows = new HashSet<int>();
        try
        {
            foreach (var (row, col) in cells)
            {
                int dataRow = MapRow(row);
                if ((uint)dataRow >= (uint)items.Count || !((ITabularDataNode)this).IsColumnEditable(col))
                {
                    continue;
                }

                var column = Columns[col];
                var item = items[dataRow];
                if (!TryEmptyValue(item, column, out object? empty))
                {
                    continue;
                }

                object? old = column.textGetter is not null ? column.textGetter(item) : column.objectGetter?.Invoke(item);
                int capturedRow = dataRow;
                if (undoEnabledValue)
                {
                    undoStack!.Execute(UndoCommand.Create(
                        $"{description} {column.Header}",
                        () => { SetRawValue(items[capturedRow], column, empty); InvalidateCellCache(capturedRow); },
                        () => { SetRawValue(items[capturedRow], column, old); InvalidateCellCache(capturedRow); }));
                }
                else
                {
                    SetRawValue(item, column, empty);
                    InvalidateCellCache(dataRow);
                }

                any = true;
                if (changedRows.Add(dataRow))
                {
                    onChangeHandler?.Invoke(item);
                }
            }
        }
        finally
        {
            batch?.Dispose();
        }

        foreach (int dataRow in changedRows)
        {
            int displayRow = DisplayRowOf(dataRow);
            if (displayRow >= 0)
            {
                ((ITabularDataNode)this).ValidateRow(displayRow);
            }
        }

        return any;
    }

    /// <summary>The value a cleared cell takes, or false when the column has none.</summary>
    private static bool TryEmptyValue(T item, DataGridColumn<T> column, out object? empty)
    {
        empty = null;
        if (column.textSetter is not null)
        {
            empty = "";
            return true;
        }

        if (column.kind != DataColumnKind.Number || column.objectSetter is null || column.objectGetter is null)
        {
            return false;
        }

        empty = column.objectGetter(item) switch
        {
            int => 0,
            long => 0L,
            short => (short)0,
            byte => (byte)0,
            uint => 0u,
            ulong => 0ul,
            float => 0f,
            double => 0d,
            decimal => 0m,
            _ => null,
        };
        return empty is not null;
    }

    private static void SetRawValue(T item, DataGridColumn<T> column, object? value)
    {
        if (column.textSetter is not null)
        {
            column.textSetter(item, value as string ?? value?.ToString() ?? "");
            return;
        }

        if (value is not null)
        {
            column.objectSetter?.Invoke(item, value);
        }
    }

    private static object? CellValue(T item, DataGridColumn<T> column)
    {
        if (column.textGetter is not null)
        {
            return column.textGetter(item);
        }

        if (column.objectGetter is not null)
        {
            return column.objectGetter(item);
        }

        if (column.boolGetter is not null)
        {
            return column.boolGetter(item);
        }

        return column.computeFunc?.Invoke(item);
    }

    /// <summary>The display row showing data row <paramref name="dataRow"/>, or -1 when it is filtered out.</summary>
    private int DisplayRowOf(int dataRow)
    {
        if (sortedIndices is not null)
        {
            return Array.IndexOf(sortedIndices, dataRow);
        }

        if (filteredIndices is not null)
        {
            return Array.IndexOf(filteredIndices, dataRow);
        }

        return dataRow;
    }

    /// <summary>Forgets cell blocks whose rows no longer mean the same thing (sort, filter, new data).</summary>
    private void ResetCellBlocks()
    {
        cellBlocks.Clear();
        anchorCol = currentCol;
    }

    /// <summary>The cell part of <see cref="ITabularDataNode.CaptureInteractionState"/>.</summary>
    private GridCellState CaptureCellState()
    {
        var blocks = new CellBlockState[cellBlocks.Count];
        for (int i = 0; i < blocks.Length; i++)
        {
            var block = cellBlocks[i];
            blocks[i] = new CellBlockState(block.Rows is null ? null : [.. block.Rows], block.ColA, block.ColB);
        }

        return new GridCellState(currentCol, anchorCol, blocks, editingRow, editingCol, editBuffer, editCursorPos);
    }

    /// <summary>The cell part of <see cref="ITabularDataNode.RestoreInteractionState"/>, clamped to this grid.</summary>
    private void RestoreCellState(GridCellState state)
    {
        int rowCount = ((ITabularDataNode)this).RowCount;
        int colCount = Columns.Count;
        currentCol = state.CurrentColumn < colCount ? state.CurrentColumn : -1;
        anchorCol = state.AnchorColumn < colCount ? state.AnchorColumn : currentCol;

        cellBlocks.Clear();
        foreach (var block in state.Blocks)
        {
            if (block.ColA >= colCount || block.ColB >= colCount)
            {
                continue;
            }

            HashSet<int>? rows = null;
            if (block.Rows is { } blockRows)
            {
                rows = [];
                foreach (int r in blockRows)
                {
                    if ((uint)r < (uint)rowCount)
                    {
                        rows.Add(r);
                    }
                }
            }

            cellBlocks.Add(new CellBlock(rows, block.ColA, block.ColB));
        }

        // An edit in progress survives a re-render (an OnChange handler that calls Invalidate()
        // must not throw away what the user is typing in the next cell).
        if (state.EditingRow >= 0 && state.EditingRow < rowCount && state.EditingCol >= 0 && state.EditingCol < colCount)
        {
            editingRow = state.EditingRow;
            editingCol = state.EditingCol;
            editBuffer = state.EditBuffer;
            editCursorPos = Math.Clamp(state.EditCursor, 0, editBuffer.Length);
        }
    }
}

/// <summary>A selected cell of a <see cref="DataGrid{T}"/>, from <see cref="DataGrid{T}.Selection"/>.</summary>
/// <typeparam name="T">The row data type.</typeparam>
/// <param name="Row">The row's item.</param>
/// <param name="RowIndex">The row's position in the grid's sort/filter order.</param>
/// <param name="Column">The column's header.</param>
/// <param name="ColumnIndex">The column's position in the grid's current column order.</param>
/// <param name="Value">The cell's value (what the column's getter returns).</param>
public readonly record struct GridCell<T>(T Row, int RowIndex, string Column, int ColumnIndex, object? Value);

/// <summary>A snapshot of the cells selected in a <see cref="DataGrid{T}"/>.</summary>
/// <typeparam name="T">The row data type.</typeparam>
public sealed class GridSelection<T>
{
    internal GridSelection(IReadOnlyList<GridCell<T>> cells)
    {
        Cells = cells;
    }

    /// <summary>The selected cells, rows top to bottom and columns left to right as on screen.</summary>
    public IReadOnlyList<GridCell<T>> Cells { get; }

    /// <summary>Number of selected cells.</summary>
    public int CellCount => Cells.Count;

    /// <summary>Whether any cell is selected.</summary>
    public bool Any => Cells.Count > 0;
}

/// <summary>One cell block in a <see cref="GridCellState"/>.</summary>
internal readonly record struct CellBlockState(int[]? Rows, int ColA, int ColB);

/// <summary>
/// A grid's current cell, cell blocks and in-progress edit, carried to the node that replaces it
/// on a re-render (part of <see cref="TabularInteractionState"/>).
/// </summary>
internal sealed record GridCellState(
    int CurrentColumn,
    int AnchorColumn,
    CellBlockState[] Blocks,
    int EditingRow,
    int EditingCol,
    string EditBuffer,
    int EditCursor);

namespace Cascade.UI;

/// <summary>How <see cref="ITabularCellGrid.SelectCell"/> changes the selection.</summary>
internal enum CellSelectKind
{
    /// <summary>The cell becomes the only selection and the new anchor (a click, an arrow key).</summary>
    Replace,

    /// <summary>The selection runs from the anchor to the cell (Shift+click, Shift+arrow).</summary>
    Extend,

    /// <summary>The cell starts a new block beside the existing ones (Ctrl+click in <see cref="CellSelectionMode.MultiRange"/>).</summary>
    Add,

    /// <summary>A new block from the anchor to the cell, beside the existing ones (Ctrl+Shift+click in <see cref="CellSelectionMode.MultiRange"/>).</summary>
    AddExtend,
}

/// <summary>
/// Cell-level state of a <see cref="DataGrid{T}"/>: the current cell (the selected row plus
/// <see cref="CurrentColumn"/>), the cell blocks of a range selection, and the operations the
/// keyboard and the pointer drive. Separate from <see cref="ITabularDataNode"/>, which both tabular
/// controls implement; a <see cref="DataTable{T}"/> has rows only.
/// </summary>
internal interface ITabularCellGrid
{
    /// <summary>Whether cells can be navigated (every <see cref="CellSelectionMode"/> but <see cref="CellSelectionMode.RowOnly"/>).</summary>
    bool CellNavigationEnabled { get; }

    /// <summary>Whether the selection is made of cell blocks (<see cref="CellSelectionMode.Range"/> and <see cref="CellSelectionMode.MultiRange"/>).</summary>
    bool RangeSelectionEnabled { get; }

    /// <summary>Whether Ctrl+click adds blocks (<see cref="CellSelectionMode.MultiRange"/>).</summary>
    bool MultiRangeEnabled { get; }

    /// <summary>The current cell's column (the row is <see cref="ITabularDataNode.SelectedRowIndex"/>), or -1.</summary>
    int CurrentColumn { get; }

    /// <summary>The column the current block is anchored at, or -1.</summary>
    int AnchorColumn { get; }

    /// <summary>
    /// Whether the selection is a set of cell blocks rather than whole rows: cells outside the
    /// blocks are not selected even on a selected row, so the painter highlights cells, not rows.
    /// </summary>
    bool HasCellBlocks { get; }

    /// <summary>Whether the cell at display row <paramref name="row"/>, column <paramref name="col"/> is selected.</summary>
    bool IsCellSelected(int row, int col);

    /// <summary>Moves the current cell to (<paramref name="row"/>, <paramref name="col"/>) and updates the selection.</summary>
    void SelectCell(int row, int col, CellSelectKind kind);

    /// <summary>Makes <paramref name="col"/> the current column without touching the row selection.</summary>
    void SetCurrentColumn(int col);

    /// <summary>Ctrl+A: every row (and, in a range mode, every visible column).</summary>
    void SelectAllCells();

    /// <summary>Shift+Space: the current block's rows, every visible column.</summary>
    void SelectEntireRows();

    /// <summary>Ctrl+Space: every row, the current block's columns. False when there is no current cell.</summary>
    bool SelectEntireColumns();

    /// <summary>
    /// Delete: clears the editable cells of a cell-block selection, otherwise the current cell, as
    /// one undo step. Returns true when anything was cleared.
    /// </summary>
    bool ClearSelectedCells();

    /// <summary>
    /// Typing on a cell that is not being edited: starts editing (row, col) with
    /// <paramref name="text"/> replacing its value. False when the cell has no text editor (read-only,
    /// boolean, select or date columns).
    /// </summary>
    bool BeginEditWithText(int row, int col, string text);
}

/// <summary>
/// Movement through the columns of a tabular control in the order they appear on screen: column
/// index order (a column reorder moves the column in the list), skipping hidden columns.
/// Rows are moved by <see cref="TabularNavigation"/>; together they move the current cell.
/// </summary>
internal static class TabularCellNavigation
{
    /// <summary>Whether <paramref name="col"/> is shown.</summary>
    internal static bool IsVisibleColumn(ITabularDataNode tdn, int col)
    {
        return (uint)col < (uint)tdn.ColumnCount && tdn.GetColumnVisible(col);
    }

    /// <summary>The first visible column, or -1.</summary>
    internal static int FirstColumn(ITabularDataNode tdn)
    {
        return Next(tdn, -1, +1);
    }

    /// <summary>The last visible column, or -1.</summary>
    internal static int LastColumn(ITabularDataNode tdn)
    {
        return Next(tdn, tdn.ColumnCount, -1);
    }

    /// <summary>
    /// The next visible column after <paramref name="col"/> in <paramref name="direction"/> (+1
    /// right, -1 left), or -1 past the edge. Does not wrap.
    /// </summary>
    internal static int Next(ITabularDataNode tdn, int col, int direction)
    {
        int step = direction >= 0 ? 1 : -1;
        for (int c = col + step; c >= 0 && c < tdn.ColumnCount; c += step)
        {
            if (tdn.GetColumnVisible(c))
            {
                return c;
            }
        }

        return -1;
    }

    /// <summary>
    /// <paramref name="col"/> when it is a visible column; otherwise the nearest visible column
    /// (the next one to the right, else to the left), or -1 when nothing is visible. Used when the
    /// current column was hidden or the grid has no current column yet.
    /// </summary>
    internal static int Clamp(ITabularDataNode tdn, int col)
    {
        if (IsVisibleColumn(tdn, col))
        {
            return col;
        }

        if (col < 0)
        {
            return FirstColumn(tdn);
        }

        int right = Next(tdn, col, +1);
        return right >= 0 ? right : Next(tdn, Math.Min(col, tdn.ColumnCount), -1);
    }

    /// <summary>Visible columns between <paramref name="a"/> and <paramref name="b"/> inclusive, in order.</summary>
    internal static bool InSpan(int col, int a, int b)
    {
        return col >= Math.Min(a, b) && col <= Math.Max(a, b);
    }

    /// <summary>Whether a cell can be edited with the keyboard (Enter, F2, typing, Tab target).</summary>
    internal static bool IsEditableCell(ITabularDataNode tdn, int col)
    {
        return IsVisibleColumn(tdn, col) && tdn.IsColumnEditable(col);
    }

    /// <summary>
    /// The next editable cell after (<paramref name="row"/>, <paramref name="col"/>) in reading
    /// order — rightwards along the row, then on to the next row on screen — or before it when
    /// <paramref name="direction"/> is -1 (Shift+Tab). Rows follow <see cref="TabularNavigation"/>,
    /// so a grouped or sorted grid is walked as painted. False at the end of the grid.
    /// </summary>
    internal static bool TryNextEditableCell(ITabularDataNode tdn, int row, int col, int direction, out int nextRow, out int nextCol)
    {
        nextRow = -1;
        nextCol = -1;
        int step = direction >= 0 ? 1 : -1;
        int rows = TabularNavigation.VisibleRowCount(tdn);
        if (rows == 0 || !HasEditableColumn(tdn))
        {
            return false;
        }

        int position = TabularNavigation.PositionOf(tdn, row, out bool hidden);
        if (position < 0 || hidden)
        {
            position = step > 0 ? 0 : rows - 1;
            col = step > 0 ? -1 : tdn.ColumnCount;
        }

        while (position >= 0 && position < rows)
        {
            int r = TabularNavigation.RowAt(tdn, position);
            for (int c = Next(tdn, col, step); c >= 0; c = Next(tdn, c, step))
            {
                if (tdn.IsColumnEditable(c))
                {
                    nextRow = r;
                    nextCol = c;
                    return true;
                }
            }

            position += step;
            col = step > 0 ? -1 : tdn.ColumnCount;
        }

        return false;
    }

    private static bool HasEditableColumn(ITabularDataNode tdn)
    {
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            if (IsEditableCell(tdn, c))
            {
                return true;
            }
        }

        return false;
    }
}

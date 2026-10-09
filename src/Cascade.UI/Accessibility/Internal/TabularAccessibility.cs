namespace Cascade.UI;

/// <summary>A column header of a table as assistive technology sees it.</summary>
/// <param name="Column">The column index (in the control's current column order).</param>
/// <param name="Position">1-based position among the visible columns.</param>
/// <param name="Header">The header text.</param>
/// <param name="Bounds">Window-logical bounds.</param>
/// <param name="SortDirection">"ascending"/"descending" for the sorted column, else null.</param>
internal readonly record struct TabularHeaderInfo(int Column, int Position, string Header, Rect Bounds, string? SortDirection);

/// <summary>A cell of a table row as assistive technology sees it.</summary>
/// <param name="Column">The column index (in the control's current column order).</param>
/// <param name="Position">1-based position among the visible columns.</param>
/// <param name="Header">The column's header text.</param>
/// <param name="Text">The cell's text (the edit buffer while it is being edited).</param>
/// <param name="Bounds">Window-logical bounds.</param>
/// <param name="Selected">Part of the selection.</param>
/// <param name="Current">The current (keyboard) cell.</param>
/// <param name="Editing">Being edited.</param>
/// <param name="Checked">For a boolean column, its value; otherwise null.</param>
internal readonly record struct TabularCellInfo(
    int Column, int Position, string Header, string Text, Rect Bounds, bool Selected, bool Current, bool Editing, bool? Checked);

/// <summary>A row of a table on screen as assistive technology sees it.</summary>
/// <param name="Row">The display row (position in the sort/filter order).</param>
/// <param name="Position">1-based position among the rows on screen order (groups, collapsed groups skipped).</param>
/// <param name="Bounds">Window-logical bounds of the whole row.</param>
/// <param name="Selected">Part of the selection.</param>
/// <param name="Current">Holds the current cell (the selected row).</param>
/// <param name="Cells">The row's visible cells, left to right.</param>
internal readonly record struct TabularRowInfo(int Row, int Position, Rect Bounds, bool Selected, bool Current, IReadOnlyList<TabularCellInfo> Cells);

/// <summary>
/// The table / row / cell structure of a <see cref="DataTable{T}"/> or <see cref="DataGrid{T}"/>
/// for the accessibility trees (the platform tree and the DevTools tree): the column headers, the
/// rows on screen with their cells, and which cell is current. Rows and cells are painted inside
/// the table node — they are not nodes — so their geometry comes from
/// <see cref="TabularRowGeometry"/>, the same source the painter and the pointer use. Only the rows
/// in the viewport are produced: a 100,000-row table costs a screenful, not 100,000 elements.
/// </summary>
internal static class TabularAccessibility
{
    /// <summary>Number of rows a screen reader can reach: every row, minus those in collapsed groups.</summary>
    internal static int RowCount(ITabularDataNode table)
    {
        return TabularNavigation.VisibleRowCount(table);
    }

    /// <summary>Number of visible columns.</summary>
    internal static int ColumnCount(ITabularDataNode table)
    {
        int count = 0;
        for (int c = 0; c < table.ColumnCount; c++)
        {
            if (table.GetColumnVisible(c))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The visible columns, left to right (column indices in the current column order).</summary>
    internal static List<int> VisibleColumns(ITabularDataNode table)
    {
        var result = new List<int>(table.ColumnCount);
        for (int c = 0; c < table.ColumnCount; c++)
        {
            if (table.GetColumnVisible(c))
            {
                result.Add(c);
            }
        }

        return result;
    }

    /// <summary>The window-logical area the rows scroll in (between the header and a bottom aggregate row).</summary>
    internal static Rect DataArea(ITabularDataNode table, Rect bounds)
    {
        float top = bounds.Y + TabularRowGeometry.DataTop(table);
        float bottom = bounds.Y + TabularRowGeometry.DataBottom(table, bounds.Height);
        return new Rect(bounds.X, top, bounds.Width, Math.Max(0f, bottom - top));
    }

    /// <summary>
    /// Display row <paramref name="row"/>'s window-logical rectangle where it is — possibly
    /// scrolled out of the data area (clip with <see cref="DataArea"/>). False before the table is
    /// painted, or for a row out of range or in a collapsed group.
    /// </summary>
    internal static bool TryGetRowBounds(ITabularDataNode table, Rect tableBounds, int row, out Rect bounds)
    {
        bounds = default;
        if (tableBounds.Width <= 0f || !TabularRowGeometry.TryGetRowContentTop(table, row, out float contentTop))
        {
            return false;
        }

        float top = tableBounds.Y + TabularRowGeometry.DataTop(table) + contentTop - table.ScrollOffsetY;
        bounds = new Rect(tableBounds.X, top, tableBounds.Width, table.GetRowHeight());
        return true;
    }

    /// <summary>Column <paramref name="col"/>'s left edge and width within a row of <paramref name="rowBounds"/>.</summary>
    internal static bool TryGetCellBounds(ITabularDataNode table, Rect rowBounds, int col, out Rect bounds)
    {
        bounds = default;
        var widths = TabularRowGeometry.ScaledColumnWidths(table, rowBounds.Width);
        if (!TabularRowGeometry.TryGetColumnSpan(table, widths, col, out float left, out float width))
        {
            return false;
        }

        bounds = new Rect(rowBounds.X + left, rowBounds.Y, width, rowBounds.Height);
        return true;
    }

    /// <summary>The selected display rows in screen order.</summary>
    internal static List<int> SelectedRows(ITabularDataNode table)
    {
        var rows = new List<int>();
        if (!table.IsGrouped)
        {
            for (int row = 0; row < table.RowCount; row++)
            {
                if (table.IsRowSelected(row))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        for (int g = 0; g < table.GroupCount; g++)
        {
            if (table.IsGroupCollapsed(g))
            {
                continue;
            }

            int count = table.GetGroupRowCount(g);
            for (int i = 0; i < count; i++)
            {
                int row = table.GetGroupDataRowIndex(g, i);
                if (table.IsRowSelected(row))
                {
                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// The current cell: its row's 1-based on-screen position and its column, or false when
    /// nothing is selected. A <see cref="DataTable{T}"/> (or a grid with cell navigation off) has a
    /// current row only: <paramref name="column"/> is then -1.
    /// </summary>
    internal static bool TryGetCurrentCell(ITabularDataNode table, out int rowPosition, out int column)
    {
        rowPosition = -1;
        column = -1;
        int row = table.SelectedRowIndex;
        int position = TabularNavigation.PositionOf(table, row, out bool hidden);
        if (position < 0 || hidden)
        {
            return false;
        }

        rowPosition = position + 1;
        if (table is ITabularCellGrid { CellNavigationEnabled: true } cells
            && TabularCellNavigation.IsVisibleColumn(table, cells.CurrentColumn))
        {
            column = cells.CurrentColumn;
        }

        return true;
    }

    /// <summary>The visible column headers, left to right. Empty before the table is painted.</summary>
    internal static List<TabularHeaderInfo> Headers(ITabularDataNode table)
    {
        var result = new List<TabularHeaderInfo>(table.ColumnCount);
        var tableBounds = table.AbsoluteBounds;
        if (tableBounds.Width <= 0f)
        {
            return result;
        }

        var widths = TabularRowGeometry.ScaledColumnWidths(table, tableBounds.Width);
        float headerHeight = TabularRowGeometry.HeaderHeight(table);
        int position = 0;
        for (int c = 0; c < table.ColumnCount; c++)
        {
            if (!TabularRowGeometry.TryGetColumnSpan(table, widths, c, out float left, out float width))
            {
                continue;
            }

            position++;
            string? sort = table.IsSortable && table.SortColumnIndex == c
                ? table.SortDirectionValue == SortDirection.Descending ? "descending" : "ascending"
                : null;
            result.Add(new TabularHeaderInfo(c, position, table.GetColumnHeader(c), new Rect(tableBounds.X + left, tableBounds.Y, width, headerHeight), sort));
        }

        return result;
    }

    /// <summary>
    /// The rows inside the table's viewport, top to bottom, with their visible cells. Rows the
    /// painter draws only as an off-screen virtualization buffer are not included.
    /// </summary>
    internal static List<TabularRowInfo> RowsOnScreen(ITabularDataNode table)
    {
        var result = new List<TabularRowInfo>();
        var tableBounds = table.AbsoluteBounds;
        float rowHeight = table.GetRowHeight();
        if (tableBounds.Width <= 0f || tableBounds.Height <= 0f || rowHeight <= 0f)
        {
            return result;
        }

        var widths = TabularRowGeometry.ScaledColumnWidths(table, tableBounds.Width);
        float areaTop = tableBounds.Y + TabularRowGeometry.DataTop(table);
        float areaBottom = tableBounds.Y + TabularRowGeometry.DataBottom(table, tableBounds.Height);
        float scroll = table.ScrollOffsetY;
        // Ungrouped rows without detail panels are uniform: start at the first row in view.
        if (!table.IsGrouped && !table.HasRowDetail)
        {
            int first = Math.Max(0, (int)(scroll / rowHeight));
            for (int row = first; row < table.RowCount; row++)
            {
                float top = areaTop + (row * rowHeight) - scroll;
                if (top >= areaBottom)
                {
                    break;
                }

                if (top + rowHeight > areaTop)
                {
                    result.Add(BuildRow(table, widths, row, row, new Rect(tableBounds.X, top, tableBounds.Width, rowHeight)));
                }
            }

            return result;
        }

        // Otherwise walk the content once, top to bottom, as TabularRowGeometry lays it out.
        float y = 0f;
        int position = 0;
        int groups = table.IsGrouped ? table.GroupCount : 1;
        for (int g = 0; g < groups; g++)
        {
            int count = table.RowCount;
            if (table.IsGrouped)
            {
                y += TabularRowGeometry.GroupHeaderHeight;
                if (table.IsGroupCollapsed(g))
                {
                    continue;
                }

                count = table.GetGroupRowCount(g);
            }

            for (int i = 0; i < count; i++)
            {
                int row = table.IsGrouped ? table.GetGroupDataRowIndex(g, i) : i;
                float top = areaTop + y - scroll;
                if (top >= areaBottom)
                {
                    return result;
                }

                if (top + rowHeight > areaTop)
                {
                    result.Add(BuildRow(table, widths, row, position, new Rect(tableBounds.X, top, tableBounds.Width, rowHeight)));
                }

                y += rowHeight;
                if (table.HasRowDetail && table.IsRowExpanded(row))
                {
                    y += table.GetRowDetailHeight(row);
                }

                position++;
            }
        }

        return result;
    }

    private static TabularRowInfo BuildRow(ITabularDataNode table, float[] widths, int row, int position, Rect bounds)
    {
        var cells = new List<TabularCellInfo>(table.ColumnCount);
        var cellGrid = table as ITabularCellGrid;
        bool current = row == table.SelectedRowIndex;
        int currentColumn = cellGrid is { CellNavigationEnabled: true } ? cellGrid.CurrentColumn : -1;
        int columnPosition = 0;
        for (int c = 0; c < table.ColumnCount; c++)
        {
            if (!TabularRowGeometry.TryGetColumnSpan(table, widths, c, out float left, out float width))
            {
                continue;
            }

            columnPosition++;
            bool editing = table.IsEditing && table.EditingRow == row && table.EditingCol == c;
            bool selected = cellGrid is { HasCellBlocks: true } ? cellGrid.IsCellSelected(row, c) : table.IsRowSelected(row);
            bool? isChecked = table.IsBoolColumn(c) ? table.GetBoolValue(row, c) : null;
            string text = editing ? table.EditBuffer : table.GetCellText(row, c);
            cells.Add(new TabularCellInfo(
                c,
                columnPosition,
                table.GetColumnHeader(c),
                text,
                new Rect(bounds.X + left, bounds.Y, width, bounds.Height),
                selected,
                current && c == currentColumn,
                editing,
                isChecked));
        }

        return new TabularRowInfo(row, position + 1, bounds, table.IsRowSelected(row), current, cells);
    }

    /// <summary>The state properties of a table element: its size and its current cell.</summary>
    internal static Dictionary<string, string> TableStates(ITabularDataNode table)
    {
        var states = new Dictionary<string, string>
        {
            ["row_count"] = RowCount(table).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["column_count"] = ColumnCount(table).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if (table.SelectedRowCount > 1)
        {
            states["selected_rows"] = table.SelectedRowCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (TryGetCurrentCell(table, out int rowPosition, out int column))
        {
            states["current_row"] = rowPosition.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (column >= 0)
            {
                states["current_column"] = (VisiblePosition(table, column) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                states["current_column_header"] = table.GetColumnHeader(column);
            }
        }

        if (table.IsEditing)
        {
            states["editing"] = "true";
        }

        return states;
    }

    /// <summary>The state properties of a cell element.</summary>
    internal static Dictionary<string, string> CellStates(TabularRowInfo row, TabularCellInfo cell)
    {
        var states = new Dictionary<string, string>
        {
            ["row_index"] = row.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["column_index"] = cell.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["column_header"] = cell.Header,
        };

        if (cell.Selected)
        {
            states["selected"] = "true";
        }

        if (cell.Current)
        {
            states["current"] = "true";
        }

        if (cell.Editing)
        {
            states["editing"] = "true";
        }

        if (cell.Checked is { } isChecked)
        {
            states["checked"] = isChecked ? "true" : "false";
        }

        return states;
    }

    /// <summary>The state properties of a row element.</summary>
    internal static Dictionary<string, string> RowStates(TabularRowInfo row)
    {
        var states = new Dictionary<string, string>
        {
            ["row_index"] = row.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if (row.Selected)
        {
            states["selected"] = "true";
        }

        if (row.Current)
        {
            states["current"] = "true";
        }

        return states;
    }

    /// <summary>The state properties of a column header element.</summary>
    internal static Dictionary<string, string> HeaderStates(TabularHeaderInfo header)
    {
        var states = new Dictionary<string, string>
        {
            ["column_index"] = header.Position.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if (header.SortDirection is { } sort)
        {
            states["sort"] = sort;
        }

        return states;
    }

    private static int VisiblePosition(ITabularDataNode table, int column)
    {
        int position = 0;
        for (int c = 0; c < column; c++)
        {
            if (table.GetColumnVisible(c))
            {
                position++;
            }
        }

        return position;
    }
}

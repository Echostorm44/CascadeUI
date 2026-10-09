namespace Cascade.UI;

/// <summary>
/// DataTable / DataGrid structure for UI Automation. The table is one entry of the
/// <see cref="AccessibleTree"/> (its rows and cells are painted, not nodes — and a 100,000-row
/// table must not cost 100,000 entries per rebuild), so its parts are addressed lazily from the
/// table element, the way a list's rows are: a header row of column headers first, then one row
/// per row on screen order (groups as painted, collapsed groups skipped), each holding one cell per
/// visible column. The table implements the Grid, Table and Selection patterns; cells implement
/// GridItem, TableItem, SelectionItem and ScrollItem; the current cell of a focused grid is what
/// has keyboard focus. Positions are screen order; columns are the control's current order.
/// </summary>
internal sealed partial class UiaElement : IGridProvider, IGridItemProvider, ITableProvider, ITableItemProvider
{
    private int column = -1;
    private Dictionary<(UiaElementKind Kind, int Row, int Column), UiaElement>? tableParts;

    private bool IsTablePart => kind is UiaElementKind.TableHeaderRow or UiaElementKind.TableHeader
        or UiaElementKind.TableRow or UiaElementKind.TableCell;

    private ITabularDataNode TableNode => (ITabularDataNode)list!.node!;

    /// <summary>The table part at (<paramref name="position"/>, <paramref name="col"/>) of this table element (created once, then remembered).</summary>
    private UiaElement TablePart(UiaElementKind partKind, int position, int col)
    {
        tableParts ??= [];
        var key = (partKind, position, col);
        if (!tableParts.TryGetValue(key, out var element))
        {
            element = new UiaElement(Context, partKind, null, this, position, null, -1) { column = col };
            tableParts[key] = element;
        }

        return element;
    }

    /// <summary>The cell element at screen position <paramref name="position"/>, column <paramref name="col"/> of this table element.</summary>
    internal UiaElement TableCell(int position, int col)
    {
        return TablePart(UiaElementKind.TableCell, position, col);
    }

    /// <summary>The row element at screen position <paramref name="position"/> of this table element.</summary>
    internal UiaElement TableRow(int position)
    {
        return TablePart(UiaElementKind.TableRow, position, -1);
    }

    private UiaElement TableHeaderRow()
    {
        return TablePart(UiaElementKind.TableHeaderRow, -1, -1);
    }

    private UiaElement TableHeader(int col)
    {
        return TablePart(UiaElementKind.TableHeader, -1, col);
    }

    /// <summary>The element a focused table puts keyboard focus on: its current cell, else its selected row; null when none.</summary>
    internal UiaElement? FocusedTablePart()
    {
        if (node is not ITabularDataNode table)
        {
            return null;
        }

        int position = TabularNavigation.PositionOf(table, table.SelectedRowIndex, out bool hidden);
        if (position < 0 || hidden)
        {
            return null;
        }

        if (table is ITabularCellGrid { CellNavigationEnabled: true } cells
            && TabularCellNavigation.IsVisibleColumn(table, cells.CurrentColumn)
            && table.RowActionStrip is not { FocusedIndex: >= 0 })
        {
            return TableCell(position, cells.CurrentColumn);
        }

        return TableRow(position);
    }

    /// <summary>The table part under a window-logical point inside this table element, or null.</summary>
    internal UiaElement? TablePartAt(Point point, Rect tableBounds)
    {
        var table = (ITabularDataNode)node!;
        float relY = point.Y - tableBounds.Y;
        var hit = TabularRowGeometry.HitTest(table, relY, tableBounds.Height);
        int col = HitColumn(table, tableBounds.Width, point.X - tableBounds.X);
        if (hit.Kind == TabularHitKind.Header)
        {
            return col >= 0 ? TableHeader(col) : TableHeaderRow();
        }

        if (hit.Kind != TabularHitKind.Row)
        {
            return null;
        }

        int position = TabularNavigation.PositionOf(table, hit.Index, out bool hidden);
        if (position < 0 || hidden)
        {
            return null;
        }

        return col >= 0 ? TableCell(position, col) : TableRow(position);
    }

    private static int HitColumn(ITabularDataNode table, float tableWidth, float relX)
    {
        var widths = TabularRowGeometry.ScaledColumnWidths(table, tableWidth);
        for (int c = 0; c < widths.Length; c++)
        {
            if (TabularRowGeometry.TryGetColumnSpan(table, widths, c, out float left, out float width) && relX >= left && relX < left + width)
            {
                return c;
            }
        }

        return -1;
    }

    private int ResolveTablePart(AccessibleTree tree)
    {
        int tableIndex = list!.Resolve(tree);
        if (tableIndex == AccessibleTree.None || list.node is not ITabularDataNode table)
        {
            return AccessibleTree.None;
        }

        bool rowOk = kind is UiaElementKind.TableHeaderRow or UiaElementKind.TableHeader
            || (index >= 0 && index < TabularNavigation.VisibleRowCount(table));
        bool columnOk = kind is UiaElementKind.TableRow or UiaElementKind.TableHeaderRow
            || TabularCellNavigation.IsVisibleColumn(table, column);
        return rowOk && columnOk ? tableIndex : AccessibleTree.None;
    }

    /// <summary>The display row of a row or cell element.</summary>
    private int DisplayRow => TabularNavigation.RowAt(TableNode, index);

    private UiaFragment? NavigateTablePart(int direction)
    {
        var table = TableNode;
        var columns = TabularAccessibility.VisibleColumns(table);
        int rowCount = TabularNavigation.VisibleRowCount(table);
        switch (kind)
        {
            case UiaElementKind.TableHeaderRow:
                return direction switch
                {
                    UiaIds.NavigateDirection_Parent => list,
                    UiaIds.NavigateDirection_NextSibling => rowCount > 0 ? list!.TableRow(0) : null,
                    UiaIds.NavigateDirection_FirstChild => columns.Count > 0 ? list!.TableHeader(columns[0]) : null,
                    UiaIds.NavigateDirection_LastChild => columns.Count > 0 ? list!.TableHeader(columns[^1]) : null,
                    _ => null,
                };

            case UiaElementKind.TableRow:
                return direction switch
                {
                    UiaIds.NavigateDirection_Parent => list,
                    UiaIds.NavigateDirection_NextSibling => index + 1 < rowCount ? list!.TableRow(index + 1) : null,
                    UiaIds.NavigateDirection_PreviousSibling => index > 0 ? list!.TableRow(index - 1) : list!.TableHeaderRow(),
                    UiaIds.NavigateDirection_FirstChild => columns.Count > 0 ? list!.TableCell(index, columns[0]) : null,
                    UiaIds.NavigateDirection_LastChild => columns.Count > 0 ? list!.TableCell(index, columns[^1]) : null,
                    _ => null,
                };

            default:
            {
                int at = columns.IndexOf(column);
                bool header = kind == UiaElementKind.TableHeader;
                return direction switch
                {
                    UiaIds.NavigateDirection_Parent => header ? list!.TableHeaderRow() : list!.TableRow(index),
                    UiaIds.NavigateDirection_NextSibling => at >= 0 && at + 1 < columns.Count
                        ? (header ? list!.TableHeader(columns[at + 1]) : list!.TableCell(index, columns[at + 1]))
                        : null,
                    UiaIds.NavigateDirection_PreviousSibling => at > 0
                        ? (header ? list!.TableHeader(columns[at - 1]) : list!.TableCell(index, columns[at - 1]))
                        : null,
                    _ => null,
                };
            }
        }
    }

    /// <summary>A table element's first / last child: the header row / the last row.</summary>
    private UiaFragment? NavigateIntoTable(ITabularDataNode table, int direction)
    {
        if (direction == UiaIds.NavigateDirection_FirstChild)
        {
            return TableHeaderRow();
        }

        int rowCount = TabularNavigation.VisibleRowCount(table);
        return rowCount > 0 ? TableRow(rowCount - 1) : TableHeaderRow();
    }

    /// <summary>The table element's window-logical bounds (from the accessible tree, so they hold before a paint).</summary>
    private Rect TableBounds()
    {
        var tree = Context.Tree;
        int tableIndex = list!.Resolve(tree);
        return tableIndex == AccessibleTree.None ? default : tree[tableIndex].Bounds;
    }

    private Rect TablePartBounds()
    {
        var table = TableNode;
        var tableBounds = TableBounds();
        if (kind is UiaElementKind.TableHeaderRow or UiaElementKind.TableHeader)
        {
            var headerRow = new Rect(tableBounds.X, tableBounds.Y, tableBounds.Width, TabularRowGeometry.HeaderHeight(table));
            if (kind == UiaElementKind.TableHeaderRow)
            {
                return headerRow;
            }

            return TabularAccessibility.TryGetCellBounds(table, headerRow, column, out var headerCell) ? headerCell : default;
        }

        if (!TabularAccessibility.TryGetRowBounds(table, tableBounds, DisplayRow, out var row))
        {
            return default;
        }

        if (kind == UiaElementKind.TableRow)
        {
            return row;
        }

        return TabularAccessibility.TryGetCellBounds(table, row, column, out var cell) ? cell : default;
    }

    /// <summary>Rows and cells are clipped by the data area (they scroll under the header).</summary>
    private Rect TablePartVisible(Rect tableVisible)
    {
        var bounds = TablePartBounds();
        var clip = kind is UiaElementKind.TableHeaderRow or UiaElementKind.TableHeader
            ? tableVisible
            : tableVisible.Intersect(TabularAccessibility.DataArea(TableNode, TableBounds()));
        var visible = clip.Intersect(bounds);
        return visible.Width > 0 && visible.Height > 0 ? visible : default;
    }

    private string? TablePartName()
    {
        var table = TableNode;
        switch (kind)
        {
            case UiaElementKind.TableHeaderRow:
                return "Column headers";

            case UiaElementKind.TableHeader:
                return table.GetColumnHeader(column);

            case UiaElementKind.TableRow:
            {
                var columns = TabularAccessibility.VisibleColumns(table);
                return columns.Count > 0 ? table.GetCellText(DisplayRow, columns[0]) : null;
            }

            default:
            {
                int row = DisplayRow;
                if (table.IsEditing && table.EditingRow == row && table.EditingCol == column)
                {
                    return table.EditBuffer;
                }

                return table.IsBoolColumn(column)
                    ? (table.GetBoolValue(row, column) ? "Yes" : "No")
                    : table.GetCellText(row, column);
            }
        }
    }

    private int TablePartControlType()
    {
        return kind switch
        {
            UiaElementKind.TableHeaderRow => UiaIds.HeaderControl,
            UiaElementKind.TableHeader => UiaIds.HeaderItemControl,
            UiaElementKind.TableRow => UiaIds.DataItemControl,
            _ => UiaIds.CustomControl,
        };
    }

    private bool TablePartSelected()
    {
        var table = TableNode;
        int row = DisplayRow;
        if (kind == UiaElementKind.TableRow)
        {
            return table.IsRowSelected(row);
        }

        return table is ITabularCellGrid { HasCellBlocks: true } cells
            ? cells.IsCellSelected(row, column)
            : table.IsRowSelected(row);
    }

    private bool TablePartSupportsPattern(int patternId)
    {
        return kind switch
        {
            UiaElementKind.TableRow => patternId is UiaIds.SelectionItemPattern or UiaIds.ScrollItemPattern,
            UiaElementKind.TableCell => patternId is UiaIds.GridItemPattern or UiaIds.TableItemPattern
                or UiaIds.SelectionItemPattern or UiaIds.ScrollItemPattern,
            _ => false,
        };
    }

    private UiaVariant TablePartProperty(int propertyId)
    {
        switch (propertyId)
        {
            case UiaIds.LocalizedControlTypeProperty when kind == UiaElementKind.TableCell:
                return UiaVariant.From("cell");
            case UiaIds.GridItemRowProperty when kind == UiaElementKind.TableCell:
                return UiaVariant.From(index);
            case UiaIds.GridItemColumnProperty when kind == UiaElementKind.TableCell:
                return UiaVariant.From(ColumnPosition());
            case UiaIds.GridItemRowSpanProperty when kind == UiaElementKind.TableCell:
            case UiaIds.GridItemColumnSpanProperty when kind == UiaElementKind.TableCell:
                return UiaVariant.From(1);
            case UiaIds.SelectionItemIsSelectedProperty when kind is UiaElementKind.TableRow or UiaElementKind.TableCell:
                return UiaVariant.From(TablePartSelected());
            case UiaIds.PositionInSetProperty when kind == UiaElementKind.TableRow:
                return UiaVariant.From(index + 1);
            case UiaIds.SizeOfSetProperty when kind == UiaElementKind.TableRow:
                return UiaVariant.From(TabularNavigation.VisibleRowCount(TableNode));
            default:
                return UiaVariant.Empty;
        }
    }

    /// <summary>The column's 0-based position among the visible columns.</summary>
    private int ColumnPosition()
    {
        return TabularAccessibility.VisibleColumns(TableNode).IndexOf(column);
    }

    private void TablePartFocus()
    {
        var table = TableNode;
        if (!ReferenceEquals(FocusManager.FocusedElement, list!.node))
        {
            Input.AutomationFocus(list.node!);
        }

        if (kind is UiaElementKind.TableRow or UiaElementKind.TableCell)
        {
            Input.AutomationSelectTableCell(table, DisplayRow, kind == UiaElementKind.TableCell ? column : -1);
        }
    }

    // ── Table element (a node whose control is a DataTable/DataGrid) ──

    private List<object> TableSelection(ITabularDataNode table)
    {
        var result = new List<object>();
        foreach (int row in TabularAccessibility.SelectedRows(table))
        {
            int position = TabularNavigation.PositionOf(table, row, out bool hidden);
            if (position >= 0 && !hidden)
            {
                result.Add(TableRow(position));
            }
        }

        return result;
    }

    private UiaElement GridItem(int row, int columnPosition)
    {
        var table = (ITabularDataNode)node!;
        var columns = TabularAccessibility.VisibleColumns(table);
        if (row < 0 || row >= TabularNavigation.VisibleRowCount(table) || columnPosition < 0 || columnPosition >= columns.Count)
        {
            throw new UiaException("The cell is outside the grid.", UiaIds.E_INVALIDARG);
        }

        return TableCell(row, columns[columnPosition]);
    }

    // ── COM: IGridProvider (table) ────────────────────────────────────

    int IGridProvider.GetItem(int row, int columnIndex, out nint provider)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = UiaComObjects.Simple(GridItem(row, columnIndex));
            return UiaIds.S_OK;
        });
        provider = result;
        return hr;
    }

    int IGridProvider.GetRowCount(out int value)
    {
        int result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = TabularNavigation.VisibleRowCount((ITabularDataNode)node!);
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    int IGridProvider.GetColumnCount(out int value)
    {
        int result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = TabularAccessibility.ColumnCount((ITabularDataNode)node!);
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    // ── COM: ITableProvider (table) ───────────────────────────────────

    int ITableProvider.GetRowHeaders(out nint safeArray)
    {
        safeArray = UiaComObjects.ProviderArray([]);
        return UiaIds.S_OK;
    }

    int ITableProvider.GetColumnHeaders(out nint safeArray)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            var headers = new List<object>();
            foreach (int col in TabularAccessibility.VisibleColumns((ITabularDataNode)node!))
            {
                headers.Add(TableHeader(col));
            }

            result = UiaComObjects.ProviderArray(headers);
            return UiaIds.S_OK;
        });
        safeArray = result;
        return hr;
    }

    int ITableProvider.GetRowOrColumnMajor(out int value)
    {
        value = UiaIds.RowOrColumnMajor_RowMajor;
        return UiaIds.S_OK;
    }

    // ── COM: IGridItemProvider (cell) ─────────────────────────────────

    int IGridItemProvider.GetRow(out int value)
    {
        int result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = index;
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    int IGridItemProvider.GetColumn(out int value)
    {
        int result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = ColumnPosition();
            return UiaIds.S_OK;
        });
        value = result;
        return hr;
    }

    int IGridItemProvider.GetRowSpan(out int value)
    {
        value = 1;
        return UiaIds.S_OK;
    }

    int IGridItemProvider.GetColumnSpan(out int value)
    {
        value = 1;
        return UiaIds.S_OK;
    }

    int IGridItemProvider.GetContainingGrid(out nint provider)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = UiaComObjects.Simple(list);
            return UiaIds.S_OK;
        });
        provider = result;
        return hr;
    }

    // ── COM: ITableItemProvider (cell) ────────────────────────────────

    int ITableItemProvider.GetRowHeaderItems(out nint safeArray)
    {
        safeArray = UiaComObjects.ProviderArray([]);
        return UiaIds.S_OK;
    }

    int ITableItemProvider.GetColumnHeaderItems(out nint safeArray)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            ResolveOrThrow(out _);
            result = UiaComObjects.ProviderArray([list!.TableHeader(column)]);
            return UiaIds.S_OK;
        });
        safeArray = result;
        return hr;
    }
}

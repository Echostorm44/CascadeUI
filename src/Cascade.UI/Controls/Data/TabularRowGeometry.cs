namespace Cascade.UI;

/// <summary>What a point inside a <see cref="DataTable{T}"/> or <see cref="DataGrid{T}"/> lands on.</summary>
internal enum TabularHitKind
{
    /// <summary>Outside the table, or below the last row.</summary>
    None,

    /// <summary>The column header band.</summary>
    Header,

    /// <summary>The filter row under the header.</summary>
    FilterRow,

    /// <summary>The aggregate (summary) row, at the top or the bottom.</summary>
    AggregateRow,

    /// <summary>A group header; the index is the group.</summary>
    GroupHeader,

    /// <summary>A data row; the index is the display row.</summary>
    Row,

    /// <summary>The expanded detail panel under a row; the index is that row's display row.</summary>
    RowDetail,
}

/// <summary>A <see cref="TabularRowGeometry.HitTest"/> result: the kind and its row or group index.</summary>
internal readonly record struct TabularHit(TabularHitKind Kind, int Index)
{
    internal static readonly TabularHit Nothing = new(TabularHitKind.None, -1);
}

/// <summary>
/// Where the rows of a tabular control are, matching what <c>NodePainter.PaintTabularData</c>
/// draws: a header band (row height + 4), an optional filter row, an optional aggregate row at
/// the top, then the data area — rows, group headers and expanded detail panels laid out top to
/// bottom and shifted up by <see cref="ITabularDataNode.ScrollOffsetY"/> — and an optional
/// aggregate row pinned to the bottom. One implementation, used by click, hover and the context
/// menu, so they cannot disagree about which row is under the pointer.
/// </summary>
internal static class TabularRowGeometry
{
    /// <summary>Height of a group header, as painted.</summary>
    internal const float GroupHeaderHeight = 32f;

    /// <summary>Height of the filter row, as painted.</summary>
    internal const float FilterRowHeight = 28f;

    /// <summary>
    /// Width of the inline row-action strip at the right end of the rows (0 without
    /// <c>RowActions</c> or before it is measured), capped at half the table so the data columns
    /// always keep room. The painter and every column hit test resolve columns in
    /// <c>tableWidth - ActionStripWidth</c>.
    /// </summary>
    internal static float ActionStripWidth(ITabularDataNode tdn, float tableWidth)
    {
        if (tdn.RowActionStrip is not { StripWidth: > 0f } actions || tableWidth <= 0f)
        {
            return 0f;
        }

        return Math.Min(actions.StripWidth, MathF.Floor(tableWidth / 2f));
    }

    /// <summary>
    /// The row action under a point (relative to the table's top-left corner), and the control
    /// inside it the point lands on. Uses the painter's last layout of the row's actions and the
    /// current scroll, like every other row hit test.
    /// </summary>
    internal static bool TryHitRowAction(
        ITabularDataNode tdn, float relX, float relY, float tableHeight, out PaintedRowAction action, out Node target)
    {
        action = default;
        target = null!;
        if (tdn.RowActionStrip is not { } actions || actions.Painted.Count == 0)
        {
            return false;
        }

        var hit = HitTest(tdn, relY, tableHeight);
        if (hit.Kind != TabularHitKind.Row || !TryGetRowContentTop(tdn, hit.Index, out float contentTop))
        {
            return false;
        }

        float rowTop = DataTop(tdn) + contentTop - tdn.ScrollOffsetY;
        return actions.TryHit(hit.Index, relX, relY - rowTop, out action, out target);
    }

    /// <summary>Width of the row-detail expand chevron column that shifts the cells right.</summary>
    internal const float ExpandIndicatorWidth = 24f;

    /// <summary>
    /// The painted width of every column (0 for a hidden one): each column's resolved width, the
    /// sorted column widened by its sort arrow, then all scaled down to fit when together they
    /// exceed the table less the row-action strip and the column-chooser button. The painter, the
    /// pointer hit tests and the accessibility tree all use this, so they agree on where a cell is.
    /// </summary>
    internal static float[] ScaledColumnWidths(ITabularDataNode tdn, float tableWidth)
    {
        // The row-action strip sits after the last column; columns resolve in what is left.
        float availableWidth = tableWidth - ActionStripWidth(tdn, tableWidth);
        float[] widths = new float[tdn.ColumnCount];
        float total = 0f;
        const float sortIndicatorReserve = 5f + 3f; // arrowW + arrowGap
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            widths[c] = tdn.GetColumnWidth(c, availableWidth);
            if (widths[c] > 0f && tdn.IsSortable && tdn.SortColumnIndex == c)
            {
                widths[c] += sortIndicatorReserve;
            }
            total += widths[c];
        }

        const float chooserBtnSize = 24f;
        float chooserReserve = tdn.IsColumnChooserEnabled ? chooserBtnSize + 4f : 0f;
        float usable = availableWidth - chooserReserve;
        if (total > usable && total > 0f)
        {
            float scale = usable / total;
            for (int c = 0; c < tdn.ColumnCount; c++)
            {
                widths[c] = MathF.Floor(widths[c] * scale);
            }
        }

        return widths;
    }

    /// <summary>
    /// The left edge and width of column <paramref name="col"/> relative to the table's left edge
    /// (after the row-detail chevron column), from <see cref="ScaledColumnWidths"/>. False for a
    /// hidden or out-of-range column.
    /// </summary>
    internal static bool TryGetColumnSpan(ITabularDataNode tdn, float[] widths, int col, out float left, out float width)
    {
        left = tdn.HasRowDetail ? ExpandIndicatorWidth : 0f;
        width = 0f;
        if ((uint)col >= (uint)widths.Length || widths[col] <= 0f)
        {
            return false;
        }

        for (int c = 0; c < col; c++)
        {
            left += widths[c];
        }

        width = widths[col];
        return true;
    }

    /// <summary>
    /// The window-logical rectangle of the cell at display row <paramref name="row"/>, column
    /// <paramref name="col"/>, kept inside the visible data area like
    /// <see cref="TryGetVisibleRowBounds"/> — where a cell's dropdown, calendar or menu anchors.
    /// False when the table has not been painted, or the row or column is not shown.
    /// </summary>
    internal static bool TryGetVisibleCellBounds(ITabularDataNode tdn, int row, int col, out Rect bounds)
    {
        bounds = default;
        if (!TryGetVisibleRowBounds(tdn, row, out var rowBounds))
        {
            return false;
        }

        var widths = ScaledColumnWidths(tdn, tdn.AbsoluteBounds.Width);
        if (!TryGetColumnSpan(tdn, widths, col, out float left, out float width))
        {
            return false;
        }

        bounds = new Rect(rowBounds.X + left, rowBounds.Y, width, rowBounds.Height);
        return true;
    }

    /// <summary>Height of the column header band.</summary>
    internal static float HeaderHeight(ITabularDataNode tdn)
    {
        return tdn.GetRowHeight() + 4f;
    }

    /// <summary>Top of the scrolling data area, relative to the table's top edge.</summary>
    internal static float DataTop(ITabularDataNode tdn)
    {
        float top = HeaderHeight(tdn);
        if (tdn.HasFilterRow)
        {
            top += FilterRowHeight;
        }

        if (tdn.HasAggregateRow && tdn.AggregatePos == AggregatePosition.Top)
        {
            top += tdn.GetAggregateRowHeight();
        }

        return top;
    }

    /// <summary>Bottom of the scrolling data area, relative to the table's top edge.</summary>
    internal static float DataBottom(ITabularDataNode tdn, float tableHeight)
    {
        if (tdn.HasAggregateRow && tdn.AggregatePos == AggregatePosition.Bottom)
        {
            return tableHeight - tdn.GetAggregateRowHeight();
        }

        return tableHeight;
    }

    /// <summary>
    /// What lies at <paramref name="relY"/> (relative to the table's top edge), with the current
    /// vertical scroll applied to the data area.
    /// </summary>
    internal static TabularHit HitTest(ITabularDataNode tdn, float relY, float tableHeight)
    {
        if (relY < 0f || relY >= tableHeight)
        {
            return TabularHit.Nothing;
        }

        float y = HeaderHeight(tdn);
        if (relY < y)
        {
            return new TabularHit(TabularHitKind.Header, -1);
        }

        if (tdn.HasFilterRow)
        {
            if (relY < y + FilterRowHeight)
            {
                return new TabularHit(TabularHitKind.FilterRow, -1);
            }

            y += FilterRowHeight;
        }

        if (tdn.HasAggregateRow && tdn.AggregatePos == AggregatePosition.Top)
        {
            float aggregateHeight = tdn.GetAggregateRowHeight();
            if (relY < y + aggregateHeight)
            {
                return new TabularHit(TabularHitKind.AggregateRow, -1);
            }

            y += aggregateHeight;
        }

        if (relY >= DataBottom(tdn, tableHeight))
        {
            return tdn.HasAggregateRow
                ? new TabularHit(TabularHitKind.AggregateRow, -1)
                : TabularHit.Nothing;
        }

        return HitTestContent(tdn, relY - y + tdn.ScrollOffsetY);
    }

    /// <summary>
    /// What lies at <paramref name="contentY"/> in the data content (0 = the first row's top edge,
    /// before scrolling).
    /// </summary>
    internal static TabularHit HitTestContent(ITabularDataNode tdn, float contentY)
    {
        float rowHeight = tdn.GetRowHeight();
        if (contentY < 0f || rowHeight <= 0f)
        {
            return TabularHit.Nothing;
        }

        if (tdn.IsGrouped)
        {
            return HitTestGrouped(tdn, contentY, rowHeight);
        }

        if (!tdn.HasRowDetail)
        {
            int row = (int)(contentY / rowHeight);
            return row < tdn.RowCount ? new TabularHit(TabularHitKind.Row, row) : TabularHit.Nothing;
        }

        float y = 0f;
        for (int r = 0; r < tdn.RowCount; r++)
        {
            var hit = HitTestRowAndDetail(tdn, r, contentY, rowHeight, ref y);
            if (hit.Kind != TabularHitKind.None)
            {
                return hit;
            }
        }

        return TabularHit.Nothing;
    }

    private static TabularHit HitTestGrouped(ITabularDataNode tdn, float contentY, float rowHeight)
    {
        float y = 0f;
        for (int g = 0; g < tdn.GroupCount; g++)
        {
            if (contentY < y + GroupHeaderHeight)
            {
                return new TabularHit(TabularHitKind.GroupHeader, g);
            }

            y += GroupHeaderHeight;
            if (tdn.IsGroupCollapsed(g))
            {
                continue;
            }

            int count = tdn.GetGroupRowCount(g);
            for (int i = 0; i < count; i++)
            {
                var hit = HitTestRowAndDetail(tdn, tdn.GetGroupDataRowIndex(g, i), contentY, rowHeight, ref y);
                if (hit.Kind != TabularHitKind.None)
                {
                    return hit;
                }
            }
        }

        return TabularHit.Nothing;
    }

    /// <summary>Tests one row (and its detail panel) starting at <paramref name="y"/>, advancing it past them.</summary>
    private static TabularHit HitTestRowAndDetail(ITabularDataNode tdn, int row, float contentY, float rowHeight, ref float y)
    {
        if (contentY < y + rowHeight)
        {
            return new TabularHit(TabularHitKind.Row, row);
        }

        y += rowHeight;
        if (tdn.HasRowDetail && tdn.IsRowExpanded(row))
        {
            float detailHeight = tdn.GetRowDetailHeight(row);
            if (contentY < y + detailHeight)
            {
                return new TabularHit(TabularHitKind.RowDetail, row);
            }

            y += detailHeight;
        }

        return TabularHit.Nothing;
    }

    /// <summary>
    /// The top of display row <paramref name="row"/> in the data content (before scrolling).
    /// False when the row is out of range or hidden in a collapsed group.
    /// </summary>
    internal static bool TryGetRowContentTop(ITabularDataNode tdn, int row, out float top)
    {
        top = 0f;
        if (row < 0 || row >= tdn.RowCount)
        {
            return false;
        }

        float rowHeight = tdn.GetRowHeight();
        if (tdn.IsGrouped)
        {
            return TryGetGroupedRowTop(tdn, row, rowHeight, out top);
        }

        top = row * rowHeight;
        if (tdn.HasRowDetail)
        {
            for (int r = 0; r < row; r++)
            {
                if (tdn.IsRowExpanded(r))
                {
                    top += tdn.GetRowDetailHeight(r);
                }
            }
        }

        return true;
    }

    private static bool TryGetGroupedRowTop(ITabularDataNode tdn, int row, float rowHeight, out float top)
    {
        float y = 0f;
        for (int g = 0; g < tdn.GroupCount; g++)
        {
            y += GroupHeaderHeight;
            bool collapsed = tdn.IsGroupCollapsed(g);
            int count = tdn.GetGroupRowCount(g);
            for (int i = 0; i < count; i++)
            {
                int r = tdn.GetGroupDataRowIndex(g, i);
                if (r == row)
                {
                    top = y;
                    return !collapsed;
                }

                if (collapsed)
                {
                    continue;
                }

                y += rowHeight;
                if (tdn.HasRowDetail && tdn.IsRowExpanded(r))
                {
                    y += tdn.GetRowDetailHeight(r);
                }
            }
        }

        top = 0f;
        return false;
    }

    /// <summary>
    /// Display row <paramref name="row"/>'s on-screen rectangle (window-logical, from the
    /// painter-stamped <see cref="ITabularDataNode.AbsoluteBounds"/>), kept inside the visible
    /// data area so an anchor never sits over the header or outside the table. False when the
    /// table has not been painted, or the row is out of range or in a collapsed group.
    /// </summary>
    internal static bool TryGetVisibleRowBounds(ITabularDataNode tdn, int row, out Rect bounds)
    {
        bounds = default;
        var table = tdn.AbsoluteBounds;
        if (table.Width <= 0f || table.Height <= 0f || !TryGetRowContentTop(tdn, row, out float contentTop))
        {
            return false;
        }

        float rowHeight = tdn.GetRowHeight();
        float areaTop = table.Y + DataTop(tdn);
        float areaBottom = table.Y + DataBottom(tdn, table.Height);
        float top = areaTop + contentTop - tdn.ScrollOffsetY;
        float maxTop = Math.Max(areaTop, areaBottom - rowHeight);
        top = Math.Clamp(top, areaTop, maxTop);
        bounds = new Rect(table.X, top, table.Width, rowHeight);
        return true;
    }
}

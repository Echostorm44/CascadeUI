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

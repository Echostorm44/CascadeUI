namespace Cascade.UI;

/// <summary>
/// Keyboard movement through the rows of a <see cref="DataTable{T}"/> or <see cref="DataGrid{T}"/>
/// in the order they appear on screen. Selection indices are display rows (positions in the sorted
/// and filtered order), but a grouped grid shows them group by group and hides the rows of a
/// collapsed group, so "the next row" is the next row in that visual sequence — not display row
/// + 1. Group headers are not selectable (a click toggles them) and are skipped, as are the rows of
/// collapsed groups. Ungrouped, the visual order is the display order. One implementation, used
/// by both controls, so arrow keys, Home/End and paging cannot disagree with what is painted.
/// </summary>
internal static class TabularNavigation
{
    /// <summary>Number of rows on screen (or scrolled to): every row, minus those in collapsed groups.</summary>
    internal static int VisibleRowCount(ITabularDataNode tdn)
    {
        if (!tdn.IsGrouped)
        {
            return tdn.RowCount;
        }

        int count = 0;
        for (int g = 0; g < tdn.GroupCount; g++)
        {
            if (!tdn.IsGroupCollapsed(g))
            {
                count += tdn.GetGroupRowCount(g);
            }
        }

        return count;
    }

    /// <summary>The display row at visual position <paramref name="position"/>, or -1 when out of range.</summary>
    internal static int RowAt(ITabularDataNode tdn, int position)
    {
        if (position < 0)
        {
            return -1;
        }

        if (!tdn.IsGrouped)
        {
            return position < tdn.RowCount ? position : -1;
        }

        int remaining = position;
        for (int g = 0; g < tdn.GroupCount; g++)
        {
            if (tdn.IsGroupCollapsed(g))
            {
                continue;
            }

            int count = tdn.GetGroupRowCount(g);
            if (remaining < count)
            {
                return tdn.GetGroupDataRowIndex(g, remaining);
            }

            remaining -= count;
        }

        return -1;
    }

    /// <summary>
    /// The visual position of display row <paramref name="row"/>, or -1 when it is not a row of
    /// this control. A row inside a collapsed group has no position of its own:
    /// <paramref name="hidden"/> is then true and the result is the position its group's rows
    /// would start at (the number of visible rows before the group).
    /// </summary>
    internal static int PositionOf(ITabularDataNode tdn, int row, out bool hidden)
    {
        hidden = false;
        if (row < 0 || row >= tdn.RowCount)
        {
            return -1;
        }

        if (!tdn.IsGrouped)
        {
            return row;
        }

        int before = 0;
        for (int g = 0; g < tdn.GroupCount; g++)
        {
            bool collapsed = tdn.IsGroupCollapsed(g);
            int count = tdn.GetGroupRowCount(g);
            for (int i = 0; i < count; i++)
            {
                if (tdn.GetGroupDataRowIndex(g, i) != row)
                {
                    continue;
                }

                hidden = collapsed;
                return collapsed ? before : before + i;
            }

            if (!collapsed)
            {
                before += count;
            }
        }

        return -1;
    }

    /// <summary>The first row on screen, or -1 when none is (no rows, or every group collapsed).</summary>
    internal static int First(ITabularDataNode tdn)
    {
        return RowAt(tdn, 0);
    }

    /// <summary>The last row on screen, or -1 when none is.</summary>
    internal static int Last(ITabularDataNode tdn)
    {
        return RowAt(tdn, VisibleRowCount(tdn) - 1);
    }

    /// <summary>
    /// The row <paramref name="delta"/> rows away from <paramref name="from"/> in visual order,
    /// clamped to the first and last visible rows. With nothing selected (or the selection gone),
    /// the first visible row. From a row hidden in a collapsed group, Down goes to the first
    /// visible row after the group and Up to the last one before it. -1 when no row is visible.
    /// </summary>
    internal static int Step(ITabularDataNode tdn, int from, int delta)
    {
        int count = VisibleRowCount(tdn);
        if (count == 0)
        {
            return -1;
        }

        int position = PositionOf(tdn, from, out bool hidden);
        if (position < 0)
        {
            return RowAt(tdn, 0);
        }

        int target = hidden && delta > 0 ? position + delta - 1 : position + delta;
        return RowAt(tdn, Math.Clamp(target, 0, count - 1));
    }

    /// <summary>
    /// Rows moved by Page Up / Page Down: one viewport of rows, less one so the row at the edge
    /// stays in view as context. Measured from the viewport the painter recorded, not from
    /// <see cref="ITabularDataNode.VisibleRowCount"/>, which also counts the off-screen rows painted
    /// as a virtualization buffer.
    /// </summary>
    internal static int PageSize(ITabularDataNode tdn)
    {
        float rowHeight = tdn.GetRowHeight();
        if (rowHeight <= 0f || tdn.ViewportHeight <= 0f)
        {
            return 1;
        }

        return Math.Max(1, (int)(tdn.ViewportHeight / rowHeight) - 1);
    }

    /// <summary>
    /// Appends the rows of <paramref name="selected"/> (or <paramref name="single"/> when the set
    /// is empty and it is a row) to <paramref name="into"/> in the order the grid shows them: group
    /// by group when grouped — rows of a collapsed group included, where the group sits — else
    /// display order. What copy, cut and the grid's selection snapshot walk.
    /// </summary>
    internal static void AddSelectedRowsInScreenOrder(ITabularDataNode tdn, HashSet<int> selected, int single, List<int> into)
    {
        int rowCount = tdn.RowCount;
        if (selected.Count == 0)
        {
            if ((uint)single < (uint)rowCount)
            {
                into.Add(single);
            }

            return;
        }

        if (!tdn.IsGrouped)
        {
            if (selected.Count * 4 < rowCount)
            {
                foreach (int row in selected)
                {
                    if ((uint)row < (uint)rowCount)
                    {
                        into.Add(row);
                    }
                }

                into.Sort();
                return;
            }

            for (int row = 0; row < rowCount; row++)
            {
                if (selected.Contains(row))
                {
                    into.Add(row);
                }
            }

            return;
        }

        for (int g = 0; g < tdn.GroupCount; g++)
        {
            int count = tdn.GetGroupRowCount(g);
            for (int i = 0; i < count; i++)
            {
                int row = tdn.GetGroupDataRowIndex(g, i);
                if (selected.Contains(row))
                {
                    into.Add(row);
                }
            }
        }
    }

    /// <summary>
    /// The rows between <paramref name="anchor"/> and <paramref name="row"/> inclusive, in visual
    /// order — what a Shift+click selects. Rows in collapsed groups between them are not included:
    /// they are not on screen. Falls back to the display-index range when either end is not
    /// visible.
    /// </summary>
    internal static void AddVisualRange(ITabularDataNode tdn, int anchor, int row, HashSet<int> into)
    {
        int a = PositionOf(tdn, anchor, out bool anchorHidden);
        int b = PositionOf(tdn, row, out bool rowHidden);
        if (a < 0 || b < 0 || anchorHidden || rowHidden)
        {
            int lo = Math.Min(anchor, row);
            int hi = Math.Max(anchor, row);
            for (int i = lo; i <= hi; i++)
            {
                into.Add(i);
            }

            return;
        }

        int from = Math.Min(a, b);
        int to = Math.Max(a, b);
        for (int p = from; p <= to; p++)
        {
            into.Add(RowAt(tdn, p));
        }
    }
}

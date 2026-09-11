namespace Cascade.UI;

/// <summary>
/// How one column wants to be sized. Produced by <see cref="ITabularDataNode.GetColumnSizing"/>
/// so <see cref="TabularColumnWidths"/> can resolve widths for <see cref="DataTable{T}"/> and
/// <see cref="DataGrid{T}"/> through one implementation. A readonly struct so the per-column
/// walk during layout allocates nothing.
/// </summary>
/// <param name="Fixed">Explicit width from <c>Width(float)</c>, or null for a fill column.</param>
/// <param name="Override">Width the user produced by dragging the column border, if any. Wins
/// over <paramref name="Fixed"/> — a drag is a deliberate act by the person looking at the
/// data.</param>
/// <param name="Min">Lower clamp from <c>MinWidth</c>.</param>
/// <param name="Max">Upper clamp from <c>MaxWidth</c>.</param>
/// <param name="Visible">False for a hidden column, which resolves to zero width.</param>
/// <param name="Auto">
/// True for <see cref="DataColumnWidth.Auto"/> — sized to its content. The painter measures those
/// columns and publishes the results through <see cref="ITabularDataNode.AutoColumnWidths"/>,
/// which arrives back here as <paramref name="Measured"/>. An Auto column with no measurement yet
/// falls back to fill.
/// </param>
/// <param name="Measured">Content width for an Auto column, once the painter has measured it.</param>
internal readonly record struct ColumnSizing(
    float? Fixed,
    float? Override,
    float? Min,
    float? Max,
    bool Visible,
    bool Auto = false,
    float? Measured = null);

/// <summary>
/// What the person using the table has done to it: what they selected, how they sorted it, and
/// where they scrolled to. Captured from the outgoing node and restored onto the incoming one
/// during reconcile so an unrelated <c>Invalidate()</c> elsewhere in the view does not throw it
/// away — see <see cref="ITabularDataNode.CaptureInteractionState"/>.
/// </summary>
internal readonly record struct TabularInteractionState(
    int SortColumnIndex,
    SortDirection SortDirection,
    int SelectedRowIndex,
    int AnchorRow,
    int[]? SelectedRows,
    float ScrollOffsetY,
    float ScrollOffsetX);

/// <summary>
/// Resolves tabular column widths. Both tabular controls call this, so the two cannot drift:
/// before this existed <see cref="DataGrid{T}"/> distributed fill space while
/// <see cref="DataTable{T}"/> silently gave every unsized column
/// <c>availableWidth / columnCount</c>, and neither honoured <c>MaxWidth</c>.
/// </summary>
internal static class TabularColumnWidths
{
    /// <summary>
    /// Floor applied when a resizable column has no explicit <c>MinWidth</c>. Without it a drag
    /// can collapse a column to nothing, and there is no border left to grab to get it back.
    /// </summary>
    internal const float MinimumResizeWidth = 40f;

    /// <summary>
    /// Width for one column: a drag override wins, then an explicit width, otherwise the space
    /// left after the sized columns split evenly between the fill columns. The result is always
    /// clamped to the column's own <c>MinWidth</c>/<c>MaxWidth</c>.
    /// </summary>
    /// <param name="node">The table or grid being laid out.</param>
    /// <param name="col">Column index.</param>
    /// <param name="availableWidth">Width available to all columns together.</param>
    public static float Resolve(ITabularDataNode node, int col, float availableWidth)
    {
        ColumnSizing sizing = node.GetColumnSizing(col);
        if (!sizing.Visible)
        {
            return 0f;
        }

        if (sizing.Override is { } dragged)
        {
            return Clamp(dragged, sizing);
        }

        if (sizing.Fixed is { } fixedWidth)
        {
            return Clamp(fixedWidth, sizing);
        }

        if (sizing is { Auto: true, Measured: { } measured })
        {
            return Clamp(measured, sizing);
        }

        // Fill column: take an equal share of whatever the sized columns left behind.
        float sizedTotal = 0f;
        int fillCount = 0;
        int columnCount = node.ColumnCount;

        for (int c = 0; c < columnCount; c++)
        {
            ColumnSizing other = node.GetColumnSizing(c);
            if (!other.Visible)
            {
                continue;
            }

            if (other.Override is { } otherDragged)
            {
                sizedTotal += Clamp(otherDragged, other);
            }
            else if (other.Fixed is { } otherFixed)
            {
                sizedTotal += Clamp(otherFixed, other);
            }
            else if (other is { Auto: true, Measured: { } otherMeasured })
            {
                sizedTotal += Clamp(otherMeasured, other);
            }
            else
            {
                fillCount++;
            }
        }

        float remaining = availableWidth - sizedTotal;
        if (float.IsNaN(remaining) || remaining < 0f)
        {
            // The sized columns already overflow. Fill columns collapse rather than going
            // negative and dragging the whole row layout with them.
            remaining = 0f;
        }

        float share = fillCount > 0 ? remaining / fillCount : 0f;
        return Clamp(share, sizing);
    }

    /// <summary>
    /// Applies a column's min/max. A max below the min would make <see cref="Math.Clamp(float,
    /// float, float)"/> throw, so the min wins — a column can always show at least what it was
    /// told it must.
    /// </summary>
    internal static float Clamp(float width, ColumnSizing sizing)
    {
        if (float.IsNaN(width))
        {
            width = 0f;
        }

        float min = sizing.Min ?? 0f;
        float max = sizing.Max ?? float.MaxValue;
        if (max < min)
        {
            max = min;
        }

        return Math.Clamp(width, min, max);
    }
}

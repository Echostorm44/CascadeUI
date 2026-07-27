namespace Cascade.UI;

/// <summary>
/// Resolved column information for a <see cref="Grid"/> layout.
/// Produced by <see cref="GridColumnDef.Resolve"/>.
/// </summary>
internal readonly struct ResolvedGridColumns
{
    internal readonly float[] Widths;
    internal readonly float ColumnSpacing;

    internal ResolvedGridColumns(float[] widths, float columnSpacing)
    {
        Widths = widths;
        ColumnSpacing = columnSpacing;
    }

    internal int Count => Widths.Length;
}

/// <summary>
/// Discriminator for <see cref="GridColumn"/> subtypes.
/// </summary>
internal enum GridColumnKind
{
    Fixed,
    Flex,
    Auto
}

/// <summary>
/// Layout algorithm for <see cref="Grid"/>. Handles adaptive, fixed-count,
/// and explicit column definitions. Assigns children to cells in row-major
/// order and computes row heights from cell measurements.
/// </summary>
internal static class GridLayout
{
    /// <summary>
    /// Measures and positions children in a grid layout. Returns the content size.
    /// </summary>
    internal static Size Measure(
        IReadOnlyList<Node> children,
        GridColumnDef columns,
        float rowSpacing,
        LayoutConstraints constraints)
    {
        if (children.Count == 0)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        float availableWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? 0
            : constraints.MaxWidth;

        // Resolve column widths from the column definition
        var resolved = columns.Resolve(availableWidth);
        int colCount = resolved.Count;

        if (colCount == 0)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        int rowCount = (children.Count + colCount - 1) / colCount;

        // Phase 1: Measure all cells
        var cellSizes = new Size[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            int col = i % colCount;
            float colWidth = resolved.Widths[col];

            // Account for column spacing in available width for each cell
            var cellConstraints = new LayoutConstraints(
                colWidth, colWidth, 0, constraints.MaxHeight);

            var allocSize = LayoutSolver.MeasureChild(children[i], cellConstraints);
            cellSizes[i] = allocSize;
        }

        // Phase 2: Compute row heights (max cell height per row)
        var rowHeights = new float[rowCount];
        for (int i = 0; i < children.Count; i++)
        {
            int row = i / colCount;
            rowHeights[row] = Math.Max(rowHeights[row], cellSizes[i].Height);
        }

        // Phase 3: Position children
        float yPos = 0;
        for (int row = 0; row < rowCount; row++)
        {
            float xPos = 0;
            for (int col = 0; col < colCount; col++)
            {
                int index = row * colCount + col;
                if (index >= children.Count)
                {
                    break;
                }

                LayoutSolver.PositionChild(children[index], xPos, yPos);
                xPos += resolved.Widths[col] + resolved.ColumnSpacing;
            }
            yPos += rowHeights[row] + rowSpacing;
        }

        // Phase 4: Compute grid size
        float totalWidth = 0;
        for (int c = 0; c < colCount; c++)
        {
            totalWidth += resolved.Widths[c];
        }
        totalWidth += resolved.ColumnSpacing * Math.Max(0, colCount - 1);

        float totalHeight = 0;
        for (int r = 0; r < rowCount; r++)
        {
            totalHeight += rowHeights[r];
        }
        totalHeight += rowSpacing * Math.Max(0, rowCount - 1);

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(totalHeight));
    }
}

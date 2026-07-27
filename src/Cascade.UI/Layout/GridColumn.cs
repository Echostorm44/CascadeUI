namespace Cascade.UI;

/// <summary>
/// Defines a single column in a <see cref="Grid"/> layout.
/// Created via static factory methods.
/// </summary>
public abstract class GridColumn
{
    private GridColumn()
    {
    }

    /// <summary>
    /// Discriminator for the column type, used by the layout solver.
    /// </summary>
    internal abstract GridColumnKind ColumnKind { get; }

    /// <summary>
    /// The fixed width value (meaningful only for Fixed columns).
    /// </summary>
    internal virtual float FixedWidthValue => 0;

    /// <summary>
    /// The flex factor value (meaningful only for Flex columns).
    /// </summary>
    internal virtual float FlexFactorValue => 0;

    /// <summary>
    /// A column with a fixed pixel width.
    /// </summary>
    public static GridColumn Fixed(float width)
    {
        return new FixedColumn(width);
    }

    /// <summary>
    /// A flex column that takes remaining space proportionally.
    /// </summary>
    public static GridColumn Flex(float factor = 1)
    {
        return new FlexColumn(factor);
    }

    /// <summary>
    /// An auto-width column that sizes to the widest item.
    /// </summary>
    public static GridColumn Auto()
    {
        return new AutoColumn();
    }

    private sealed class FixedColumn(float width) : GridColumn
    {
        public float Width { get; } = width;
        internal override GridColumnKind ColumnKind => GridColumnKind.Fixed;
        internal override float FixedWidthValue => Width;
    }

    private sealed class FlexColumn(float factor) : GridColumn
    {
        public float Factor { get; } = factor;
        internal override GridColumnKind ColumnKind => GridColumnKind.Flex;
        internal override float FlexFactorValue => Factor;
    }

    private sealed class AutoColumn : GridColumn
    {
        internal override GridColumnKind ColumnKind => GridColumnKind.Auto;
    }
}

/// <summary>
/// A grid column definition describing the overall column strategy for a
/// <see cref="Grid"/>. Created via <see cref="GridColumns"/> factory methods.
/// </summary>
public abstract class GridColumnDef
{
    internal GridColumnDef()
    {
    }

    /// <summary>
    /// Resolves the column widths for the given available width.
    /// Called by the grid layout algorithm.
    /// </summary>
    internal abstract ResolvedGridColumns Resolve(float availableWidth);
}

/// <summary>
/// Factory methods for creating <see cref="GridColumnDef"/> instances.
/// </summary>
public static class GridColumns
{
    /// <summary>
    /// Responsive grid: as many columns as fit, each at least
    /// <paramref name="minWidth"/> wide.
    /// </summary>
    public static GridColumnDef Adaptive(float minWidth, float spacing = 0)
    {
        return new AdaptiveDef(minWidth, spacing);
    }

    /// <summary>
    /// Fixed number of equal-width columns.
    /// </summary>
    public static GridColumnDef Fixed(int count, float spacing = 0)
    {
        return new FixedCountDef(count, spacing);
    }

    /// <summary>
    /// Explicit per-column definitions.
    /// </summary>
    public static GridColumnDef Define(params GridColumn[] columns)
    {
        return new ExplicitDef(columns);
    }

    private sealed class AdaptiveDef(float minWidth, float spacing) : GridColumnDef
    {
        public float MinWidth { get; } = minWidth;
        public float Spacing { get; } = spacing;

        internal override ResolvedGridColumns Resolve(float availableWidth)
        {
            if (availableWidth <= 0)
            {
                return new ResolvedGridColumns([MinWidth], Spacing);
            }

            int count = Math.Max(1,
                (int)((availableWidth + Spacing) / (MinWidth + Spacing)));
            float totalSpacing = Spacing * Math.Max(0, count - 1);
            float colWidth = (availableWidth - totalSpacing) / count;
            var widths = new float[count];
            Array.Fill(widths, colWidth);
            return new ResolvedGridColumns(widths, Spacing);
        }
    }

    private sealed class FixedCountDef(int count, float spacing) : GridColumnDef
    {
        public int Count { get; } = count;
        public float Spacing { get; } = spacing;

        internal override ResolvedGridColumns Resolve(float availableWidth)
        {
            int colCount = Math.Max(1, Count);
            float totalSpacing = Spacing * Math.Max(0, colCount - 1);
            float colWidth = Math.Max(0, (availableWidth - totalSpacing) / colCount);
            var widths = new float[colCount];
            Array.Fill(widths, colWidth);
            return new ResolvedGridColumns(widths, Spacing);
        }
    }

    private sealed class ExplicitDef(GridColumn[] columns) : GridColumnDef
    {
        public GridColumn[] Columns { get; } = columns;

        internal override ResolvedGridColumns Resolve(float availableWidth)
        {
            int colCount = Columns.Length;
            if (colCount == 0)
            {
                return new ResolvedGridColumns([], 0);
            }

            var widths = new float[colCount];
            float fixedTotal = 0;
            float totalFlex = 0;

            // Phase 1: Resolve fixed columns
            for (int i = 0; i < colCount; i++)
            {
                var col = Columns[i];
                if (col.ColumnKind == GridColumnKind.Fixed)
                {
                    widths[i] = col.FixedWidthValue;
                    fixedTotal += col.FixedWidthValue;
                }
                else if (col.ColumnKind == GridColumnKind.Flex)
                {
                    totalFlex += col.FlexFactorValue;
                }
            }

            // Phase 2: Distribute remaining space to flex columns
            float remaining = Math.Max(0, availableWidth - fixedTotal);
            for (int i = 0; i < colCount; i++)
            {
                var col = Columns[i];
                if (col.ColumnKind == GridColumnKind.Flex && totalFlex > 0)
                {
                    widths[i] = remaining * (col.FlexFactorValue / totalFlex);
                }
                else if (col.ColumnKind == GridColumnKind.Auto)
                {
                    // Auto columns default to equal share of remaining space
                    widths[i] = totalFlex > 0 ? 0 : remaining / colCount;
                }
            }

            return new ResolvedGridColumns(widths, 0);
        }
    }
}

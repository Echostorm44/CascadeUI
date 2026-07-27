namespace Cascade.UI;

/// <summary>A single cell in a heat map chart.</summary>
/// <param name="Row">The row category for this cell.</param>
/// <param name="Column">The column category for this cell.</param>
/// <param name="Value">The numeric value that determines the cell color.</param>
public readonly record struct HeatMapCell(object Row, object Column, double Value);

/// <summary>Color scale strategy for mapping values to colors in heat maps.</summary>
public enum HeatMapColorScale
{
    /// <summary>Gradient from low to high color.</summary>
    Sequential,

    /// <summary>Gradient with a midpoint (e.g. negative → neutral → positive).</summary>
    Diverging,

    /// <summary>Discrete color steps at defined thresholds.</summary>
    Stepped,

    /// <summary>Custom color mapping function.</summary>
    Custom
}

/// <summary>
/// A heat map chart that renders a grid of cells colored by their numeric values.
/// Rows and columns are defined by the data; color is derived from the value
/// using a configurable color scale.
/// </summary>
public sealed class HeatMapChart : ChartBase<HeatMapChart>
{
    internal List<HeatMapCell> cellsList = [];
    internal HeatMapColorScale colorScale = HeatMapColorScale.Sequential;
    internal ColorValue? lowColor;
    internal ColorValue? highColor;
    internal ColorValue? midColor;
    internal ColorValue? nullColor;
    internal bool showValueLabels;
    internal AxisFormat? valueLabelFormat;
    internal float cellGap = 2f;
    internal float cellRadius = 2f;
    internal bool showColorLegend = true;
    internal Func<double, ColorValue>? customColorMapper;

    // Paint-time layout cache. Built on first paint, reused until cellsList
    // identity changes. Transferred across re-renders by the reconciler
    // (see HeatMapChart block in Reconciler.TransferInteractiveState).
    // Framework stance: no INPC. If a caller mutates cellsList contents in
    // place they must call InvalidateLayoutCache() — consistent with the
    // DataGrid/DataTable cell text cache and explicit-reactivity design.
    internal HeatMapLayoutCache? layoutCache;
    internal object? layoutCacheKey;

    /// <summary>Creates a heat map chart from cell data.</summary>
    /// <param name="data">The cells to render, each specifying a row, column, and value.</param>
    public HeatMapChart(IEnumerable<HeatMapCell> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        cellsList.AddRange(data);
        layoutCacheKey = cellsList;
    }

    /// <summary>
    /// Invalidates the cached row/column/grid layout. Call this if the
    /// underlying cell data is mutated in place after construction. Not
    /// required when a new HeatMapChart is created with different data —
    /// the reconciler detects identity mismatch automatically.
    /// </summary>
    public void InvalidateLayoutCache()
    {
        layoutCache = null;
    }

    /// <summary>The cells in this chart.</summary>
    internal IReadOnlyList<HeatMapCell> Cells => cellsList;

    /// <summary>Sets the color scale strategy for mapping values to colors.</summary>
    /// <param name="scale">The color scale mode.</param>
    public HeatMapChart ColorScale(HeatMapColorScale scale)
    {
        colorScale = scale;
        return this;
    }

    /// <summary>Sets the low and high colors for a sequential color scale.</summary>
    /// <param name="low">Color representing the lowest value.</param>
    /// <param name="high">Color representing the highest value.</param>
    public HeatMapChart Colors(ColorValue low, ColorValue high)
    {
        lowColor = low;
        highColor = high;
        return this;
    }

    /// <summary>Sets the low, mid, and high colors for a diverging color scale.</summary>
    /// <param name="low">Color representing the lowest value.</param>
    /// <param name="mid">Color representing the midpoint value.</param>
    /// <param name="high">Color representing the highest value.</param>
    public HeatMapChart Colors(ColorValue low, ColorValue mid, ColorValue high)
    {
        lowColor = low;
        midColor = mid;
        highColor = high;
        return this;
    }

    /// <summary>Sets the color used for cells with missing or null data.</summary>
    /// <param name="color">The color to render for absent cells.</param>
    public HeatMapChart NullColor(ColorValue color)
    {
        nullColor = color;
        return this;
    }

    /// <summary>Configures whether value labels are displayed inside each cell.</summary>
    /// <param name="show">True to show value labels on cells.</param>
    /// <param name="format">Optional format for the displayed values.</param>
    public HeatMapChart ValueLabels(bool show, AxisFormat? format = null)
    {
        showValueLabels = show;
        valueLabelFormat = format;
        return this;
    }

    /// <summary>Sets the gap in logical pixels between cells.</summary>
    /// <param name="gap">The gap size between cells.</param>
    public HeatMapChart CellGap(float gap)
    {
        cellGap = gap;
        return this;
    }

    /// <summary>Sets the corner radius of each cell.</summary>
    /// <param name="radius">The corner radius in logical pixels.</param>
    public HeatMapChart CellRadius(float radius)
    {
        cellRadius = radius;
        return this;
    }

    /// <summary>Shows or hides the color bar legend indicating the value-to-color mapping.</summary>
    /// <param name="show">True to display the color legend.</param>
    public HeatMapChart ColorLegend(bool show)
    {
        showColorLegend = show;
        return this;
    }

    /// <summary>Sets a custom function that maps a cell value to a color.</summary>
    /// <param name="mapper">A function that receives a value and returns the cell color.</param>
    public HeatMapChart CustomColorMap(Func<double, ColorValue> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        customColorMapper = mapper;
        colorScale = HeatMapColorScale.Custom;
        return this;
    }
}

/// <summary>
/// Cached layout derived from <see cref="HeatMapChart.cellsList"/>.
/// Built once per unique cell-data identity and reused across paints.
/// </summary>
internal sealed class HeatMapLayoutCache
{
    // Row / column labels in encounter order.
    public string[] RowLabels = [];
    public string[] ColLabels = [];

    // Flat value grid indexed by r * cols + c. -1 sentinel unused;
    // HasValue tracked by parallel bitmap.
    public double[] Values = [];
    public bool[] HasValue = [];

    // Optional per-cell formatted "F0" text for ValueLabels mode.
    // Lazy: null until a paint needs it, then sized rows*cols.
    public string?[]? ValueLabelText;

    public int Rows;
    public int Cols;
    public double MinVal;
    public double MaxVal;
}


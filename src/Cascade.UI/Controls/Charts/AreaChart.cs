namespace Cascade.UI;

/// <summary>
/// A filled area chart that renders data series as lines with the region beneath filled.
/// Supports stacking, fill opacity control, smooth interpolation, and missing value handling.
/// </summary>
public sealed class AreaChart : CartesianChart<AreaChart>
{
    internal List<ChartSeries> seriesList = [];
    internal bool stackedEnabled;
    internal float fillOpacityValue = 0.25f;
    internal bool smoothEnabled;
    internal PointDisplay pointDisplay = PointDisplay.Auto;
    internal MissingValueMode missingValueMode = MissingValueMode.Gap;

    // Paint-time caches. Transferred across re-renders by the reconciler
    // when seriesList identity is unchanged (matches HeatMap/DataGrid
    // cache pattern). Zero-alloc steady state is the goal.
    //
    // xLabelCache: per-series string[] of points[i].X?.ToString() results.
    //              Jagged to allow different series lengths.
    // gridLabelCache: 5 strings for gridline value labels. Keyed on
    //                 (gridCeilKey, gridFloorKey) — invalidated when
    //                 ceil/floor change.
    // yPositionsPool / prevBaselinePool: reusable float[] buffers.
    internal string?[]?[]? xLabelCache;
    internal string?[]? gridLabelCache;
    internal double gridCeilKey = double.NaN;
    internal double gridFloorKey = double.NaN;
    internal float[]? yPositionsPool;
    internal float[]? prevBaselinePool;
    internal object? paintCacheKey;

    /// <summary>Creates a single-series area chart from labeled X/Y data points.</summary>
    /// <param name="data">The data points with arbitrary-typed X and numeric Y values.</param>
    public AreaChart(IEnumerable<(object X, double Y)> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
        paintCacheKey = seriesList;
    }

    /// <summary>Creates a single-series area chart from Y values with array indices as X values.</summary>
    /// <param name="data">The Y values; X values are inferred from array indices.</param>
    public AreaChart(IEnumerable<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        seriesList.Add(new ChartSeries("Series 1", data));
        paintCacheKey = seriesList;
    }

    /// <summary>Creates a multi-series area chart.</summary>
    /// <param name="series">The data series to plot.</param>
    public AreaChart(IReadOnlyList<ChartSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        seriesList.AddRange(series);
        paintCacheKey = seriesList;
    }

    /// <summary>The data series in this chart.</summary>
    internal IReadOnlyList<ChartSeries> Series => seriesList;

    /// <summary>Enables or disables stacking of area series. When stacked, values sum cumulatively.</summary>
    /// <param name="stacked">True to stack areas, false to overlay them.</param>
    public AreaChart Stacked(bool stacked)
    {
        stackedEnabled = stacked;
        return this;
    }

    /// <summary>Sets the opacity of the filled area region. Default is 0.25.</summary>
    /// <param name="opacity">Fill opacity from 0.0 (transparent) to 1.0 (opaque).</param>
    public AreaChart FillOpacity(float opacity)
    {
        fillOpacityValue = Math.Clamp(opacity, 0f, 1f);
        return this;
    }

    /// <summary>Enables or disables Catmull-Rom smooth interpolation between data points.</summary>
    /// <param name="smooth">True for smooth curves, false for straight line segments.</param>
    public AreaChart Smooth(bool smooth)
    {
        smoothEnabled = smooth;
        return this;
    }

    /// <summary>Controls when data point markers are displayed on the area boundary.</summary>
    /// <param name="display">The point display strategy.</param>
    public AreaChart Points(PointDisplay display)
    {
        pointDisplay = display;
        return this;
    }

    /// <summary>Controls how null or missing values in the data are handled.</summary>
    /// <param name="mode">The missing value handling mode.</param>
    public AreaChart MissingValues(MissingValueMode mode)
    {
        missingValueMode = mode;
        return this;
    }
}

namespace Cascade.UI;

/// <summary>
/// Abstract base class for chart controls that support tooltips, legends, animation,
/// accessibility, and export. Extended by all chart types except
/// <see cref="Sparkline"/> and <see cref="DonutGauge"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete chart type, enabling fluent method chaining.</typeparam>
public abstract class ChartBase<TSelf> : Node where TSelf : ChartBase<TSelf>
{
    internal TooltipMode tooltipMode = TooltipMode.None;
    internal Func<IReadOnlyList<TooltipPoint>, Node>? tooltipRender;
    internal LegendPosition legendPosition = LegendPosition.None;
    internal int? legendColumns;
    internal AnimateTrigger animateTrigger = AnimateTrigger.Load;
    internal SharedChartTooltip? sharedTooltipContext;
    internal string? accessibleSummaryText;

    /// <summary>Configures the tooltip display mode.</summary>
    /// <param name="mode">The tooltip behavior when hovering over data.</param>
    public TSelf Tooltip(TooltipMode mode)
    {
        tooltipMode = mode;
        return (TSelf)this;
    }

    /// <summary>Configures a tooltip with custom content rendered by the provided function.</summary>
    /// <param name="mode">The tooltip behavior when hovering over data.</param>
    /// <param name="render">A function that receives the tooltip data points and returns a node tree to render.</param>
    public TSelf Tooltip(TooltipMode mode, Func<IReadOnlyList<TooltipPoint>, Node> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        tooltipMode = mode;
        tooltipRender = render;
        return (TSelf)this;
    }

    /// <summary>Configures the legend position and optional column layout.</summary>
    /// <param name="position">Where the legend is placed relative to the chart area.</param>
    /// <param name="columns">Optional number of columns for the legend items.</param>
    public TSelf Legend(LegendPosition position, int? columns = null)
    {
        legendPosition = position;
        legendColumns = columns;
        return (TSelf)this;
    }

    /// <summary>Configures when chart animations are triggered.</summary>
    /// <param name="on">The animation trigger mode.</param>
    public TSelf Animate(AnimateTrigger on = AnimateTrigger.Load)
    {
        animateTrigger = on;
        return (TSelf)this;
    }

    /// <summary>Synchronizes tooltip display with other charts sharing the same context.</summary>
    /// <param name="context">The shared tooltip context instance.</param>
    public TSelf SharedTooltip(SharedChartTooltip context)
    {
        ArgumentNullException.ThrowIfNull(context);
        sharedTooltipContext = context;
        return (TSelf)this;
    }

    /// <summary>Provides a custom accessibility summary announced by screen readers on focus.</summary>
    /// <param name="summary">The text summary describing the chart content.</param>
    public TSelf AccessibleSummary(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        accessibleSummaryText = summary;
        return (TSelf)this;
    }

    /// <summary>Exports the chart as an SVG string.</summary>
    /// <param name="textMode">Whether text is rendered as text elements or converted to paths.</param>
    public Task<string> ExportSvgAsync(SvgTextMode textMode = SvgTextMode.Text)
    {
        string svg = ChartExporter.ExportSvg(this, textMode);
        return Task.FromResult(svg);
    }

    /// <summary>Exports the chart as a PNG byte array at the specified scale factor.</summary>
    /// <param name="scale">The scale multiplier for the exported image resolution.</param>
    public Task<byte[]> ExportPngAsync(float scale = 1.0f)
    {
        byte[] png = ChartExporter.ExportPng(this, scale);
        return Task.FromResult(png);
    }

    /// <summary>Exports the chart as a PDF byte array.</summary>
    public Task<byte[]> ExportPdfAsync()
    {
        byte[] pdf = ChartExporter.ExportPdf(this);
        return Task.FromResult(pdf);
    }
}

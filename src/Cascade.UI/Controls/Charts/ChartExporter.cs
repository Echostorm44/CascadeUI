using System.Globalization;
using System.Text;

namespace Cascade.UI;

/// <summary>
/// Generates SVG, PNG, and PDF exports from chart data.
/// Used internally by <see cref="ChartBase{TSelf}"/> export methods.
/// </summary>
internal static class ChartExporter
{
    private const int DefaultWidth = 800;
    private const int DefaultHeight = 400;
    private const int Padding = 40;

    // Default chart color palette (Material design inspired)
    private static readonly string[] Palette =
    [
        "#4285F4", "#EA4335", "#FBBC04", "#34A853", "#FF6D01",
        "#46BDC6", "#7BAAF7", "#F07B72", "#FCD04F", "#71C287",
    ];

    /// <summary>Exports chart data to a real SVG document.</summary>
    public static string ExportSvg(object chart, SvgTextMode textMode, int width = DefaultWidth, int height = DefaultHeight)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" data-text-mode=\"{textMode}\">");
        sb.AppendLine("  <style>");
        sb.AppendLine("    text { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; }");
        sb.AppendLine("    .axis-label { font-size: 12px; fill: #666; }");
        sb.AppendLine("    .axis-title { font-size: 14px; fill: #333; font-weight: 500; }");
        sb.AppendLine("    .chart-title { font-size: 16px; fill: #222; font-weight: 600; }");
        sb.AppendLine("    .data-label { font-size: 11px; fill: #444; }");
        sb.AppendLine("  </style>");

        float plotLeft = Padding + 30;
        float plotTop = Padding;
        float plotRight = width - Padding;
        float plotBottom = height - Padding - 20;
        float plotWidth = plotRight - plotLeft;
        float plotHeight = plotBottom - plotTop;

        switch (chart)
        {
            case BarChart bar:
                RenderBarChartSvg(sb, bar, plotLeft, plotTop, plotWidth, plotHeight, textMode);
                break;
            case LineChart line:
                RenderLineChartSvg(sb, line, plotLeft, plotTop, plotWidth, plotHeight, textMode);
                break;
            case AreaChart area:
                RenderAreaChartSvg(sb, area, plotLeft, plotTop, plotWidth, plotHeight, textMode);
                break;
            case PieChart pie:
                RenderPieChartSvg(sb, pie, width, height, textMode);
                break;
            case ScatterPlot scatter:
                RenderScatterPlotSvg(sb, scatter, plotLeft, plotTop, plotWidth, plotHeight, textMode);
                break;
            default:
                // Render axes frame for unknown chart types
                RenderAxesFrame(sb, plotLeft, plotTop, plotWidth, plotHeight);
                break;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>Exports chart data to PNG bytes using software rasterization.</summary>
    public static byte[] ExportPng(object chart, float scale)
    {
        int width = (int)(DefaultWidth * scale);
        int height = (int)(DefaultHeight * scale);

        // Software rasterize: create pixel buffer, draw chart geometry, encode as PNG
        byte[] pixels = new byte[width * height * 4]; // RGBA

        // Fill with white background
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;     // R
            pixels[i + 1] = 255; // G
            pixels[i + 2] = 255; // B
            pixels[i + 3] = 255; // A
        }

        float plotLeft = (Padding + 30) * scale;
        float plotTop = Padding * scale;
        float plotRight = width - Padding * scale;
        float plotBottom = height - (Padding + 20) * scale;
        float plotWidth = plotRight - plotLeft;
        float plotHeight = plotBottom - plotTop;

        switch (chart)
        {
            case BarChart bar:
                RasterizeBarChart(pixels, width, height, bar, plotLeft, plotTop, plotWidth, plotHeight);
                break;
            case LineChart line:
                RasterizeLineChart(pixels, width, height, line, plotLeft, plotTop, plotWidth, plotHeight);
                break;
            case PieChart pie:
                RasterizePieChart(pixels, width, height, pie);
                break;
            default:
                // Draw axes frame
                DrawRect(pixels, width, height, (int)plotLeft, (int)plotTop, (int)plotWidth, (int)plotHeight, 0xCC, 0xCC, 0xCC);
                break;
        }

        return EncodePng(pixels, width, height);
    }

    /// <summary>Exports chart data to PDF bytes.</summary>
    public static byte[] ExportPdf(object chart)
    {
        // Generate SVG content for the chart
        string svgContent = ExportSvg(chart, SvgTextMode.Path);

        // Create a minimal PDF with the SVG data embedded as an annotation stream.
        // For a proper implementation, we convert chart geometry to PDF drawing operators.
        return BuildPdf(chart);
    }

    // ── SVG Renderers ───────────────────────────────────────────

    private static void RenderBarChartSvg(StringBuilder sb, BarChart chart,
        float x, float y, float w, float h, SvgTextMode textMode)
    {
        RenderAxesFrame(sb, x, y, w, h);
        var series = chart.seriesList;
        if (series.Count == 0)
        {
            return;
        }

        // Find data range
        double maxY = 0;
        int categoryCount = 0;
        foreach (var s in series)
        {
            categoryCount = Math.Max(categoryCount, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, Math.Abs(yVal));
            }
        }

        if (categoryCount == 0 || maxY == 0)
        {
            return;
        }

        // Render Y axis scale
        RenderYAxisLabels(sb, x, y, h, 0, maxY, textMode);

        float catWidth = w / categoryCount;
        float barWidth = catWidth * chart.barWidthFraction / series.Count;

        for (int si = 0; si < series.Count; si++)
        {
            string color = series[si].colorOverride?.ToString() ?? Palette[si % Palette.Length];
            var points = series[si].dataPointsList;

            for (int pi = 0; pi < points.Count; pi++)
            {
                double val = points[pi].Y;
                float barH = (float)(val / maxY * h);
                float barX = x + pi * catWidth + si * barWidth + (catWidth - barWidth * series.Count) / 2;
                float barY = y + h - barH;

                sb.AppendLine(FormattableString.Invariant(
                    $"  <rect x=\"{barX:F1}\" y=\"{barY:F1}\" width=\"{barWidth:F1}\" height=\"{barH:F1}\" fill=\"{color}\" rx=\"2\" />"));

                // Category label
                if (si == 0 && textMode != SvgTextMode.Path)
                {
                    float labelX = x + pi * catWidth + catWidth / 2;
                    string label = points[pi].X?.ToString() ?? pi.ToString(CultureInfo.InvariantCulture);
                    sb.AppendLine(FormattableString.Invariant(
                        $"  <text x=\"{labelX:F1}\" y=\"{y + h + 16}\" text-anchor=\"middle\" class=\"axis-label\">{EscapeXml(label)}</text>"));
                }
            }
        }
    }

    private static void RenderLineChartSvg(StringBuilder sb, LineChart chart,
        float x, float y, float w, float h, SvgTextMode textMode)
    {
        RenderAxesFrame(sb, x, y, w, h);
        RenderCartesianSeriesSvg(sb, chart.seriesList, x, y, w, h, textMode, filled: false);
    }

    private static void RenderAreaChartSvg(StringBuilder sb, AreaChart chart,
        float x, float y, float w, float h, SvgTextMode textMode)
    {
        RenderAxesFrame(sb, x, y, w, h);
        RenderCartesianSeriesSvg(sb, chart.seriesList, x, y, w, h, textMode, filled: true);
    }

    private static void RenderCartesianSeriesSvg(StringBuilder sb, List<ChartSeries> seriesList,
        float x, float y, float w, float h, SvgTextMode textMode, bool filled)
    {
        if (seriesList.Count == 0)
        {
            return;
        }

        double maxY = 0;
        double minY = 0;
        int maxPoints = 0;
        foreach (var s in seriesList)
        {
            maxPoints = Math.Max(maxPoints, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, yVal);
                minY = Math.Min(minY, yVal);
            }
        }

        double range = maxY - minY;
        if (range == 0)
        {
            range = 1;
        }

        RenderYAxisLabels(sb, x, y, h, minY, maxY, textMode);

        for (int si = 0; si < seriesList.Count; si++)
        {
            string color = seriesList[si].colorOverride?.ToString() ?? Palette[si % Palette.Length];
            var points = seriesList[si].dataPointsList;
            if (points.Count == 0)
            {
                continue;
            }

            var pathPoints = new StringBuilder();
            for (int pi = 0; pi < points.Count; pi++)
            {
                float px = x + (maxPoints <= 1 ? w / 2 : pi * w / (maxPoints - 1));
                float py = y + h - (float)((points[pi].Y - minY) / range * h);
                pathPoints.Append(pi == 0 ? FormattableString.Invariant($"M{px:F1},{py:F1}") : FormattableString.Invariant($" L{px:F1},{py:F1}"));
            }

            if (filled)
            {
                // Close the area path back to the baseline
                float baseY = y + h;
                float firstX = x;
                float lastX = x + (maxPoints <= 1 ? w / 2 : (points.Count - 1) * w / (maxPoints - 1));
                sb.AppendLine(FormattableString.Invariant(
                    $"  <path d=\"{pathPoints} L{lastX:F1},{baseY:F1} L{firstX:F1},{baseY:F1} Z\" fill=\"{color}\" fill-opacity=\"0.3\" stroke=\"none\" />"));
            }

            sb.AppendLine(FormattableString.Invariant(
                $"  <path d=\"{pathPoints}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{seriesList[si].lineWidthValue:F1}\" />"));

            // Data point markers
            for (int pi = 0; pi < points.Count; pi++)
            {
                float px = x + (maxPoints <= 1 ? w / 2 : pi * w / (maxPoints - 1));
                float py = y + h - (float)((points[pi].Y - minY) / range * h);
                sb.AppendLine(FormattableString.Invariant(
                    $"  <circle cx=\"{px:F1}\" cy=\"{py:F1}\" r=\"3\" fill=\"{color}\" />"));
            }

            // Category labels on X axis
            if (si == 0 && textMode != SvgTextMode.Path)
            {
                for (int pi = 0; pi < points.Count; pi++)
                {
                    float px = x + (maxPoints <= 1 ? w / 2 : pi * w / (maxPoints - 1));
                    string label = points[pi].X?.ToString() ?? pi.ToString(CultureInfo.InvariantCulture);
                    sb.AppendLine(FormattableString.Invariant(
                        $"  <text x=\"{px:F1}\" y=\"{y + h + 16}\" text-anchor=\"middle\" class=\"axis-label\">{EscapeXml(label)}</text>"));
                }
            }
        }
    }

    private static void RenderScatterPlotSvg(StringBuilder sb, ScatterPlot chart,
        float x, float y, float w, float h, SvgTextMode textMode)
    {
        RenderAxesFrame(sb, x, y, w, h);
        var seriesList = chart.seriesList;
        if (seriesList.Count == 0)
        {
            return;
        }

        double maxX = double.MinValue, minX = double.MaxValue;
        double maxY = double.MinValue, minY = double.MaxValue;

        foreach (var s in seriesList)
        {
            foreach (var point in s.dataPointsList)
            {
                maxX = Math.Max(maxX, point.X);
                minX = Math.Min(minX, point.X);
                maxY = Math.Max(maxY, point.Y);
                minY = Math.Min(minY, point.Y);
            }
        }

        double rangeX = maxX - minX;
        double rangeY = maxY - minY;
        if (rangeX == 0) { rangeX = 1; }
        if (rangeY == 0) { rangeY = 1; }

        RenderYAxisLabels(sb, x, y, h, minY, maxY, textMode);

        for (int si = 0; si < seriesList.Count; si++)
        {
            string color = seriesList[si].colorOverride?.ToString() ?? Palette[si % Palette.Length];
            float radius = chart.pointRadiusValue;

            foreach (var point in seriesList[si].dataPointsList)
            {
                float px = x + (float)((point.X - minX) / rangeX * w);
                float py = y + h - (float)((point.Y - minY) / rangeY * h);
                sb.AppendLine(FormattableString.Invariant(
                    $"  <circle cx=\"{px:F1}\" cy=\"{py:F1}\" r=\"{radius:F1}\" fill=\"{color}\" fill-opacity=\"0.7\" />"));
            }
        }
    }

    private static void RenderPieChartSvg(StringBuilder sb, PieChart chart,
        int width, int height, SvgTextMode textMode)
    {
        float cx = width / 2f;
        float cy = height / 2f;
        float radius = Math.Min(width, height) / 2f - Padding;
        float innerRadius = radius * chart.holeRadiusValue;

        var slices = chart.slicesList;
        double total = 0;
        foreach (var slice in slices)
        {
            total += slice.Value;
        }

        if (total == 0 || slices.Count == 0)
        {
            return;
        }

        double startAngle = chart.startAngleValue.InRadians;

        for (int i = 0; i < slices.Count; i++)
        {
            double fraction = slices[i].Value / total;
            double sweepAngle = fraction * 2 * Math.PI;
            double endAngle = startAngle + sweepAngle;
            string color = slices[i].colorOverride?.ToString() ?? Palette[i % Palette.Length];

            float x1 = cx + radius * (float)Math.Cos(startAngle);
            float y1 = cy + radius * (float)Math.Sin(startAngle);
            float x2 = cx + radius * (float)Math.Cos(endAngle);
            float y2 = cy + radius * (float)Math.Sin(endAngle);

            int largeArc = sweepAngle > Math.PI ? 1 : 0;

            if (innerRadius > 0)
            {
                // Donut arc
                float ix1 = cx + innerRadius * (float)Math.Cos(startAngle);
                float iy1 = cy + innerRadius * (float)Math.Sin(startAngle);
                float ix2 = cx + innerRadius * (float)Math.Cos(endAngle);
                float iy2 = cy + innerRadius * (float)Math.Sin(endAngle);

                sb.AppendLine(FormattableString.Invariant(
                    $"  <path d=\"M{x1:F1},{y1:F1} A{radius:F1},{radius:F1} 0 {largeArc},1 {x2:F1},{y2:F1} L{ix2:F1},{iy2:F1} A{innerRadius:F1},{innerRadius:F1} 0 {largeArc},0 {ix1:F1},{iy1:F1} Z\" fill=\"{color}\" stroke=\"white\" stroke-width=\"2\" />"));
            }
            else
            {
                // Full pie slice
                sb.AppendLine(FormattableString.Invariant(
                    $"  <path d=\"M{cx:F1},{cy:F1} L{x1:F1},{y1:F1} A{radius:F1},{radius:F1} 0 {largeArc},1 {x2:F1},{y2:F1} Z\" fill=\"{color}\" stroke=\"white\" stroke-width=\"2\" />"));
            }

            // Slice label
            if (textMode != SvgTextMode.Path)
            {
                double midAngle = startAngle + sweepAngle / 2;
                float labelRadius = innerRadius > 0 ? (radius + innerRadius) / 2 : radius * 0.7f;
                float lx = cx + labelRadius * (float)Math.Cos(midAngle);
                float ly = cy + labelRadius * (float)Math.Sin(midAngle);
                string label = slices[i].Label;
                sb.AppendLine(FormattableString.Invariant(
                    $"  <text x=\"{lx:F1}\" y=\"{ly:F1}\" text-anchor=\"middle\" dominant-baseline=\"central\" class=\"data-label\">{EscapeXml(label)}</text>"));
            }

            startAngle = endAngle;
        }
    }

    // ── SVG Helpers ──────────────────────────────────────────────

    private static void RenderAxesFrame(StringBuilder sb, float x, float y, float w, float h)
    {
        // Y axis line
        sb.AppendLine(FormattableString.Invariant(
            $"  <line x1=\"{x:F1}\" y1=\"{y:F1}\" x2=\"{x:F1}\" y2=\"{y + h:F1}\" stroke=\"#ccc\" stroke-width=\"1\" />"));
        // X axis line
        sb.AppendLine(FormattableString.Invariant(
            $"  <line x1=\"{x:F1}\" y1=\"{y + h:F1}\" x2=\"{x + w:F1}\" y2=\"{y + h:F1}\" stroke=\"#ccc\" stroke-width=\"1\" />"));
    }

    private static void RenderYAxisLabels(StringBuilder sb, float x, float y, float h,
        double minVal, double maxVal, SvgTextMode textMode)
    {
        if (textMode == SvgTextMode.Path)
        {
            return;
        }

        int tickCount = 5;
        for (int i = 0; i <= tickCount; i++)
        {
            double val = minVal + (maxVal - minVal) * i / tickCount;
            float ty = y + h - i * h / tickCount;
            string label = val.ToString("G4", CultureInfo.InvariantCulture);

            // Tick mark
            sb.AppendLine(FormattableString.Invariant(
                $"  <line x1=\"{x - 4:F1}\" y1=\"{ty:F1}\" x2=\"{x:F1}\" y2=\"{ty:F1}\" stroke=\"#ccc\" />"));
            // Grid line
            sb.AppendLine(FormattableString.Invariant(
                $"  <line x1=\"{x:F1}\" y1=\"{ty:F1}\" x2=\"{x + 700:F1}\" y2=\"{ty:F1}\" stroke=\"#f0f0f0\" stroke-dasharray=\"4,4\" />"));
            // Label
            sb.AppendLine(FormattableString.Invariant(
                $"  <text x=\"{x - 8:F1}\" y=\"{ty + 4:F1}\" text-anchor=\"end\" class=\"axis-label\">{label}</text>"));
        }
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    // ── PNG Rasterization ───────────────────────────────────────

    private static void RasterizeBarChart(byte[] pixels, int width, int height,
        BarChart chart, float plotX, float plotY, float plotW, float plotH)
    {
        var series = chart.seriesList;
        if (series.Count == 0)
        {
            return;
        }

        double maxY = 0;
        int categoryCount = 0;
        foreach (var s in series)
        {
            categoryCount = Math.Max(categoryCount, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, Math.Abs(yVal));
            }
        }

        if (categoryCount == 0 || maxY == 0)
        {
            return;
        }

        float catWidth = plotW / categoryCount;
        float barWidth = catWidth * chart.barWidthFraction / series.Count;

        for (int si = 0; si < series.Count; si++)
        {
            var (r, g, b) = ParseColor(Palette[si % Palette.Length]);
            var points = series[si].dataPointsList;

            for (int pi = 0; pi < points.Count; pi++)
            {
                double val = points[pi].Y;
                float barH = (float)(val / maxY * plotH);
                float barX = plotX + pi * catWidth + si * barWidth + (catWidth - barWidth * series.Count) / 2;
                float barY = plotY + plotH - barH;

                FillRect(pixels, width, height, (int)barX, (int)barY, (int)barWidth, (int)barH, r, g, b);
            }
        }
    }

    private static void RasterizeLineChart(byte[] pixels, int width, int height,
        LineChart chart, float plotX, float plotY, float plotW, float plotH)
    {
        var seriesList = chart.seriesList;
        if (seriesList.Count == 0)
        {
            return;
        }

        double maxY = double.MinValue;
        double minY = double.MaxValue;
        int maxPoints = 0;
        foreach (var s in seriesList)
        {
            maxPoints = Math.Max(maxPoints, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, yVal);
                minY = Math.Min(minY, yVal);
            }
        }

        double range = maxY - minY;
        if (range == 0)
        {
            range = 1;
        }

        for (int si = 0; si < seriesList.Count; si++)
        {
            var (r, g, b) = ParseColor(Palette[si % Palette.Length]);
            var points = seriesList[si].dataPointsList;

            for (int pi = 1; pi < points.Count; pi++)
            {
                float x1 = plotX + (maxPoints <= 1 ? plotW / 2 : (pi - 1) * plotW / (maxPoints - 1));
                float y1 = plotY + plotH - (float)((points[pi - 1].Y - minY) / range * plotH);
                float x2 = plotX + (maxPoints <= 1 ? plotW / 2 : pi * plotW / (maxPoints - 1));
                float y2 = plotY + plotH - (float)((points[pi].Y - minY) / range * plotH);

                DrawLine(pixels, width, height, (int)x1, (int)y1, (int)x2, (int)y2, r, g, b);
            }
        }
    }

    private static void RasterizePieChart(byte[] pixels, int width, int height, PieChart chart)
    {
        float cx = width / 2f;
        float cy = height / 2f;
        float radius = Math.Min(width, height) / 2f - Padding;

        var slices = chart.slicesList;
        double total = 0;
        foreach (var slice in slices)
        {
            total += slice.Value;
        }

        if (total == 0)
        {
            return;
        }

        // Simple angle-based rasterization
        double startAngle = chart.startAngleValue.InRadians;
        double[] sliceAngles = new double[slices.Count + 1];
        sliceAngles[0] = startAngle;
        for (int i = 0; i < slices.Count; i++)
        {
            sliceAngles[i + 1] = sliceAngles[i] + slices[i].Value / total * 2 * Math.PI;
        }

        // For each pixel in the bounding box, determine which slice it belongs to
        int minX = Math.Max(0, (int)(cx - radius));
        int maxX = Math.Min(width - 1, (int)(cx + radius));
        int minYPx = Math.Max(0, (int)(cy - radius));
        int maxYPx = Math.Min(height - 1, (int)(cy + radius));

        for (int py = minYPx; py <= maxYPx; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist > radius || dist < radius * chart.holeRadiusValue)
                {
                    continue;
                }

                double angle = Math.Atan2(dy, dx);
                // Normalize angle to [startAngle, startAngle + 2*PI)
                while (angle < sliceAngles[0])
                {
                    angle += 2 * Math.PI;
                }

                for (int i = 0; i < slices.Count; i++)
                {
                    double end = sliceAngles[i + 1];
                    // Normalize end
                    while (end < sliceAngles[0])
                    {
                        end += 2 * Math.PI;
                    }

                    if (angle < end)
                    {
                        var (r, g, b) = ParseColor(Palette[i % Palette.Length]);
                        int offset = (py * width + px) * 4;
                        pixels[offset] = r;
                        pixels[offset + 1] = g;
                        pixels[offset + 2] = b;
                        pixels[offset + 3] = 255;
                        break;
                    }
                }
            }
        }
    }

    // ── Drawing Primitives ──────────────────────────────────────

    private static void FillRect(byte[] pixels, int width, int height,
        int rx, int ry, int rw, int rh, byte r, byte g, byte b)
    {
        for (int py = Math.Max(0, ry); py < Math.Min(height, ry + rh); py++)
        {
            for (int px = Math.Max(0, rx); px < Math.Min(width, rx + rw); px++)
            {
                int offset = (py * width + px) * 4;
                pixels[offset] = r;
                pixels[offset + 1] = g;
                pixels[offset + 2] = b;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static void DrawRect(byte[] pixels, int width, int height,
        int rx, int ry, int rw, int rh, byte r, byte g, byte b)
    {
        // Top and bottom edges
        for (int px = rx; px < Math.Min(width, rx + rw); px++)
        {
            SetPixel(pixels, width, height, px, ry, r, g, b);
            SetPixel(pixels, width, height, px, ry + rh - 1, r, g, b);
        }
        // Left and right edges
        for (int py = ry; py < Math.Min(height, ry + rh); py++)
        {
            SetPixel(pixels, width, height, rx, py, r, g, b);
            SetPixel(pixels, width, height, rx + rw - 1, py, r, g, b);
        }
    }

    private static void DrawLine(byte[] pixels, int width, int height,
        int x0, int y0, int x1, int y1, byte r, byte g, byte b)
    {
        // Bresenham's line algorithm
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixel(pixels, width, height, x0, y0, r, g, b);
            // Thicken line by drawing adjacent pixels
            SetPixel(pixels, width, height, x0 + 1, y0, r, g, b);
            SetPixel(pixels, width, height, x0, y0 + 1, r, g, b);

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(byte[] pixels, int width, int height, int x, int y, byte r, byte g, byte b)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            int offset = (y * width + x) * 4;
            pixels[offset] = r;
            pixels[offset + 1] = g;
            pixels[offset + 2] = b;
            pixels[offset + 3] = 255;
        }
    }

    private static (byte r, byte g, byte b) ParseColor(string hex)
    {
        if (hex.Length == 7 && hex[0] == '#')
        {
            byte r = byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return (r, g, b);
        }
        return (0x42, 0x85, 0xF4); // Default blue
    }

    // ── PNG Encoder ─────────────────────────────────────────────

    private static byte[] EncodePng(byte[] pixels, int width, int height)
        => Imaging.ImageCodec.EncodePng(pixels, width, height);

    // ── PDF Generator ───────────────────────────────────────────

    private static byte[] BuildPdf(object chart)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        // PDF header
        writer.Write("%PDF-1.4\n");

        // Catalog (object 1)
        long obj1Offset = ms.Position;
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Pages (object 2)
        long obj2Offset = ms.Position;
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Generate chart drawing operators
        string contentStream = BuildPdfContentStream(chart);

        // Content stream (object 4)
        long obj4Offset = ms.Position;
        writer.Write($"4 0 obj\n<< /Length {contentStream.Length} >>\nstream\n");
        writer.Write(contentStream);
        writer.Write("\nendstream\nendobj\n");

        // Page (object 3)
        long obj3Offset = ms.Position;
        writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 800 400] /Contents 4 0 R >>\nendobj\n");

        writer.Flush();

        // Cross-reference table
        long xrefOffset = ms.Position;
        writer.Write("xref\n0 5\n");
        writer.Write("0000000000 65535 f \n");
        writer.Write($"{obj1Offset:D10} 00000 n \n");
        writer.Write($"{obj2Offset:D10} 00000 n \n");
        writer.Write($"{obj3Offset:D10} 00000 n \n");
        writer.Write($"{obj4Offset:D10} 00000 n \n");

        // Trailer
        writer.Write($"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }

    private static string BuildPdfContentStream(object chart)
    {
        var sb = new StringBuilder();
        float pageWidth = 800;
        float pageHeight = 400;
        float plotLeft = Padding + 30;
        float plotTop = Padding;
        float plotWidth = pageWidth - Padding * 2 - 30;
        float plotHeight = pageHeight - Padding * 2 - 20;

        // PDF Y axis is bottom-up, so we flip
        float yBase = pageHeight - plotTop - plotHeight;

        switch (chart)
        {
            case BarChart bar:
                BuildBarChartPdf(sb, bar, plotLeft, yBase, plotWidth, plotHeight);
                break;
            case LineChart line:
                BuildLineChartPdf(sb, line, plotLeft, yBase, plotWidth, plotHeight);
                break;
            case PieChart pie:
                BuildPieChartPdf(sb, pie, pageWidth, pageHeight);
                break;
            default:
                // Draw axes frame
                sb.AppendLine(FormattableString.Invariant($"0.8 0.8 0.8 RG"));
                sb.AppendLine(FormattableString.Invariant($"{plotLeft:F1} {yBase:F1} {plotWidth:F1} {plotHeight:F1} re S"));
                break;
        }

        return sb.ToString();
    }

    private static void BuildBarChartPdf(StringBuilder sb, BarChart chart,
        float x, float y, float w, float h)
    {
        var series = chart.seriesList;
        if (series.Count == 0)
        {
            return;
        }

        double maxY = 0;
        int categoryCount = 0;
        foreach (var s in series)
        {
            categoryCount = Math.Max(categoryCount, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, Math.Abs(yVal));
            }
        }

        if (categoryCount == 0 || maxY == 0)
        {
            return;
        }

        float catWidth = w / categoryCount;
        float barWidth = catWidth * chart.barWidthFraction / series.Count;

        for (int si = 0; si < series.Count; si++)
        {
            var (r, g, b) = ParseColor(Palette[si % Palette.Length]);
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;
            sb.AppendLine(FormattableString.Invariant($"{rf:F3} {gf:F3} {bf:F3} rg"));

            var points = series[si].dataPointsList;
            for (int pi = 0; pi < points.Count; pi++)
            {
                double val = points[pi].Y;
                float barH = (float)(val / maxY * h);
                float barX = x + pi * catWidth + si * barWidth + (catWidth - barWidth * series.Count) / 2;
                float barY = y;

                sb.AppendLine(FormattableString.Invariant($"{barX:F1} {barY:F1} {barWidth:F1} {barH:F1} re f"));
            }
        }
    }

    private static void BuildLineChartPdf(StringBuilder sb, LineChart chart,
        float x, float y, float w, float h)
    {
        var seriesList = chart.seriesList;
        if (seriesList.Count == 0)
        {
            return;
        }

        double maxY = double.MinValue;
        double minY = double.MaxValue;
        int maxPoints = 0;
        foreach (var s in seriesList)
        {
            maxPoints = Math.Max(maxPoints, s.dataPointsList.Count);
            foreach (var (_, yVal) in s.dataPointsList)
            {
                maxY = Math.Max(maxY, yVal);
                minY = Math.Min(minY, yVal);
            }
        }

        double range = maxY - minY;
        if (range == 0)
        {
            range = 1;
        }

        for (int si = 0; si < seriesList.Count; si++)
        {
            var (r, g, b) = ParseColor(Palette[si % Palette.Length]);
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;
            sb.AppendLine(FormattableString.Invariant($"{rf:F3} {gf:F3} {bf:F3} RG"));
            sb.AppendLine("2 w"); // line width

            var points = seriesList[si].dataPointsList;
            for (int pi = 0; pi < points.Count; pi++)
            {
                float px = x + (maxPoints <= 1 ? w / 2 : pi * w / (maxPoints - 1));
                float py = y + (float)((points[pi].Y - minY) / range * h);

                if (pi == 0)
                {
                    sb.AppendLine(FormattableString.Invariant($"{px:F1} {py:F1} m"));
                }
                else
                {
                    sb.AppendLine(FormattableString.Invariant($"{px:F1} {py:F1} l"));
                }
            }
            sb.AppendLine("S"); // Stroke
        }
    }

    private static void BuildPieChartPdf(StringBuilder sb, PieChart chart, float pageWidth, float pageHeight)
    {
        float cx = pageWidth / 2;
        float cy = pageHeight / 2;
        float radius = Math.Min(pageWidth, pageHeight) / 2 - Padding;

        var slices = chart.slicesList;
        double total = 0;
        foreach (var slice in slices)
        {
            total += slice.Value;
        }

        if (total == 0)
        {
            return;
        }

        double startAngle = chart.startAngleValue.InRadians;

        for (int i = 0; i < slices.Count; i++)
        {
            double fraction = slices[i].Value / total;
            double sweepAngle = fraction * 2 * Math.PI;
            double endAngle = startAngle + sweepAngle;

            var (r, g, b) = ParseColor(Palette[i % Palette.Length]);
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;

            // Approximate arc with line segments
            sb.AppendLine(FormattableString.Invariant($"{rf:F3} {gf:F3} {bf:F3} rg"));
            sb.AppendLine(FormattableString.Invariant($"{cx:F1} {cy:F1} m"));

            int segments = Math.Max(4, (int)(sweepAngle / Math.PI * 16));
            for (int s = 0; s <= segments; s++)
            {
                double angle = startAngle + sweepAngle * s / segments;
                float px = cx + radius * (float)Math.Cos(angle);
                float py = cy + radius * (float)Math.Sin(angle);
                sb.AppendLine(FormattableString.Invariant($"{px:F1} {py:F1} l"));
            }
            sb.AppendLine("f"); // Fill

            startAngle = endAngle;
        }
    }
}

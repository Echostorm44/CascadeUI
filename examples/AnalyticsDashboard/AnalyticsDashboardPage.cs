// Golden Example 10 — Analytics Dashboard (Charts)
//
// Demonstrates:
//   - DonutGauge for KPI cards (with threshold color changes)
//   - LineChart with multi-series and smooth interpolation
//   - BarChart with stacked series
//   - DataTable with Sparkline cells and computed columns
//   - ToggleGroup for date range selection
//   - Cross-filtering via segment selection
//   - Skeleton loading placeholders
//   - Reactive computed properties that derive filtered/aggregated views

using Cascade.UI;

namespace AnalyticsDashboard;

// ── Data models ──────────────────────────────────────────────────────────────

internal sealed record RevenuePoint(DateOnly Date, decimal Revenue, decimal Target);
internal sealed record SegmentPoint(string Label, decimal Revenue, decimal Cost);
internal sealed record ProductRow(
    string                     Product,
    decimal                    Revenue,
    decimal                    Growth,
    IReadOnlyList<decimal>     WeeklySparkline
);
internal sealed record DashboardData(
    decimal                          TotalRevenue,
    decimal                          RevenueTarget,
    float                            ConversionRate,
    int                              ActiveUsers,
    IReadOnlyList<RevenuePoint>      RevenueSeries,
    IReadOnlyList<SegmentPoint>      SegmentBreakdown,
    IReadOnlyList<ProductRow>        TopProducts
);

// ── Date range option ────────────────────────────────────────────────────────

internal enum DateRange { Last7Days, Last30Days, Last90Days, LastYear }

// ── Mock data generator ──────────────────────────────────────────────────────

internal static class MockAnalyticsData
{
#pragma warning disable CA5394 // Mock data generation uses Random for deterministic seeding, not security
    public static DashboardData Generate(DateRange range)
    {
        var days = range switch
        {
            DateRange.Last7Days  => 7,
            DateRange.Last30Days => 30,
            DateRange.Last90Days => 90,
            DateRange.LastYear   => 365,
            _ => 30
        };

        var baseRevenue = range switch
        {
            DateRange.Last7Days  => 42_000m,
            DateRange.Last30Days => 185_000m,
            DateRange.Last90Days => 520_000m,
            DateRange.LastYear   => 2_100_000m,
            _ => 185_000m
        };

        // Attainment varies by range so the Revenue gauge actually moves: a strong
        // recent week, easing off over longer horizons (target = revenue ∕ attainment).
        var targetMultiplier = range switch
        {
            DateRange.Last7Days  => 1.05m,   // ~95% of target
            DateRange.Last30Days => 1.15m,   // ~87%
            DateRange.Last90Days => 1.25m,   // ~80%
            DateRange.LastYear   => 1.38m,   // ~72%
            _ => 1.15m
        };
        var target = baseRevenue * targetMultiplier;
        var rng = new Random(42 + days);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var revenueSeries = new List<RevenuePoint>();
        var step = days > 90 ? 7 : 1;
        for (int i = days; i >= 0; i -= step)
        {
            var date = today.AddDays(-i);
            var daily = baseRevenue / days * (decimal)(0.7 + rng.NextDouble() * 0.6);
            var targetDaily = target / days;
            revenueSeries.Add(new RevenuePoint(date, Math.Round(daily, 2), Math.Round(targetDaily, 2)));
        }

        var segments = new List<SegmentPoint>
        {
            new("Enterprise",  baseRevenue * 0.42m, baseRevenue * 0.15m),
            new("SMB",         baseRevenue * 0.28m, baseRevenue * 0.12m),
            new("Consumer",    baseRevenue * 0.18m, baseRevenue * 0.08m),
            new("Government",  baseRevenue * 0.12m, baseRevenue * 0.05m),
        };

        var products = new List<ProductRow>
        {
            new("Platform Pro",   baseRevenue * 0.35m, 0.12m,  GenSparkline(rng, 7, true)),
            new("Analytics Suite", baseRevenue * 0.25m, 0.08m,  GenSparkline(rng, 7, true)),
            new("API Gateway",    baseRevenue * 0.20m, -0.03m, GenSparkline(rng, 7, false)),
            new("Data Pipeline",  baseRevenue * 0.12m, 0.22m,  GenSparkline(rng, 7, true)),
            new("Edge CDN",       baseRevenue * 0.08m, -0.05m, GenSparkline(rng, 7, false)),
        };

        return new DashboardData(
            TotalRevenue:     baseRevenue,
            RevenueTarget:    target,
            ConversionRate:   0.073f + (float)(rng.NextDouble() * 0.02),
            ActiveUsers:      4200 + rng.Next(0, 800),
            RevenueSeries:    revenueSeries,
            SegmentBreakdown: segments,
            TopProducts:      products
        );
    }

    private static IReadOnlyList<decimal> GenSparkline(Random rng, int points, bool trending)
    {
        var result = new List<decimal>();
        var value = 100m;
        for (int i = 0; i < points; i++)
        {
            var drift = trending ? 2m : -1.5m;
            value += drift + (decimal)(rng.NextDouble() * 8 - 4);
            result.Add(Math.Max(10m, Math.Round(value, 1)));
        }
        return result;
    }
#pragma warning restore CA5394
}

// ── Icons ────────────────────────────────────────────────────────────────────

internal static class DashboardIcons
{
    // Lucide "x" — two crossing strokes on a 24×24 view box, centred so the
    // AA icon rasterizer draws it dead-centre in a circular button.
    internal static readonly Icon Close = new(
        ["M18 6 6 18", "M6 6 18 18"],
        new Size(24, 24), 24f, "Clear filter");
}

// ── Page ─────────────────────────────────────────────────────────────────────

internal sealed partial class AnalyticsDashboardPage : Component
{
    private DashboardData? data;
    private bool loading = true;
    private DateRange dateRange = DateRange.Last30Days;
    private string? activeSegment;
    private string? statusMessage;

    // Computed: filtered revenue series based on active segment
    private IReadOnlyList<ChartSeries> FilteredRevenueSeries
    {
        get
        {
            if (data is null)
            {
                return [];
            }

            if (activeSegment is null)
            {
                return
                [
                    new ChartSeries("Revenue", RevenueLineData())
                        .LineWidth(2.5f),
                    new ChartSeries("Target", TargetLineData())
                        .LineStyle(LineStyle.Dashed)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted)
                ];
            }

            return
            [
                new ChartSeries("Revenue", RevenueLineData())
                    .LineWidth(2.5f),
                new ChartSeries(activeSegment, SegmentLineData(activeSegment))
                    .Color(ThemeSwitcher.Current.Palette.Orange)
                    .LineWidth(2.5f)
            ];
        }
    }

    private IEnumerable<(object X, double Y)> RevenueLineData() =>
        data!.RevenueSeries.Select(p => ((object)p.Date, (double)p.Revenue));

    private IEnumerable<(object X, double Y)> TargetLineData() =>
        data!.RevenueSeries.Select(p => ((object)p.Date, (double)p.Target));

    private IEnumerable<(object X, double Y)> SegmentLineData(string segment)
    {
        var segmentTotal = data!.SegmentBreakdown.FirstOrDefault(s => s.Label == segment);
        if (segmentTotal is null)
        {
            return [];
        }

        var ratio = SegmentShare(segmentTotal);
        return data.RevenueSeries.Select(p => ((object)p.Date, (double)p.Revenue * ratio));
    }

    // The selected segment's breakdown row, or null when no segment filter is active.
    private SegmentPoint? ActiveSegmentData =>
        activeSegment is null ? null : data?.SegmentBreakdown.FirstOrDefault(s => s.Label == activeSegment);

    // A segment's share of total revenue (0–1). Drives the cross-filtered KPI gauges.
    private double SegmentShare(SegmentPoint segment) =>
        data!.TotalRevenue == 0 ? 0 : (double)(segment.Revenue / data.TotalRevenue);

    protected override async Task OnMounted()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        loading = true;
        Invalidate();

        // Simulate async data load
        await Task.Delay(300);

        data = MockAnalyticsData.Generate(dateRange);
        loading = false;
        Invalidate();
    }

    private async Task ExportCsvAsync()
    {
        var result = await FilePicker.SaveAsync(
            title: "Export Analytics CSV",
            suggestedName: "analytics.csv",
            filters: [new FileFilter("CSV Files", "*.csv")]);

        if (result is null)
        {
            return;
        }

        var lines = new List<string>
        {
            "Date,Revenue,Target,Segment,Cost",
            "2024-01-15,12500.00,11000.00,Enterprise,3200.00",
            "2024-01-16,13200.50,11000.00,SMB,2800.00",
            "2024-01-17,11800.75,11000.00,Enterprise,3100.00",
            "2024-01-18,14500.25,11000.00,Startup,1500.00",
            "2024-01-19,9800.00,11000.00,SMB,2600.00",
            "2024-01-20,15100.00,11000.00,Enterprise,3400.00",
            "2024-01-21,13700.80,11000.00,Startup,1800.00"
        };

        await File.WriteAllLinesAsync(result.Path, lines);

        statusMessage = $"Exported to {result.FileName}";
        Invalidate();
    }

    protected override Node Render()
    {
        return new ScrollView(
            new Column(spacing: 24, children:
            [
                // Header
                new Row(spacing: 16,
                    crossAxisAlignment: CrossAxisAlignment.Center,
                    children:
                [
                    new Label("Analytics Dashboard")
                        .FontSize(28)
                        .Bold(),
                    new Spacer(),
                    new Button("Export CSV", onClick: () =>
                    {
                        _ = ExportCsvAsync();
                    }),
                    statusMessage is not null
                        ? new Label(statusMessage)
                            .FontSize(12)
                            .Color(ThemeSwitcher.ActiveColors.Success)
                        : Node.Empty
                ]),

                // Segment filter + Date range
                SegmentFilterRow(),
                DateRangeRow(),

                // Content
                loading
                    ? LoadingSkeleton()
                    : DashboardContent()
            ]).Padding(24)
        );
    }

    // ── Date range picker ────────────────────────────────────────────────────

    private Node DateRangeRow()
    {
        return new Row(spacing: 12,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
        [
            new Label("Date Range")
                .Color(ThemeSwitcher.ActiveColors.TextMuted),

            new ToggleGroup<DateRange>(
                value: new Bindable<DateRange>(dateRange, v => { dateRange = v; _ = OnDateRangeChanged(); Invalidate(); }),
                options:
                [
                    new ToggleOption<DateRange>(DateRange.Last7Days,  "7 Days"),
                    new ToggleOption<DateRange>(DateRange.Last30Days, "30 Days"),
                    new ToggleOption<DateRange>(DateRange.Last90Days, "90 Days"),
                    new ToggleOption<DateRange>(DateRange.LastYear,   "1 Year")
                ])
        ]);
    }

    private async Task OnDateRangeChanged()
    {
        activeSegment = null;
        await LoadDataAsync();
    }

    private Node FilterChip(string segment)
    {
        var colors = ThemeSwitcher.ActiveColors;

        return new Row(spacing: 8,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
        [
            new Label("Filtered:")
                .FontSize(12)
                .Color(colors.TextMuted),
            new Label(segment)
                .FontSize(12)
                .Bold()
                .Color(colors.Primary),
            new IconButton(DashboardIcons.Close, onClick: () =>
                {
                    activeSegment = null;
                    Invalidate();
                })
                .IconSize(17)
                .AccessibleLabel("Clear filter")
                .Tooltip("Clear filter")
        ])
        .Padding(EdgeInsets.Only(top: 5, right: 5, bottom: 5, left: 14))
        .Background(colors.SurfaceAlt)
        .Border(colors.Border, 1f, 999f)
        .CornerRadius(999);
    }

    // ── Loading skeleton ─────────────────────────────────────────────────────

    private static Node LoadingSkeleton()
    {
        return new Column(spacing: 24, children:
        [
            // KPI row
            new Row(spacing: 16, children:
            [
                Skeleton.Rect(200, 140).Grow(1).CornerRadius(8),
                Skeleton.Rect(200, 140).Grow(1).CornerRadius(8),
                Skeleton.Rect(200, 140).Grow(1).CornerRadius(8)
            ]),
            // Line chart
            Skeleton.Rect(600, 280).CornerRadius(8),
            // Bar chart
            Skeleton.Rect(600, 200).CornerRadius(8),
            // Table
            Skeleton.Rect(600, 240).CornerRadius(8)
        ]);
    }

    // ── Dashboard content ────────────────────────────────────────────────────

    private Node DashboardContent()
    {
        return new Column(spacing: 24, children:
        [
            KpiRow(),
            RevenueTrendCard(),
            SegmentBreakdownCard(),
            TopProductsCard()
        ]);
    }

    // ── KPI gauges ───────────────────────────────────────────────────────────

    private Node KpiRow()
    {
        var seg = ActiveSegmentData;

        // Active Users cross-filters to the segment's share of the business.
        int activeUsers = seg is null
            ? data!.ActiveUsers
            : (int)Math.Round(data!.ActiveUsers * SegmentShare(seg));

        return new Row(spacing: 16, children:
        [
            KpiCard(
                label:  seg is null ? "Revenue" : $"{seg.Label} Revenue",
                gauge:  RevenueGauge(seg),
                value:  seg is null ? $"${data!.TotalRevenue:N0}" : $"${seg.Revenue:N0}",
                target: seg is null
                            ? $"Target: ${data!.RevenueTarget:N0}"
                            : $"of ${data!.TotalRevenue:N0} total"
            ),
            KpiCard(
                label:  "Conversion Rate",
                gauge:  new DonutGauge(value: data!.ConversionRate)
                            .Size(100)
                            .Thickness(12)
                            .Format(GaugeFormat.Percent)
                            .Color(ThemeSwitcher.ActiveColors.Primary)
                            .Animate(on: AnimateTrigger.Both),
                value:  $"{data.ConversionRate:P1}",
                target: null
            ),
            KpiCard(
                label:  seg is null ? "Active Users" : $"{seg.Label} Users",
                gauge:  new DonutGauge(value: Math.Min(1f, activeUsers / 10000f))
                            .Size(100)
                            .Thickness(12)
                            .Format(GaugeFormat.Percent)
                            .Color(ThemeSwitcher.ActiveColors.Success)
                            .Animate(on: AnimateTrigger.Both),
                value:  $"{activeUsers:N0}",
                target: null
            )
        ]);
    }

    // The Revenue gauge: global attainment-vs-target (colour-graded by thresholds),
    // or, when a segment filter is active, that segment's share of total revenue.
    private DonutGauge RevenueGauge(SegmentPoint? seg)
    {
        if (seg is null)
        {
            return new DonutGauge(value: (float)(data!.TotalRevenue / data.RevenueTarget))
                .Size(100)
                .Thickness(12)
                .Format(GaugeFormat.Percent)
                .Thresholds(
                [
                    new GaugeThreshold(0.0f,  ThemeSwitcher.ActiveColors.Danger),
                    new GaugeThreshold(0.6f,  ThemeSwitcher.ActiveColors.Warning),
                    new GaugeThreshold(0.9f,  ThemeSwitcher.ActiveColors.Success)
                ])
                .Animate(on: AnimateTrigger.Both);
        }

        return new DonutGauge(value: (float)SegmentShare(seg))
            .Size(100)
            .Thickness(12)
            .Format(GaugeFormat.Percent)
            .Color(ThemeSwitcher.ActiveColors.Primary)
            .Animate(on: AnimateTrigger.Both);
    }

    private static Node KpiCard(string label, Node gauge, string value, string? target)
    {
        return new Column(spacing: 8,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
        [
            new Label(label)
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            gauge,
            new Label(value)
                .FontSize(22)
                .Bold()
                .Color(ThemeSwitcher.ActiveColors.Text),
            target is not null
                ? new Label(target)
                    .FontSize(11)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                : new Label(" ")
                    .FontSize(11)
                    .Color(ColorValue.Transparent)
        ])
            .Padding(20)
            .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
            .CornerRadius(8)
            .Grow(1);
    }

    // ── Revenue trend ────────────────────────────────────────────────────────

    private Node RevenueTrendCard()
    {
        return ChartCard(
            title:   "Revenue Trend",
            height:  280,
            content: new LineChart(series: FilteredRevenueSeries)
                         .Smooth(true)
                         .Points(PointDisplay.Auto)
                         .XAxis(format: AxisFormat.Date)
                         .YAxis(label: "Revenue", format: AxisFormat.Currency)
                         .Legend(LegendPosition.Top)
                         .Tooltip(TooltipMode.Crosshair)
                         // Both: a date-range change reloads (re-entrance via the
                         // skeleton) and a segment filter changes the data in place.
                         .Animate(on: AnimateTrigger.Both)
        );
    }

    // ── Segment breakdown ────────────────────────────────────────────────────

    private Node SegmentBreakdownCard()
    {
        var revenueSeries = new ChartSeries(
            name: "Revenue",
            data: data!.SegmentBreakdown.Select(s => ((object)s.Label, (double)s.Revenue))
        ).Color(ThemeSwitcher.ActiveColors.Primary);

        var costSeries = new ChartSeries(
            name: "Cost",
            data: data.SegmentBreakdown.Select(s => ((object)s.Label, (double)s.Cost))
        ).Color(ThemeSwitcher.ActiveColors.Danger.Opacity(0.7f));

        return ChartCard(
            title:   "Segment Breakdown",
            height:  200,
            content: new BarChart(series: [revenueSeries, costSeries])
                         .GroupMode(BarGroupMode.Stacked)
                         .XAxis(label: "Segment")
                         .YAxis(format: AxisFormat.Currency)
                         .Legend(LegendPosition.Top)
                         .Tooltip(TooltipMode.All)
                         .DataLabels(enabled: false)
                         .Animate(on: AnimateTrigger.Both)
        );
    }

    // ── Segment filter (cross-filtering via toggle) ──────────────────────────

    private Node SegmentFilterRow()
    {
        if (data is null)
        {
            return Node.Empty;
        }

        var buttons = new List<Node>
        {
            new Label("Filter by Segment:")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        };

        foreach (var segment in data.SegmentBreakdown)
        {
            var name = segment.Label;
            var isActive = activeSegment == name;
            var btn = new Button(name, onClick: () =>
                {
                    activeSegment = activeSegment == name ? null : name;
                    Invalidate();
                })
                .CornerRadius(6);
            buttons.Add(isActive ? btn : btn.Variant("outline"));
        }

        var buttonRow = new Row(spacing: 8,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: buttons.ToArray());

        if (activeSegment is null)
        {
            return buttonRow;
        }

        return new Column(spacing: 8,
            crossAxisAlignment: CrossAxisAlignment.Start,
            children:
        [
            buttonRow,
            FilterChip(activeSegment)
        ]);
    }

    // ── Top products table ───────────────────────────────────────────────────

    private Node TopProductsCard()
    {
        return ChartCard(
            title:   "Top Products",
            height:  null,
            content: new DataTable<ProductRow>(
                items:   data!.TopProducts,
                columns:
                [
                    DataColumn<ProductRow>.Text("Product", row => row.Product)
                        .Width(DataColumnWidth.Fill)
                        .MinWidth(120),

                    DataColumn<ProductRow>.Number("Revenue", row => (object)row.Revenue,
                        format: "C0")
                        .Width(110)
                        .Align(ColumnAlignment.Right),

                    DataColumn<ProductRow>.Custom("Growth",
                        render: row => GrowthBadge(row.Growth))
                        .Width(90)
                        .Align(ColumnAlignment.Right)
                        .Sortable(true),

                    DataColumn<ProductRow>.Custom("Trend",
                        render: row => new Sparkline(row.WeeklySparkline.Select(v => (double)v))
                                           .Type(SparklineType.Line)
                                           .Width(80)
                                           .Height(28)
                                           .Color(row.Growth >= 0
                                               ? ThemeSwitcher.ActiveColors.Success
                                               : ThemeSwitcher.ActiveColors.Danger))
                        .Width(100)
                        .Sortable(false)
                ]
            )
            .DefaultSort("Revenue", SortDirection.Descending)
            .RowHeight(44)
            .Striped(true)
        );
    }

    private static Node GrowthBadge(decimal growth)
    {
        bool   positive = growth >= 0;
        var    color    = positive ? ThemeSwitcher.ActiveColors.Success : ThemeSwitcher.ActiveColors.Danger;
        string arrow    = positive ? "▲" : "▼";
        string text     = $"{Math.Abs(growth):P1}";

        return new Row(spacing: 4,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
        [
            new Label(arrow)
                .FontSize(10)
                .Color(color),
            new Label(positive ? $"+{text}" : $"−{text}")
                .FontSize(12)
                .Color(color)
        ]);
    }

    // ── Shared chart card container ──────────────────────────────────────────

    private static Node ChartCard(string title, int? height, Node content)
    {
        var chartContent = height is not null
            ? content.Height(height.Value)
            : content;

        return new Column(spacing: 12, children:
        [
            new Label(title)
                .FontSize(14)
                .Bold()
                .Color(ThemeSwitcher.ActiveColors.Text),
            chartContent
        ])
        .Padding(20)
        .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
        .CornerRadius(8);
    }
}

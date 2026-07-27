using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class ChartsPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            LineChartSection(),
            BarChartSection(),
            AreaChartSection(),
            PieChartSection(),
            DonutGaugeSection(),
            ScatterPlotSection(),
            SparklineSection(),
            HeatMapSection(),
            TreeMapSection(),
            WaterfallSection(),
        ]);

    // ── LineChart ────────────────────────────────────────────────────────

    static Node LineChartSection() =>
        Section("LineChart",
            "Line chart with single and multi-series data.",
            new Column(spacing: 12, children:
            [
                new LineChart(new (object, double)[]
                {
                    ("Jan", 10), ("Feb", 25), ("Mar", 18), ("Apr", 32),
                    ("May", 28), ("Jun", 45), ("Jul", 38), ("Aug", 52),
                }).Width(500).Height(200),
                new LineChart(
                [
                    new ChartSeries("Sales", [("Q1", 120.0), ("Q2", 180.0), ("Q3", 150.0), ("Q4", 210.0)]),
                    new ChartSeries("Costs", [("Q1", 80.0), ("Q2", 110.0), ("Q3", 95.0), ("Q4", 130.0)]),
                ]).Width(500).Height(200),
            ]));

    // ── BarChart ─────────────────────────────────────────────────────────

    static Node BarChartSection() =>
        Section("BarChart",
            "Vertical bar chart with grouped series.",
            new Column(spacing: 12, children:
            [
                new BarChart(new (object, double)[]
                {
                    ("Mon", 12), ("Tue", 19), ("Wed", 15), ("Thu", 22), ("Fri", 30),
                }).Width(500).Height(200),
                new BarChart(
                [
                    new ChartSeries("2024", [("Q1", 90.0), ("Q2", 120.0), ("Q3", 100.0), ("Q4", 140.0)]),
                    new ChartSeries("2025", [("Q1", 110.0), ("Q2", 150.0), ("Q3", 130.0), ("Q4", 170.0)]),
                ]).Width(500).Height(200),
            ]));

    // ── AreaChart ────────────────────────────────────────────────────────

    static Node AreaChartSection() =>
        Section("AreaChart",
            "Filled area chart for trend visualization.",
            new AreaChart(new (object, double)[]
            {
                ("Jan", 5), ("Feb", 15), ("Mar", 12), ("Apr", 28),
                ("May", 22), ("Jun", 35), ("Jul", 30),
            }).Width(500).Height(200));

    // ── PieChart ─────────────────────────────────────────────────────────

    static Node PieChartSection() =>
        Section("PieChart",
            "Pie chart with labeled slices.",
            new PieChart(new (string, double)[]
            {
                ("Desktop", 55), ("Mobile", 30), ("Tablet", 15),
            }).Width(250).Height(250));

    // ── DonutGauge ───────────────────────────────────────────────────────

    static Node DonutGaugeSection() =>
        Section("DonutGauge",
            "Donut gauge at various levels.",
            new Row(spacing: 24, children:
            [
                new DonutGauge(0.25f).Width(100).Height(100),
                new DonutGauge(0.50f).Width(100).Height(100),
                new DonutGauge(0.75f).Width(100).Height(100),
                new DonutGauge(1.0f).Width(100).Height(100),
            ]));

    // ── ScatterPlot ──────────────────────────────────────────────────────

    static Node ScatterPlotSection() =>
        Section("ScatterPlot",
            "Scatter plot showing point distribution.",
            new ScatterPlot(new (double, double)[]
            {
                (1, 2), (2, 4), (3, 3), (4, 7), (5, 5),
                (6, 8), (7, 6), (8, 9), (9, 7), (10, 10),
                (2.5, 5), (4.5, 3), (6.5, 8.5), (8.5, 4),
            }).Width(400).Height(250));

    // ── Sparkline ────────────────────────────────────────────────────────

    static Node SparklineSection() =>
        Section("Sparkline",
            "Compact inline sparkline for trends.",
            new Row(spacing: 24, children:
            [
                new Column(spacing: 4, children:
                [
                    new Label("Revenue").FontSize(12).Color(ThemeHelper.SubtleText),
                    new Sparkline([5, 10, 8, 15, 12, 20, 18, 25]).Width(120).Height(32),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Users").FontSize(12).Color(ThemeHelper.SubtleText),
                    new Sparkline([20, 18, 22, 25, 23, 28, 30, 35]).Width(120).Height(32),
                ]),
                new Column(spacing: 4, children:
                [
                    new Label("Errors").FontSize(12).Color(ThemeHelper.SubtleText),
                    new Sparkline([3, 5, 2, 8, 1, 4, 2, 1]).Width(120).Height(32),
                ]),
            ]));

    // ── HeatMapChart ─────────────────────────────────────────────────────

    static Node HeatMapSection() =>
        Section("HeatMapChart",
            "Grid heat map with intensity coloring.",
            new HeatMapChart(
            [
                new HeatMapCell(0, 0, 10), new HeatMapCell(1, 0, 25), new HeatMapCell(2, 0, 40),
                new HeatMapCell(0, 1, 55), new HeatMapCell(1, 1, 70), new HeatMapCell(2, 1, 85),
                new HeatMapCell(0, 2, 30), new HeatMapCell(1, 2, 50), new HeatMapCell(2, 2, 95),
            ]).Width(300).Height(200));

    // ── TreeMapChart ─────────────────────────────────────────────────────

    static Node TreeMapSection() =>
        Section("TreeMapChart",
            "Hierarchical tree map showing proportional areas.",
            new TreeMapChart(
            [
                new TreeMapNode("Frontend", 45),
                new TreeMapNode("Backend", 30),
                new TreeMapNode("DevOps", 15),
                new TreeMapNode("Design", 10),
            ]).Width(400).Height(200));

    // ── WaterfallChart ───────────────────────────────────────────────────

    static Node WaterfallSection() =>
        Section("WaterfallChart",
            "Waterfall chart showing incremental changes.",
            new WaterfallChart(
            [
                new WaterfallItem("Revenue", 500),
                new WaterfallItem("COGS", -200),
                new WaterfallItem("Expenses", -150),
                new WaterfallItem("Tax", -50),
                new WaterfallItem("Profit", 100, WaterfallItemType.Total),
            ]).Width(500).Height(250));

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}

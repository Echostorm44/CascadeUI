namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture view for the CLI integration suite. Renders a small, static set of
/// uniquely labeled controls that the tests assert against:
/// <list type="bullet">
///   <item>"CliFixtureTitle" — unique Label for find-by-label tests.</item>
///   <item>"UniqueFixtureButton" — unique Button; clicking increments the
///         counter Label ("Clicks: N"), proving real handlers fire.</item>
///   <item>A 300×240 ScrollView at the window center with 40 uniquely labeled
///         rows ("ScrollRow01"–"ScrollRow40") — the WP-3504 zero-sleep loop
///         test scrolls it repeatedly, so its content must be tall enough for
///         20 × 40 px scrolls and every scroll must change visible pixels.
///         The window center must stay inside the ScrollView: the default
///         scroll target of <c>cascade mcp scroll</c> is the client-area
///         center.</item>
///   <item>"DisabledFixtureButton" — a disabled Button for the WP-3515
///         find_nodes <c>disabled</c> filter test (after the ScrollView so the
///         window center stays inside it).</item>
/// </list>
/// No animation, no async content — every presented frame is identical until
/// an interaction changes state.
/// </summary>
internal sealed class FixtureView : Component
{
    private const int ScrollRowCount = 40;

    private int clickCount;

    private string ClickLabel => $"Clicks: {clickCount}";

    protected override Node Render()
    {
        return new Center(
            new Column(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children: new Node[]
                {
                    new Label("CliFixtureTitle").FontSize(20),
                    new Label(ClickLabel),
                    new Button("UniqueFixtureButton", onClick: () => { clickCount++; Invalidate(); }),
                    new ScrollView(ScrollContent()).Width(300).Height(240),
                    // WP-3515: a disabled control so the find_nodes `disabled`
                    // filter has something to match. Placed after the ScrollView
                    // so the window center stays inside the ScrollView (the
                    // WP-3504 zero-sleep scroll loop targets the client center).
                    new Button("DisabledFixtureButton", onClick: () => { }).Disabled(),
                }
            )
        );
    }

    private static Node ScrollContent()
    {
        var rows = new Node[ScrollRowCount];
        for (int i = 0; i < ScrollRowCount; i++)
        {
            rows[i] = new Label($"ScrollRow{i + 1:D2}");
        }

        return new Column(spacing: 8, children: rows);
    }
}

namespace Cascade.UI.WatchFixture;

/// <summary>
/// Minimal fixture view for the watch-loop integration test. Renders a static
/// label that can be edited to exercise hot reload and a counter that can be
/// changed structurally to force full rebuilds.
/// </summary>
internal sealed class WatchFixtureView : Component
{
    private int counter;

    // EDIT_MARKER: the test replaces this string each hot-reload cycle.
    private string StatusLabel => $"Watch fixture: {counter}";

    protected override Node Render()
    {
        return new Center(
            new Column(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children: new Node[]
                {
                    new Label("WatchFixtureTitle").FontSize(20),
                    new Label(StatusLabel),
                    new Button("Increment", onClick: () => { counter++; Invalidate(); }),
                }
            )
        );
    }
}

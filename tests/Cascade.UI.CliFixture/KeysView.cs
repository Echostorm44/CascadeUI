using Cascade.UI;

namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the CLI key tests (CASCADE_FIXTURE_VIEW=keys): a focused text field inside a
/// KeyHandler that logs every key-down and key-up to a label, so a test can see exactly which
/// events and characters a simulated key press produced.
/// </summary>
internal sealed class KeysView : Component
{
    private string text = "";
    private string log = "";

    protected override Node Render()
    {
        return new KeyHandler(new Column(spacing: 8, children:
            [
                new TextInput(Bind(text, v => { text = v; })).AutoFocus(),
                new Label($"Text: [{text}]"),
                new Label($"Keys: {log}"),
            ]))
        {
            KeyDown = e =>
            {
                log += $" down:{e.Modifiers}+{e.Key}";
                Invalidate();
                return false;
            },
            KeyUp = e =>
            {
                log += $" up:{e.Modifiers}+{e.Key}";
                Invalidate();
            },
        };
    }
}

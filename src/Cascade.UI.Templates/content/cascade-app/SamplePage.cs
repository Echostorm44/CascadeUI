using Cascade.UI;

namespace CascadeApp;

/// <summary>
/// A sample page demonstrating reactive state, two-way binding,
/// and basic Cascade UI patterns.
/// </summary>
public class SamplePage : Component
{
    private string name = "";
    private bool submitted;

    protected override Node Render()
    {
        if (submitted)
        {
            return new Column(
                spacing: 16,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label($"Hello, {name}!")
                        .FontSize(24),
                    new Button("Reset", () => { submitted = false; name = ""; })
                ]
            ).Padding(40);
        }

        return new Column(
            spacing: 16,
            children:
            [
                new Label("What is your name?")
                    .FontSize(18),
                new TextInput(Bind(ref name))
                    .Placeholder("Enter your name"),
                new Button("Say Hello", () => { submitted = true; })
                    .Disabled(name.Length == 0)
            ]
        ).Padding(40);
    }
}

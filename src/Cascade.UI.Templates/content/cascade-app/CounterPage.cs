using Cascade.UI;

namespace CascadeApp;

/// <summary>
/// A minimal counter demonstrating reactive state.
/// </summary>
public partial class CounterPage : Component
{
    private int count;

    protected override Node Render() =>
        new Column(
            spacing: 16,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
            [
                new Label($"{count}")
                    .FontSize(48),
                new Row(
                    spacing: 12,
                    children:
                    [
                        new Button("−", () => { count--; }),
                        new Button("+", () => { count++; })
                    ]
                )
            ]
        ).Padding(40);
}

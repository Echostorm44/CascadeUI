using Cascade.UI;

namespace CascadeApp;

/// <summary>
/// An empty starting page. Replace this with your own content.
/// </summary>
public class BlankPage : Component
{
    protected override Node Render() =>
        new Center(
            new Label("Welcome to Cascade UI")
                .FontSize(18)
        );
}

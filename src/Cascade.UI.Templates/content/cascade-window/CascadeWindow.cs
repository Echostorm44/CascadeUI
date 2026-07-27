using Cascade.UI;

namespace CascadeWindow.Namespace;

public class CascadeWindow : Component
{
    protected override Node Render() =>
        new Center(
            new Label("CascadeWindow")
                .FontSize(18)
        );
}

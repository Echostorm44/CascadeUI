using Cascade.UI;

namespace CascadeAppBlank;

public partial class MainWindow : Component
{
    protected override Node Render() =>
        new Center(
            new Label("Welcome to Cascade UI")
                .FontSize(18)
        );
}

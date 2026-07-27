using Cascade.UI;

namespace CascadePage.Namespace;

[Route("/cascade-page")]
public partial class CascadePage : Component
{
    protected override Node Render() =>
        Column(
            children: Label("CascadePage")
        );
}

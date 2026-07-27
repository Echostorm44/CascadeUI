using Cascade.UI;

namespace CascadeComponent.Namespace;

/// <summary>
/// A custom Cascade UI component. Override <see cref="Render"/> to define
/// the component's visual tree. Fields are automatically reactive —
/// use <c>readonly</c> to opt out.
/// </summary>
public partial class CascadeComponent : Component
{
    protected override Node Render() =>
        Label("CascadeComponent");
}

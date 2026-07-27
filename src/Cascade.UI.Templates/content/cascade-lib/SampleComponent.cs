using Cascade.UI;

namespace CascadeLib;

/// <summary>
/// A sample reusable component. Replace with your own components.
/// </summary>
public class SampleComponent : Component
{
    private string message = "Hello from CascadeLib";

    protected override Node Render() =>
        Label(message);
}

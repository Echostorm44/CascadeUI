using Cascade.UI;

namespace CascadeControls;

/// <summary>
/// A sample custom control. Replace with your own controls.
/// Controls are leaf nodes — they define visual output without
/// using Component/Render() lifecycle.
/// </summary>
public sealed class SampleControl : Node
{
    private readonly string label;

    public SampleControl(string label = "Sample Control")
    {
        this.label = label;
    }

    /// <summary>The display label.</summary>
    public string Label => label;
}

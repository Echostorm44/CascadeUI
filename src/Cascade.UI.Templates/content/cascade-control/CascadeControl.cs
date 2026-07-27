using Cascade.UI;

namespace CascadeControl.Namespace;

/// <summary>
/// A custom control. Controls are leaf nodes that define their own
/// visual representation. Use fluent extension methods for configuration.
/// </summary>
public sealed class CascadeControl : Node
{
    internal string label = "CascadeControl";

    public CascadeControl(string label = "CascadeControl")
    {
        this.label = label;
    }
}

public static class CascadeControlExtensions
{
    public static CascadeControl Label(this CascadeControl control, string label)
    {
        control.label = label;
        return control;
    }
}

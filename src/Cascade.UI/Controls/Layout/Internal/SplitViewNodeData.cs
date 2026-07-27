namespace Cascade.UI;

/// <summary>
/// Internal data storage for <see cref="SplitView"/> fluent modifiers.
/// Populated by <see cref="SplitViewExtensions"/> and consumed by the layout solver.
/// </summary>
internal sealed class SplitViewNodeData
{
    internal float? FirstSizePixels;
    internal SplitSize? FirstSizeDescriptor;
    internal float? FirstMinPixels;
    internal float? FirstMaxPixels;
    internal float? SecondMinPixels;
    internal float? SecondMaxPixels;
    internal Bindable<bool>? CollapseFirstBind;
    internal SplitCollapseButton CollapseButton = SplitCollapseButton.None;
    internal float CollapseThreshold;
    internal string? PersistKey;
}

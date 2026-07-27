namespace Cascade.UI;

/// <summary>
/// Internal data storage for <see cref="Expander"/> fluent modifiers.
/// Populated by <see cref="ExpanderExtensions"/> and consumed by the layout solver.
/// </summary>
internal sealed class ExpanderNodeData
{
    internal Action<bool>? OnToggleHandler;
}

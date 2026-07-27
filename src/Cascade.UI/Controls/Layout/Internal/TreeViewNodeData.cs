namespace Cascade.UI;

/// <summary>
/// Internal data storage for <see cref="TreeView{T}"/> fluent modifiers.
/// Populated by <see cref="TreeViewExtensions"/> and consumed by the layout solver.
/// </summary>
internal sealed class TreeViewNodeData
{
    internal float? FixedItemHeight;
    internal ItemHeight? ItemHeightStrategy;
    internal TreeSelectionMode SelectionMode = TreeSelectionMode.Single;
    internal Delegate? OnSelectHandler;
    internal Delegate? SelectedBind;
    internal Delegate? CheckedBind;
    internal bool TriStateCheckbox;
    internal Delegate? OnCheckChangeHandler;
    internal TreeEditTrigger? EditTrigger;
    internal Delegate? EditValidate;
    internal Delegate? EditCommit;
    internal Delegate? CanDrag;
    internal Delegate? CanDrop;
    internal Delegate? OnDrop;
    internal TreeDropIndicator DropIndicator = TreeDropIndicator.Line;
    internal Delegate? ContextMenuFactory;
    internal Delegate? OnDeleteHandler;
}

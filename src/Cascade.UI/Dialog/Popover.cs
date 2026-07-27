namespace Cascade.UI;

/// <summary>
/// Static methods for showing popovers. Popovers contain arbitrary Cascade
/// content anchored to a node. They are not async — the content component
/// communicates results through its props. Popovers share the dialog stack.
/// </summary>
public static class Popover
{
    internal sealed record PopoverRequest(
        Type ComponentType,
        Node? Anchor,
        Point? Position,
        PopoverOptions Options);

    internal static PopoverRequest? LastRequest { get; private set; }

    /// <summary>
    /// Shows a popover with a custom component anchored to a node.
    /// The popover dismisses when <see cref="Dialog.Dismiss"/> is called
    /// from within, when the user clicks outside (if <c>Dismissable = true</c>),
    /// or when the anchor node is unmounted.
    /// </summary>
    /// <typeparam name="TComponent">The popover content component type.</typeparam>
    /// <param name="anchor">The node to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    public static void Show<TComponent>(
        Node anchor,
        PopoverOptions? options = null)
        where TComponent : Component
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var resolved = options ?? new PopoverOptions();
        LastRequest = new PopoverRequest(typeof(TComponent), anchor, null, resolved);
    }

    /// <summary>
    /// Shows a popover at a specific screen position.
    /// </summary>
    /// <typeparam name="TComponent">The popover content component type.</typeparam>
    /// <param name="position">The screen position to anchor the popover to.</param>
    /// <param name="options">Popover configuration options.</param>
    public static void Show<TComponent>(
        Point position,
        PopoverOptions? options = null)
        where TComponent : Component
    {
        var resolved = options ?? new PopoverOptions();
        LastRequest = new PopoverRequest(typeof(TComponent), null, position, resolved);
    }
}

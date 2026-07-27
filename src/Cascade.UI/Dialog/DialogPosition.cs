namespace Cascade.UI;

/// <summary>
/// Controls where a dialog is positioned within the window.
/// </summary>
public class DialogPosition
{
    private DialogPosition()
    {
    }

    internal Node? Anchor { get; private init; }

    /// <summary>Centered in the window (default).</summary>
    public static DialogPosition Center { get; } = new();

    /// <summary>Slides up from the bottom edge — "bottom sheet" positioning.</summary>
    public static DialogPosition Bottom { get; } = new();

    /// <summary>Slides down from the top edge.</summary>
    public static DialogPosition Top { get; } = new();

    /// <summary>
    /// Positioned relative to a specific node's bounding box — foundation
    /// for popovers and context menus. Flips to the opposite side if there
    /// is insufficient space.
    /// </summary>
    /// <param name="anchor">The node to anchor the dialog to.</param>
    public static DialogPosition Anchored(Node anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        return new DialogPosition
        {
            Anchor = anchor
        };
    }
}

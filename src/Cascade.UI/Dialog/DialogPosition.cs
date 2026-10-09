namespace Cascade.UI;

/// <summary>
/// Controls where a dialog is positioned within the window.
/// </summary>
public class DialogPosition
{
    private DialogPosition(DialogPositionKind kind, Node? anchor = null)
    {
        Kind = kind;
        Anchor = anchor;
    }

    internal DialogPositionKind Kind { get; }

    internal Node? Anchor { get; }

    /// <summary>Centered in the window (default).</summary>
    public static DialogPosition Center { get; } = new(DialogPositionKind.Center);

    /// <summary>Against the bottom edge, horizontally centered — "bottom sheet" positioning.</summary>
    public static DialogPosition Bottom { get; } = new(DialogPositionKind.Bottom);

    /// <summary>Near the top edge, horizontally centered.</summary>
    public static DialogPosition Top { get; } = new(DialogPositionKind.Top);

    /// <summary>
    /// Positioned against a node's bounding box: below it, or above it when there is more room
    /// there, centered on it and kept inside the window. The node must be in the rendered tree
    /// when the dialog opens.
    /// </summary>
    /// <param name="anchor">The node to anchor the dialog to.</param>
    public static DialogPosition Anchored(Node anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        return new DialogPosition(DialogPositionKind.Anchored, anchor);
    }
}

/// <summary>Which <see cref="DialogPosition"/> a dialog uses.</summary>
internal enum DialogPositionKind
{
    Center,
    Bottom,
    Top,
    Anchored,
}

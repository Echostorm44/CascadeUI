namespace Cascade.UI;

/// <summary>
/// A thin bar at the bottom of the window showing contextual information.
/// Contains three zones (left, center, right) that accept arbitrary nodes.
/// </summary>
public sealed class StatusBar : Node
{
    public StatusBar(
        Node? left = null,
        Node? center = null,
        Node? right = null)
    {
        Left = left ?? Node.Empty;
        Center = center ?? Node.Empty;
        Right = right ?? Node.Empty;
    }

    /// <summary>Content for the left zone of the status bar.</summary>
    public Node Left { get; }

    /// <summary>Content for the center zone of the status bar.</summary>
    public Node Center { get; }

    /// <summary>Content for the right zone of the status bar.</summary>
    public Node Right { get; }
}

namespace Cascade.UI;

/// <summary>
/// A layout node that centers its single child within the available space.
/// </summary>
public sealed class Center : Node
{
    /// <summary>
    /// Creates a centering container.
    /// </summary>
    /// <param name="child">The node to center.</param>
    public Center(Node child)
    {
        Child = child;
    }

    /// <summary>The centered child node.</summary>
    public Node Child { get; }
}

namespace Cascade.UI;

/// <summary>
/// A layout node that layers its children in z-order — later children render
/// on top of earlier children. All children are positioned at the same origin
/// and sized independently. The Stack's own size is the maximum of its
/// children's sizes unless an explicit size is given.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix — Stack is the correct layout term
public sealed class Stack : Node
#pragma warning restore CA1711
{
    /// <summary>
    /// Creates a z-order stacking container.
    /// </summary>
    /// <param name="children">Child nodes layered from bottom to top.</param>
    public Stack(params Node[] children)
    {
        Children = children;
    }

    /// <summary>The child nodes, ordered from bottom to top.</summary>
    public IReadOnlyList<Node> Children { get; }
}

namespace Cascade.UI;

/// <summary>
/// A layout node that absorbs remaining space in a Row or Column.
/// Behaves like a flex grow item with no visual content.
/// </summary>
public sealed class Spacer : Node
{
    /// <summary>
    /// Creates a spacer.
    /// </summary>
    /// <param name="minSize">Minimum size in the main axis direction (default 0).</param>
    public Spacer(float minSize = 0)
    {
        MinSize = minSize;
    }

    /// <summary>Minimum size in logical pixels along the main axis.</summary>
    public float MinSize { get; }
}

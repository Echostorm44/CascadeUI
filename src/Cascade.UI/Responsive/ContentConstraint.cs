namespace Cascade.UI;

/// <summary>
/// Constrains content to a maximum width, optionally centering it in the
/// available space. Prevents text and cards from stretching uncomfortably wide
/// on large displays.
/// </summary>
public class ContentConstraint : Component
{
    private readonly float? maxWidth;
    private readonly Alignment alignment;
    private readonly Node child;
    private readonly Node renderedTree;

    /// <summary>
    /// Creates a new <see cref="ContentConstraint"/>.
    /// </summary>
    /// <param name="child">The content to constrain.</param>
    /// <param name="maxWidth">
    /// Maximum width in logical pixels, or <c>null</c> for no constraint (full width).
    /// </param>
    /// <param name="alignment">
    /// Alignment of the constrained content within available space.
    /// Defaults to <see cref="Alignment.TopCenter"/>.
    /// </param>
    public ContentConstraint(
        Node child,
        float? maxWidth = null,
        Alignment alignment = default)
    {
        this.child = child;
        this.maxWidth = maxWidth;
        this.alignment = alignment == default ? Alignment.TopCenter : alignment;

        if (maxWidth == null)
        {
            renderedTree = child;
        }
        else
        {
            var crossAxis = this.alignment.X > 0 ? CrossAxisAlignment.End
                : this.alignment.X == 0f ? CrossAxisAlignment.Center
                : CrossAxisAlignment.Start;
            renderedTree = new Column(
                crossAxisAlignment: crossAxis,
                children: [child]
            ).MaxWidth(maxWidth.Value);
        }
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        return renderedTree;
    }
}

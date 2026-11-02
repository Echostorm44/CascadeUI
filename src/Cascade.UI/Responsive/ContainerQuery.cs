namespace Cascade.UI;

/// <summary>
/// Size information about a container, provided to <see cref="ContainerQuery"/>
/// builders for container-based responsive behavior.
/// </summary>
public readonly record struct ContainerInfo
{
    /// <summary>
    /// Size of the container in logical pixels.
    /// </summary>
    public Size Size { get; init; }

    /// <summary>
    /// Container width in logical pixels.
    /// </summary>
    public float Width => Size.Width;

    /// <summary>
    /// Container height in logical pixels.
    /// </summary>
    public float Height => Size.Height;
}

/// <summary>
/// A component that adapts its content based on the size of its parent container
/// rather than the window. Use when the same component appears at different sizes
/// within the same window (e.g., sidebar vs. main content area).
/// </summary>
public class ContainerQuery : Component
{
    private readonly Func<ContainerInfo, Node> builder;

    /// <summary>
    /// Creates a new <see cref="ContainerQuery"/> that evaluates the
    /// <paramref name="builder"/> with the container's measured size.
    /// </summary>
    /// <param name="builder">
    /// A function that receives container size information and returns the
    /// appropriate node for that size.
    /// </param>
    public ContainerQuery(Func<ContainerInfo, Node> builder)
    {
        this.builder = builder;
    }

    // The size layout last gave this container. Layout runs after render, so the first render
    // builds with no size (nothing is known yet on first mount) and the frame after layout builds
    // again with the real one; every resize does the same. Render reads Bounds, which always
    // equals this once layout has run, so it is never written there.
    private Size laidOutSize = new(-1f, -1f);

    /// <inheritdoc/>
    protected override Node Render()
    {
        return builder(new ContainerInfo { Size = new Size(Bounds.Width, Bounds.Height) });
    }

    internal override void OnLayoutBoundsUpdated(Rect current)
    {
        if (current.Width == laidOutSize.Width && current.Height == laidOutSize.Height)
        {
            return;
        }

        laidOutSize = new Size(current.Width, current.Height);
        Invalidate();
    }
}

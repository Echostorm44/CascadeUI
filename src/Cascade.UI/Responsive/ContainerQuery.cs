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

    /// <inheritdoc/>
    protected override Node Render()
    {
        var info = new ContainerInfo { Size = new Size(Bounds.Width, Bounds.Height) };
        return builder(info);
    }
}

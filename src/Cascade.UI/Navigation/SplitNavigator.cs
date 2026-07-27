namespace Cascade.UI;

/// <summary>
/// A multi-pane navigation container that divides the screen into columns,
/// each with its own independent navigation stack. Common layouts include
/// two-pane master/detail and three-pane sidebar/list/detail.
/// </summary>
/// <remarks>
/// <code>
/// SplitNavigator(
///     panes: [
///         new Navigator(initialPage: new OrderListPage()),
///         new Navigator(initialPage: new OrderDetailPlaceholder())
///     ],
///     options: new SplitNavigatorOptions { Ratios = [0.4f] }
/// )
/// </code>
/// </remarks>
public class SplitNavigator : Component
{
    private readonly IReadOnlyList<Navigator> panes;
    private readonly SplitNavigatorOptions options;
    private readonly SplitPaneManager paneManager;
    private readonly Row cachedRow;

    /// <summary>
    /// Creates a split navigator with the specified panes and layout options.
    /// </summary>
    /// <param name="panes">The navigator instances for each pane.</param>
    /// <param name="options">Layout and behavior options for the split view.</param>
    public SplitNavigator(IReadOnlyList<Navigator> panes, SplitNavigatorOptions? options = null)
    {
        this.panes = panes;
        this.options = options ?? new SplitNavigatorOptions();
        paneManager = new SplitPaneManager(panes);

        var children = new Node[panes.Count];
        for (int i = 0; i < panes.Count; i++)
        {
            children[i] = panes[i];
        }

        cachedRow = new Row(children: children);
    }

    /// <summary>
    /// Accesses a pane's navigator by index. Use named properties in the
    /// owning component to avoid magic numbers.
    /// </summary>
    /// <param name="index">Zero-based pane index.</param>
    public INavigator this[int index]
    {
        get { return paneManager.GetPane(index); }
    }

    /// <summary>The number of panes in this split navigator.</summary>
    public int PaneCount => panes.Count;

    /// <summary>The index of the currently active pane.</summary>
    public int ActivePaneIndex => paneManager.ActivePaneIndex;

    /// <summary>
    /// Sets which pane is currently active (has focus).
    /// </summary>
    public void SetActivePane(int index)
    {
        paneManager.SetActivePane(index);
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        return cachedRow;
    }
}

/// <summary>
/// Configuration options for <see cref="SplitNavigator"/> layout and behavior.
/// </summary>
public class SplitNavigatorOptions
{
    /// <summary>
    /// Column proportions. Fewer entries than panes means the remainder
    /// is allocated to the last pane. Null or empty means even split.
    /// </summary>
    public float[]? Ratios { get; init; }

    /// <summary>
    /// Show a drag handle between panes for user resizing. Default: false.
    /// </summary>
    public bool Resizable { get; init; }

    /// <summary>
    /// Window width (logical pixels) below which the layout collapses
    /// to a single column. Null means never collapse.
    /// </summary>
    public float? AdaptiveBreakpoint { get; init; }
}

/// <summary>
/// Holds a typed reference to a <see cref="SplitNavigator"/>, providing
/// indexed access to each pane's navigator. Attach via the <c>ref</c>
/// parameter when constructing a SplitNavigator in Render().
/// </summary>
public sealed class SplitNavigatorRef
{
    private SplitNavigator? target;

    /// <summary>
    /// Binds this ref to a SplitNavigator instance.
    /// </summary>
    internal void Bind(SplitNavigator navigator)
    {
        target = navigator;
    }

    /// <summary>
    /// Accesses a pane's navigator by index.
    /// </summary>
    /// <param name="index">Zero-based pane index.</param>
    public INavigator this[int index]
    {
        get
        {
            if (target is null)
            {
                throw new InvalidOperationException(
                    "SplitNavigatorRef is not bound to a SplitNavigator.");
            }

            return target[index];
        }
    }
}

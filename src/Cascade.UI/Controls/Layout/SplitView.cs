namespace Cascade.UI;

/// <summary>
/// Orientation of the split divider.
/// </summary>
public enum SplitOrientation
{
    /// <summary>Side-by-side panes (left/right), divider is vertical.</summary>
    Horizontal,

    /// <summary>Stacked panes (top/bottom), divider is horizontal.</summary>
    Vertical
}

/// <summary>
/// Style for the pane collapse button on the divider.
/// </summary>
public enum SplitCollapseButton
{
    /// <summary>No collapse button — collapse is purely programmatic.</summary>
    None,

    /// <summary>A small arrow button sits on the divider.</summary>
    OnDivider
}

/// <summary>
/// Describes the initial size of a <see cref="SplitView"/> pane.
/// </summary>
public sealed class SplitSize
{
    private SplitSize(SplitSizeKind kind, float value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>The kind of size descriptor.</summary>
    internal SplitSizeKind Kind { get; }

    /// <summary>The fractional value (0..1) when <see cref="Kind"/> is <see cref="SplitSizeKind.Fraction"/>.</summary>
    internal float Value { get; }

    /// <summary>The pane takes all remaining space (the other pane uses its minimum).</summary>
    public static SplitSize Fill { get; } = new(SplitSizeKind.Fill, 0);

    /// <summary>The pane takes a fraction of the total available space.</summary>
    public static SplitSize Fraction(float value)
    {
        if (value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Fraction must be between 0 and 1.");
        }

        return new SplitSize(SplitSizeKind.Fraction, value);
    }
}

/// <summary>
/// Identifies the kind of a <see cref="SplitSize"/> descriptor.
/// </summary>
internal enum SplitSizeKind
{
    /// <summary>Fill remaining space.</summary>
    Fill,

    /// <summary>Fraction of the total available space.</summary>
    Fraction
}

/// <summary>
/// Two panes divided by a draggable splitter. Supports horizontal and vertical
/// orientations, min/max constraints, collapsible panes, and layout persistence.
/// </summary>
public sealed class SplitView : Node
{
    public SplitView(
        Node first,
        Node second,
        SplitOrientation orientation = SplitOrientation.Horizontal)
    {
        First = first;
        Second = second;
        Orientation = orientation;
    }

    /// <summary>Content of the first (left or top) pane.</summary>
    public Node First { get; }

    /// <summary>Content of the second (right or bottom) pane.</summary>
    public Node Second { get; }

    /// <summary>Whether the split is horizontal (left/right) or vertical (top/bottom).</summary>
    public SplitOrientation Orientation { get; }

    /// <summary>Window-space bounds, set by the painter each frame for drag hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Layout traversal index assigned by LayoutSolver for drag state tracking.</summary>
    internal int LayoutIndex { get; set; }
}

/// <summary>
/// Fluent extension methods for <see cref="SplitView"/>.
/// </summary>
public static class SplitViewExtensions
{
    /// <summary>Sets the initial pixel size of the first pane.</summary>
    public static SplitView FirstSize(this SplitView view, float pixels)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.FirstSizePixels = pixels;
        return view;
    }

    /// <summary>Sets the initial size of the first pane using a <see cref="SplitSize"/> descriptor.</summary>
    public static SplitView FirstSize(this SplitView view, SplitSize size)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(size);
        var data = EnsureSplitData(view);
        data.FirstSizeDescriptor = size;
        return view;
    }

    /// <summary>Sets the minimum pixel width/height of the first pane.</summary>
    public static SplitView FirstMin(this SplitView view, float pixels)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.FirstMinPixels = pixels;
        return view;
    }

    /// <summary>Sets the maximum pixel width/height of the first pane.</summary>
    public static SplitView FirstMax(this SplitView view, float pixels)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.FirstMaxPixels = pixels;
        return view;
    }

    /// <summary>Sets the minimum pixel width/height of the second pane.</summary>
    public static SplitView SecondMin(this SplitView view, float pixels)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.SecondMinPixels = pixels;
        return view;
    }

    /// <summary>Sets the maximum pixel width/height of the second pane.</summary>
    public static SplitView SecondMax(this SplitView view, float pixels)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.SecondMaxPixels = pixels;
        return view;
    }

    /// <summary>Enables collapsing the first pane with optional threshold and button style.</summary>
    public static SplitView CollapseFirst(
        this SplitView view,
        Bindable<bool> collapsed,
        SplitCollapseButton collapseButton = SplitCollapseButton.None,
        float threshold = 0)
    {
        ArgumentNullException.ThrowIfNull(view);
        var data = EnsureSplitData(view);
        data.CollapseFirstBind = collapsed;
        data.CollapseButton = collapseButton;
        data.CollapseThreshold = threshold;
        return view;
    }

    /// <summary>Persists the splitter position and collapsed state to local storage under the given key.</summary>
    public static SplitView PersistLayout(this SplitView view, string key)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(key);
        var data = EnsureSplitData(view);
        data.PersistKey = key;
        return view;
    }

    private static SplitViewNodeData EnsureSplitData(SplitView view)
    {
        view.LayoutData.SplitData ??= new SplitViewNodeData();
        return view.LayoutData.SplitData;
    }
}

namespace Cascade.UI;

/// <summary>
/// Position and index information provided to a drop target's onDrop callback.
/// </summary>
public record DropPosition
{
    /// <summary>Pixel coordinates relative to the drop target's origin.</summary>
    public required Point Point { get; init; }

    /// <summary>For list-style drop targets, the insertion index between items.</summary>
    public required int Index { get; init; }
}

/// <summary>
/// Visual feedback style applied to a drop target during drag-over.
/// </summary>
public enum DragFeedbackKind
{
    /// <summary>The target area is highlighted on drag-over.</summary>
    Highlight,

    /// <summary>A border is added around the target on drag-over.</summary>
    Border,

    /// <summary>No automatic visual feedback.</summary>
    None
}

/// <summary>
/// Represents a file dropped from the operating system's file manager.
/// </summary>
public record OsDroppedFile(string Path, string Name, string Extension, long SizeBytes);

/// <summary>
/// Data payload received when files are dragged from the OS into a Cascade app.
/// </summary>
public record OsFileDropData(IReadOnlyList<OsDroppedFile> Files);

/// <summary>
/// A file to export during an OS drag-out operation.
/// </summary>
public record OsExportFile(string Path);

/// <summary>
/// Configuration for dragging content out of a Cascade app to the OS.
/// </summary>
public sealed class OsDragExport
{
    /// <summary>Physical files to export.</summary>
    public IReadOnlyList<OsExportFile>? Files { get; init; }
}

/// <summary>
/// Reactive state available during a drag operation, accessible from any Render() method.
/// </summary>
public static class DragState
{
    private static readonly object stateLock = new();
    private static object? draggedData;
    private static Type? draggedType;
    private static Node? overNode;

    /// <summary>Whether a drag is currently in progress anywhere in the application.</summary>
    public static bool IsDragging
    {
        get
        {
            lock (stateLock)
            {
                return draggedData is not null;
            }
        }
    }

    /// <summary>The data payload of the current drag, or null if no drag is in progress.</summary>
    public static object? DraggedData
    {
        get
        {
            lock (stateLock)
            {
                return draggedData;
            }
        }
    }

    /// <summary>The runtime type of the dragged data, or null if no drag is in progress.</summary>
    public static Type? DraggedType
    {
        get
        {
            lock (stateLock)
            {
                return draggedType;
            }
        }
    }

    /// <summary>Returns true if the drag pointer is currently over the specified node.</summary>
    public static bool IsOver<T>(NodeRef<T> nodeRef) where T : Node
    {
        ArgumentNullException.ThrowIfNull(nodeRef);

        lock (stateLock)
        {
            return nodeRef.Node is not null && ReferenceEquals(nodeRef.Node, overNode);
        }
    }

    internal static void BeginDrag(object? data, Node? initialOverNode = null)
    {
        lock (stateLock)
        {
            draggedData = data;
            draggedType = data?.GetType();
            overNode = initialOverNode;
        }
    }

    internal static void UpdateDragOver(Node? node)
    {
        lock (stateLock)
        {
            overNode = node;
        }
    }

    internal static void EndDrag()
    {
        lock (stateLock)
        {
            draggedData = null;
            draggedType = null;
            overNode = null;
        }
    }

    internal static void Reset()
    {
        EndDrag();
    }
}

/// <summary>
/// Extension methods for drag-and-drop on any <see cref="Node"/>.
/// </summary>
public static class DragDropExtensions
{
    /// <summary>Makes a node draggable with the specified data payload.</summary>
    public static T Draggable<T>(this T node, object data) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var dragData = EnsureDragData(node);
        dragData.IsDraggable = true;
        dragData.Payload = data;
        dragData.PayloadType = data?.GetType();
        return node;
    }

    /// <summary>
    /// Marks this node as the drag handle for its parent draggable.
    /// Only pointer-down on this node initiates a drag.
    /// </summary>
    public static T DragHandle<T>(this T node) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var dragData = EnsureDragData(node);
        dragData.IsHandle = true;
        return node;
    }

    /// <summary>Overrides the default drag preview with a custom node.</summary>
    public static T DragPreview<T>(this T node, Node preview) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(preview);

        var dragData = EnsureDragData(node);
        dragData.Preview = preview;
        return node;
    }

    /// <summary>Enables dragging content out of the app to the OS file manager.</summary>
    public static T DragToOs<T>(this T node, Func<OsDragExport> export) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(export);

        var dragData = EnsureDragData(node);
        dragData.ExportFactory = export;
        return node;
    }

    /// <summary>Designates a node as a drop target that accepts dragged data.</summary>
    public static T DropTarget<T>(
        this T node,
        Func<object, bool> accepts,
        Action<object, DropPosition> onDrop) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(accepts);
        ArgumentNullException.ThrowIfNull(onDrop);

        var dragData = EnsureDragData(node);
        dragData.IsDropTarget = true;
        dragData.Accepts = accepts;
        dragData.OnDrop = onDrop;
        return node;
    }

    /// <summary>Sets the visual feedback style shown on the drop target during drag-over.</summary>
    public static T DropFeedback<T>(this T node, DragFeedbackKind feedback) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var dragData = EnsureDragData(node);
        dragData.Feedback = feedback;
        dragData.FeedbackBorderColor = null;
        dragData.FeedbackBorderWidth = 0f;
        return node;
    }

    /// <summary>Sets a custom border color and width for <see cref="DragFeedbackKind.Border"/> feedback.</summary>
    public static T DropFeedback<T>(this T node, ColorValue color, float width) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var dragData = EnsureDragData(node);
        dragData.Feedback = DragFeedbackKind.Border;
        dragData.FeedbackBorderColor = color;
        dragData.FeedbackBorderWidth = width;
        return node;
    }

    /// <summary>Provides a fully custom node replacement for drag-over feedback.</summary>
    public static T DropFeedbackCustom<T>(this T node, Func<bool, Node> builder) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(builder);

        var dragData = EnsureDragData(node);
        dragData.CustomFeedbackBuilder = builder;
        return node;
    }

    private static DragNodeData EnsureDragData(Node node)
    {
        node.LayoutData.DragData ??= new DragNodeData();
        return node.LayoutData.DragData;
    }
}

internal sealed class DragNodeData
{
    internal bool IsDraggable;
    internal bool IsHandle;
    internal object? Payload;
    internal Type? PayloadType;
    internal Node? Preview;
    internal Func<OsDragExport>? ExportFactory;
    internal bool IsDropTarget;
    internal Func<object, bool>? Accepts;
    internal Action<object, DropPosition>? OnDrop;
    internal DragFeedbackKind Feedback = DragFeedbackKind.None;
    internal ColorValue? FeedbackBorderColor;
    internal float FeedbackBorderWidth;
    internal Func<bool, Node>? CustomFeedbackBuilder;

    /// <summary>
    /// Absolute bounds set during paint pass — used for drag overlay rendering.
    /// </summary>
    internal Rect AbsoluteBounds;
}

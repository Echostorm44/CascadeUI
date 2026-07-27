using System.Collections.Generic;

namespace Cascade.UI;

/// <summary>
/// Direction of a completed swipe gesture.
/// </summary>
public enum SwipeDirection
{
    /// <summary>Swipe from right to left.</summary>
    Left,

    /// <summary>Swipe from left to right.</summary>
    Right,

    /// <summary>Swipe from bottom to top.</summary>
    Up,

    /// <summary>Swipe from top to bottom.</summary>
    Down
}

/// <summary>
/// Event data for low-level pointer events (pointer down, move, up).
/// All positions are in logical pixels relative to the node's origin.
/// </summary>
public record PointerEventArgs
{
    /// <summary>Current pointer position relative to the target node.</summary>
    public required Point Position { get; init; }

    /// <summary>Movement delta since the last pointer event.</summary>
    public required Point Delta { get; init; }

    /// <summary>Pressure level (1.0 = normal, 0.0-1.0 range for stylus).</summary>
    public float Pressure { get; init; } = 1f;

    /// <summary>Stylus tilt angle in degrees (0 = perpendicular to surface).</summary>
    public float Tilt { get; init; }
}

/// <summary>
/// Extension methods for gesture recognition on any <see cref="Node"/>.
/// </summary>
public static class GestureExtensions
{
    /// <summary>Registers a tap (click) handler.</summary>
    public static T OnTap<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.Tap = handler;
        return node;
    }

    /// <summary>Registers a double-tap (double-click) handler.</summary>
    public static T OnDoubleTap<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.DoubleTap = handler;
        return node;
    }

    /// <summary>Registers a long-press handler.</summary>
    public static T OnLongPress<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.LongPress = handler;
        return node;
    }

    /// <summary>Registers a swipe gesture handler for a specific direction.</summary>
    public static T OnSwipe<T>(this T node, SwipeDirection direction, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.SwipeHandlers[direction] = handler;
        return node;
    }

    /// <summary>Registers a continuous pan (drag) handler that fires during movement.</summary>
    public static T OnPan<T>(this T node, Action<Point> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.Pan = handler;
        return node;
    }

    /// <summary>Registers a pinch (scale) gesture handler.</summary>
    public static T OnPinch<T>(this T node, Action<float> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.Pinch = handler;
        return node;
    }

    /// <summary>
    /// Registers a context menu handler. Unifies right-click (desktop) and
    /// long-press (touch) into a single cross-platform trigger.
    /// </summary>
    public static T OnContextMenu<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.ContextMenu = handler;
        return node;
    }

    /// <summary>Registers a low-level pointer-down handler.</summary>
    public static T OnPointerDown<T>(this T node, Action<PointerEventArgs> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.PointerDown = handler;
        return node;
    }

    /// <summary>Registers a low-level pointer-move handler.</summary>
    public static T OnPointerMove<T>(this T node, Action<PointerEventArgs> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.PointerMove = handler;
        return node;
    }

    /// <summary>Registers a low-level pointer-up handler.</summary>
    public static T OnPointerUp<T>(this T node, Action<PointerEventArgs> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.PointerUp = handler;
        return node;
    }

    /// <summary>Registers a handler invoked when the pointer enters this node.</summary>
    public static T OnPointerEnter<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.PointerEnter = handler;
        return node;
    }

    /// <summary>Registers a handler invoked when the pointer leaves this node.</summary>
    public static T OnPointerLeave<T>(this T node, Action handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.PointerLeave = handler;
        return node;
    }

    /// <summary>Registers a custom scroll event handler on this node.</summary>
    public static T OnScroll<T>(this T node, Action<Point> handler) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(handler);
        var data = EnsureGestureData(node);
        data.Scroll = handler;
        return node;
    }

    private static GestureNodeData EnsureGestureData(Node node)
    {
        node.LayoutData.GestureData ??= new GestureNodeData();
        return node.LayoutData.GestureData;
    }
}

/// <summary>
/// Rich gesture event data describing the pointer position, delta, scale, and velocity.
/// </summary>
public sealed record class GestureInfo(Point Position, Point Delta, float Scale, Point Velocity);

internal sealed class GestureNodeData
{
    internal Action? Tap;
    internal Action? DoubleTap;
    internal Action? LongPress;
    internal Dictionary<SwipeDirection, Action> SwipeHandlers { get; } = new();
    internal Action<Point>? Pan;
    internal Action<float>? Pinch;
    internal Action? ContextMenu;
    internal Action<PointerEventArgs>? PointerDown;
    internal Action<PointerEventArgs>? PointerMove;
    internal Action<PointerEventArgs>? PointerUp;
    internal Action? PointerEnter;
    internal Action? PointerLeave;
    internal Action<Point>? Scroll;
}

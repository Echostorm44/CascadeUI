using System.Collections.Generic;

namespace Cascade.UI;

/// <summary>
/// Tracks the currently focused node and provides programmatic focus traversal.
/// </summary>
public static class FocusManager
{
    private static readonly object focusLock = new();
    private static readonly List<WeakReference<Node>> focusableNodes = new();
    private static long registrationCounter;
    private static Node? focusedElement;

    /// <summary>
    /// True when the most recent focus change was caused by keyboard navigation (Tab/Shift+Tab).
    /// Used by the animation system to show focus rings only for keyboard users.
    /// </summary>
    public static bool LastFocusWasKeyboard { get; internal set; }

    /// <summary>The node that currently holds focus, or null when nothing is focused.</summary>
    public static Node? FocusedElement
    {
        get
        {
            lock (focusLock)
            {
                return focusedElement;
            }
        }
    }

    /// <summary>Requests focus for the specified node.</summary>
    public static void RequestFocus(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node? previous;
        lock (focusLock)
        {
            previous = focusedElement;
            focusedElement = node;
            RegisterFocusableUnsafe(node);
        }

        // Mouse-initiated focus — don't show focus ring
        LastFocusWasKeyboard = false;

        NotifyFocusChanged(previous, false);
        if (!ReferenceEquals(previous, node))
        {
            NotifyFocusChanged(node, true);
        }
    }

    /// <summary>Clears focus, notifying the previously focused node if necessary.</summary>
    public static void ClearFocus()
    {
        Node? previous;
        lock (focusLock)
        {
            previous = focusedElement;
            focusedElement = null;
        }

        NotifyFocusChanged(previous, false);
    }

    /// <summary>
    /// Moves focus in the specified direction. Returns true when a new node is focused.
    /// </summary>
    public static bool MoveFocus(FocusDirection direction)
    {
        Node? target;

        lock (focusLock)
        {
            var ordered = GetOrderedFocusableNodesUnsafe();
            if (ordered.Count == 0)
            {
                return false;
            }

            var current = focusedElement;
            target = direction switch
            {
                FocusDirection.Previous or FocusDirection.Up or FocusDirection.Left
                    => GetPreviousNode(ordered, current),
                _ => GetNextNode(ordered, current)
            };
        }

        if (target is null)
        {
            return false;
        }

        LastFocusWasKeyboard = true;
        RequestFocusInternal(target);
        return true;
    }

    internal static void RegisterFocusable(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        lock (focusLock)
        {
            RegisterFocusableUnsafe(node);
        }
    }

    /// <summary>
    /// Internal focus request that does not reset <see cref="LastFocusWasKeyboard"/>.
    /// Used by <see cref="MoveFocus"/> which sets the flag before calling this.
    /// </summary>
    private static void RequestFocusInternal(Node node)
    {
        Node? previous;
        lock (focusLock)
        {
            previous = focusedElement;
            focusedElement = node;
            RegisterFocusableUnsafe(node);
        }

        NotifyFocusChanged(previous, false);
        if (!ReferenceEquals(previous, node))
        {
            NotifyFocusChanged(node, true);
        }
    }

    internal static void Reset()
    {
        lock (focusLock)
        {
            focusedElement = null;
            focusableNodes.Clear();
            registrationCounter = 0;
        }
    }

    /// <summary>
    /// Called by the reconciler when a node instance is replaced by a new instance
    /// of the same type at the same tree position. Transfers focus and focusable
    /// registration from the old node to the replacement so that focus survives
    /// tree rebuilds triggered by <see cref="Component.Invalidate"/>.
    /// </summary>
    internal static void NotifyNodeReplaced(Node oldNode, Node replacement)
    {
        lock (focusLock)
        {
            if (ReferenceEquals(focusedElement, oldNode))
            {
                focusedElement = replacement;
            }

            for (int i = 0; i < focusableNodes.Count; i++)
            {
                if (focusableNodes[i].TryGetTarget(out var target)
                    && ReferenceEquals(target, oldNode))
                {
                    focusableNodes[i] = new WeakReference<Node>(replacement);

                    // Transfer focus data (tab index, registration order)
                    if (oldNode.LayoutData.FocusData is not null)
                    {
                        replacement.LayoutData.FocusData = oldNode.LayoutData.FocusData;
                    }

                    break;
                }
            }
        }
    }

    private static void RegisterFocusableUnsafe(Node node)
    {
        CleanupUnsafe();

        foreach (var reference in focusableNodes)
        {
            if (reference.TryGetTarget(out var existing) && ReferenceEquals(existing, node))
            {
                EnsureFocusData(node).RegistrationOrder = ++registrationCounter;
                return;
            }
        }

        focusableNodes.Add(new WeakReference<Node>(node));
        EnsureFocusData(node).RegistrationOrder = ++registrationCounter;
    }

    private static void CleanupUnsafe()
    {
        focusableNodes.RemoveAll(reference => !reference.TryGetTarget(out _));
    }

    private static List<Node> GetOrderedFocusableNodesUnsafe()
    {
        CleanupUnsafe();

        var result = new List<Node>();
        foreach (var reference in focusableNodes)
        {
            if (!reference.TryGetTarget(out var node))
            {
                continue;
            }

            var data = node.LayoutData.FocusData;
            if (data is null || !data.IsFocusable)
            {
                continue;
            }

            result.Add(node);
        }

        result.Sort(CompareFocusOrder);
        return result;
    }

    private static int CompareFocusOrder(Node left, Node right)
    {
        var leftData = left.LayoutData.FocusData;
        var rightData = right.LayoutData.FocusData;

        var leftValue = leftData?.TabIndexValue ?? TabIndex.Natural.Value;
        var rightValue = rightData?.TabIndexValue ?? TabIndex.Natural.Value;

        var leftPriority = leftValue > 0 ? 0 : 1;
        var rightPriority = rightValue > 0 ? 0 : 1;

        if (leftPriority != rightPriority)
        {
            return leftPriority.CompareTo(rightPriority);
        }

        if (leftValue != rightValue)
        {
            return leftValue.CompareTo(rightValue);
        }

        var leftOrder = leftData?.RegistrationOrder ?? 0;
        var rightOrder = rightData?.RegistrationOrder ?? 0;
        return leftOrder.CompareTo(rightOrder);
    }

    private static Node? GetNextNode(IReadOnlyList<Node> nodes, Node? current)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        if (current is null)
        {
            return nodes[0];
        }

        var index = IndexOf(nodes, current);
        if (index < 0)
        {
            return nodes[0];
        }

        if (nodes.Count == 1)
        {
            return nodes[0];
        }

        var nextIndex = (index + 1) % nodes.Count;
        return nodes[nextIndex];
    }

    private static Node? GetPreviousNode(IReadOnlyList<Node> nodes, Node? current)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        if (nodes.Count == 1)
        {
            return nodes[0];
        }

        if (current is null)
        {
            return nodes[^1];
        }

        var index = IndexOf(nodes, current);
        if (index < 0)
        {
            return nodes[^1];
        }

        var previousIndex = (index - 1 + nodes.Count) % nodes.Count;
        return nodes[previousIndex];
    }

    private static int IndexOf(IReadOnlyList<Node> nodes, Node target)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target))
            {
                return i;
            }
        }

        return -1;
    }

    private static void NotifyFocusChanged(Node? node, bool isFocused)
    {
        if (node is null)
        {
            return;
        }

        var data = node.LayoutData.FocusData;
        data?.FocusChanged?.Invoke(isFocused);
    }

    private static FocusNodeData EnsureFocusData(Node node)
    {
        node.LayoutData.FocusData ??= new FocusNodeData();
        return node.LayoutData.FocusData;
    }
}

/// <summary>
/// Represents a tab-order value for focus traversal.
/// </summary>
public readonly record struct TabIndex
{
    /// <summary>The numeric tab order value.</summary>
    public int Value { get; }

    /// <summary>Creates a tab index with the specified order value.</summary>
    public TabIndex(int value)
    {
        Value = value;
    }

    /// <summary>Removes the node from the tab order entirely.</summary>
    public static TabIndex Skip { get; } = new(-1);

    /// <summary>Default tab order following document order.</summary>
    public static TabIndex Natural { get; } = new(0);

    /// <summary>Implicit conversion from int for convenient inline usage.</summary>
#pragma warning disable CA2225
    public static implicit operator TabIndex(int value)
#pragma warning restore CA2225
    {
        return new TabIndex(value);
    }
}

/// <summary>
/// A binding between a keyboard shortcut and an action handler.
/// </summary>
public record KeyBinding(Hotkey Hotkey, Action Handler, bool When = true);

/// <summary>
/// A layout-transparent wrapper that captures keyboard shortcuts. Shortcuts fire
/// when focus is anywhere within the wrapped content.
/// </summary>
public sealed class KeyHandler : Node
{
    public KeyHandler(Node content, params KeyBinding[] bindings)
    {
        Content = content;
        Bindings = bindings;
    }

    /// <summary>The wrapped content node that scopes the keyboard shortcuts.</summary>
    public Node Content { get; }

    /// <summary>The keyboard shortcut bindings active within this scope.</summary>
    public IReadOnlyList<KeyBinding> Bindings { get; }
}

/// <summary>
/// Extension methods for focus management on any <see cref="Node"/>.
/// </summary>
public static class FocusExtensions
{
    /// <summary>Sets the tab order for this node in focus traversal.</summary>
    public static T TabIndex<T>(this T node, TabIndex index) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var data = EnsureFocusData(node);
        data.TabIndexValue = index.Value;
        FocusManager.RegisterFocusable(node);
        return node;
    }

    /// <summary>Automatically focuses this node when it is mounted.</summary>
    public static T AutoFocus<T>(this T node, bool autoFocus = true) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var data = EnsureFocusData(node);
        data.AutoFocus = autoFocus;
        if (autoFocus)
        {
            FocusManager.RegisterFocusable(node);
        }

        return node;
    }

    /// <summary>
    /// Traps focus within this node's subtree. Tab and Shift+Tab cycle
    /// within the node rather than escaping to the rest of the page.
    /// </summary>
    public static T FocusTrap<T>(this T node, bool trap = true) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var data = EnsureFocusData(node);
        data.FocusTrap = trap;
        return node;
    }

    /// <summary>Sets the element that receives focus when a focus trap activates.</summary>
    public static T InitialFocus<T, TRef>(this T node, NodeRef<TRef> initialFocus)
        where T : Node
        where TRef : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(initialFocus);

        var data = EnsureFocusData(node);
        data.InitialFocus = initialFocus;
        return node;
    }

    /// <summary>Marks the node as focusable with an optional tab index override.</summary>
    public static T Focusable<T>(this T node, TabIndex? index = null) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var data = EnsureFocusData(node);
        var resolvedIndex = index ?? global::Cascade.UI.TabIndex.Natural;
        data.TabIndexValue = resolvedIndex.Value;
        FocusManager.RegisterFocusable(node);
        return node;
    }

    /// <summary>Controls visibility of the focus ring for this node.</summary>
    public static T FocusRing<T>(this T node, bool visible = true) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        var data = EnsureFocusData(node);
        data.FocusRingVisible = visible;
        return node;
    }

    /// <summary>Registers a callback invoked whenever this node's focus state changes.</summary>
    public static T OnFocusChanged<T>(this T node, Action<bool> callback) where T : Node
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(callback);

        var data = EnsureFocusData(node);
        data.FocusChanged = callback;
        FocusManager.RegisterFocusable(node);
        return node;
    }

    private static FocusNodeData EnsureFocusData(Node node)
    {
        node.LayoutData.FocusData ??= new FocusNodeData();
        return node.LayoutData.FocusData;
    }
}

internal sealed class FocusNodeData
{
    internal int TabIndexValue = global::Cascade.UI.TabIndex.Natural.Value;
    internal long RegistrationOrder;
    internal bool AutoFocus;
    internal bool FocusTrap;
    internal bool FocusRingVisible = true;
    internal INodeRefInternal? InitialFocus;
    internal Action<bool>? FocusChanged;

    internal bool IsFocusable => TabIndexValue != global::Cascade.UI.TabIndex.Skip.Value;
}

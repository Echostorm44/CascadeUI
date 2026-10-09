namespace Cascade.UI.Tests;

/// <summary>
/// Drives the overlay layer through a real <see cref="FrameOrchestrator"/> (layout, focus,
/// input dispatch; nothing painted) for the dialog, popover and bottom sheet tests.
/// </summary>
internal static class OverlayTestKit
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    internal static FrameOrchestrator Mount<TRoot>(float width = 800f, float height = 600f)
        where TRoot : Component, new()
    {
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<TRoot>(width, height);
        orch.Tick();
        return orch;
    }

    /// <summary>Runs frames, then lets every open/close animation finish.</summary>
    internal static void Settle(FrameOrchestrator orch)
    {
        orch.Tick();
        for (int i = 0; i < 20 && orch.Overlays.IsAnimating; i++)
        {
            orch.Overlays.Advance(0.1f);
        }

        orch.Tick();
    }

    internal static OverlayEntry Top(FrameOrchestrator orch)
    {
        return orch.Overlays.Topmost ?? throw new InvalidOperationException("no overlay is open");
    }

    internal static T Find<T>(Node root, Func<T, bool>? match = null)
        where T : Node
    {
        return TryFind(root, match) ?? throw new InvalidOperationException($"no {typeof(T).Name} in the tree");
    }

    internal static T? TryFind<T>(Node root, Func<T, bool>? match = null)
        where T : Node
    {
        if (root is T typed && (match is null || match(typed)))
        {
            return typed;
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            if (TryFind(child, match) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    internal static Button FindButton(Node root, string label)
    {
        return Find<Button>(root, b => b.Label.Resolve() == label);
    }

    /// <summary>The window-logical center of <paramref name="node"/>, searched under <paramref name="root"/>.</summary>
    internal static Point CenterOf(Node root, Node node)
    {
        if (!HitTester.TryGetAbsoluteBounds(root, node, out var bounds))
        {
            throw new InvalidOperationException($"{node.GetType().Name} is not under the root");
        }

        return bounds.Center;
    }

    internal static void Click(FrameOrchestrator orch, Point point)
    {
        Mouse(orch, NativeMouseEventType.MouseMove, point);
        Mouse(orch, NativeMouseEventType.MouseDown, point);
        Mouse(orch, NativeMouseEventType.MouseUp, point);
    }

    /// <summary>Clicks <paramref name="button"/> of the topmost overlay.</summary>
    internal static void ClickInTop(FrameOrchestrator orch, string label)
    {
        var tree = Top(orch).Tree!;
        Click(orch, CenterOf(tree, FindButton(tree, label)));
    }

    internal static void Mouse(FrameOrchestrator orch, NativeMouseEventType type, Point point, NativeMouseButton button = NativeMouseButton.Left)
    {
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = point.X, Y = point.Y, Type = type, Button = button });
    }

    internal static void Key(FrameOrchestrator orch, Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    /// <summary>Enter as Windows delivers it: the key-down, the CR character, the key-up.</summary>
    internal static void PressEnter(FrameOrchestrator orch)
    {
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Cascade.UI.Key.Enter, Type = NativeKeyEventType.KeyDown });
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Cascade.UI.Key.None, Character = '\r', Type = NativeKeyEventType.KeyDown });
        orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Cascade.UI.Key.Enter, Type = NativeKeyEventType.KeyUp });
    }

    internal static void Type(FrameOrchestrator orch, string text)
    {
        foreach (char c in text)
        {
            orch.Input.HandleKeyEvent(new NativeKeyEvent { Key = Cascade.UI.Key.None, Character = c, Type = NativeKeyEventType.KeyDown });
        }
    }

    internal static async Task<T> Within<T>(Task<T> task)
    {
        return await task.WaitAsync(Timeout).ConfigureAwait(false);
    }

    internal static async Task Within(Task task)
    {
        await task.WaitAsync(Timeout).ConfigureAwait(false);
    }
}

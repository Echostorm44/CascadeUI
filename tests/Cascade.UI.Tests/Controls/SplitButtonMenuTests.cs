#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// A <see cref="SplitButton"/>'s arrow opens its items as the shared menu anchored below the
/// button (keyboard navigation, disabled items and submenus included); the primary zone still
/// runs the primary action.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager"])]
public class SplitButtonMenuTests
{
    private static readonly List<string> Log = [];

    private sealed class SplitButtonView : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 0, children:
            [
                new Label("header").Height(60),
                new SplitButton("Save", () => { Log.Add("save"); },
                [
                    ContextMenuItem.Action("Save As…", () => { Log.Add("save-as"); }),
                    ContextMenuItem.Action("Save Copy", () => { Log.Add("save-copy"); }, disabled: true),
                    ContextMenuItem.Separator(),
                    ContextMenuItem.Action("Export PDF", () => { Log.Add("export"); }),
                ]).Width(160).Height(36),
            ]);
        }
    }

    private static (FrameOrchestrator Orch, SplitButton Button, Rect Bounds) Mount()
    {
        Log.Clear();
        FocusManager.Reset();
        var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<SplitButtonView>(800, 500);
        orch.Tick();

        var button = (SplitButton)((Column)orch.RootHost!.RenderedTree!).Children[1];
        HitTester.TryGetAbsoluteBounds(orch.RootHost.RenderedTree!, button, out var bounds);
        // Stamped by the painter in a running app.
        button.AbsoluteBounds = bounds;
        button.ArrowZoneX = bounds.Width - 36f;
        return (orch, button, bounds);
    }

    private static void Click(InputDispatcher input, float x, float y)
    {
        input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
    }

    private static void Press(InputDispatcher input, Key key)
    {
        input.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown });
    }

    [Test]
    public async Task Arrow_OpensTheMenuBelowTheButton_OwnedByIt()
    {
        var (orch, button, bounds) = Mount();
        using var _ = orch;

        Click(orch.Input, bounds.Right - 10, bounds.Y + 10);

        await Assert.That(button.IsOpen).IsTrue();
        var root = orch.Input.Menu!.Levels[0];
        await Assert.That(root.Bounds.X).IsEqualTo(bounds.X + MenuOverlay.EdgeMargin);
        await Assert.That(root.Bounds.Y).IsEqualTo(bounds.Bottom + MenuOverlay.AnchorGap);
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task Arrow_WhileOpen_ClosesIt()
    {
        var (orch, button, bounds) = Mount();
        using var _ = orch;

        Click(orch.Input, bounds.Right - 10, bounds.Y + 10);
        Click(orch.Input, bounds.Right - 10, bounds.Y + 10);

        await Assert.That(button.IsOpen).IsFalse();
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task Keyboard_SkipsTheDisabledItem_AndActivates()
    {
        var (orch, _, bounds) = Mount();
        using var __ = orch;

        Click(orch.Input, bounds.Right - 10, bounds.Y + 10);
        Press(orch.Input, Key.Down);
        Press(orch.Input, Key.Down);
        Press(orch.Input, Key.Enter);

        await Assert.That(string.Join(",", Log)).IsEqualTo("export");
    }

    [Test]
    public async Task PrimaryZone_RunsThePrimaryAction()
    {
        var (orch, button, bounds) = Mount();
        using var _ = orch;

        Click(orch.Input, bounds.X + 20, bounds.Y + 10);

        await Assert.That(string.Join(",", Log)).IsEqualTo("save");
        await Assert.That(button.IsOpen).IsFalse();
    }
}

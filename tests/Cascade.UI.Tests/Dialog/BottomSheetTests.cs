#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Cascade.UI.Tests.OverlayTestKit;

namespace Cascade.UI.Tests;

/// <summary>
/// Bottom sheets through a real frame loop: placement against the bottom edge, dismissal by
/// Escape, backdrop and handle drag (a short drag snaps back), action sheets, and results.
/// </summary>
[NotInParallel("Dialog")]
public sealed class BottomSheetTests
{
    private sealed class Page : Component
    {
        protected override Node Render()
        {
            return new Button("Page", () => { }).Width(120).Height(32);
        }
    }

    private sealed class ShareSheet : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 8, children:
            [
                new Label("Share"),
                new Button("Copy link", () => { DialogContext.Close(1); }),
                new Button("Email", () => { DialogContext.Close(2); }),
            ]).Padding(16);
        }
    }

    [Test]
    public async Task ShowAsync_SitsOnTheBottomEdge_Centered_AtMostSheetWidth()
    {
        using var orch = Mount<Page>();
        var task = BottomSheet.ShowAsync<ShareSheet, int>();
        orch.Tick();

        var top = Top(orch);
        await Assert.That(top.Kind).IsEqualTo(OverlayKind.Sheet);
        await Assert.That(top.PanelBounds.Bottom).IsGreaterThanOrEqualTo(600f);
        await Assert.That(top.PanelBounds.Bottom).IsLessThan(601f);
        await Assert.That(top.PanelBounds.Width).IsEqualTo(OverlayManager.SheetMaxWidth);
        await Assert.That(top.PanelBounds.X).IsEqualTo((800f - OverlayManager.SheetMaxWidth) / 2f);
        await Assert.That(top.Animation).IsEqualTo(DialogAnimationKind.SlideUp);

        ClickInTop(orch, "Email");
        await Assert.That(await Within(task)).IsEqualTo(2);
    }

    [Test]
    public async Task Escape_And_Backdrop_Dismiss()
    {
        using var orch = Mount<Page>();
        var escaped = BottomSheet.ShowAsync<ShareSheet, int>();
        orch.Tick();
        Key(orch, Cascade.UI.Key.Escape);
        await Assert.That(await Within(escaped)).IsEqualTo(0);

        orch.Tick();
        var clicked = BottomSheet.ShowAsync<ShareSheet>();
        orch.Tick();
        Click(orch, new Point(400f, 10f));
        await Within(clicked);
        await Assert.That(orch.Overlays.HasLiveEntries).IsFalse();
    }

    [Test]
    public async Task DraggingTheHandle_PastTheThreshold_Dismisses()
    {
        using var orch = Mount<Page>();
        var task = BottomSheet.ShowAsync<ShareSheet, int>();
        Settle(orch);
        var panel = Top(orch).PanelBounds;
        var grab = new Point(panel.Center.X, panel.Y + 6f);

        Mouse(orch, NativeMouseEventType.MouseDown, grab);
        Mouse(orch, NativeMouseEventType.MouseMove, new Point(grab.X, grab.Y + (panel.Height * 0.6f)));
        await Assert.That(Math.Abs(Top(orch).DragOffset.Current - (panel.Height * 0.6f))).IsLessThan(0.01f);
        Mouse(orch, NativeMouseEventType.MouseUp, new Point(grab.X, grab.Y + (panel.Height * 0.6f)));

        await Assert.That(await Within(task)).IsEqualTo(0);
    }

    [Test]
    public async Task ShortDrag_SnapsBack()
    {
        using var orch = Mount<Page>();
        var task = BottomSheet.ShowAsync<ShareSheet, int>();
        Settle(orch);
        var panel = Top(orch).PanelBounds;
        var grab = new Point(panel.Center.X, panel.Y + 6f);

        Mouse(orch, NativeMouseEventType.MouseDown, grab);
        Mouse(orch, NativeMouseEventType.MouseMove, new Point(grab.X, grab.Y + 10f));
        Mouse(orch, NativeMouseEventType.MouseUp, new Point(grab.X, grab.Y + 10f));
        orch.Tick();

        await Assert.That(task.IsCompleted).IsFalse();
        await Assert.That(orch.Overlays.IsAnimating).IsTrue();
        Settle(orch);
        await Assert.That(Top(orch).DragOffset.Current).IsEqualTo(0f);

        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
    }

    [Test]
    public async Task NotDismissable_IgnoresDragAndBackdrop()
    {
        using var orch = Mount<Page>();
        var task = BottomSheet.ShowAsync<ShareSheet, int>(new DialogOptions { Dismissable = false });
        Settle(orch);
        var panel = Top(orch).PanelBounds;

        Mouse(orch, NativeMouseEventType.MouseDown, new Point(panel.Center.X, panel.Y + 6f));
        Mouse(orch, NativeMouseEventType.MouseMove, new Point(panel.Center.X, panel.Bottom));
        Mouse(orch, NativeMouseEventType.MouseUp, new Point(panel.Center.X, panel.Bottom));
        Click(orch, new Point(400f, 10f));
        Key(orch, Cascade.UI.Key.Escape);

        await Assert.That(task.IsCompleted).IsFalse();
        ClickInTop(orch, "Copy link");
        await Assert.That(await Within(task)).IsEqualTo(1);
    }

    [Test]
    public async Task ShowActionsAsync_ReturnsTheChosenAction_OrNull()
    {
        using var orch = Mount<Page>();
        var chosen = BottomSheet.ShowActionsAsync("Share via", ["Copy Link", "Send by Email"]);
        orch.Tick();
        await Assert.That(Top(orch).Tree!.LayoutData.A11yLabel).IsEqualTo("Share via");
        ClickInTop(orch, "Send by Email");
        await Assert.That(await Within(chosen)).IsEqualTo("Send by Email");

        orch.Tick();
        var cancelled = BottomSheet.ShowActionsAsync("Share via", ["Copy Link"], cancel: "Never mind");
        orch.Tick();
        ClickInTop(orch, "Never mind");
        await Assert.That(await Within(cancelled)).IsNull();
    }
}

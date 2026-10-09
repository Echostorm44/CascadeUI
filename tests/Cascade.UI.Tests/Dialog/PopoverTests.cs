#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Cascade.UI.Tests.OverlayTestKit;

namespace Cascade.UI.Tests;

/// <summary>
/// Popovers: placement against an anchor (side, flip, clamp, offsets) as pure geometry, and
/// light-dismiss behaviour through a real frame loop — outside press (passed through unless
/// modal), Escape, window deactivation, the anchor leaving the tree, a dialog opening.
/// </summary>
[NotInParallel("Dialog")]
public sealed class PopoverTests
{
    private static readonly Size Window = new(800f, 600f);

    private sealed class Page : Component
    {
        public Page()
        {
            Instance = this;
        }

        public static Page? Instance { get; private set; }

        public int OtherClicks { get; private set; }

        public bool ShowAnchor { get; set; } = true;

        public void Refresh()
        {
            Invalidate();
        }

        protected override Node Render()
        {
            // "Other" sits well away from the anchor, so a popover under the anchor never covers it.
            return new Column(spacing: 240, children:
            [
                ShowAnchor ? new Button("Anchor", () => { }).Width(120).Height(32) : Node.Empty,
                new Button("Other", () => { OtherClicks++; }).Width(120).Height(32),
            ]).Padding(200);
        }
    }

    private sealed class InfoPopover : Component
    {
        protected override Node Render()
        {
            return new Label("Copied 3 minutes ago").Padding(12).Width(180);
        }
    }

    private sealed class FieldPopover : Component
    {
        private string text = "";

        protected override Node Render()
        {
            return new Column(spacing: 6, children:
            [
                new TextInput(Bind(text, v => { text = v; }), placeholder: "Name"),
                new Button("Apply", () => { DialogContext.Close(); }),
            ]).Padding(12).Width(220);
        }
    }

    private static Button PageButton(FrameOrchestrator orch, string label)
    {
        return FindButton(orch.RootHost!.RenderedTree!, label);
    }

    private static Rect PageBounds(FrameOrchestrator orch, string label)
    {
        HitTester.TryGetAbsoluteBounds(orch.RootHost!.RenderedTree!, PageButton(orch, label), out var bounds);
        return bounds;
    }

    // ── Placement (pure geometry) ─────────────────────────────────────

    [Test]
    public async Task Place_Bottom_CentersBelowTheAnchor()
    {
        var anchor = new Rect(300f, 100f, 100f, 30f);
        var (bounds, side) = OverlayManager.PlaceAgainst(new Size(200f, 80f), anchor, PopoverSide.Bottom, 0f, 0f, Window);

        await Assert.That(side).IsEqualTo(PopoverSide.Bottom);
        await Assert.That(bounds).IsEqualTo(new Rect(250f, 130f + OverlayManager.PopoverGap, 200f, 80f));
    }

    [Test]
    public async Task Place_Bottom_FlipsAboveNearTheBottomEdge()
    {
        var anchor = new Rect(300f, 550f, 100f, 30f);
        var (bounds, side) = OverlayManager.PlaceAgainst(new Size(200f, 80f), anchor, PopoverSide.Bottom, 0f, 0f, Window);

        await Assert.That(side).IsEqualTo(PopoverSide.Top);
        await Assert.That(bounds.Bottom).IsEqualTo(550f - OverlayManager.PopoverGap);
    }

    [Test]
    public async Task Place_Auto_PrefersBelow_ElseTheRoomierSide()
    {
        var top = new Rect(300f, 40f, 100f, 30f);
        var (_, below) = OverlayManager.PlaceAgainst(new Size(200f, 80f), top, PopoverSide.Auto, 0f, 0f, Window);
        await Assert.That(below).IsEqualTo(PopoverSide.Bottom);

        var low = new Rect(300f, 520f, 100f, 30f);
        var (_, above) = OverlayManager.PlaceAgainst(new Size(200f, 80f), low, PopoverSide.Auto, 0f, 0f, Window);
        await Assert.That(above).IsEqualTo(PopoverSide.Top);
    }

    [Test]
    public async Task Place_Right_FlipsLeftNearTheRightEdge_AndClampsVertically()
    {
        var anchor = new Rect(700f, 5f, 60f, 20f);
        var (bounds, side) = OverlayManager.PlaceAgainst(new Size(150f, 100f), anchor, PopoverSide.Right, 0f, 0f, Window);

        await Assert.That(side).IsEqualTo(PopoverSide.Left);
        await Assert.That(bounds.Right).IsEqualTo(700f - OverlayManager.PopoverGap);
        await Assert.That(bounds.Y).IsEqualTo(OverlayManager.PopoverMargin);
    }

    [Test]
    public async Task Place_Right_TooWideForEitherSide_GoesBelow()
    {
        var anchor = new Rect(300f, 100f, 100f, 30f);
        var (bounds, side) = OverlayManager.PlaceAgainst(new Size(500f, 80f), anchor, PopoverSide.Right, 0f, 0f, Window);

        await Assert.That(side).IsEqualTo(PopoverSide.Bottom);
        await Assert.That(bounds.Y).IsEqualTo(130f + OverlayManager.PopoverGap);
    }

    [Test]
    public async Task Place_ClampsHorizontally_AndAppliesOffsets()
    {
        var anchor = new Rect(5f, 100f, 20f, 20f);
        var (clamped, _) = OverlayManager.PlaceAgainst(new Size(200f, 80f), anchor, PopoverSide.Bottom, 0f, 0f, Window);
        await Assert.That(clamped.X).IsEqualTo(OverlayManager.PopoverMargin);

        var middle = new Rect(300f, 100f, 100f, 30f);
        var (offset, _) = OverlayManager.PlaceAgainst(new Size(200f, 80f), middle, PopoverSide.Bottom, 12f, 4f, Window);
        await Assert.That(offset.X).IsEqualTo(262f);
        await Assert.That(offset.Y).IsEqualTo(134f + OverlayManager.PopoverGap);
    }

    [Test]
    public async Task Place_SnapsToDevicePixels()
    {
        var anchor = new Rect(300.3f, 100f, 100f, 30.2f);
        var (bounds, _) = OverlayManager.PlaceAgainst(new Size(200f, 80f), anchor, PopoverSide.Bottom, 0f, 0f, Window, pixelRatio: 1.5f);

        await Assert.That(MathF.Abs((bounds.X * 1.5f) - MathF.Round(bounds.X * 1.5f))).IsLessThan(0.001f);
        await Assert.That(MathF.Abs((bounds.Y * 1.5f) - MathF.Round(bounds.Y * 1.5f))).IsLessThan(0.001f);
    }

    // ── In a window ───────────────────────────────────────────────────

    [Test]
    public async Task Show_AnchoredToANode_IsLaidOutBelowIt_NonModal()
    {
        using var orch = Mount<Page>();
        var anchor = PageBounds(orch, "Anchor");

        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"), new PopoverOptions { PreferredSide = PopoverSide.Bottom, AccessibleLabel = "Clip info" });
        orch.Tick();

        var top = Top(orch);
        await Assert.That(Popover.IsOpen).IsTrue();
        await Assert.That(top.Kind).IsEqualTo(OverlayKind.Popover);
        await Assert.That(top.BlocksInput).IsFalse();
        await Assert.That(top.PanelBounds.Y).IsEqualTo(anchor.Bottom + OverlayManager.PopoverGap);
        await Assert.That(Math.Abs(top.PanelBounds.Center.X - anchor.Center.X)).IsLessThanOrEqualTo(1f);
        await Assert.That(top.Tree!.LayoutData.A11yRole).IsEqualTo(AccessibleRole.Dialog);
        await Assert.That(top.Tree.LayoutData.A11yLabel).IsEqualTo("Clip info");

        Popover.Close();
        await Assert.That(Popover.IsOpen).IsFalse();
    }

    [Test]
    public async Task Show_AtAPoint_PlacesAgainstThePoint()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(new Point(400f, 300f), new PopoverOptions { PreferredSide = PopoverSide.Bottom });
        orch.Tick();

        await Assert.That(Top(orch).PanelBounds.Y).IsEqualTo(300f + OverlayManager.PopoverGap);
        Popover.Close();
    }

    [Test]
    public async Task Show_WithAnAnchorNotInTheTree_Throws()
    {
        using var orch = Mount<Page>();
        var stray = new Button("Nowhere", () => { });

        await Assert.That(() => Popover.Show<InfoPopover>(stray)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task OutsidePress_DismissesAndPassesThrough()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();

        Click(orch, PageBounds(orch, "Other").Center);

        await Assert.That(Popover.IsOpen).IsFalse();
        await Assert.That(Page.Instance!.OtherClicks).IsEqualTo(1);
    }

    [Test]
    public async Task ModalPopover_SwallowsTheOutsidePress()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"), new PopoverOptions { Modal = true });
        orch.Tick();
        await Assert.That(Top(orch).BlocksInput).IsTrue();

        Click(orch, PageBounds(orch, "Other").Center);

        await Assert.That(Popover.IsOpen).IsFalse();
        await Assert.That(Page.Instance!.OtherClicks).IsEqualTo(0);
    }

    [Test]
    public async Task Escape_Dismisses_WithFocusStillOnThePage()
    {
        using var orch = Mount<Page>();
        var other = PageButton(orch, "Other");
        FocusManager.RequestFocus(other);
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(other);

        Key(orch, Cascade.UI.Key.Escape);

        await Assert.That(Popover.IsOpen).IsFalse();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(other);
    }

    [Test]
    public async Task FocusableContent_TakesFocus_TabStaysInside_CloseRestores()
    {
        using var orch = Mount<Page>();
        var anchor = PageButton(orch, "Anchor");
        FocusManager.RequestFocus(anchor);
        Popover.Show<FieldPopover>(anchor);
        orch.Tick();

        var tree = Top(orch).Tree!;
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(Find<TextInput>(tree));
        for (int i = 0; i < 3; i++)
        {
            Key(orch, Cascade.UI.Key.Tab);
            await Assert.That(OverlayManager.Contains(Top(orch), FocusManager.FocusedElement!)).IsTrue();
        }

        ClickInTop(orch, "Apply");
        await Assert.That(Popover.IsOpen).IsFalse();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(anchor);
    }

    [Test]
    public async Task WindowDeactivation_DismissesPopovers()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();

        orch.Input.HandleWindowDeactivation();

        await Assert.That(Popover.IsOpen).IsFalse();
    }

    [Test]
    public async Task AnchorLeavingTheTree_DismissesThePopover()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();

        Page.Instance!.ShowAnchor = false;
        Page.Instance.Refresh();
        orch.Tick();

        await Assert.That(Popover.IsOpen).IsFalse();
    }

    [Test]
    public async Task AnchorReRendered_PopoverFollowsTheNewInstance()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();

        Page.Instance!.Refresh();
        orch.Tick();

        await Assert.That(Popover.IsOpen).IsTrue();
        await Assert.That(Top(orch).Anchor).IsSameReferenceAs(PageButton(orch, "Anchor"));
        Popover.Close();
    }

    [Test]
    public async Task OpeningADialog_ClosesPopovers()
    {
        using var orch = Mount<Page>();
        Popover.Show<InfoPopover>(PageButton(orch, "Anchor"));
        orch.Tick();

        var task = Dialog.ConfirmAsync("Title", "Message");
        orch.Tick();

        await Assert.That(Popover.IsOpen).IsFalse();
        await Assert.That(Top(orch).Kind).IsEqualTo(OverlayKind.Dialog);
        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
    }
}

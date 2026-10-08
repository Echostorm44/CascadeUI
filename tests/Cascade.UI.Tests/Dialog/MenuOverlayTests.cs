#pragma warning disable CA2000

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// The shared menu overlay's pure state and geometry: placement at a point and against an
/// anchor (flipping at the right/bottom window edges, clamping inside a small window, snapping to
/// device pixels), submenu placement, highlight navigation and hit-testing. A fixed-width text
/// measurer keeps the widths deterministic.
/// </summary>
public sealed class MenuOverlayTests
{
    private static readonly MenuMetrics Metrics = new(
        ItemHeight:        30f,
        ItemPaddingH:      10f,
        FontSize:          14f,
        ShortcutFontSize:  12f,
        SeparatorHeight:   9f,
        PaddingV:          6f,
        InsetH:            6f,
        IconColumn:        24f,
        ShortcutGap:       24f,
        SubmenuArrowWidth: 16f,
        MinWidth:          160f);

    private static readonly Size Window = new(800f, 500f);

    // 10 logical px per character, whatever the size.
    private static float Measure(string text, float size)
    {
        return text.Length * 10f;
    }

    private static MenuOverlay NewMenu()
    {
        var menu = new MenuOverlay();
        menu.SetMeasurer(Measure);
        return menu;
    }

    private static ContextMenuItem[] FourItems() =>
    [
        ContextMenuItem.Action("Paste", () => { }, shortcut: "Enter"),
        ContextMenuItem.Separator(),
        ContextMenuItem.Action("Pin", () => { }, disabled: true),
        ContextMenuItem.Action("Delete", () => { }, style: MenuItemStyle.Destructive),
    ];

    private static Rect OpenAt(MenuOverlay menu, IReadOnlyList<ContextMenuItem> items, MenuPlacement placement, Size viewport, float pixelRatio = 1f)
    {
        bool opened = menu.Open(items, placement, Metrics, viewport, pixelRatio, Point.Zero, highlightFirst: false, owner: null);
        if (!opened)
        {
            throw new InvalidOperationException("menu did not open");
        }
        return menu.Levels[0].Bounds;
    }

    [Test]
    public async Task Size_FitsLabelsShortcutsAndPadding()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(100, 100)), Window);

        // 2×(6+10) inset + "Delete" 60 + gap 24 + "Enter" 50 = 166; height 6+30+9+30+30+6.
        await Assert.That(bounds.Width).IsEqualTo(166f);
        await Assert.That(bounds.Height).IsEqualTo(111f);
    }

    [Test]
    public async Task AtPoint_ThatFits_PutsTopLeftAtThePoint()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(100, 120)), Window);

        await Assert.That(bounds.X).IsEqualTo(100f);
        await Assert.That(bounds.Y).IsEqualTo(120f);
    }

    [Test]
    public async Task AtPoint_NearRightEdge_FlipsToTheLeftOfThePoint()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(750, 120)), Window);

        await Assert.That(bounds.Right).IsEqualTo(750f);
        await Assert.That(bounds.Y).IsEqualTo(120f);
    }

    [Test]
    public async Task AtPoint_NearBottomEdge_FlipsAboveThePoint()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(100, 450)), Window);

        await Assert.That(bounds.Bottom).IsEqualTo(450f);
        await Assert.That(bounds.X).IsEqualTo(100f);
    }

    [Test]
    public async Task AtPoint_InTheCorner_FlipsBothWays()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(790, 490)), Window);

        await Assert.That(bounds.Right).IsEqualTo(790f);
        await Assert.That(bounds.Bottom).IsEqualTo(490f);
    }

    [Test]
    public async Task TallMenu_InSmallWindow_IsClampedInside_AndScrolls()
    {
        var items = Enumerable.Range(0, 30).Select(i => ContextMenuItem.Action($"Item {i}", () => { })).ToArray();
        var menu = NewMenu();
        var bounds = OpenAt(menu, items, MenuPlacement.AtPoint(new Point(300, 200)), Window);

        await Assert.That(bounds.Y).IsEqualTo(MenuOverlay.EdgeMargin);
        await Assert.That(bounds.Bottom).IsEqualTo(Window.Height - MenuOverlay.EdgeMargin);
        await Assert.That(menu.Levels[0].MaxScroll).IsGreaterThan(0f);

        // Keyboard navigation to the last item scrolls it into view.
        menu.HighlightEdge(last: true);
        var level = menu.Levels[0];
        await Assert.That(level.Highlighted).IsEqualTo(29);
        await Assert.That(level.ItemTop(29) + level.ItemHeights[29]).IsLessThanOrEqualTo(bounds.Bottom - Metrics.PaddingV + 0.01f);
    }

    [Test]
    public async Task WideMenu_IsNeverWiderThanTheWindow()
    {
        var items = new[] { ContextMenuItem.Action(new string('W', 200), () => { }) };
        var bounds = OpenAt(NewMenu(), items, MenuPlacement.AtPoint(new Point(10, 10)), new Size(300, 200));

        await Assert.That(bounds.X).IsEqualTo(MenuOverlay.EdgeMargin);
        await Assert.That(bounds.Right).IsEqualTo(300f - MenuOverlay.EdgeMargin);
    }

    [Test]
    public async Task BelowAnchor_IsLeftAligned_UnderTheAnchor()
    {
        var anchor = new Rect(40, 100, 300, 30);
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.Below(anchor), Window);

        await Assert.That(bounds.X).IsEqualTo(40f);
        await Assert.That(bounds.Y).IsEqualTo(anchor.Bottom + MenuOverlay.AnchorGap);
    }

    [Test]
    public async Task BelowAnchor_NearBottom_FlipsAbove()
    {
        var anchor = new Rect(40, 440, 300, 30);
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.Below(anchor), Window);

        await Assert.That(bounds.Bottom).IsEqualTo(anchor.Y - MenuOverlay.AnchorGap);
    }

    [Test]
    public async Task BelowAnchor_NearRightEdge_RightAlignsWithTheAnchor()
    {
        var anchor = new Rect(700, 100, 80, 30);
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.Below(anchor), Window);

        await Assert.That(bounds.Right).IsEqualTo(anchor.Right);
    }

    [Test]
    public async Task Edges_SnapToDevicePixels()
    {
        var bounds = OpenAt(NewMenu(), FourItems(), MenuPlacement.AtPoint(new Point(100.3f, 120.7f)), Window, pixelRatio: 1.5f);

        await Assert.That(MathF.Abs((bounds.X * 1.5f) - MathF.Round(bounds.X * 1.5f))).IsLessThan(0.001f);
        await Assert.That(MathF.Abs((bounds.Y * 1.5f) - MathF.Round(bounds.Y * 1.5f))).IsLessThan(0.001f);
    }

    [Test]
    public async Task Submenu_OpensToTheRight_LevelWithItsItem()
    {
        var menu = NewMenu();
        var items = new[]
        {
            ContextMenuItem.Action("Open", () => { }),
            ContextMenuItem.Submenu("Move to", [ContextMenuItem.Action("Done", () => { })]),
        };
        var root = OpenAt(menu, items, MenuPlacement.AtPoint(new Point(100, 100)), Window);

        await Assert.That(menu.OpenSubmenu(0, 1, highlightFirst: true)).IsTrue();
        var sub = menu.Levels[1].Bounds;
        await Assert.That(sub.X).IsEqualTo(root.Right - MenuOverlay.SubmenuOverlap);
        await Assert.That(sub.Y).IsEqualTo(menu.Levels[0].ItemTop(1) - Metrics.PaddingV);
        await Assert.That(menu.Levels[1].Highlighted).IsEqualTo(0);
    }

    [Test]
    public async Task Submenu_NearRightEdge_OpensToTheLeft()
    {
        var menu = NewMenu();
        var items = new[] { ContextMenuItem.Submenu("Move to", [ContextMenuItem.Action("Done", () => { })]) };
        var root = OpenAt(menu, items, MenuPlacement.AtPoint(new Point(790, 100)), Window);

        menu.OpenSubmenu(0, 0, highlightFirst: false);
        var sub = menu.Levels[1].Bounds;
        await Assert.That(sub.Right).IsEqualTo(root.X + MenuOverlay.SubmenuOverlap);
    }

    [Test]
    public async Task Highlight_SkipsSeparatorsAndDisabled_AndWraps()
    {
        var menu = NewMenu();
        OpenAt(menu, FourItems(), MenuPlacement.AtPoint(new Point(10, 10)), Window);
        var level = menu.Levels[0];

        menu.MoveHighlight(+1);
        await Assert.That(level.Highlighted).IsEqualTo(0);
        menu.MoveHighlight(+1);
        await Assert.That(level.Highlighted).IsEqualTo(3); // skipped the separator and disabled "Pin"
        menu.MoveHighlight(+1);
        await Assert.That(level.Highlighted).IsEqualTo(0); // wrapped
        menu.MoveHighlight(-1);
        await Assert.That(level.Highlighted).IsEqualTo(3);
    }

    [Test]
    public async Task Letter_JumpsToTheNextMatchingItem()
    {
        var menu = NewMenu();
        var items = new[]
        {
            ContextMenuItem.Action("Copy", () => { }),
            ContextMenuItem.Action("Paste", () => { }),
            ContextMenuItem.Action("Copy as Plain Text", () => { }),
        };
        OpenAt(menu, items, MenuPlacement.AtPoint(new Point(10, 10)), Window);

        menu.HighlightByLetter('c');
        await Assert.That(menu.Levels[0].Highlighted).IsEqualTo(0);
        menu.HighlightByLetter('C');
        await Assert.That(menu.Levels[0].Highlighted).IsEqualTo(2);
        await Assert.That(menu.HighlightByLetter('z')).IsFalse();
    }

    [Test]
    public async Task HitTest_MapsPointsToItems_PaddingAndOutsideToNone()
    {
        var menu = NewMenu();
        var bounds = OpenAt(menu, FourItems(), MenuPlacement.AtPoint(new Point(100, 100)), Window);
        float x = bounds.X + 20;

        await Assert.That(menu.HitTest(new Point(x, bounds.Y + 2))).IsEqualTo((0, -1));        // top padding
        await Assert.That(menu.HitTest(new Point(x, bounds.Y + 6 + 15))).IsEqualTo((0, 0));    // Paste
        await Assert.That(menu.HitTest(new Point(x, bounds.Y + 6 + 30 + 4))).IsEqualTo((0, 1)); // separator
        await Assert.That(menu.HitTest(new Point(x, bounds.Y + 6 + 39 + 45))).IsEqualTo((0, 3)); // Delete
        await Assert.That(menu.HitTest(new Point(bounds.X - 5, bounds.Y + 20))).IsEqualTo((-1, -1));
    }

    [Test]
    public async Task Activate_DisabledOrSeparator_DoesNothing()
    {
        var menu = NewMenu();
        OpenAt(menu, FourItems(), MenuPlacement.AtPoint(new Point(100, 100)), Window);

        await Assert.That(menu.Activate(0, 1, fromKeyboard: false) is null).IsTrue();
        await Assert.That(menu.Activate(0, 2, fromKeyboard: false) is null).IsTrue();
        await Assert.That(menu.Activate(0, 3, fromKeyboard: false) is null).IsFalse();
    }

    [Test]
    public async Task NothingLabelled_DoesNotOpen()
    {
        var menu = NewMenu();
        bool opened = menu.Open([ContextMenuItem.Separator()], MenuPlacement.AtPoint(Point.Zero), Metrics, Window, 1f, Point.Zero, false, null);

        await Assert.That(opened).IsFalse();
        await Assert.That(menu.IsOpen).IsFalse();
    }
}

#pragma warning disable CA2000

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// The menu item kinds the shared menu overlay shows beyond actions and submenus: toggles and
/// radio choices (a check column, choosing flips / selects), section headers and custom content
/// rows (laid out, never highlighted or activated by the keyboard).
/// </summary>
public sealed class MenuItemKindTests
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
        MinWidth:          160f,
        HeaderHeight:      22f,
        HeaderFontSize:    12f,
        CheckColumn:       20f);

    private static readonly Size Window = new(800f, 500f);

    private static MenuOverlay NewMenu()
    {
        var menu = new MenuOverlay();
        menu.SetMeasurer((text, _) => text.Length * 10f);
        menu.SetNodeMeasurer((_, _) => new Size(250f, 40f));
        return menu;
    }

    [Test]
    public async Task Toggle_FlipsTheValue_Radio_Selects()
    {
        bool? toggled = null;
        bool selected = false;
        var toggle = ContextMenuItem.Toggle("Wrap", isChecked: true, v => { toggled = v; });
        var radio = ContextMenuItem.Radio("Dark", isSelected: false, () => { selected = true; });

        await Assert.That(toggle.Kind).IsEqualTo(MenuItemKind.Toggle);
        await Assert.That(toggle.IsChecked).IsTrue();
        toggle.OnClick!();
        radio.OnClick!();
        await Assert.That(toggled).IsEqualTo(false);
        await Assert.That(selected).IsTrue();
    }

    [Test]
    public async Task Checks_ReserveAColumn_HeadersAndContent_GetTheirOwnHeight()
    {
        var menu = NewMenu();
        ContextMenuItem[] items =
        [
            ContextMenuItem.Header("View"),
            ContextMenuItem.Toggle("Wrap", true, _ => { }),
            ContextMenuItem.Custom(new Label("zoom")),
            ContextMenuItem.Radio("Dark", false, () => { }),
        ];
        menu.Open(items, MenuPlacement.AtPoint(new Point(10, 10)), Metrics, Window, 1f, Point.Zero, highlightFirst: true, owner: null);
        var level = menu.Levels[0];

        await Assert.That(level.HasChecks).IsTrue();
        await Assert.That(level.ItemHeights[0]).IsEqualTo(22f);
        await Assert.That(level.ItemHeights[2]).IsEqualTo(40f);

        // Content 250 wide + 2×6 inset beats 2×16 + check 20 + "Wrap" 40 + slack 4.
        await Assert.That(level.Bounds.Width).IsEqualTo(262f);

        // The header is skipped by the keyboard: the first highlight is the toggle, and Down
        // passes over the custom row to the radio item.
        await Assert.That(level.Highlighted).IsEqualTo(1);
        menu.MoveHighlight(+1);
        await Assert.That(level.Highlighted).IsEqualTo(3);
        bool contentActivates = menu.Activate(0, 2, fromKeyboard: false) is not null;
        bool headerActivates = menu.Activate(0, 0, fromKeyboard: false) is not null;
        await Assert.That(contentActivates).IsFalse();
        await Assert.That(headerActivates).IsFalse();
    }

    [Test]
    public async Task AMenuOfOnlyContent_StillOpens()
    {
        var menu = NewMenu();
        bool opened = menu.Open([ContextMenuItem.Custom(new Label("x"))], MenuPlacement.AtPoint(Point.Zero), Metrics, Window, 1f, Point.Zero, false, null);
        await Assert.That(opened).IsTrue();

        bool separatorsOnly = menu.Open([ContextMenuItem.Separator()], MenuPlacement.AtPoint(Point.Zero), Metrics, Window, 1f, Point.Zero, false, null);
        await Assert.That(separatorsOnly).IsFalse();
    }

    [Test]
    public async Task CustomRow_HitGivesThePointInsideTheRow()
    {
        var menu = NewMenu();
        menu.Open([ContextMenuItem.Action("A", () => { }), ContextMenuItem.Custom(new Label("x"))],
            MenuPlacement.AtPoint(new Point(100, 100)), Metrics, Window, 1f, Point.Zero, false, null);

        // Panel at (100,100): padding 6, the action 30 tall, so the custom row starts at y 136.
        bool hit = menu.TryHitCustom(new Point(120, 146), out int level, out int item, out var local);
        await Assert.That(hit).IsTrue();
        await Assert.That(item).IsEqualTo(1);
        await Assert.That(local).IsEqualTo(new Point(14, 10));
        await Assert.That(menu.TryHitCustom(new Point(120, 110), out _, out _, out _)).IsFalse();
    }
}

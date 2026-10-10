#pragma warning disable CA2000

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// The tray menu drawn by Cascade (<see cref="TrayMenuPopup"/>): where it opens against the taskbar
/// on any edge and monitor, how <see cref="TrayMenuItem"/>s map onto the shared menu, the rules
/// that close it, its theme, and the access keys a native menu has. The popup windows themselves
/// are exercised end to end by the shell integration test.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager"])]
public sealed class TrayMenuTests
{
    private static readonly Size Work = new(1000f, 700f);
    private static readonly Size Panel = new(200f, 300f);

    // ── Placement against the taskbar ──────────────────────────────────

    [Test]
    public async Task BottomTaskbar_OpensAboveIt_AtThePoint()
    {
        var rect = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(500f, 720f), ScreenEdge.Bottom, 700f), Work);
        await Assert.That(rect).IsEqualTo(new Rect(500f, 700f - MenuOverlay.EdgeMargin - 300f, 200f, 300f));
    }

    [Test]
    public async Task BottomTaskbar_NearTheRightEdge_EndsAtThePoint()
    {
        var rect = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(950f, 720f), ScreenEdge.Bottom, 700f), Work);
        await Assert.That(rect.X).IsEqualTo(750f);
        await Assert.That(rect.Bottom).IsEqualTo(700f - MenuOverlay.EdgeMargin);
    }

    [Test]
    public async Task TopTaskbar_OpensBelowIt()
    {
        var rect = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(300f, -20f), ScreenEdge.Top, 0f), Work);
        await Assert.That(rect).IsEqualTo(new Rect(300f, MenuOverlay.EdgeMargin, 200f, 300f));
    }

    [Test]
    public async Task LeftTaskbar_OpensBesideIt_FlippedUpNearTheBottom()
    {
        var mid = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(-30f, 200f), ScreenEdge.Left, 0f), Work);
        await Assert.That(mid).IsEqualTo(new Rect(MenuOverlay.EdgeMargin, 200f, 200f, 300f));

        var low = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(-30f, 650f), ScreenEdge.Left, 0f), Work);
        await Assert.That(low.Y).IsEqualTo(350f).Because("a menu that would cross the bottom ends at the point");
    }

    [Test]
    public async Task RightTaskbar_OpensBesideIt()
    {
        var rect = MenuOverlay.PlaceRoot(Panel, MenuPlacement.FromTaskbar(new Point(1030f, 100f), ScreenEdge.Right, 1000f), Work);
        await Assert.That(rect).IsEqualTo(new Rect(1000f - MenuOverlay.EdgeMargin - 200f, 100f, 200f, 300f));
    }

    [Test]
    public async Task PointOffTheTaskbar_IsPlacedAsAtAPoint()
    {
        // The hidden-icons flyout sits above the taskbar: the point is inside the work area.
        var placement = MenuPlacement.FromTaskbar(new Point(900f, 650f), ScreenEdge.Bottom, 700f);
        await Assert.That(placement.IsOnTaskbar).IsFalse();
        var rect = MenuOverlay.PlaceRoot(Panel, placement, Work);
        await Assert.That(rect).IsEqualTo(new Rect(700f, 350f, 200f, 300f));
    }

    [Test]
    public async Task TallMenu_StaysInsideTheWorkArea()
    {
        var tall = new Size(200f, 900f);
        var rect = MenuOverlay.PlaceRoot(tall, MenuPlacement.FromTaskbar(new Point(500f, 720f), ScreenEdge.Bottom, 700f), Work);
        await Assert.That(rect.Y).IsEqualTo(MenuOverlay.EdgeMargin);
    }

    [Test]
    public async Task Placement_SnapsToDevicePixels_At150Percent()
    {
        var rect = MenuOverlay.PlaceRoot(new Size(200.3f, 300.1f), MenuPlacement.FromTaskbar(new Point(500.2f, 720f), ScreenEdge.Bottom, 700f), Work, pixelRatio: 1.5f);
        foreach (float v in new[] { rect.X, rect.Y, rect.Width, rect.Height })
        {
            float device = v * 1.5f;
            await Assert.That(MathF.Abs(device - MathF.Round(device))).IsLessThan(0.001f);
        }
    }

    [Test]
    [Arguments(1.0f)]
    [Arguments(1.5f)]
    [Arguments(2.25f)]
    public async Task PlacementFor_MapsPhysicalPixelsOfASecondMonitor(float scale)
    {
        // A 2560×1440 monitor right of the primary, taskbar 72 px tall at the bottom.
        var monitor = Rect(2560, 0, 5120, 1440);
        var work = Rect(2560, 0, 5120, 1368);
        var anchor = new Win32.POINT { x = 3000, y = 1420 };

        var placement = TrayMenuPopup.PlacementFor(anchor, monitor, work, appBar: null, scale);

        await Assert.That(placement.Edge).IsEqualTo(ScreenEdge.Bottom);
        await Assert.That(placement.IsOnTaskbar).IsTrue();
        await Assert.That(MathF.Abs(placement.Anchor.X - (440f / scale))).IsLessThan(0.001f);
        await Assert.That(MathF.Abs(placement.EdgeLine - (1368f / scale))).IsLessThan(0.001f);

        var viewport = new Size(2560f / scale, 1368f / scale);
        var rect = MenuOverlay.PlaceRoot(Panel, placement, viewport, scale);
        await Assert.That(rect.Bottom).IsLessThanOrEqualTo(viewport.Height - MenuOverlay.EdgeMargin + 0.01f);
        await Assert.That(rect.Bottom).IsGreaterThan(viewport.Height - MenuOverlay.EdgeMargin - 1f);
    }

    // ── Which edge the taskbar is on ───────────────────────────────────

    [Test]
    public async Task FindTaskbarEdge_FromTheWorkArea_OnEveryEdge()
    {
        var monitor = Rect(0, 0, 1920, 1080);
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(900, 1060), monitor, Rect(0, 0, 1920, 1040), null)).IsEqualTo((ScreenEdge.Bottom, 1040));
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(900, 10), monitor, Rect(0, 40, 1920, 1080), null)).IsEqualTo((ScreenEdge.Top, 40));
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(10, 500), monitor, Rect(60, 0, 1920, 1080), null)).IsEqualTo((ScreenEdge.Left, 60));
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(1900, 500), monitor, Rect(0, 0, 1860, 1080), null)).IsEqualTo((ScreenEdge.Right, 1860));
    }

    [Test]
    public async Task FindTaskbarEdge_AutoHide_UsesTheTaskbarRectangle()
    {
        // An auto-hiding taskbar leaves the work area whole; its own rectangle says where it is.
        var monitor = Rect(0, 0, 1920, 1080);
        var appBar = (Rect(0, 1032, 1920, 1080), Win32.ABE_BOTTOM);
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(1500, 1060), monitor, monitor, appBar)).IsEqualTo((ScreenEdge.Bottom, 1032));
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(1500, 500), monitor, monitor, appBar)).IsEqualTo((ScreenEdge.None, 0));
    }

    [Test]
    public async Task FindTaskbarEdge_PointInTheWorkArea_IsNoEdge()
    {
        var monitor = Rect(0, 0, 1920, 1080);
        await Assert.That(TrayMenuPopup.FindTaskbarEdge(Pt(1700, 900), monitor, Rect(0, 0, 1920, 1040), null).Edge).IsEqualTo(ScreenEdge.None);
    }

    // ── Mapping tray items onto the shared menu ────────────────────────

    [Test]
    public async Task Mapping_CoversEveryKind()
    {
        var icon = new Icon("M2 2h20v20H2z", new Size(24, 24), 16f, "Box");
        var custom = new Label("custom");
        var items = TrayMenuMapping.ToContextMenuItems(
        [
            TrayMenuItem.Action("Open", () => { }, icon),
            TrayMenuItem.Separator(),
            TrayMenuItem.Header("Section"),
            TrayMenuItem.Info("Version: 1.0"),
            TrayMenuItem.Toggle("Launch at login", true, _ => { }),
            TrayMenuItem.Radio("Compact", false, () => { }),
            TrayMenuItem.Action("Pinned", () => { }, @checked: true),
            TrayMenuItem.Submenu("More", [TrayMenuItem.Action("Inner", () => { })], enabled: false),
            TrayMenuItem.Custom(custom),
        ], iconColor: null);

        await Assert.That(items.Select(i => i.Kind)).IsEquivalentTo(new[]
        {
            MenuItemKind.Action, MenuItemKind.Separator, MenuItemKind.Header, MenuItemKind.Action, MenuItemKind.Toggle,
            MenuItemKind.Radio, MenuItemKind.Toggle, MenuItemKind.Submenu, MenuItemKind.Custom,
        });
        await Assert.That(items[0].Icon).IsTypeOf<IconView>();
        await Assert.That(items[3].Disabled).IsTrue().Because("an informational row cannot be chosen");
        await Assert.That(MenuOverlay.IsActionable(items[3])).IsFalse();
        await Assert.That(items[4].IsChecked).IsTrue();
        await Assert.That(items[6].IsChecked).IsTrue().Because("a checked action shows its check mark");
        await Assert.That(items[7].Disabled).IsTrue();
        await Assert.That(items[7].Items!.Single().Label).IsEqualTo("Inner");
        await Assert.That(items[8].Content).IsSameReferenceAs(custom);
    }

    [Test]
    public async Task Mapping_ToggleAndRadio_RunTheirHandlers()
    {
        bool? toggled = null;
        int selected = 0;
        var items = TrayMenuMapping.ToContextMenuItems(
        [
            TrayMenuItem.Toggle("Login", isChecked: true, value => { toggled = value; }),
            TrayMenuItem.Radio("Dark", isSelected: false, () => { selected++; }),
        ], iconColor: null);

        items[0].OnClick!();
        items[1].OnClick!();

        await Assert.That(toggled).IsEqualTo(false);
        await Assert.That(selected).IsEqualTo(1);
    }

    [Test]
    public async Task Mapping_BitmapIcon_IsA16PxDecorativeImage()
    {
        using var bitmap = ImageSource.FromBytes(new byte[16 * 16 * 4], 16, 16);
        var items = TrayMenuMapping.ToContextMenuItems([TrayMenuItem.Action("Open", () => { }, bitmap)], iconColor: null);
        await Assert.That(items[0].Icon).IsTypeOf<Image>();
    }

    [Test]
    public async Task Mapping_HighContrast_TintsVectorIcons()
    {
        var icon = new Icon("M2 2h20v20H2z", new Size(24, 24), 16f, "Box");
        var color = new ColorValue("#FFFF00");
        var items = TrayMenuMapping.ToContextMenuItems([TrayMenuItem.Action("Open", () => { }, icon)], color);
        await Assert.That(((IconView)items[0].Icon!).ColorOverride).IsEqualTo(color);
    }

    [Test]
    public async Task Mapping_AccessKeys_FollowNativeMenuLabels()
    {
        var items = TrayMenuMapping.ToContextMenuItems(
        [
            TrayMenuItem.Action("&Quit", () => { }),
            TrayMenuItem.Action("Save && Exit", () => { }),
            TrayMenuItem.Action("Op&en", () => { }),
        ], iconColor: null);

        await Assert.That(items[0].Label).IsEqualTo("Quit");
        await Assert.That(items[0].AccessKeyIndex).IsEqualTo(0);
        await Assert.That(items[1].Label).IsEqualTo("Save & Exit");
        await Assert.That(items[1].AccessKeyIndex).IsEqualTo(-1);
        await Assert.That(items[1].AccessKey).IsEqualTo('S').Because("without a marker the first letter is the key");
        await Assert.That(items[2].Label).IsEqualTo("Open");
        await Assert.That(items[2].AccessKey).IsEqualTo('E');
    }

    [Test]
    public async Task TrayMenuItem_Kinds_AreReported()
    {
        await Assert.That(TrayMenuItem.Separator().IsSeparator).IsTrue();
        await Assert.That(TrayMenuItem.Info("v1").Kind).IsEqualTo(TrayMenuItemKind.Info);
        await Assert.That(TrayMenuItem.Info("v1").Enabled).IsFalse();
        await Assert.That(TrayMenuItem.Header("H").Kind).IsEqualTo(TrayMenuItemKind.Header);
        await Assert.That(TrayMenuItem.Custom(new Label("x")).Kind).IsEqualTo(TrayMenuItemKind.Custom);
        await Assert.That(TrayMenuItem.Action("A").Kind).IsEqualTo(TrayMenuItemKind.Action);
    }

    // ── The menu's own dispatcher: keyboard, pointer, dismissal ─────────

    private static readonly List<string> Log = [];

    private static IReadOnlyList<TrayMenuItem> TrayItems() =>
    [
        TrayMenuItem.Action("Open", () => { Log.Add("open"); }),
        TrayMenuItem.Separator(),
        TrayMenuItem.Info("Version: 2.7.3"),
        TrayMenuItem.Action("Manual", () => { Log.Add("manual"); }),
        TrayMenuItem.Action("Mail us", () => { Log.Add("mail"); }),
        TrayMenuItem.Submenu("Density", [TrayMenuItem.Radio("Compact", true, () => { Log.Add("compact"); })]),
        TrayMenuItem.Action("&Quit", () => { Log.Add("quit"); }),
    ];

    private static InputDispatcher OpenHost(bool fromKeyboard = false)
    {
        Log.Clear();
        var host = InputDispatcher.CreateMenuHost();
        host.OpenHostedMenu(
            TrayMenuMapping.ToContextMenuItems(TrayItems(), iconColor: null),
            MenuPlacement.FromTaskbar(new Point(500f, 720f), ScreenEdge.Bottom, 700f),
            MenuMetrics.ForTray(),
            Work,
            pixelRatio: 1f,
            highlightFirst: fromKeyboard,
            measure: (text, size) => text.Length * size * 0.5f);
        return host;
    }

    private static void Press(InputDispatcher host, Key key, ModifierKeys modifiers = ModifierKeys.None, char? character = null)
    {
        host.HostKey(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        if (character is { } c)
        {
            host.HostKey(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers, Character = c });
        }
    }

    [Test]
    public async Task Keyboard_Open_HighlightsTheFirstItem_AndEnterRunsIt()
    {
        var host = OpenHost(fromKeyboard: true);
        await Assert.That(host.Menu!.Levels[0].Highlighted).IsEqualTo(0);

        Press(host, Cascade.UI.Key.Enter, character: '\r');

        await Assert.That(host.IsMenuOpen).IsFalse();
        await Assert.That(string.Join(",", Log)).IsEqualTo("open");
    }

    [Test]
    public async Task Arrows_SkipSeparatorsAndInformationalRows()
    {
        var host = OpenHost(fromKeyboard: true);
        Press(host, Cascade.UI.Key.Down);
        var level = host.Menu!.Levels[0];
        await Assert.That(level.Items[level.Highlighted].Label).IsEqualTo("Manual");
    }

    [Test]
    public async Task RightOpensTheSubmenu_LeftAndEscapeCloseIt()
    {
        var host = OpenHost(fromKeyboard: true);
        Press(host, Cascade.UI.Key.End);
        Press(host, Cascade.UI.Key.Up);
        Press(host, Cascade.UI.Key.Right);
        await Assert.That(host.Menu!.Levels.Count).IsEqualTo(2);

        Press(host, Cascade.UI.Key.Left);
        await Assert.That(host.Menu!.Levels.Count).IsEqualTo(1);

        Press(host, Cascade.UI.Key.Right);
        Press(host, Cascade.UI.Key.Escape);
        await Assert.That(host.Menu!.Levels.Count).IsEqualTo(1).Because("Escape closes a submenu first");
        await Assert.That(host.IsMenuOpen).IsTrue();
    }

    [Test]
    [Arguments(global::Cascade.UI.Key.Escape, ModifierKeys.None)]
    [Arguments(global::Cascade.UI.Key.Apps, ModifierKeys.None)]
    [Arguments(global::Cascade.UI.Key.F10, ModifierKeys.Shift)]
    [Arguments(global::Cascade.UI.Key.None, ModifierKeys.Alt)]
    public async Task DismissalKeys_CloseWithoutRunningAnything(Key key, ModifierKeys modifiers)
    {
        var host = OpenHost();
        int repaints = 0;
        host.RequestRepaint = () => { repaints++; };

        Press(host, key, modifiers);

        await Assert.That(host.IsMenuOpen).IsFalse();
        await Assert.That(repaints).IsGreaterThan(0).Because("the popup closes its windows from the repaint callback");
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task UniqueAccessKey_RunsTheItemAtOnce()
    {
        var host = OpenHost();
        Press(host, Cascade.UI.Key.Q, character: 'q');
        await Assert.That(host.IsMenuOpen).IsFalse();
        await Assert.That(string.Join(",", Log)).IsEqualTo("quit");
    }

    [Test]
    public async Task SharedAccessKey_CyclesTheHighlight()
    {
        var host = OpenHost();
        Press(host, Cascade.UI.Key.M, character: 'm');
        var level = host.Menu!.Levels[0];
        await Assert.That(level.Items[level.Highlighted].Label).IsEqualTo("Manual");

        Press(host, Cascade.UI.Key.M, character: 'm');
        await Assert.That(level.Items[level.Highlighted].Label).IsEqualTo("Mail us");
        await Assert.That(host.IsMenuOpen).IsTrue();
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task Pointer_HoverHighlights_ClickRuns()
    {
        var host = OpenHost();
        var level = host.Menu!.Levels[0];
        int manual = Array.FindIndex(level.Items, i => i.Label == "Manual");
        float x = level.Bounds.X + 40f;
        float y = level.ItemTop(manual) + (level.ItemHeights[manual] / 2f);

        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseMove, X = x, Y = y });
        await Assert.That(level.Highlighted).IsEqualTo(manual);

        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseDown, X = x, Y = y, Button = NativeMouseButton.Left });
        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseUp, X = x, Y = y, Button = NativeMouseButton.Left });

        await Assert.That(host.IsMenuOpen).IsFalse();
        await Assert.That(string.Join(",", Log)).IsEqualTo("manual");
    }

    [Test]
    public async Task ClickOnAnInformationalRow_DoesNothing()
    {
        var host = OpenHost();
        var level = host.Menu!.Levels[0];
        int info = Array.FindIndex(level.Items, i => i.Label == "Version: 2.7.3");
        float x = level.Bounds.X + 40f;
        float y = level.ItemTop(info) + 5f;

        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseDown, X = x, Y = y, Button = NativeMouseButton.Left });
        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseUp, X = x, Y = y, Button = NativeMouseButton.Left });

        await Assert.That(host.IsMenuOpen).IsTrue();
        await Assert.That(Log).IsEmpty();
    }

    [Test]
    public async Task MenuHost_NeverBecomesTheActiveDispatcher()
    {
        var before = InputDispatcher.Active;
        var host = OpenHost(fromKeyboard: true);
        Press(host, Cascade.UI.Key.Down);
        host.HostMouse(new NativeMouseEvent { Type = NativeMouseEventType.MouseMove, X = 600f, Y = 500f });
        host.AutomationActivateMenuItem(0, host.Menu!.Levels[0].Highlighted);

        await Assert.That(ReferenceEquals(InputDispatcher.Active, host)).IsFalse();
        await Assert.That(ReferenceEquals(InputDispatcher.Active, before)).IsTrue();
    }

    // ── Theme ──────────────────────────────────────────────────────────

    [Test]
    public async Task ResolveTheme_DarkAndLightVariantsOfTheAppTheme()
    {
        var light = new AppleTheme(ThemeMode.Light);
        var dark = TrayMenuPopup.ResolveTheme(light, dark: true);
        await Assert.That(dark).IsTypeOf<AppleTheme>();
        await Assert.That(dark.Mode).IsEqualTo(ThemeMode.Dark);
        await Assert.That(TrayMenuPopup.ResolveTheme(light, dark: false)).IsSameReferenceAs(light);

        var appDark = new FluentTheme(ThemeMode.Dark);
        await Assert.That(TrayMenuPopup.ResolveTheme(appDark, dark: true)).IsSameReferenceAs(appDark);
        await Assert.That(TrayMenuPopup.ResolveTheme(appDark, dark: false).Mode).IsEqualTo(ThemeMode.Light);
    }

    [Test]
    public async Task WithMode_KeepsACustomSubclass()
    {
        var custom = new CustomApple();
        await Assert.That(custom.WithMode(ThemeMode.Dark)).IsSameReferenceAs(custom);
    }

    private sealed class CustomApple : AppleTheme
    {
    }

    [Test]
    public async Task SystemColors_MapToTheMenuPalette()
    {
        var palette = MenuPalette.FromSystemColors(index => index switch
        {
            SystemColorIndex.Menu => new ColorValue("#000000"),
            SystemColorIndex.MenuText => new ColorValue("#FFFFFF"),
            SystemColorIndex.Highlight => new ColorValue("#1AEBFF"),
            SystemColorIndex.HighlightText => new ColorValue("#000001"),
            SystemColorIndex.GrayText => new ColorValue("#3FF23F"),
            _ => new ColorValue("#FF00FF"),
        });

        await Assert.That(palette.IsSystemColors).IsTrue();
        await Assert.That(palette.Background).IsEqualTo(new ColorValue("#000000"));
        await Assert.That(palette.Text).IsEqualTo(new ColorValue("#FFFFFF"));
        await Assert.That(palette.HighlightBackground).IsEqualTo(new ColorValue("#1AEBFF"));
        await Assert.That(palette.HighlightText).IsEqualTo(new ColorValue("#000001"));
        await Assert.That(palette.DisabledText).IsEqualTo(new ColorValue("#3FF23F"));
    }

    [Test]
    public async Task ColorRef_IsBgrInTheLowBytes()
    {
        await Assert.That(TrayMenuPopup.ToColorRef(new ColorValue("#102030"))).IsEqualTo(0x302010);
    }

    // ── Metrics ────────────────────────────────────────────────────────

    [Test]
    public async Task TrayMetrics_AreRoomierThanInWindowMenus()
    {
        var tray = MenuMetrics.ForTray();
        var inWindow = MenuMetrics.FromTheme(new FluentTheme(ThemeMode.Dark));
        await Assert.That(tray.ItemHeight).IsGreaterThan(inWindow.ItemHeight);
        await Assert.That(tray.FontSize).IsEqualTo(13f);
        await Assert.That(tray.IconColumn).IsGreaterThanOrEqualTo(TrayMenuMapping.IconSize + 6f);
    }

    [Test]
    public async Task AppleMenus_UseTheMenuTextSize_NotBodyText()
    {
        var apple = new AppleTheme(ThemeMode.Dark);
        await Assert.That(MenuMetrics.FromTheme(apple).FontSize).IsEqualTo(14f);
        await Assert.That(apple.Typography.Body.Size).IsEqualTo(17f);
        var fluent = new FluentTheme(ThemeMode.Dark);
        await Assert.That(MenuMetrics.FromTheme(fluent).FontSize).IsEqualTo(fluent.Typography.Body.Size);
    }

    // ── Access-key labels ──────────────────────────────────────────────

    [Test]
    [Arguments("&File", "File", 0)]
    [Arguments("Save && Exit", "Save & Exit", -1)]
    [Arguments("E&xit", "Exit", 1)]
    [Arguments("Plain", "Plain", -1)]
    [Arguments("", "", -1)]
    public async Task AccessKeyText_Parse(string label, string display, int index)
    {
        var (shown, key) = AccessKeyText.Parse(label, firstLetterFallback: false);
        await Assert.That(shown).IsEqualTo(display);
        await Assert.That(key).IsEqualTo(index);
    }

    [Test]
    public async Task AccessKeyText_FirstLetterFallback()
    {
        await Assert.That(AccessKeyText.Parse("  9 lives", firstLetterFallback: true)).IsEqualTo(("  9 lives", 2));
    }

    private static Win32.RECT Rect(int left, int top, int right, int bottom)
    {
        return new Win32.RECT { left = left, top = top, right = right, bottom = bottom };
    }

    private static Win32.POINT Pt(int x, int y)
    {
        return new Win32.POINT { x = x, y = y };
    }
}

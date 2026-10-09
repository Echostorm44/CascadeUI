#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// <see cref="MenuBar"/> on the shared menu overlay: a click opens a menu (the overlay, owned by
/// the bar), moving across the bar switches menus, the open label closes it; toggles, radios,
/// headers and custom rows behave as menu items; Alt (tapped alone) and F10 give the bar keyboard
/// focus, Left/Right move between menus (also from an open menu), Down opens, Escape steps back
/// to the bar and then out, Alt+letter opens by access key, and the platform's own Alt/F10
/// handling is suppressed only when the bar used the key.
/// </summary>
[NotInParallel(["ContextMenu", "FocusManager"])]
public sealed class MenuBarOverlayTests
{
    private readonly FluentTheme theme = new();
    private InputDispatcher dispatcher = null!;
    private readonly List<string> log = [];
    private Node root = null!;
    private bool wrap;
    private string mode = "Light";

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher { ViewportSize = new Size(800, 600) };
        FocusManager.Reset();
        log.Clear();
        wrap = false;
        mode = "Light";
    }

    private MenuBar Build()
    {
        var bar = new MenuBar(
            new Menu("&File",
                MenuItem.Action("New", () => { log.Add("new"); }),
                MenuItem.Separator(),
                MenuItem.Action("Quit", () => { log.Add("quit"); })),
            new Menu("&Edit",
                MenuItem.Header("Clipboard"),
                MenuItem.Action("Copy", () => { log.Add("copy"); }),
                MenuItem.Submenu("Paste as", MenuItem.Action("Plain", () => { log.Add("plain"); }))),
            new Menu("View",
                MenuItem.Toggle("Word wrap", wrap, v => { wrap = v; log.Add($"wrap={v}"); }),
                MenuItem.Radio("Light", "Light", new Bindable<string>(mode, v => { mode = v; log.Add($"mode={v}"); })),
                MenuItem.Radio("Dark", "Dark", new Bindable<string>(mode, v => { mode = v; log.Add($"mode={v}"); })),
                MenuItem.Custom(new Row(spacing: 4, children:
                [
                    new Button("Zoom in", () => { log.Add("zoom"); }).Width(100).Height(28),
                ]))));
        root = new Column(spacing: 0, children: [bar, new Button("Body", () => { })]);
        root.LayoutData.Bounds = new Rect(0, 0, 800, 600);
        root.LayoutData.IsVisible = true;
        new LayoutEngine().Layout(root, LayoutConstraints.Tight(new Size(800, 600)));
        dispatcher.SetRoot(root);
        Paint(root);
        return bar;
    }

    private void Paint(Node root)
    {
        var painter = new NodePainter(new DrawContext { Size = new Size(800, 600), PixelRatio = 1f }, theme);
        painter.Menu = dispatcher.Menu;
        painter.Paint(root);
    }

    private static Point LabelCenter(MenuBar bar, int index)
    {
        var b = bar.MenuLabelBounds[index];
        return new Point(b.X + (b.Width / 2f), b.Y + (b.Height / 2f));
    }

    private void Mouse(NativeMouseEventType type, Point p)
    {
        dispatcher.HandleMouseEvent(new NativeMouseEvent { X = p.X, Y = p.Y, Type = type, Button = NativeMouseButton.Left });
    }

    private void Click(Point p)
    {
        Mouse(NativeMouseEventType.MouseDown, p);
        Mouse(NativeMouseEventType.MouseUp, p);
    }

    private void Press(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyUp, Modifiers = modifiers });
    }

    /// <summary>Alt pressed and released on its own.</summary>
    private bool TapAlt()
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyDown, Modifiers = ModifierKeys.Alt });
        dispatcher.TakeSystemKeyHandled();
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyUp, Modifiers = ModifierKeys.None });
        return dispatcher.TakeSystemKeyHandled();
    }

    private MenuOverlay Overlay => dispatcher.Menu!;

    private string HighlightedLabel()
    {
        var level = Overlay.Levels[^1];
        return level.Highlighted < 0 ? "none" : level.Items[level.Highlighted].Label ?? "?";
    }

    // ── Pointer ──────────────────────────────────────────────────────

    [Test]
    public async Task Click_OpensTheMenuOnTheOverlay_BelowItsLabel()
    {
        var bar = Build();
        Click(LabelCenter(bar, 1));

        await Assert.That(Overlay.IsOpen).IsTrue();
        await Assert.That(Overlay.Owner).IsSameReferenceAs(bar);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(1);
        var panel = Overlay.Levels[0].Bounds;
        await Assert.That(panel.X).IsEqualTo(MathF.Round(bar.MenuLabelBounds[1].X));
        await Assert.That(panel.Y).IsGreaterThan(bar.MenuLabelBounds[1].Bottom);
        await Assert.That(string.Join(",", Overlay.Levels[0].Items.Select(i => i.Kind))).IsEqualTo("Header,Action,Submenu");

        // The display label drops the access-key marker.
        await Assert.That(bar.Menus[0].DisplayLabel).IsEqualTo("File");
    }

    [Test]
    public async Task MovingAcrossTheBar_SwitchesMenus_ClickingTheOpenLabelCloses()
    {
        var bar = Build();
        Click(LabelCenter(bar, 0));
        Mouse(NativeMouseEventType.MouseMove, LabelCenter(bar, 2));
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(2);
        await Assert.That(Overlay.Levels[0].Items[0].Label).IsEqualTo("Word wrap");

        Click(LabelCenter(bar, 2));
        await Assert.That(Overlay.IsOpen).IsFalse();
        await Assert.That(bar.IsActive).IsFalse();
    }

    [Test]
    public async Task ChoosingAnItem_RunsIt_AndLeavesTheBar()
    {
        var bar = Build();
        Click(LabelCenter(bar, 0));
        var level = Overlay.Levels[0];
        var newItem = new Point(level.Bounds.X + 30, level.ItemTop(0) + 5);
        Click(newItem);

        await Assert.That(string.Join(",", log)).IsEqualTo("new");
        await Assert.That(Overlay.IsOpen).IsFalse();
        await Assert.That(bar.IsActive).IsFalse();
    }

    [Test]
    public async Task ToggleAndRadio_ChangeTheirValues_ACustomRowsButtonRuns()
    {
        var bar = Build();
        Click(LabelCenter(bar, 2));
        var items = Overlay.Levels[0].Items;
        await Assert.That(items[0].Kind).IsEqualTo(MenuItemKind.Toggle);
        await Assert.That(items[0].IsChecked).IsFalse();
        await Assert.That(items[1].IsChecked).IsTrue();
        await Assert.That(items[2].Kind).IsEqualTo(MenuItemKind.Radio);

        Press(Key.Down);
        Press(Key.Enter);
        await Assert.That(wrap).IsTrue();

        Click(LabelCenter(bar, 2));
        Press(Key.End);
        await Assert.That(HighlightedLabel()).IsEqualTo("Dark");
        Press(Key.Enter);
        await Assert.That(mode).IsEqualTo("Dark");

        Click(LabelCenter(bar, 2));
        Paint(root);
        var level = Overlay.Levels[0];
        var button = new Point(level.Bounds.X + 6 + 50, level.ItemTop(3) + 14);
        Click(button);
        await Assert.That(string.Join(",", log)).IsEqualTo("wrap=True,mode=Dark,zoom");
        await Assert.That(Overlay.IsOpen).IsFalse();
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    [Test]
    public async Task AltTap_FocusesTheBar_ArrowsMove_DownOpens_EscapeStepsBack()
    {
        var bar = Build();
        bool suppressed = TapAlt();
        await Assert.That(suppressed).IsTrue();
        await Assert.That(bar.FocusedMenuIndex).IsEqualTo(0);
        await Assert.That(bar.ShowAccessKeys).IsTrue();

        Press(Key.Right);
        await Assert.That(bar.FocusedMenuIndex).IsEqualTo(1);
        Press(Key.Down);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(1);
        await Assert.That(HighlightedLabel()).IsEqualTo("Copy");

        // Right on an item without a submenu moves to the next menu; Left back.
        Press(Key.Right);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(2);
        Press(Key.Left);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(1);

        // Right on a submenu item opens it; Left closes it (and stays in the menu).
        Press(Key.Down);
        await Assert.That(HighlightedLabel()).IsEqualTo("Paste as");
        Press(Key.Right);
        await Assert.That(Overlay.Levels.Count).IsEqualTo(2);
        Press(Key.Left);
        await Assert.That(Overlay.Levels.Count).IsEqualTo(1);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(1);

        Press(Key.Escape);
        await Assert.That(Overlay.IsOpen).IsFalse();
        await Assert.That(bar.FocusedMenuIndex).IsEqualTo(1);
        Press(Key.Escape);
        await Assert.That(bar.IsActive).IsFalse();
        await Assert.That(bar.ShowAccessKeys).IsFalse();
    }

    [Test]
    public async Task AltLetter_OpensByAccessKey_TheFirstLetterWithoutAMarker()
    {
        var bar = Build();
        Press(Key.E, ModifierKeys.Alt);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(1);
        await Assert.That(HighlightedLabel()).IsEqualTo("Copy");

        Press(Key.V, ModifierKeys.Alt);
        await Assert.That(bar.OpenMenuIndex).IsEqualTo(2);

        // An Alt+letter no menu has is left alone (for app shortcuts, or the platform).
        Press(Key.Escape);
        Press(Key.Escape);
        Press(Key.Q, ModifierKeys.Alt);
        await Assert.That(Overlay.IsOpen).IsFalse();
    }

    [Test]
    public async Task F10_TogglesTheBar_AndIsKeptFromThePlatform()
    {
        var bar = Build();
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.F10, Type = NativeKeyEventType.KeyDown });
        await Assert.That(dispatcher.TakeSystemKeyHandled()).IsTrue();
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.F10, Type = NativeKeyEventType.KeyUp });
        await Assert.That(dispatcher.TakeSystemKeyHandled()).IsTrue();
        await Assert.That(bar.FocusedMenuIndex).IsEqualTo(0);

        Press(Key.F10);
        await Assert.That(bar.IsActive).IsFalse();
    }

    [Test]
    public async Task AltWhileAMenuIsOpen_LeavesTheBar_AndItsReleaseDoesNotReactivateIt()
    {
        var bar = Build();
        Click(LabelCenter(bar, 0));
        bool suppressed = TapAlt();

        await Assert.That(suppressed).IsTrue();
        await Assert.That(Overlay.IsOpen).IsFalse();
        await Assert.That(bar.IsActive).IsFalse();
    }

    [Test]
    public async Task WithoutAMenuBar_AltIsLeftToThePlatform()
    {
        var plain = new Column(spacing: 0, children: [new Button("Body", () => { })]);
        plain.LayoutData.Bounds = new Rect(0, 0, 800, 600);
        plain.LayoutData.IsVisible = true;
        dispatcher.SetRoot(plain);

        await Assert.That(TapAlt()).IsFalse();
    }

    [Test]
    public async Task WindowDeactivation_LeavesTheBar()
    {
        var bar = Build();
        Click(LabelCenter(bar, 0));
        dispatcher.HandleWindowDeactivation();

        await Assert.That(Overlay.IsOpen).IsFalse();
        await Assert.That(bar.IsActive).IsFalse();
    }

    [Test]
    public async Task TheStateSurvivesAReRender()
    {
        var bar = Build();
        Click(LabelCenter(bar, 1));
        var next = new MenuBar([.. bar.Menus]);
        InputDispatcher.NotifyNodeReplaced(bar, next);

        await Assert.That(next.OpenMenuIndex).IsEqualTo(1);
        await Assert.That(Overlay.Owner).IsSameReferenceAs(next);
        Press(Key.Right);
        await Assert.That(next.OpenMenuIndex).IsEqualTo(2);
    }
}

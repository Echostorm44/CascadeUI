namespace Cascade.UI;

/// <summary>
/// A dispatcher that hosts nothing but a menu — the tray menu, drawn in its own popup windows.
/// It runs the same menu policy as a window's context menu (hover, submenus, the keyboard,
/// activation, and the UI Automation patterns that reach menus through
/// <see cref="InputDispatcher"/>), without a page beneath it, and it never becomes
/// <see cref="Active"/>: app code calling <see cref="ContextMenu.Show(IReadOnlyList{ContextMenuItem})"/>
/// or a re-render of the main window keeps talking to the main window's dispatcher.
/// </summary>
internal sealed partial class InputDispatcher
{
    private readonly bool isMenuHost;

    /// <summary>Creates a window's dispatcher.</summary>
    internal InputDispatcher()
    {
    }

    private InputDispatcher(bool menuHost)
    {
        isMenuHost = menuHost;
    }

    /// <summary>Creates a dispatcher that only hosts a menu (see the type remarks).</summary>
    internal static InputDispatcher CreateMenuHost()
    {
        return new InputDispatcher(menuHost: true);
    }

    /// <summary>Whether this dispatcher only hosts a menu.</summary>
    internal bool IsMenuHost => isMenuHost;

    /// <summary>
    /// Opens the hosted menu with explicit geometry: <paramref name="viewport"/> is the area the
    /// panels must stay inside (a monitor's work area, in the host's logical coordinates) and
    /// <paramref name="measure"/> shapes labels with the host's font. Returns false when there is
    /// nothing to show.
    /// </summary>
    internal bool OpenHostedMenu(
        IReadOnlyList<ContextMenuItem> items,
        MenuPlacement placement,
        MenuMetrics metrics,
        Size viewport,
        float pixelRatio,
        bool highlightFirst,
        Func<string, float, float>? measure)
    {
        ArgumentNullException.ThrowIfNull(items);

        menu ??= new MenuOverlay();
        if (measure is not null)
        {
            menu.SetMeasurer(measure);
        }

        ViewportSize = viewport;
        PixelRatio = pixelRatio;
        LastInputWasKeyboard = highlightFirst;
        bool opened = menu.Open(items, placement, metrics, viewport, pixelRatio, new Point(placement.Anchor.X, placement.Anchor.Y), highlightFirst, owner: null);
        RequestRepaint?.Invoke();
        return opened;
    }

    /// <summary>A pointer event in host coordinates (any of the menu's windows).</summary>
    internal void HostMouse(NativeMouseEvent evt)
    {
        LastInputWasKeyboard = false;
        HandleMenuMouse(evt);
    }

    /// <summary>A wheel event in host coordinates.</summary>
    internal void HostScroll(NativeScrollEvent evt)
    {
        HandleMenuScroll(evt);
    }

    /// <summary>
    /// A key event from the menu's window. Key-ups are ignored; a character that belongs to a key
    /// the menu already used (Enter, Escape) is swallowed, as in a window.
    /// </summary>
    internal void HostKey(NativeKeyEvent evt)
    {
        if (evt.Type != NativeKeyEventType.KeyDown)
        {
            return;
        }

        bool isCharacter = evt.Character is not null && evt.Key == Key.None;
        if (isCharacter && suppressNextCharacter)
        {
            suppressNextCharacter = false;
            return;
        }

        if (!isCharacter)
        {
            suppressNextCharacter = false;
            if (evt.Key != Key.None)
            {
                LastInputWasKeyboard = true;
            }
        }

        HandleMenuKey(evt);
    }
}

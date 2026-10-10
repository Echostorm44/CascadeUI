namespace Cascade.UI;

/// <summary>
/// Represents a system tray icon with tooltip, click handler, and context menu.
/// On macOS this is a menu bar extra; on Linux it uses the StatusNotifierItem
/// (SNI) protocol. The same API works identically across all platforms.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private static uint nextIconId = 1;
    private static readonly object registryLock = new();
    private static readonly Dictionary<uint, TrayIcon> registry = [];

    private readonly uint iconId;
    private bool isShown;
    private bool disposed;
    private ImageSource? icon;
    private string? tooltip;
    private nint ownedIcon; // HICON built from Icon; destroyed when replaced or on Dispose
    private CocoaTray? cocoaTray;
    private LinuxTray? linuxTray;

    /// <summary>Initialises a new TrayIcon instance with a unique Win32 icon ID.</summary>
    public TrayIcon()
    {
        iconId = System.Threading.Interlocked.Increment(ref nextIconId);
    }

    /// <summary>
    /// The icon displayed in the system tray. On Windows, supply it at the tray size (16×16 at
    /// 100 % scale, 32×32 at 200 %); when null, the executable's own icon is used. Changing it
    /// while shown updates the tray.
    /// </summary>
    public ImageSource? Icon
    {
        get => icon;
        set
        {
            icon = value;
            if (isShown && OperatingSystem.IsWindows())
            {
                Modify(Win32.NIF_ICON);
            }
        }
    }

    /// <summary>
    /// The tooltip text shown when hovering over the tray icon (up to 127 characters on Windows).
    /// Changing it while shown updates the tray.
    /// </summary>
    public string? Tooltip
    {
        get => tooltip;
        set
        {
            tooltip = value;
            if (isShown && OperatingSystem.IsWindows())
            {
                Modify(Win32.NIF_TIP);
            }
        }
    }

    /// <summary>
    /// Handler invoked when the user left-clicks the tray icon.
    /// </summary>
    public Action? OnClick { get; set; }

    /// <summary>
    /// The context menu shown when the user right-clicks the tray icon
    /// (or left-clicks on macOS, following platform convention).
    /// The menu factory is re-evaluated each time the menu opens,
    /// so it reflects current app state.
    /// </summary>
    public TrayMenuDefinition? Menu { get; set; }

    /// <summary>
    /// Builds the context menu each time it opens, so checked/enabled states reflect current app
    /// state. Takes precedence over <see cref="Menu"/>.
    /// </summary>
    public Func<TrayMenuDefinition>? MenuProvider { get; set; }

    /// <summary>
    /// Whether the context menu is drawn light or dark. <see cref="ThemeMode.System"/> (the default)
    /// draws it dark when the app's theme is dark or the taskbar is (Windows' "default Windows mode"),
    /// like the shell's own menus; <see cref="ThemeMode.Light"/> or <see cref="ThemeMode.Dark"/> fixes it.
    /// The menu uses the app's theme in that mode (high contrast uses the system colours).
    /// </summary>
    public ThemeMode MenuThemeMode { get; set; } = ThemeMode.System;

    /// <summary>
    /// Shows a balloon notification near the tray icon using the Win32
    /// Shell_NotifyIcon balloon mechanism.
    /// </summary>
    /// <param name="notification">The notification to display.</param>
    public unsafe void ShowNotification(TrayNotification notification)
    {
        if (!isShown)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;
            if (hwnd == 0)
            {
                return;
            }

            Win32.NOTIFYICONDATAW nid = default;
            nid.cbSize      = (uint)sizeof(Win32.NOTIFYICONDATAW);
            nid.hWnd        = hwnd;
            nid.uID         = iconId;
            nid.uFlags      = Win32.NIF_INFO;
            nid.dwInfoFlags = Win32.NIIF_INFO;

            CopyString(notification.Body ?? "", nid.szInfo, 255);
            CopyString(notification.Title ?? "", nid.szInfoTitle, 63);

            if (!notification.Duration.IsPersistent)
            {
                nid.uVersion = (uint)Math.Clamp((int)notification.Duration.TotalMilliseconds, 0, 30000);
            }

            Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, &nid);
        }
        else if (OperatingSystem.IsMacOS())
        {
            cocoaTray?.ShowNotification(notification.Title, notification.Body);
        }
        else if (OperatingSystem.IsLinux())
        {
            linuxTray?.ShowNotification(notification.Title, notification.Body);
        }
    }

    /// <summary>
    /// Makes the tray icon visible in the system tray.
    /// </summary>
    public unsafe void Show()
    {
        if (OperatingSystem.IsWindows())
        {
            if (isShown)
            {
                UpdateIcon();
                return;
            }

            nint hwnd = App.nativeWindow?.Handle ?? 0;
            if (hwnd == 0)
            {
                return;
            }

            Win32.NOTIFYICONDATAW nid = default;
            nid.cbSize          = (uint)sizeof(Win32.NOTIFYICONDATAW);
            nid.hWnd            = hwnd;
            nid.uID             = iconId;
            nid.uFlags          = Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_MESSAGE | Win32.NIF_SHOWTIP;
            nid.uCallbackMessage = Win32.WM_TRAYICON;
            nid.hIcon           = ResolveIcon();

            CopyString(Tooltip ?? "", nid.szTip, 127);

            if (Win32.Shell_NotifyIconW(Win32.NIM_ADD, &nid))
            {
                // Version 4: messages carry the click point (the menu opens there) and the shell
                // sends WM_CONTEXTMENU / NIN_KEYSELECT when the icon is used from the keyboard.
                // Version 4 hides the standard tooltip unless NIF_SHOWTIP is set (it is, above).
                nid.uVersion = Win32.NOTIFYICON_VERSION_4;
                if (!Win32.Shell_NotifyIconW(Win32.NIM_SETVERSION, &nid))
                {
                    TrayMenuLog.Write("Shell_NotifyIcon(NIM_SETVERSION, 4) failed; keyboard access to the tray icon is limited.");
                }

                isShown = true;
                lock (registryLock)
                {
                    registry[iconId] = this;
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (cocoaTray is null)
            {
                cocoaTray = new CocoaTray();
            }

            cocoaTray.Show(Tooltip);

            if (!isShown)
            {
                isShown = true;
                lock (registryLock)
                {
                    registry[iconId] = this;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (linuxTray is null)
            {
                linuxTray = new LinuxTray();
            }

            linuxTray.Show(Tooltip);

            if (!isShown)
            {
                isShown = true;
                lock (registryLock)
                {
                    registry[iconId] = this;
                }
            }
        }
    }

    /// <summary>
    /// Removes the tray icon from the system tray.
    /// </summary>
    public unsafe void Hide()
    {
        if (!isShown)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            nint hwnd = App.nativeWindow?.Handle ?? 0;
            if (hwnd == 0)
            {
                return;
            }

            Win32.NOTIFYICONDATAW nid = default;
            nid.cbSize = (uint)sizeof(Win32.NOTIFYICONDATAW);
            nid.hWnd   = hwnd;
            nid.uID    = iconId;

            Win32.Shell_NotifyIconW(Win32.NIM_DELETE, &nid);
            isShown = false;
            TrayMenuPopup.CloseCurrent();
            ReleaseOwnedIcon();

            lock (registryLock)
            {
                registry.Remove(iconId);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            cocoaTray?.Hide();
            isShown = false;

            lock (registryLock)
            {
                registry.Remove(iconId);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            linuxTray?.Hide();
            isShown = false;

            lock (registryLock)
            {
                registry.Remove(iconId);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (OperatingSystem.IsWindows())
        {
            Hide();
            ReleaseOwnedIcon();
        }
        else if (OperatingSystem.IsMacOS())
        {
            cocoaTray?.Dispose();
            cocoaTray = null;
        }
        else if (OperatingSystem.IsLinux())
        {
            linuxTray?.Dispose();
            linuxTray = null;
        }
    }

    // ── Internal message routing ──────────────────────────────────────

    /// <summary>
    /// Handles a WM_TRAYICON callback routed from the App message loop. The icon uses
    /// NOTIFYICON_VERSION_4: LOWORD(lParam) is the event, HIWORD(lParam) the icon id, and wParam
    /// the anchor point in screen pixels (the click point, or the icon for keyboard events).
    /// </summary>
    internal static void HandleTrayMessage(nuint wParam, nint lParam)
    {
        uint notifyMsg = (uint)(lParam.ToInt64() & 0xFFFF);
        uint iconId = (uint)((lParam.ToInt64() >> 16) & 0xFFFF);
        TrayIcon? target;
        lock (registryLock)
        {
            if (!registry.TryGetValue(iconId, out target))
            {
                return;
            }
        }

        var anchor = new Win32.POINT { x = Win32.GetXLParam((nint)wParam), y = Win32.GetYLParam((nint)wParam) };
        switch (notifyMsg)
        {
            case Win32.WM_LBUTTONUP:
                target.Activate(anchor, fromKeyboard: false);
                break;

            case Win32.NIN_KEYSELECT:
                // Enter or Space on the focused icon. The shell sends Enter's twice.
                long now = Environment.TickCount64;
                if (now - target.lastKeySelect > KeySelectRepeatMs)
                {
                    target.Activate(anchor, fromKeyboard: true);
                }
                target.lastKeySelect = now;
                break;

            case Win32.WM_RBUTTONUP:
                // The shell follows a right-click with WM_CONTEXTMENU, which opens the menu.
                target.lastRightButtonUp = Environment.TickCount64;
                break;

            case Win32.WM_CONTEXTMENU:
                // A right-click, or the context-menu key / Shift+F10 on the focused icon.
                bool fromMouse = Environment.TickCount64 - target.lastRightButtonUp <= ContextMenuAfterClickMs;
                target.lastRightButtonUp = 0;
                target.ShowMenu(anchor, fromKeyboard: !fromMouse);
                break;
        }
    }

    // A second NIN_KEYSELECT this soon after the first is Enter's duplicate.
    private const long KeySelectRepeatMs = 400;

    // WM_CONTEXTMENU this soon after WM_RBUTTONUP came from that click.
    private const long ContextMenuAfterClickMs = 1000;

    private long lastKeySelect;
    private long lastRightButtonUp;

    /// <summary>The icon was clicked or activated from the keyboard: OnClick, or the menu without one.</summary>
    private void Activate(Win32.POINT anchor, bool fromKeyboard)
    {
        if (OnClick is { } onClick)
        {
            onClick();
            return;
        }

        ShowMenu(anchor, fromKeyboard);
    }

    /// <summary>
    /// Re-adds every shown icon after Explorer restarts (the "TaskbarCreated" broadcast): the new
    /// taskbar starts empty, so icons that are not re-added silently disappear.
    /// </summary>
    internal static void ReAddAll()
    {
        TrayIcon[] icons;
        lock (registryLock)
        {
            icons = [.. registry.Values];
        }
        foreach (var trayIcon in icons)
        {
            trayIcon.isShown = false;
            trayIcon.Show();
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private void UpdateIcon() => Modify(Win32.NIF_TIP | Win32.NIF_ICON);

    private unsafe void Modify(uint flags)
    {
        nint hwnd = App.nativeWindow?.Handle ?? 0;
        if (hwnd == 0)
        {
            return;
        }

        Win32.NOTIFYICONDATAW nid = default;
        nid.cbSize  = (uint)sizeof(Win32.NOTIFYICONDATAW);
        nid.hWnd    = hwnd;
        nid.uID     = iconId;
        nid.uFlags  = flags | Win32.NIF_SHOWTIP;
        if ((flags & Win32.NIF_ICON) != 0)
        {
            nid.hIcon = ResolveIcon();
        }

        CopyString(Tooltip ?? "", nid.szTip, 127);

        Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, &nid);
    }

    // The HICON for the tray: built from Icon, else the executable's embedded icon, else the
    // stock application icon.
    private nint ResolveIcon()
    {
        ReleaseOwnedIcon();
        if (icon is { } source)
        {
            ownedIcon = CreateIcon(source);
            if (ownedIcon != 0)
            {
                return ownedIcon;
            }
        }

        if (Environment.ProcessPath is { Length: > 0 } exe
            && Win32.ExtractIconExW(exe, 0, out nint large, out nint small, 1) > 0)
        {
            if (large != 0)
            {
                Win32.DestroyIcon(large);
            }
            if (small != 0)
            {
                ownedIcon = small;
                return small;
            }
        }

        return Win32.LoadIconW(0, Win32.IDI_APPLICATION);
    }

    private void ReleaseOwnedIcon()
    {
        if (ownedIcon != 0)
        {
            Win32.DestroyIcon(ownedIcon);
            ownedIcon = 0;
        }
    }

    // A 32-bpp icon from RGBA pixels: a top-down BGRA DIB with straight alpha for colour, and an
    // all-zero mask (the alpha channel decides transparency).
    private static unsafe nint CreateIcon(ImageSource source)
    {
        int width = source.Width;
        int height = source.Height;
        var header = new Win32.BITMAPV5HEADER
        {
            bV5Size = (uint)sizeof(Win32.BITMAPV5HEADER),
            bV5Width = width,
            bV5Height = -height,
            bV5Planes = 1,
            bV5BitCount = 32,
            bV5Compression = Win32.BI_BITFIELDS,
            bV5RedMask = 0x00FF0000,
            bV5GreenMask = 0x0000FF00,
            bV5BlueMask = 0x000000FF,
            bV5AlphaMask = 0xFF000000,
        };

        nint color = Win32.CreateDIBSection(0, &header, Win32.DIB_RGB_COLORS, out nint bits, 0, 0);
        if (color == 0 || bits == 0)
        {
            return 0;
        }

        var rgba = source.Pixels;
        var bgra = new Span<byte>((void*)bits, width * height * 4);
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = rgba[i + 3];
        }

        nint mask = Win32.CreateBitmap(width, height, 1, 1, 0);
        var info = new Win32.ICONINFO { fIcon = 1, hbmMask = mask, hbmColor = color };
        nint hIcon = Win32.CreateIconIndirect(ref info);
        Win32.DeleteObject(color);
        if (mask != 0)
        {
            Win32.DeleteObject(mask);
        }
        return hIcon;
    }

    /// <summary>
    /// Opens the context menu at <paramref name="anchor"/> (physical screen pixels; the cursor when
    /// the shell sent none): Cascade's own menu (<see cref="TrayMenuPopup"/>), or the native Win32
    /// menu if that could not be created.
    /// </summary>
    private void ShowMenu(Win32.POINT anchor, bool fromKeyboard)
    {
        var definition = MenuProvider?.Invoke() ?? Menu;
        if (definition is null || definition.Items.Count == 0)
        {
            return;
        }

        if (anchor.x == 0 && anchor.y == 0)
        {
            Win32.GetCursorPos(out anchor);
        }

        if (TrayMenuPopup.TryShow(definition.Items, anchor, fromKeyboard, MenuThemeMode, Tooltip))
        {
            return;
        }

        ShowNativeMenu(definition, anchor);
    }

    // The fallback: a Win32 popup menu, run modally, that then runs the chosen item. The owner
    // window must be foreground for the menu to close when the user clicks elsewhere, and the
    // posted WM_NULL makes the next click after it work (both documented TrackPopupMenu
    // requirements).
    private static void ShowNativeMenu(TrayMenuDefinition definition, Win32.POINT anchor)
    {
        nint hwnd = App.nativeWindow?.Handle ?? 0;
        if (hwnd == 0)
        {
            return;
        }

        var actions = new List<Action?>();
        nint menu = BuildMenu(definition.Items, actions);
        try
        {
            Win32.SetForegroundWindow(hwnd);
            int command = Win32.TrackPopupMenu(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY, anchor.x, anchor.y, 0, hwnd, 0);
            Win32.PostMessageW(hwnd, Win32.WM_NULL, 0, 0);
            if (command > 0 && command <= actions.Count)
            {
                actions[command - 1]?.Invoke();
            }
        }
        finally
        {
            Win32.DestroyMenu(menu); // also destroys attached submenus
        }
    }

    private static nint BuildMenu(IReadOnlyList<TrayMenuItem> items, List<Action?> actions)
    {
        nint menu = Win32.CreatePopupMenu();
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, 0, null);
                continue;
            }
            if (item.Kind == TrayMenuItemKind.Custom)
            {
                continue; // a native menu cannot host a Cascade node
            }

            bool enabled = item.Enabled && item.Kind is not (TrayMenuItemKind.Header or TrayMenuItemKind.Info);
            uint flags = (enabled ? 0 : Win32.MF_GRAYED) | (item.Checked ? Win32.MF_CHECKED : 0);
            string label = item.Label ?? "";
            if (item.SubItems is { Count: > 0 } subItems)
            {
                nint submenu = BuildMenu(subItems, actions);
                Win32.AppendMenuW(menu, Win32.MF_POPUP | flags, (nuint)submenu, label);
                continue;
            }

            // Command ids are 1-based indexes into actions (0 means "menu dismissed").
            actions.Add(item.OnClick);
            Win32.AppendMenuW(menu, Win32.MF_STRING | flags, (nuint)actions.Count, label);
        }
        return menu;
    }

    private static unsafe void CopyString(string value, char* dest, int maxChars)
    {
        int len = Math.Min(value.Length, maxChars);
        for (int i = 0; i < len; i++)
        {
            dest[i] = value[i];
        }
        dest[len] = '\0';
    }
}

/// <summary>
/// An in-app notification displayed near the tray icon. Themed to match
/// the app. No OS permissions required.
/// </summary>
public sealed class TrayNotification
{
    /// <summary>The notification title.</summary>
    public string? Title { get; init; }

    /// <summary>The notification body text.</summary>
    public string? Body { get; init; }

    /// <summary>Optional icon displayed in the notification.</summary>
    public ImageSource? Icon { get; init; }

    /// <summary>How long the notification is displayed before auto-dismissing.</summary>
    public Duration Duration { get; init; } = Duration.Seconds(4);

    /// <summary>Handler invoked when the user clicks the notification.</summary>
    public Action? OnClick { get; init; }
}

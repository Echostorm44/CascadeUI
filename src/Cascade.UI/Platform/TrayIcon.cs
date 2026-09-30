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
            nid.uFlags          = Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_MESSAGE;
            nid.uCallbackMessage = Win32.WM_TRAYICON;
            nid.hIcon           = ResolveIcon();

            CopyString(Tooltip ?? "", nid.szTip, 127);

            if (Win32.Shell_NotifyIconW(Win32.NIM_ADD, &nid))
            {
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
    /// Handles WM_TRAYICON messages routed from the App message loop.
    /// </summary>
    internal static void HandleTrayMessage(uint iconId, uint notifyMsg)
    {
        TrayIcon? target;
        lock (registryLock)
        {
            if (!registry.TryGetValue(iconId, out target))
            {
                return;
            }
        }

        if (notifyMsg == Win32.WM_LBUTTONUP)
        {
            if (target.OnClick is { } onClick)
            {
                onClick();
            }
            else
            {
                target.ShowMenu();
            }
        }
        else if (notifyMsg is Win32.WM_RBUTTONUP or Win32.WM_CONTEXTMENU)
        {
            target.ShowMenu();
        }
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
        nid.uFlags  = flags;
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

    // Shows the Win32 context menu at the cursor and runs the chosen item. The owner window must be
    // foreground for the menu to close when the user clicks elsewhere, and the posted WM_NULL
    // makes the next click after it work (both documented TrackPopupMenu requirements).
    private void ShowMenu()
    {
        var definition = MenuProvider?.Invoke() ?? Menu;
        nint hwnd = App.nativeWindow?.Handle ?? 0;
        if (definition is null || definition.Items.Count == 0 || hwnd == 0)
        {
            return;
        }

        var actions = new List<Action?>();
        nint menu = BuildMenu(definition.Items, actions);
        try
        {
            Win32.GetCursorPos(out Win32.POINT pt);
            Win32.SetForegroundWindow(hwnd);
            int command = Win32.TrackPopupMenu(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY, pt.x, pt.y, 0, hwnd, 0);
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
            if (item.CustomNode is not null && item.Label is null)
            {
                continue; // a native menu cannot host a Cascade node
            }

            uint flags = (item.Enabled ? 0 : Win32.MF_GRAYED) | (item.Checked ? Win32.MF_CHECKED : 0);
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
/// Definition of a tray context menu, containing a list of menu items.
/// </summary>
public sealed class TrayMenuDefinition
{
    /// <summary>The items in the tray menu.</summary>
    public IReadOnlyList<TrayMenuItem> Items { get; init; } = [];
}

/// <summary>
/// A single item in a tray context menu. Use the static factory methods
/// to create action items, separators, submenus, or custom-rendered items.
/// </summary>
public sealed class TrayMenuItem
{
    private TrayMenuItem() { }

    /// <summary>The display label.</summary>
    public string? Label { get; private init; }

    /// <summary>Optional icon.</summary>
    public ImageSource? Icon { get; private init; }

    /// <summary>Whether this item is enabled and clickable. Default: true.</summary>
    public bool Enabled { get; private init; } = true;

    /// <summary>Whether this item shows a check mark. Default: false.</summary>
    public bool Checked { get; private init; }

    /// <summary>Click handler for action items.</summary>
    public Action? OnClick { get; private init; }

    /// <summary>Sub-items for submenu items.</summary>
    public IReadOnlyList<TrayMenuItem>? SubItems { get; private init; }

    /// <summary>Custom node content (Windows and macOS only).</summary>
    public Node? CustomNode { get; private init; }

    /// <summary>True if this is a separator item.</summary>
    public bool IsSeparator { get; private init; }

    /// <summary>Creates a clickable action menu item.</summary>
    public static TrayMenuItem Action(
        string label,
        Action? onClick = null,
        ImageSource? icon = null,
        bool enabled = true,
        bool @checked = false)
    {
        return new TrayMenuItem
        {
            Label = label,
            OnClick = onClick,
            Icon = icon,
            Enabled = enabled,
            Checked = @checked
        };
    }

    /// <summary>Creates a visual separator line.</summary>
    public static TrayMenuItem Separator()
    {
        return new TrayMenuItem { IsSeparator = true };
    }

    /// <summary>Creates a submenu containing nested items.</summary>
    public static TrayMenuItem Submenu(
        string label,
        IEnumerable<TrayMenuItem> items,
        ImageSource? icon = null)
    {
        return new TrayMenuItem
        {
            Label = label,
            SubItems = items.ToArray(),
            Icon = icon
        };
    }

    /// <summary>
    /// Creates a menu item that renders a fully custom Cascade node.
    /// Not rendered by native menus (the Windows tray menu skips it; Linux SNI substitutes a text
    /// item).
    /// </summary>
    public static TrayMenuItem Custom(Node node)
    {
        return new TrayMenuItem { CustomNode = node };
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

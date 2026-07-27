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
    private CocoaTray? cocoaTray;
    private LinuxTray? linuxTray;

    /// <summary>Initialises a new TrayIcon instance with a unique Win32 icon ID.</summary>
    public TrayIcon()
    {
        iconId = System.Threading.Interlocked.Increment(ref nextIconId);
    }

    /// <summary>
    /// The icon displayed in the system tray. Should be an .ico on Windows,
    /// .png on macOS/Linux. Set via <c>EmbeddedIcon()</c> or from an image source.
    /// </summary>
    public ImageSource? Icon { get; set; }

    /// <summary>
    /// The tooltip text shown when hovering over the tray icon.
    /// </summary>
    public string? Tooltip { get; set; }

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
            nid.hIcon           = Win32.LoadIconW(0, Win32.IDI_APPLICATION);

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

        if (OperatingSystem.IsWindows() && isShown)
        {
            Hide();
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
        TrayIcon? icon;
        lock (registryLock)
        {
            if (!registry.TryGetValue(iconId, out icon))
            {
                return;
            }
        }

        if (notifyMsg == Win32.WM_LBUTTONUP)
        {
            icon.OnClick?.Invoke();
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private unsafe void UpdateIcon()
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
        nid.uFlags  = Win32.NIF_TIP;

        CopyString(Tooltip ?? "", nid.szTip, 127);

        Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, &nid);
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
    /// Supported on Windows and macOS. On Linux (SNI), the framework
    /// substitutes a standard text item automatically.
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

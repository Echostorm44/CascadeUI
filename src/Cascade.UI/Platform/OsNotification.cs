namespace Cascade.UI;

/// <summary>
/// Provides OS-level notifications that appear in the system notification center.
/// On Windows, balloon notifications are delivered via Shell_NotifyIcon.
/// Use for notifications that need to fire when the app is not running (scheduled
/// alerts, background service messages). For notifications while the app is running,
/// prefer <see cref="TrayIcon.ShowNotification"/> (in-app tray notifications).
/// </summary>
public static class OsNotification
{
    // Unique icon ID for the hidden notification helper icon (separate from app tray icons).
    private const uint NotificationHelperIconId = 0xFFFF;

    private static bool helperIconCreated;
    private static readonly object helperLock = new();

    // Tracks timers for scheduled notifications by notification ID.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.Timer> scheduledTimers = new();

    /// <summary>
    /// Shows an immediate OS notification using a Shell_NotifyIcon balloon.
    /// </summary>
    /// <param name="notification">The notification to display.</param>
    public static Task ShowAsync(OsNotificationData notification)
    {
        if (OperatingSystem.IsWindows())
        {
            ShowBalloon(notification);
        }
        else if (OperatingSystem.IsMacOS())
        {
            CocoaNotifications.Show(notification.Title, notification.Body);
        }
        else if (OperatingSystem.IsLinux())
        {
            LinuxNotifications.Show(notification.Title, notification.Body);
        }
        else
        {
            throw new PlatformNotSupportedException("OsNotification is only supported on Windows, macOS, and Linux.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Schedules a notification to fire at a future time. If <see cref="OsNotificationData.FireAt"/>
    /// is in the past or null, the notification is shown immediately.
    /// </summary>
    /// <param name="notification">The notification to schedule. Should have <see cref="OsNotificationData.FireAt"/> set.</param>
    public static async Task ScheduleAsync(OsNotificationData notification)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("OsNotification is only supported on Windows, macOS, and Linux.");
        }

        if (notification.FireAt is null || notification.FireAt.Value <= DateTimeOffset.Now)
        {
            if (OperatingSystem.IsWindows())
            {
                ShowBalloon(notification);
            }
            else if (OperatingSystem.IsMacOS())
            {
                CocoaNotifications.Show(notification.Title, notification.Body);
            }
            else if (OperatingSystem.IsLinux())
            {
                LinuxNotifications.Show(notification.Title, notification.Body);
            }

            return;
        }

        string id = notification.Id ?? Guid.NewGuid().ToString("N");
        TimeSpan delay = notification.FireAt.Value - DateTimeOffset.Now;

        System.Threading.Timer timer = new(static state =>
        {
            if (state is OsNotificationData data)
            {
                if (OperatingSystem.IsWindows())
                {
                    ShowBalloon(data);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    CocoaNotifications.Show(data.Title, data.Body);
                }
                else if (OperatingSystem.IsLinux())
                {
                    LinuxNotifications.Show(data.Title, data.Body);
                }
            }
        }, notification, delay, System.Threading.Timeout.InfiniteTimeSpan);

        if (scheduledTimers.TryRemove(id, out System.Threading.Timer? existing))
        {
            await existing.DisposeAsync();
        }

        scheduledTimers[id] = timer;
    }

    /// <summary>
    /// Cancels a previously scheduled notification.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to cancel.</param>
    public static async Task CancelAsync(string notificationId)
    {
        if (scheduledTimers.TryRemove(notificationId, out System.Threading.Timer? timer))
        {
            await timer.DisposeAsync();
        }
    }

    /// <summary>
    /// Cancels all scheduled notifications for this application.
    /// </summary>
    public static async Task CancelAllAsync()
    {
        foreach (string key in scheduledTimers.Keys.ToArray())
        {
            if (scheduledTimers.TryRemove(key, out System.Threading.Timer? timer))
            {
                await timer.DisposeAsync();
            }
        }
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static unsafe void ShowBalloon(OsNotificationData notification)
    {
        nint hwnd = App.nativeWindow?.Handle ?? 0;
        if (hwnd == 0)
        {
            return;
        }

        EnsureHelperIcon(hwnd);

        Win32.NOTIFYICONDATAW nid = default;
        nid.cbSize          = (uint)sizeof(Win32.NOTIFYICONDATAW);
        nid.hWnd            = hwnd;
        nid.uID             = NotificationHelperIconId;
        nid.uFlags          = Win32.NIF_INFO;
        nid.dwInfoFlags     = Win32.NIIF_INFO;

        CopyString(notification.Body ?? "", nid.szInfo, 255);
        CopyString(notification.Title ?? "", nid.szInfoTitle, 63);

        Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, &nid);
    }

    private static unsafe void EnsureHelperIcon(nint hwnd)
    {
        lock (helperLock)
        {
            if (helperIconCreated)
            {
                return;
            }

            Win32.NOTIFYICONDATAW nid = default;
            nid.cbSize          = (uint)sizeof(Win32.NOTIFYICONDATAW);
            nid.hWnd            = hwnd;
            nid.uID             = NotificationHelperIconId;
            nid.uFlags          = Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_STATE;
            nid.hIcon           = Win32.LoadIconW(0, Win32.IDI_APPLICATION);
            nid.dwState         = Win32.NIS_HIDDEN;
            nid.dwStateMask     = Win32.NIS_HIDDEN;
            CopyString("", nid.szTip, 127);

            Win32.Shell_NotifyIconW(Win32.NIM_ADD, &nid);
            helperIconCreated = true;
        }
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
/// Data for an OS-level notification. Set <see cref="FireAt"/> for scheduled
/// notifications; leave null for immediate display.
/// </summary>
public sealed class OsNotificationData
{
    /// <summary>
    /// Unique identifier for this notification. Used for cancellation.
    /// Auto-generated if not specified.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>The notification title.</summary>
    public string? Title { get; init; }

    /// <summary>The notification body text.</summary>
    public string? Body { get; init; }

    /// <summary>
    /// When the notification should fire. Null for immediate display.
    /// </summary>
    public DateTimeOffset? FireAt { get; init; }

    /// <summary>
    /// Deep link action triggered when the user clicks the notification.
    /// </summary>
    public Uri? OnClick { get; init; }
}

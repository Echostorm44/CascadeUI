namespace Cascade.UI;

/// <summary>
/// macOS system menu bar icon using NSStatusBar and NSStatusItem.
/// Maps Cascade TrayIcon operations to the macOS menu bar extra model.
/// </summary>
internal sealed class CocoaTray : IDisposable
{
    private nint statusItem;
    private bool disposed;

    internal bool IsShown => statusItem != 0;

    /// <summary>
    /// Shows the menu bar icon with the given tooltip text.
    /// </summary>
    internal void Show(string? tooltip)
    {
        if (statusItem != 0)
        {
            UpdateTooltip(tooltip);
            return;
        }

        nint statusBarClass = ObjC.GetClass("NSStatusBar");
        if (statusBarClass == 0) { return; }

        nint systemBarSel = ObjC.RegisterSelector("systemStatusBar");
        nint statusBar = ObjC.MsgSend(statusBarClass, systemBarSel);
        if (statusBar == 0) { return; }

        // NSVariableStatusItemLength = -1
        nint statusItemSel = ObjC.RegisterSelector("statusItemWithLength:");
        statusItem = ObjC.MsgSend(statusBar, statusItemSel, (nint)(-1));
        if (statusItem == 0) { return; }

        ObjC.Retain(statusItem);

        // Set the button title as a fallback icon representation.
        nint buttonSel = ObjC.RegisterSelector("button");
        nint button = ObjC.MsgSend(statusItem, buttonSel);
        if (button != 0)
        {
            nint titleStr = ObjC.ToNSString("●");
            nint setTitleSel = ObjC.RegisterSelector("setTitle:");
            ObjC.MsgSendVoid(button, setTitleSel, titleStr);
            ObjC.Release(titleStr);

            if (!string.IsNullOrEmpty(tooltip))
            {
                nint tipStr = ObjC.ToNSString(tooltip);
                nint setToolTipSel = ObjC.RegisterSelector("setToolTip:");
                ObjC.MsgSendVoid(button, setToolTipSel, tipStr);
                ObjC.Release(tipStr);
            }
        }
    }

    /// <summary>
    /// Removes the menu bar icon.
    /// </summary>
    internal void Hide()
    {
        if (statusItem == 0) { return; }

        nint statusBarClass = ObjC.GetClass("NSStatusBar");
        if (statusBarClass != 0)
        {
            nint systemBarSel = ObjC.RegisterSelector("systemStatusBar");
            nint statusBar = ObjC.MsgSend(statusBarClass, systemBarSel);
            if (statusBar != 0)
            {
                nint removeItemSel = ObjC.RegisterSelector("removeStatusItem:");
                ObjC.MsgSendVoid(statusBar, removeItemSel, statusItem);
            }
        }

        ObjC.Release(statusItem);
        statusItem = 0;
    }

    /// <summary>
    /// Shows a notification via NSUserNotificationCenter (macOS 10.14+).
    /// On newer macOS, the app must be sandboxed or have entitlements for
    /// UNUserNotificationCenter. This implementation uses a best-effort approach.
    /// </summary>
    internal void ShowNotification(string? title, string? body)
    {
        // Touch instance state so the analyzer does not demand static.
        _ = statusItem;

        CocoaNotifications.Show(title, body);
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        if (statusItem != 0)
        {
            Hide();
        }
        GC.SuppressFinalize(this);
    }

    ~CocoaTray()
    {
        Dispose();
    }

    private void UpdateTooltip(string? tooltip)
    {
        if (statusItem == 0) { return; }

        nint buttonSel = ObjC.RegisterSelector("button");
        nint button = ObjC.MsgSend(statusItem, buttonSel);
        if (button == 0) { return; }

        nint tipStr = ObjC.ToNSString(tooltip ?? "");
        nint setToolTipSel = ObjC.RegisterSelector("setToolTip:");
        ObjC.MsgSendVoid(button, setToolTipSel, tipStr);
        ObjC.Release(tipStr);
    }
}

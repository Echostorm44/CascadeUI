namespace Cascade.UI;

/// <summary>
/// macOS notification delivery via NSUserNotificationCenter.
/// Works without special entitlements on non-sandboxed apps.
/// On sandboxed macOS apps or future OS versions, UNUserNotificationCenter
/// should be preferred, but requires user permission prompts.
/// </summary>
internal static class CocoaNotifications
{
    /// <summary>
    /// Delivers an immediate notification via NSUserNotificationCenter.
    /// </summary>
    internal static void Show(string? title, string? body)
    {
        nint notifClass = ObjC.GetClass("NSUserNotification");
        if (notifClass == 0) { return; }

        nint notif = ObjC.MsgSend(notifClass, ObjC.Alloc);
        notif = ObjC.MsgSend(notif, ObjC.Init);
        if (notif == 0) { return; }

        if (!string.IsNullOrEmpty(title))
        {
            nint titleStr = ObjC.ToNSString(title);
            nint setTitleSel = ObjC.RegisterSelector("setTitle:");
            ObjC.MsgSendVoid(notif, setTitleSel, titleStr);
            ObjC.Release(titleStr);
        }

        if (!string.IsNullOrEmpty(body))
        {
            nint bodyStr = ObjC.ToNSString(body);
            nint setInfoSel = ObjC.RegisterSelector("setInformativeText:");
            ObjC.MsgSendVoid(notif, setInfoSel, bodyStr);
            ObjC.Release(bodyStr);
        }

        nint centerClass = ObjC.GetClass("NSUserNotificationCenter");
        if (centerClass != 0)
        {
            nint defaultCenterSel = ObjC.RegisterSelector("defaultUserNotificationCenter");
            nint center = ObjC.MsgSend(centerClass, defaultCenterSel);
            if (center != 0)
            {
                nint deliverSel = ObjC.RegisterSelector("deliverNotification:");
                ObjC.MsgSendVoid(center, deliverSel, notif);
            }
        }

        ObjC.Release(notif);
    }
}

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for tray icon menu construction, notification data, and
/// system integration types. Does not invoke native Shell_NotifyIcon calls.
/// </summary>
public class Win32SystemTests
{
    // ── TrayMenuItem factory methods ─────────────────────────────────

    [Test]
    public async Task TrayMenuItem_Action_SetsLabel()
    {
        TrayMenuItem item = TrayMenuItem.Action("Exit");
        await Assert.That(item.Label).IsEqualTo("Exit");
    }

    [Test]
    public async Task TrayMenuItem_Action_SetsOnClick()
    {
        bool clicked = false;
        TrayMenuItem item = TrayMenuItem.Action("Click Me", onClick: () => { clicked = true; });
        item.OnClick?.Invoke();
        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task TrayMenuItem_Action_DefaultEnabled()
    {
        TrayMenuItem item = TrayMenuItem.Action("Item");
        await Assert.That(item.Enabled).IsTrue();
    }

    [Test]
    public async Task TrayMenuItem_Action_DefaultNotChecked()
    {
        TrayMenuItem item = TrayMenuItem.Action("Item");
        await Assert.That(item.Checked).IsFalse();
    }

    [Test]
    public async Task TrayMenuItem_Action_SetsEnabled()
    {
        TrayMenuItem item = TrayMenuItem.Action("Item", enabled: false);
        await Assert.That(item.Enabled).IsFalse();
    }

    [Test]
    public async Task TrayMenuItem_Action_SetsChecked()
    {
        TrayMenuItem item = TrayMenuItem.Action("Item", @checked: true);
        await Assert.That(item.Checked).IsTrue();
    }

    [Test]
    public async Task TrayMenuItem_Action_NotSeparator()
    {
        TrayMenuItem item = TrayMenuItem.Action("Item");
        await Assert.That(item.IsSeparator).IsFalse();
    }

    [Test]
    public async Task TrayMenuItem_Separator_IsSeparator()
    {
        TrayMenuItem sep = TrayMenuItem.Separator();
        await Assert.That(sep.IsSeparator).IsTrue();
    }

    [Test]
    public async Task TrayMenuItem_Separator_HasNullLabel()
    {
        TrayMenuItem sep = TrayMenuItem.Separator();
        await Assert.That(sep.Label).IsNull();
    }

    [Test]
    public async Task TrayMenuItem_Submenu_SetsLabel()
    {
        TrayMenuItem sub = TrayMenuItem.Submenu("Options", [TrayMenuItem.Action("One")]);
        await Assert.That(sub.Label).IsEqualTo("Options");
    }

    [Test]
    public async Task TrayMenuItem_Submenu_SetsSubItems()
    {
        TrayMenuItem sub = TrayMenuItem.Submenu("Menu", [
            TrayMenuItem.Action("A"),
            TrayMenuItem.Action("B")
        ]);
        await Assert.That(sub.SubItems).IsNotNull();
        await Assert.That(sub.SubItems!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TrayMenuItem_Submenu_NotSeparator()
    {
        TrayMenuItem sub = TrayMenuItem.Submenu("Options", []);
        await Assert.That(sub.IsSeparator).IsFalse();
    }

    [Test]
    public async Task TrayMenuItem_Custom_SetsCustomNode()
    {
        Node node = Node.Empty;
        TrayMenuItem item = TrayMenuItem.Custom(node);
        await Assert.That(item.CustomNode).IsEqualTo(node);
    }

    // ── TrayMenuDefinition ───────────────────────────────────────────

    [Test]
    public async Task TrayMenuDefinition_DefaultItems_IsEmpty()
    {
        TrayMenuDefinition menu = new();
        await Assert.That(menu.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TrayMenuDefinition_Items_IsStored()
    {
        TrayMenuDefinition menu = new()
        {
            Items =
            [
                TrayMenuItem.Action("Open"),
                TrayMenuItem.Separator(),
                TrayMenuItem.Action("Exit")
            ]
        };
        await Assert.That(menu.Items.Count).IsEqualTo(3);
    }

    [Test]
    public async Task TrayMenuDefinition_ItemOrder_IsPreserved()
    {
        TrayMenuDefinition menu = new()
        {
            Items =
            [
                TrayMenuItem.Action("First"),
                TrayMenuItem.Action("Second"),
                TrayMenuItem.Action("Third")
            ]
        };
        await Assert.That(menu.Items[0].Label).IsEqualTo("First");
        await Assert.That(menu.Items[1].Label).IsEqualTo("Second");
        await Assert.That(menu.Items[2].Label).IsEqualTo("Third");
    }

    // ── TrayNotification ─────────────────────────────────────────────

    [Test]
    public async Task TrayNotification_DefaultDuration_IsFourSeconds()
    {
        TrayNotification notification = new();
        await Assert.That(notification.Duration.TotalMilliseconds).IsEqualTo(4000.0);
    }

    [Test]
    public async Task TrayNotification_Title_IsStored()
    {
        TrayNotification notification = new() { Title = "Alert" };
        await Assert.That(notification.Title).IsEqualTo("Alert");
    }

    [Test]
    public async Task TrayNotification_Body_IsStored()
    {
        TrayNotification notification = new() { Body = "Something happened." };
        await Assert.That(notification.Body).IsEqualTo("Something happened.");
    }

    [Test]
    public async Task TrayNotification_OnClick_IsInvoked()
    {
        bool invoked = false;
        TrayNotification notification = new() { OnClick = () => { invoked = true; } };
        notification.OnClick?.Invoke();
        await Assert.That(invoked).IsTrue();
    }

    // ── OsNotificationData ───────────────────────────────────────────

    [Test]
    public async Task OsNotificationData_Id_IsStored()
    {
        OsNotificationData data = new() { Id = "notif-1" };
        await Assert.That(data.Id).IsEqualTo("notif-1");
    }

    [Test]
    public async Task OsNotificationData_Title_IsStored()
    {
        OsNotificationData data = new() { Title = "Hello" };
        await Assert.That(data.Title).IsEqualTo("Hello");
    }

    [Test]
    public async Task OsNotificationData_Body_IsStored()
    {
        OsNotificationData data = new() { Body = "World" };
        await Assert.That(data.Body).IsEqualTo("World");
    }

    [Test]
    public async Task OsNotificationData_FireAt_DefaultIsNull()
    {
        OsNotificationData data = new() { Title = "Test" };
        await Assert.That(data.FireAt).IsNull();
    }

    [Test]
    public async Task OsNotificationData_FireAt_IsStored()
    {
        DateTimeOffset fireAt = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        OsNotificationData data = new() { FireAt = fireAt };
        await Assert.That(data.FireAt).IsEqualTo(fireAt);
    }

    [Test]
    public async Task OsNotificationData_OnClick_IsStored()
    {
        Uri uri = new("myapp://action");
        OsNotificationData data = new() { OnClick = uri };
        await Assert.That(data.OnClick).IsEqualTo(uri);
    }

    // ── OsNotification CancelAsync no-op ─────────────────────────────

    [Test]
    public async Task OsNotification_CancelAsync_UnknownId_DoesNotThrow()
    {
        // Cancelling a non-existent notification is a no-op.
        await OsNotification.CancelAsync("non-existent-id-xyz");
    }

    [Test]
    public async Task OsNotification_CancelAllAsync_WhenEmpty_DoesNotThrow()
    {
        await OsNotification.CancelAllAsync();
    }

    // ── TrayIcon construction ─────────────────────────────────────────

    [Test]
    public async Task TrayIcon_New_IsNotShown()
    {
        using TrayIcon icon = new();
        // Icon is created but not shown — no Win32 call made until Show().
        await Assert.That(icon.Tooltip).IsNull();
    }

    [Test]
    public async Task TrayIcon_Properties_AreSettable()
    {
        using TrayIcon icon = new();
        icon.Tooltip = "My App";
        await Assert.That(icon.Tooltip).IsEqualTo("My App");
    }
}

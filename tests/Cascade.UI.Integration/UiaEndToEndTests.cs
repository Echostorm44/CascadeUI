using System.Diagnostics;
using Cascade.UI.Integration.Uia;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// Screen-reader access, end to end: the fixture app (view "a11y") is queried from this process
/// through the Windows UI Automation COM API, exactly as Narrator or NVDA would — find a button by
/// name and invoke it, read and set a text field, read and change a list's selection, toggle a
/// checkbox, follow focus as the keyboard moves it (into the list, the context menu and a dialog),
/// and hear <c>Accessibility.Announce</c> as a UIA notification.
/// </summary>
[NotInParallel("CliIntegration")]
public class UiaEndToEndTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // What the UIA client has seen so far, for failure messages.
    private static Func<string>? state;

    [Test]
    public async Task ScreenReaderClient_FindsInvokesReadsAndFollowsFocus()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "a11y" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            nint hwnd = await WindowOf(fixture);

            // A screen reader follows the foreground window; so does this client.
            Foreground.Activate(hwnd);

            var uia = UiaClient.Create();
            var window = uia.ElementFromHandle(hwnd);
            var recorder = new UiaEventRecorder();
            state = () => $"focused now: {UiaClient.Name(uia.FocusedElement()!)}; focus events: [{string.Join(", ", recorder.Focus())}]; notifications: [{string.Join(", ", recorder.Notifications())}]; events: [{string.Join(", ", recorder.Events())}]";
            UiaClient.Check(uia.Automation.AddFocusChangedEventHandler(null, recorder));
            UiaClient.Check(uia.Automation.AddNotificationEventHandler(window, UiaClient.TreeScopeSubtree, null, recorder));
            UiaClient.Check(uia.Automation.AddAutomationEventHandler(UiaClient.MenuOpenedEvent, window, UiaClient.TreeScopeSubtree, null, recorder));

            // A button, found by name, invoked through the Invoke pattern; its handler announces.
            var save = uia.FindByName(window, "Save");
            await Assert.That(save).IsNotNull().Because("the Save button is exposed by name");
            await Assert.That(UiaClient.ControlType(save!)).IsEqualTo(UiaClient.ButtonControl);
            var windowBounds = UiaClient.Bounds(window);
            var saveBounds = UiaClient.Bounds(save!);
            await Assert.That(saveBounds.Left >= windowBounds.Left && saveBounds.Bottom <= windowBounds.Bottom && saveBounds.Right > saveBounds.Left)
                .IsTrue().Because("bounds are screen pixels inside the window");

            UiaClient.Check(UiaClient.Pattern<IUIAutomationInvokePattern>(save!, UiaClient.InvokePattern).Invoke());
            await Eventually(() => uia.FindByName(window, "Clicks: 1") is not null, "Invoke ran the click handler");
            await Eventually(() => recorder.Notifications().Any(n => n.StartsWith("Saved 1 times", StringComparison.Ordinal)), "Announce reached UIA as a notification");

            // A text field: named by its placeholder, value read and written through the Value pattern.
            var edit = uia.FindByControlType(window, UiaClient.EditControl);
            await Assert.That(edit).IsNotNull();
            await Assert.That(UiaClient.Name(edit!)).IsEqualTo("Type to filter entries");
            var value = UiaClient.Pattern<IUIAutomationValuePattern>(edit!, UiaClient.ValuePattern);
            UiaClient.SetValue(value, "hello");
            await Eventually(() => uia.FindByName(window, "Query: hello") is not null, "SetValue updated the binding");
            await Assert.That(UiaClient.ValueOf(value)).IsEqualTo("hello");

            // The list: its selected item, then a new selection through SelectionItem.
            var list = uia.FindByName(window, "Clipboard history");
            await Assert.That(list).IsNotNull();
            await Assert.That(UiaClient.ControlType(list!)).IsEqualTo(UiaClient.ListControl);
            var selection = UiaClient.Pattern<IUIAutomationSelectionPattern>(list!, UiaClient.SelectionPattern);
            await Assert.That(string.Join("|", UiaClient.SelectionNames(selection))).IsEqualTo("Shopping list");
            var row = uia.FindByName(list!, "Quarterly numbers");
            await Assert.That(row).IsNotNull();
            await Assert.That(UiaClient.ControlType(row!)).IsEqualTo(UiaClient.ListItemControl);
            UiaClient.Check(UiaClient.Pattern<IUIAutomationSelectionItemPattern>(row!, UiaClient.SelectionItemPattern).Select());
            await Eventually(() => uia.FindByName(window, "Selected: Quarterly numbers") is not null, "Select changed the binding");
            await Assert.That(string.Join("|", UiaClient.SelectionNames(selection))).IsEqualTo("Quarterly numbers");

            // A checkbox through Toggle.
            var pin = uia.FindByName(window, "Pin to top");
            var toggle = UiaClient.Pattern<IUIAutomationTogglePattern>(pin!, UiaClient.TogglePattern);
            UiaClient.Check(toggle.Toggle());
            await Eventually(() => toggle.get_CurrentToggleState(out int state) >= 0 && state == 1, "Toggle checked the box");

            // Focus: moved by UIA, then by the keyboard (Tab into the list, Down within it).
            UiaClient.Check(edit!.SetFocus());
            await Eventually(() => recorder.Focus().Contains("Edit:Type to filter entries"), "focus moved to the text field");
            await Assert.That(UiaClient.Property(edit, UiaClient.HasKeyboardFocusProperty)).IsEqualTo(true);

            await Cli(appId, "type", "--key", "Tab");
            await Eventually(() => recorder.Focus().Contains("ListItem:Quarterly numbers"), "Tab into the list announces its selected item");
            await Cli(appId, "type", "--key", "Down");
            await Eventually(() => recorder.Focus().Contains("ListItem:TODO: ship v1"), "Down moves focus to the next item");
            await Eventually(() => UiaClient.Name(uia.FocusedElement()!) == "TODO: ship v1", "GetFocusedElement agrees with the focus events");

            // The context menu: opened from the keyboard, its first item takes focus.
            await Cli(appId, "type", "--key", "Apps");
            await Eventually(() => recorder.Focus().Contains("MenuItem:Paste"), "the open menu's highlighted item has focus");
            await Eventually(() => recorder.Events().Any(e => e.StartsWith($"{UiaClient.MenuOpenedEvent}:", StringComparison.Ordinal)), "MenuOpened is raised");
            var delete = uia.FindByName(window, "Delete");
            await Assert.That(delete).IsNotNull();
            await Assert.That(UiaClient.ControlType(delete!)).IsEqualTo(UiaClient.MenuItemControl);
            UiaClient.Check(UiaClient.Pattern<IUIAutomationInvokePattern>(delete!, UiaClient.InvokePattern).Invoke());
            await Eventually(() => uia.FindByName(window, "Last: delete TODO: ship v1") is not null, "invoking a menu item runs it");

            // A dialog: a Window element with IsDialog; focus moves into it; its buttons invoke.
            var deleteAll = uia.FindByName(window, "Delete all");
            UiaClient.Check(UiaClient.Pattern<IUIAutomationInvokePattern>(deleteAll!, UiaClient.InvokePattern).Invoke());
            IUIAutomationElement? dialog = null;
            await Eventually(() => (dialog = uia.FindByName(window, "Delete all clips?")) is not null && UiaClient.ControlType(dialog) == UiaClient.WindowControl, "the dialog is exposed");
            await Assert.That(UiaClient.Property(dialog!, UiaClient.IsDialogProperty)).IsEqualTo(true);
            await Eventually(() => recorder.Focus().Any(f => f.StartsWith("Button:", StringComparison.Ordinal) && (f.EndsWith(":Keep", StringComparison.Ordinal) || f.EndsWith(":Delete", StringComparison.Ordinal))), "focus moves into the dialog");
            var keep = uia.FindByName(dialog!, "Keep");
            UiaClient.Check(UiaClient.Pattern<IUIAutomationInvokePattern>(keep!, UiaClient.InvokePattern).Invoke());
            await Eventually(() => uia.FindByName(window, "Last: kept all") is not null, "the dialog's button ran");

            UiaClient.Check(uia.Automation.RemoveAllEventHandlers());
        }
        finally
        {
            fixture.Kill();
        }
    }

    [Test]
    public async Task ScreenReaderClient_ReadsAndSelectsTabs()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string> { ["CASCADE_FIXTURE_VIEW"] = "tabs" });
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            nint hwnd = await WindowOf(fixture);
            Foreground.Activate(hwnd);

            var uia = UiaClient.Create();
            var window = uia.ElementFromHandle(hwnd);
            var recorder = new UiaEventRecorder();
            state = () => $"focus events: [{string.Join(", ", recorder.Focus())}]";
            UiaClient.Check(uia.Automation.AddFocusChangedEventHandler(null, recorder));

            // The bar is a Tab control; its tabs are TabItems with the selected one in its Selection.
            var sections = uia.FindByName(window, "Sections");
            await Assert.That(sections).IsNotNull().Because("the tab bar is exposed by its accessible label");
            await Assert.That(UiaClient.ControlType(sections!)).IsEqualTo(UiaClient.TabControl);
            var selection = UiaClient.Pattern<IUIAutomationSelectionPattern>(sections!, UiaClient.SelectionPattern);
            await Assert.That(string.Join("|", UiaClient.SelectionNames(selection))).IsEqualTo("Overview");

            var activity = uia.FindByName(sections!, "Activity");
            await Assert.That(activity).IsNotNull();
            await Assert.That(UiaClient.ControlType(activity!)).IsEqualTo(UiaClient.TabItemControl);
            UiaClient.Check(UiaClient.Pattern<IUIAutomationSelectionItemPattern>(activity!, UiaClient.SelectionItemPattern).Select());
            await Eventually(() => uia.FindByName(window, "Section: 1") is not null, "SelectionItem.Select selected the tab");
            await Assert.That(string.Join("|", UiaClient.SelectionNames(selection))).IsEqualTo("Activity");

            // Keyboard: focus on the bar is focus on its selected tab; the arrows move it.
            UiaClient.Check(activity!.SetFocus());
            await Eventually(() => recorder.Focus().Contains("TabItem:Activity"), "focus lands on the selected tab");
            await Cli(appId, "type", "--key", "Right");
            await Eventually(() => recorder.Focus().Contains("TabItem:Settings"), "Right moves focus to the next enabled tab");
            await Eventually(() => uia.FindByName(window, "Section: 2") is not null, "automatic activation selected it");

            UiaClient.Check(uia.Automation.RemoveAllEventHandlers());
        }
        finally
        {
            fixture.Kill();
        }
    }

    private static async Task<nint> WindowOf(Process process)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0)
            {
                return process.MainWindowHandle;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("The fixture's window never appeared.");
    }

    private static async Task Eventually(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException($"Timed out waiting until {what}. {state?.Invoke()}");
    }

    private static async Task Cli(string appId, params string[] args)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", .. args, "--app", appId]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"cascade mcp {string.Join(' ', args)} failed: {result.StdErr}{result.StdOut}");
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Cascade.UI.Integration.Uia;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// The tray/launcher window shell end to end on real Win32: the fixture's "shell" view starts
/// hidden in the tray, frameless, without a taskbar button and topmost; a tray click summons it,
/// the close button and deactivation hide it. Its tray menu is Cascade's own popup window, opened
/// with the messages the shell sends (NOTIFYICON_VERSION_4), placed against the taskbar, driven
/// from the keyboard, the CLI (<c>--window tray-menu</c>) and UI Automation, and captured.
/// </summary>
[NotInParallel("CliIntegration")]
public partial class ShellIntegrationTests
{
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_ACTIVATE = 0x0006;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint WM_TRAYICON = 0x0400 + 2;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000;
    private const int VK_ESCAPE = 0x1B;
    private const int SM_CMONITORS = 80;
    private const int IsEnabledProperty = 30010;
    private const int MenuControl = 50009;
    private static readonly nint DpiAwarenessPerMonitorV2 = -4;

    // The first TrayIcon created in a process gets id 2 (TrayIcon ids start after 1).
    private const int FirstTrayIconId = 2;

    [Test]
    public async Task TrayShell_StartsHidden_SummonsAndHides()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-shell-{Guid.NewGuid():N}.log");
        using var fixture = StartShell(appId, log);
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            nint hwnd = FindAppWindow(fixture.Id);
            await Assert.That(hwnd).IsNotEqualTo(0);

            // Configured state, applied at creation: hidden, no taskbar button, topmost.
            await Assert.That(IsWindowVisible(hwnd)).IsFalse();
            long exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
            await Assert.That(exStyle & WS_EX_TOOLWINDOW).IsNotEqualTo(0);
            await Assert.That(exStyle & WS_EX_APPWINDOW).IsEqualTo(0);
            await Assert.That(exStyle & WS_EX_TOPMOST).IsNotEqualTo(0);
            await WaitForLogAsync(log, $"screens {GetSystemMetrics(SM_CMONITORS)}");

            // Tray click → OnClick → App.Window.Activate(): shown and activated.
            PostTray(hwnd, WM_LBUTTONUP, 0, 0);
            await WaitForLogAsync(log, "tray-click");
            await WaitUntilAsync(() => IsWindowVisible(hwnd), "window shown after tray click");
            await WaitForLogAsync(log, "activated");

            // Frameless: the client area is the whole window. Measure in real pixels — a DPI-unaware
            // caller sees both rects scaled and rounded independently.
            SetThreadDpiAwarenessContext(DpiAwarenessPerMonitorV2);
            GetWindowRect(hwnd, out RECT window);
            GetClientRect(hwnd, out RECT client);
            await Assert.That(client.Right - client.Left).IsEqualTo(window.Right - window.Left);
            await Assert.That(client.Bottom - client.Top).IsEqualTo(window.Bottom - window.Top);

            // Close button → HideOnClose: hidden, still running.
            PostMessageW(hwnd, WM_CLOSE, 0, 0);
            await WaitUntilAsync(() => !IsWindowVisible(hwnd), "window hidden on close");
            await Assert.That(fixture.HasExited).IsFalse();

            // Deactivation → the app's Deactivated handler hides the window.
            PostTray(hwnd, WM_LBUTTONUP, 0, 0);
            await WaitUntilAsync(() => IsWindowVisible(hwnd), "window shown again");
            PostMessageW(hwnd, WM_ACTIVATE, 0, 0);
            await WaitForLogAsync(log, "deactivated");
            await WaitUntilAsync(() => !IsWindowVisible(hwnd), "window hidden on deactivate");
        }
        finally
        {
            Cleanup(fixture, log);
        }
    }

    [Test]
    public async Task TrayMenu_OpensAgainstTheTaskbar_AndIsDrivenByKeyboardCliAndUia()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-shell-{Guid.NewGuid():N}.log");
        using var fixture = StartShell(appId, log);
        try
        {
            await CliTestHarness.WaitForFixtureRegistrationAsync(appId, TimeSpan.FromSeconds(30), fixture.Id);
            nint hwnd = FindAppWindow(fixture.Id);
            SetThreadDpiAwarenessContext(DpiAwarenessPerMonitorV2);

            // A right-click on the taskbar of the primary monitor: the shell sends WM_RBUTTONUP,
            // then WM_CONTEXTMENU, each carrying the click point.
            var primary = PrimaryMonitor();
            int x = primary.Monitor.Left + 200;
            int y = primary.Monitor.Bottom - 4;
            PostTray(hwnd, WM_RBUTTONUP, x, y);
            PostTray(hwnd, WM_CONTEXTMENU, x, y);
            nint menu = await WaitForMenuAsync(fixture.Id);

            // Placed against the taskbar: starting at the click, its bottom just above the work
            // area's (when the taskbar is at the bottom), and wholly inside the work area.
            GetWindowRect(menu, out RECT bounds);
            await Assert.That(bounds.Left).IsEqualTo(x);
            await Assert.That(bounds.Top >= primary.Work.Top && bounds.Bottom <= primary.Work.Bottom).IsTrue()
                .Because($"menu {bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom} inside work area {primary.Work.Top}..{primary.Work.Bottom}");
            if (primary.Work.Bottom < primary.Monitor.Bottom)
            {
                await Assert.That(primary.Work.Bottom - bounds.Bottom).IsLessThanOrEqualTo(12);
            }

            long exStyle = GetWindowLongPtrW(menu, GWL_EXSTYLE);
            await Assert.That(exStyle & WS_EX_TOPMOST).IsNotEqualTo(0);
            await Assert.That(exStyle & WS_EX_TOOLWINDOW).IsNotEqualTo(0).Because("no taskbar button");

            // The CLI lists and captures it.
            var windows = JsonNode.Parse(await Cli(appId, "windows"))!["windows"]!.AsArray();
            var entry = windows.Single(w => w!["id"]!.GetValue<string>() == "tray-menu")!;
            await Assert.That(entry["width"]!.GetValue<int>()).IsEqualTo(bounds.Right - bounds.Left);
            string shot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-traymenu-{Guid.NewGuid():N}.png");
            try
            {
                await Cli(appId, "screenshot", "--window", "tray-menu", "-o", shot);
                await Assert.That(File.Exists(shot)).IsTrue();
                await Assert.That(PngSize(shot)).IsEqualTo((bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
            }
            finally
            {
                File.Delete(shot);
            }

            // Keyboard through the CLI: Down highlights "Open Shell" (nothing is highlighted after a
            // click), Enter runs it and the menu window goes away.
            await Cli(appId, "type", "--key", "Down", "--window", "tray-menu");
            await Cli(appId, "type", "--key", "Enter", "--window", "tray-menu");
            await WaitForLogAsync(log, "menu-open");
            await WaitUntilAsync(() => !IsWindow(menu), "menu window destroyed after an item ran");

            // The context-menu key on the focused icon (WM_CONTEXTMENU alone): the first item is
            // highlighted; a screen reader sees a menu of menu items, informational rows disabled.
            PostTray(hwnd, WM_CONTEXTMENU, x, y);
            menu = await WaitForMenuAsync(fixture.Id);
            var uia = UiaClient.Create();
            var root = uia.ElementFromHandle(menu);
            IUIAutomationElement? manual = null;
            await WaitUntilAsync(() => (manual = uia.FindByName(root, "Manual")) is not null, "UIA exposes the menu's items");
            await Assert.That(UiaClient.ControlType(manual!)).IsEqualTo(UiaClient.MenuItemControl);
            var panel = uia.FindByControlType(root, MenuControl);
            await Assert.That(panel).IsNotNull().Because("the panel is a Menu");
            await Assert.That(UiaClient.Name(panel!)).IsEqualTo("Shell fixture").Because("the menu is named after the tray icon's tooltip");
            var version = uia.FindByName(root, "Version: 2.7.3.0");
            await Assert.That(version).IsNotNull();
            await Assert.That(UiaClient.Property(version!, IsEnabledProperty)).IsEqualTo(false);

            // Invoke through UIA runs the item and closes the menu.
            UiaClient.Check(UiaClient.Pattern<IUIAutomationInvokePattern>(manual!, UiaClient.InvokePattern).Invoke());
            await WaitForLogAsync(log, "menu-manual");
            await WaitUntilAsync(() => !IsWindow(menu), "menu closed after UIA invoke");

            // Escape closes without running anything.
            PostTray(hwnd, WM_CONTEXTMENU, x, y);
            menu = await WaitForMenuAsync(fixture.Id);
            PostMessageW(menu, WM_KEYDOWN, VK_ESCAPE, 0);
            await WaitUntilAsync(() => !IsWindow(menu), "menu closed on Escape");

            // "&Quit": its access key runs it at once, and the app exits.
            PostTray(hwnd, WM_CONTEXTMENU, x, y);
            menu = await WaitForMenuAsync(fixture.Id);
            PostMessageW(menu, WM_CHAR, 'q', 0);
            await WaitForLogAsync(log, "menu-quit");
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await fixture.WaitForExitAsync(exitTimeout.Token);
            await Assert.That(fixture.HasExited).IsTrue();
        }
        finally
        {
            Cleanup(fixture, log);
        }
    }

    private static Process StartShell(string appId, string log)
    {
        return CliTestHarness.StartFixture(appId, new Dictionary<string, string>
        {
            ["CASCADE_FIXTURE_VIEW"] = "shell",
            ["CASCADE_FIXTURE_LOG"] = log,
            ["CASCADE_MCP"] = "1",
        });
    }

    private static void Cleanup(Process fixture, string log)
    {
        if (!fixture.HasExited)
        {
            fixture.Kill(entireProcessTree: true);
        }
        try { File.Delete(log); }
        catch (IOException) { /* best-effort */ }
    }

    /// <summary>A NOTIFYICON_VERSION_4 callback: lParam = (event, icon id), wParam = the point.</summary>
    private static void PostTray(nint hwnd, uint notification, int x, int y)
    {
        nuint point = (nuint)(uint)((y & 0xFFFF) << 16 | (x & 0xFFFF));
        PostMessageW(hwnd, WM_TRAYICON, point, (nint)((FirstTrayIconId << 16) | (int)notification));
    }

    private static async Task<string> Cli(string appId, params string[] args)
    {
        var result = await CliTestHarness.RunCliAsync(["mcp", .. args, "--app", appId]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"cascade mcp {string.Join(' ', args)} failed: {result.StdErr}{result.StdOut}");
        }
        return result.StdOut;
    }

    private static (int Width, int Height) PngSize(string path)
    {
        // IHDR: width and height are the big-endian ints at byte 16 and 20.
        byte[] header = new byte[24];
        using var stream = File.OpenRead(path);
        stream.ReadExactly(header);
        return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20)));
    }

    private static nint FindAppWindow(int processId)
    {
        return FindWindowOf("CascadeUIWindow", processId, visibleOnly: false);
    }

    private static nint FindWindowOf(string className, int processId, bool visibleOnly)
    {
        nint found = 0;
        while ((found = FindWindowExW(0, found, className, null)) != 0)
        {
            GetWindowThreadProcessId(found, out uint pid);
            if (pid == processId && (!visibleOnly || IsWindowVisible(found)))
            {
                return found;
            }
        }
        return 0;
    }

    // The tray menu's root panel is a top-level window of class "CascadeUIPopupMenu".
    private static async Task<nint> WaitForMenuAsync(int processId)
    {
        nint menu = 0;
        await WaitUntilAsync(() => (menu = FindWindowOf("CascadeUIPopupMenu", processId, visibleOnly: true)) != 0, "tray menu opened");
        return menu;
    }

    private static (RECT Monitor, RECT Work) PrimaryMonitor()
    {
        nint monitor = MonitorFromPoint(0, 1 /* MONITOR_DEFAULTTOPRIMARY */);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfoW(monitor, ref info);
        return (info.rcMonitor, info.rcWork);
    }

    private static async Task WaitForLogAsync(string path, string line)
    {
        await WaitUntilAsync(() => File.Exists(path) && ReadLines(path).Contains(line), $"log line '{line}'");
    }

    private static string[] ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(10))
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }
            await Task.Delay(50);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint childAfter, string? className, string? windowName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    private static partial long GetWindowLongPtrW(nint hWnd, int index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out RECT rect);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out RECT rect);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    private static partial int GetSystemMetrics(int index);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    private static partial nint SetThreadDpiAwarenessContext(nint context);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    private static partial nint MonitorFromPoint(long point, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MONITORINFO info);
}

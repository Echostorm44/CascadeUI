using System.Diagnostics;
using System.Runtime.InteropServices;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Integration;

/// <summary>
/// The tray/launcher window shell end to end on real Win32: the fixture's "shell" view starts
/// hidden in the tray, frameless, without a taskbar button and topmost; a tray click summons it,
/// the close button and deactivation hide it, and the tray menu runs its items. The test drives
/// it only through window messages from outside the process — the same messages the shell and
/// the window manager send — and observes window state and the fixture's event log.
/// </summary>
[NotInParallel("CliIntegration")]
public partial class ShellIntegrationTests
{
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_ACTIVATE = 0x0006;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_TRAYICON = 0x0400 + 2;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000;
    private const int VK_RETURN = 0x0D, VK_DOWN = 0x28;
    private const int SM_CMONITORS = 80;
    private static readonly nint DpiAwarenessPerMonitorV2 = -4;

    // The first TrayIcon created in a process gets id 2 (TrayIcon ids start after 1).
    private const nuint FirstTrayIconId = 2;

    [Test]
    public async Task TrayShell_StartsHidden_SummonsHidesAndRunsMenu()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string appId = CliTestHarness.NewFixtureAppId();
        string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-shell-{Guid.NewGuid():N}.log");
        using var fixture = CliTestHarness.StartFixture(appId, new Dictionary<string, string>
        {
            ["CASCADE_FIXTURE_VIEW"] = "shell",
            ["CASCADE_FIXTURE_LOG"] = log,
        });
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
            PostMessageW(hwnd, WM_TRAYICON, FirstTrayIconId, (nint)WM_LBUTTONUP);
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
            PostMessageW(hwnd, WM_TRAYICON, FirstTrayIconId, (nint)WM_LBUTTONUP);
            await WaitUntilAsync(() => IsWindowVisible(hwnd), "window shown again");
            PostMessageW(hwnd, WM_ACTIVATE, 0, 0);
            await WaitForLogAsync(log, "deactivated");
            await WaitUntilAsync(() => !IsWindowVisible(hwnd), "window hidden on deactivate");

            // Tray menu: right-click opens it; Down + Enter runs the first item.
            PostMessageW(hwnd, WM_TRAYICON, FirstTrayIconId, (nint)WM_RBUTTONUP);
            nint menu = await WaitForMenuAsync(fixture.Id);
            PostMessageW(menu, WM_KEYDOWN, VK_DOWN, 0);
            PostMessageW(menu, WM_KEYDOWN, VK_RETURN, 0);
            await WaitForLogAsync(log, "menu-first");

            // "Quit" (after the separator) exits the app.
            PostMessageW(hwnd, WM_TRAYICON, FirstTrayIconId, (nint)WM_RBUTTONUP);
            menu = await WaitForMenuAsync(fixture.Id);
            PostMessageW(menu, WM_KEYDOWN, VK_DOWN, 0);
            PostMessageW(menu, WM_KEYDOWN, VK_DOWN, 0);
            PostMessageW(menu, WM_KEYDOWN, VK_RETURN, 0);
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await fixture.WaitForExitAsync(exitTimeout.Token);
            await Assert.That(fixture.HasExited).IsTrue();
        }
        finally
        {
            if (!fixture.HasExited)
            {
                fixture.Kill(entireProcessTree: true);
            }
            try { File.Delete(log); }
            catch (IOException) { /* best-effort */ }
        }
    }

    private static nint FindAppWindow(int processId)
    {
        nint found = 0;
        while ((found = FindWindowExW(0, found, "CascadeUIWindow", null)) != 0)
        {
            GetWindowThreadProcessId(found, out uint pid);
            if (pid == processId)
            {
                return found;
            }
        }
        return 0;
    }

    // Popup menus are windows of class "#32768" owned by the thread running the menu loop.
    private static async Task<nint> WaitForMenuAsync(int processId)
    {
        nint menu = 0;
        await WaitUntilAsync(() =>
        {
            menu = 0;
            while ((menu = FindWindowExW(0, menu, "#32768", null)) != 0)
            {
                GetWindowThreadProcessId(menu, out uint pid);
                if (pid == processId && IsWindowVisible(menu))
                {
                    return true;
                }
            }
            return false;
        }, "tray menu opened");
        return menu;
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
}

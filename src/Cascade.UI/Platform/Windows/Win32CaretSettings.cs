using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>Reads the user's caret blink time and blink timeout into <see cref="CaretSettings"/>.</summary>
internal static partial class Win32CaretSettings
{
    private const uint Infinite = 0xFFFFFFFF;
    private const uint SpiGetCaretTimeout = 0x2022;

    /// <summary>Reads both settings. Called at startup and on <c>WM_SETTINGCHANGE</c>.</summary>
    internal static void Refresh()
    {
        // GetCaretBlinkTime returns the on (or off) phase in ms, INFINITE when blinking is off, and 0
        // on failure (then the theme's rate applies).
        uint blink = GetCaretBlinkTime();
        if (blink == 0)
        {
            DebugLogFailure("GetCaretBlinkTime");
        }
        int halfPeriod = blink == Infinite ? -1 : (int)Math.Min(blink, int.MaxValue);

        // SPI_GETCARETTIMEOUT: how long the caret blinks after the last input before it stays on.
        // Windows versions without it fail the call; the caret then blinks for as long as it is focused.
        uint timeout = 0;
        if (!SystemParametersInfoW(SpiGetCaretTimeout, 0, ref timeout, 0))
        {
            DebugLogFailure("SystemParametersInfoW(SPI_GETCARETTIMEOUT)");
            timeout = 0;
        }
        int timeoutMs = timeout == Infinite ? 0 : (int)Math.Min(timeout, int.MaxValue);

        CaretSettings.Set(halfPeriod, timeoutMs);
    }

    private static void DebugLogFailure(string call)
    {
        if (Backend.Etch.DebugLog.IsEnabled(Backend.Etch.DebugLogCategory.Frame))
        {
            Backend.Etch.DebugLog.Write(Backend.Etch.DebugLogCategory.Frame,
                $"[{DateTime.Now:O}] {call} failed: {Marshal.GetLastPInvokeError()}");
        }
    }

    [LibraryImport("user32", SetLastError = true)]
    private static partial uint GetCaretBlinkTime();

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint param, ref uint value, uint winIni);
}

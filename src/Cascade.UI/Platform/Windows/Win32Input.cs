using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Processes Win32 mouse, keyboard, touch, and pointer messages and converts
/// them into Cascade UI input events.
/// </summary>
internal static class Win32Input
{
    // ── Mouse ────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a mouse event from a Win32 window message.
    /// Returns null if the message is not a mouse message.
    /// </summary>
    internal static NativeMouseEvent? ProcessMouseMessage(uint msg, nuint wParam, nint lParam, float dpiScale)
    {
        NativeMouseEventType? eventType = GetMouseEventType(msg);
        if (eventType is null)
        {
            return null;
        }

        float x = Win32.GetXLParam(lParam) / dpiScale;
        float y = Win32.GetYLParam(lParam) / dpiScale;
        NativeMouseButton button = GetMouseButton(msg);
        ModifierKeys modifiers = GetModifierKeysFromMouseWParam(wParam);

        return new NativeMouseEvent
        {
            X = x,
            Y = y,
            Type = eventType.Value,
            Button = button,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Extracts a scroll event from WM_MOUSEWHEEL or WM_MOUSEHWHEEL.
    /// Returns null if the message is not a scroll message.
    /// WM_MOUSEWHEEL lParam contains screen coordinates — hWnd is used to
    /// convert them to client coordinates for hit testing.
    /// </summary>
    internal static NativeScrollEvent? ProcessScrollMessage(uint msg, nuint wParam, nint lParam, float dpiScale, nint hWnd)
    {
        if (msg is not (Win32.WM_MOUSEWHEEL or Win32.WM_MOUSEHWHEEL))
        {
            return null;
        }

        // WM_MOUSEWHEEL lParam is in screen coordinates — convert to client
        var pt = new Win32.POINT
        {
            x = Win32.GetXLParam(lParam),
            y = Win32.GetYLParam(lParam)
        };
        Win32.ScreenToClient(hWnd, ref pt);

        float x = pt.x / dpiScale;
        float y = pt.y / dpiScale;
        int delta = Win32.HiWord(wParam);
        float normalizedDelta = delta / 120.0f;
        ModifierKeys modifiers = GetModifierKeysFromMouseWParam(wParam);

        return new NativeScrollEvent
        {
            X = x,
            Y = y,
            DeltaX = msg == Win32.WM_MOUSEHWHEEL ? normalizedDelta : 0f,
            DeltaY = msg == Win32.WM_MOUSEWHEEL ? normalizedDelta : 0f,
            Modifiers = modifiers
        };
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a key event from WM_KEYDOWN, WM_KEYUP, WM_SYSKEYDOWN, WM_SYSKEYUP.
    /// Returns null if the message is not a keyboard message.
    /// </summary>
    internal static NativeKeyEvent? ProcessKeyMessage(uint msg, nuint wParam, nint lParam)
    {
        NativeKeyEventType? eventType = GetKeyEventType(msg);
        if (eventType is null)
        {
            return null;
        }

        int virtualKey = (int)(wParam & 0xFF);
        Key key = MapVirtualKey(virtualKey);
        ModifierKeys modifiers = GetCurrentModifierKeys();

        return new NativeKeyEvent
        {
            Key = key,
            Type = eventType.Value,
            Modifiers = modifiers,
            Character = null
        };
    }

    /// <summary>
    /// Extracts a character input event from WM_CHAR or WM_SYSCHAR.
    /// Returns null if the message is not a character message.
    /// </summary>
    internal static NativeKeyEvent? ProcessCharMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg is not (Win32.WM_CHAR or Win32.WM_SYSCHAR))
        {
            return null;
        }

        char character = (char)wParam;

        // Filter out control characters (except Tab, Enter, Backspace).
        if (char.IsControl(character) &&
            character != '\t' &&
            character != '\r' &&
            character != '\b')
        {
            return null;
        }

        ModifierKeys modifiers = GetCurrentModifierKeys();

        return new NativeKeyEvent
        {
            Key = Key.None,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = modifiers,
            Character = character
        };
    }

    // ── Touch ────────────────────────────────────────────────────────

    /// <summary>
    /// Processes WM_TOUCH messages and converts them to mouse-equivalent events.
    /// Returns an array of events (one per touch point in the message).
    /// </summary>
    internal static NativeMouseEvent[] ProcessTouchMessage(nint hWnd, nuint wParam, nint lParam, float dpiScale)
    {
        uint touchCount = (uint)(wParam & 0xFFFF);
        if (touchCount == 0)
        {
            return [];
        }

        Win32.TOUCHINPUT[] inputs = new Win32.TOUCHINPUT[touchCount];
        if (!Win32.GetTouchInputInfo(lParam, touchCount, inputs, Marshal.SizeOf<Win32.TOUCHINPUT>()))
        {
            return [];
        }

        NativeMouseEvent[] events = new NativeMouseEvent[touchCount];
        for (int i = 0; i < touchCount; i++)
        {
            ref Win32.TOUCHINPUT touch = ref inputs[i];

            // Touch coordinates are in hundredths of a pixel (centi-pixels).
            float x = (touch.x / 100.0f) / dpiScale;
            float y = (touch.y / 100.0f) / dpiScale;

            NativeMouseEventType eventType;
            if ((touch.dwFlags & Win32.TOUCHEVENTF_DOWN) != 0)
            {
                eventType = NativeMouseEventType.MouseDown;
            }
            else if ((touch.dwFlags & Win32.TOUCHEVENTF_UP) != 0)
            {
                eventType = NativeMouseEventType.MouseUp;
            }
            else
            {
                eventType = NativeMouseEventType.MouseMove;
            }

            events[i] = new NativeMouseEvent
            {
                X = x,
                Y = y,
                Type = eventType,
                Button = NativeMouseButton.Left,
                Modifiers = ModifierKeys.None
            };
        }

        Win32.CloseTouchInputHandle(lParam);
        return events;
    }

    // ── Pointer ──────────────────────────────────────────────────────

    /// <summary>
    /// Processes WM_POINTER* messages and converts them to mouse-equivalent events.
    /// </summary>
    internal static NativeMouseEvent? ProcessPointerMessage(uint msg, nuint wParam, nint lParam, float dpiScale)
    {
        NativeMouseEventType eventType = msg switch
        {
            Win32.WM_POINTERDOWN => NativeMouseEventType.MouseDown,
            Win32.WM_POINTERUP => NativeMouseEventType.MouseUp,
            Win32.WM_POINTERUPDATE => NativeMouseEventType.MouseMove,
            _ => NativeMouseEventType.MouseMove
        };

        float x = Win32.GetXLParam(lParam) / dpiScale;
        float y = Win32.GetYLParam(lParam) / dpiScale;

        return new NativeMouseEvent
        {
            X = x,
            Y = y,
            Type = eventType,
            Button = NativeMouseButton.Left,
            Modifiers = GetCurrentModifierKeys()
        };
    }

    // ── Virtual Key Mapping ──────────────────────────────────────────

    /// <summary>
    /// Maps a Win32 virtual key code to a Cascade UI <see cref="Key"/> value.
    /// </summary>
    internal static Key MapVirtualKey(int virtualKey)
    {
        // A-Z keys (0x41-0x5A).
        if (virtualKey >= 0x41 && virtualKey <= 0x5A)
        {
            return (Key)(Key.A + (virtualKey - 0x41));
        }

        // Digit keys 0-9 (0x30-0x39).
        if (virtualKey >= 0x30 && virtualKey <= 0x39)
        {
            return (Key)(Key.D0 + (virtualKey - 0x30));
        }

        // Function keys F1-F12 (0x70-0x7B).
        if (virtualKey >= Win32.VK_F1 && virtualKey <= Win32.VK_F12)
        {
            return (Key)(Key.F1 + (virtualKey - Win32.VK_F1));
        }

        // Numpad keys 0-9 (0x60-0x69).
        if (virtualKey >= Win32.VK_NUMPAD0 && virtualKey <= Win32.VK_NUMPAD9)
        {
            return (Key)(Key.NumPad0 + (virtualKey - Win32.VK_NUMPAD0));
        }

        return virtualKey switch
        {
            Win32.VK_BACK       => Key.Backspace,
            Win32.VK_TAB        => Key.Tab,
            Win32.VK_RETURN     => Key.Enter,
            Win32.VK_CAPITAL    => Key.CapsLock,
            Win32.VK_ESCAPE     => Key.Escape,
            Win32.VK_SPACE      => Key.Space,
            Win32.VK_PRIOR      => Key.PageUp,
            Win32.VK_NEXT       => Key.PageDown,
            Win32.VK_END        => Key.End,
            Win32.VK_HOME       => Key.Home,
            Win32.VK_LEFT       => Key.Left,
            Win32.VK_UP         => Key.Up,
            Win32.VK_RIGHT      => Key.Right,
            Win32.VK_DOWN       => Key.Down,
            Win32.VK_SNAPSHOT   => Key.PrintScreen,
            Win32.VK_INSERT     => Key.Insert,
            Win32.VK_DELETE     => Key.Delete,
            Win32.VK_MULTIPLY   => Key.NumPadMultiply,
            Win32.VK_ADD        => Key.NumPadAdd,
            Win32.VK_SUBTRACT   => Key.NumPadSubtract,
            Win32.VK_DECIMAL    => Key.NumPadDecimal,
            Win32.VK_DIVIDE     => Key.NumPadDivide,
            Win32.VK_NUMLOCK    => Key.NumLock,
            Win32.VK_SCROLL     => Key.ScrollLock,
            Win32.VK_PAUSE      => Key.Pause,
            Win32.VK_OEM_1      => Key.Semicolon,
            Win32.VK_OEM_PLUS   => Key.Equals,
            Win32.VK_OEM_COMMA  => Key.Comma,
            Win32.VK_OEM_MINUS  => Key.Minus,
            Win32.VK_OEM_PERIOD => Key.Period,
            Win32.VK_OEM_2      => Key.Slash,
            Win32.VK_OEM_3      => Key.Backtick,
            Win32.VK_OEM_4      => Key.LeftBracket,
            Win32.VK_OEM_5      => Key.Backslash,
            Win32.VK_OEM_6      => Key.RightBracket,
            Win32.VK_OEM_7      => Key.Quote,
            _                   => Key.None
        };
    }

    /// <summary>
    /// Maps a Cascade UI <see cref="Key"/> value back to a Win32 virtual key code.
    /// Returns 0 if no mapping exists.
    /// </summary>
    internal static int MapKeyToVirtualKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return 0x41 + (int)(key - Key.A);
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return 0x30 + (int)(key - Key.D0);
        }

        if (key >= Key.F1 && key <= Key.F12)
        {
            return Win32.VK_F1 + (int)(key - Key.F1);
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return Win32.VK_NUMPAD0 + (int)(key - Key.NumPad0);
        }

        return key switch
        {
            Key.Backspace       => Win32.VK_BACK,
            Key.Tab             => Win32.VK_TAB,
            Key.Enter           => Win32.VK_RETURN,
            Key.CapsLock        => Win32.VK_CAPITAL,
            Key.Escape          => Win32.VK_ESCAPE,
            Key.Space           => Win32.VK_SPACE,
            Key.PageUp          => Win32.VK_PRIOR,
            Key.PageDown        => Win32.VK_NEXT,
            Key.End             => Win32.VK_END,
            Key.Home            => Win32.VK_HOME,
            Key.Left            => Win32.VK_LEFT,
            Key.Up              => Win32.VK_UP,
            Key.Right           => Win32.VK_RIGHT,
            Key.Down            => Win32.VK_DOWN,
            Key.PrintScreen     => Win32.VK_SNAPSHOT,
            Key.Insert          => Win32.VK_INSERT,
            Key.Delete          => Win32.VK_DELETE,
            Key.NumPadMultiply  => Win32.VK_MULTIPLY,
            Key.NumPadAdd       => Win32.VK_ADD,
            Key.NumPadSubtract  => Win32.VK_SUBTRACT,
            Key.NumPadDecimal   => Win32.VK_DECIMAL,
            Key.NumPadDivide    => Win32.VK_DIVIDE,
            Key.NumLock         => Win32.VK_NUMLOCK,
            Key.ScrollLock      => Win32.VK_SCROLL,
            Key.Pause           => Win32.VK_PAUSE,
            Key.Semicolon       => Win32.VK_OEM_1,
            Key.Equals          => Win32.VK_OEM_PLUS,
            Key.Comma           => Win32.VK_OEM_COMMA,
            Key.Minus           => Win32.VK_OEM_MINUS,
            Key.Period          => Win32.VK_OEM_PERIOD,
            Key.Slash           => Win32.VK_OEM_2,
            Key.Backtick        => Win32.VK_OEM_3,
            Key.LeftBracket     => Win32.VK_OEM_4,
            Key.Backslash       => Win32.VK_OEM_5,
            Key.RightBracket    => Win32.VK_OEM_6,
            Key.Quote           => Win32.VK_OEM_7,
            _                   => 0
        };
    }

    // ── Modifier Key Helpers ─────────────────────────────────────────

    /// <summary>
    /// Reads the current modifier key state from the system.
    /// </summary>
    internal static ModifierKeys GetCurrentModifierKeys()
    {
        ModifierKeys mods = ModifierKeys.None;

        if ((Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0)
        {
            mods |= ModifierKeys.Ctrl;
        }

        if ((Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0)
        {
            mods |= ModifierKeys.Shift;
        }

        if ((Win32.GetKeyState(Win32.VK_MENU) & 0x8000) != 0)
        {
            mods |= ModifierKeys.Alt;
        }

        if ((Win32.GetKeyState(Win32.VK_LWIN) & 0x8000) != 0 ||
            (Win32.GetKeyState(Win32.VK_RWIN) & 0x8000) != 0)
        {
            mods |= ModifierKeys.Meta;
        }

        return mods;
    }

    /// <summary>
    /// Maps Win32 <see cref="ModifierKeys"/> flags to the Win32 hotkey modifier
    /// flags used by RegisterHotKey (MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8).
    /// </summary>
    internal static uint MapModifiersToWin32(ModifierKeys modifiers)
    {
        uint result = 0;

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            result |= 0x0001; // MOD_ALT
        }

        if ((modifiers & ModifierKeys.Ctrl) != 0)
        {
            result |= 0x0002; // MOD_CONTROL
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            result |= 0x0004; // MOD_SHIFT
        }

        if ((modifiers & ModifierKeys.Meta) != 0)
        {
            result |= 0x0008; // MOD_WIN
        }

        return result;
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static NativeMouseEventType? GetMouseEventType(uint msg)
    {
        return msg switch
        {
            Win32.WM_MOUSEMOVE     => NativeMouseEventType.MouseMove,
            Win32.WM_LBUTTONDOWN   => NativeMouseEventType.MouseDown,
            Win32.WM_LBUTTONUP     => NativeMouseEventType.MouseUp,
            Win32.WM_LBUTTONDBLCLK => NativeMouseEventType.MouseDown,
            Win32.WM_RBUTTONDOWN   => NativeMouseEventType.MouseDown,
            Win32.WM_RBUTTONUP     => NativeMouseEventType.MouseUp,
            Win32.WM_RBUTTONDBLCLK => NativeMouseEventType.MouseDown,
            Win32.WM_MBUTTONDOWN   => NativeMouseEventType.MouseDown,
            Win32.WM_MBUTTONUP     => NativeMouseEventType.MouseUp,
            Win32.WM_MBUTTONDBLCLK => NativeMouseEventType.MouseDown,
            Win32.WM_MOUSELEAVE    => NativeMouseEventType.MouseLeave,
            _                      => null
        };
    }

    private static NativeMouseButton GetMouseButton(uint msg)
    {
        return msg switch
        {
            Win32.WM_LBUTTONDOWN   => NativeMouseButton.Left,
            Win32.WM_LBUTTONUP     => NativeMouseButton.Left,
            Win32.WM_LBUTTONDBLCLK => NativeMouseButton.Left,
            Win32.WM_RBUTTONDOWN   => NativeMouseButton.Right,
            Win32.WM_RBUTTONUP     => NativeMouseButton.Right,
            Win32.WM_RBUTTONDBLCLK => NativeMouseButton.Right,
            Win32.WM_MBUTTONDOWN   => NativeMouseButton.Middle,
            Win32.WM_MBUTTONUP     => NativeMouseButton.Middle,
            Win32.WM_MBUTTONDBLCLK => NativeMouseButton.Middle,
            _                      => NativeMouseButton.None
        };
    }

    private static NativeKeyEventType? GetKeyEventType(uint msg)
    {
        return msg switch
        {
            Win32.WM_KEYDOWN    => NativeKeyEventType.KeyDown,
            Win32.WM_SYSKEYDOWN => NativeKeyEventType.KeyDown,
            Win32.WM_KEYUP      => NativeKeyEventType.KeyUp,
            Win32.WM_SYSKEYUP   => NativeKeyEventType.KeyUp,
            _                   => null
        };
    }

    private static ModifierKeys GetModifierKeysFromMouseWParam(nuint wParam)
    {
        ModifierKeys mods = ModifierKeys.None;

        if (((int)wParam & Win32.MK_CONTROL) != 0)
        {
            mods |= ModifierKeys.Ctrl;
        }

        if (((int)wParam & Win32.MK_SHIFT) != 0)
        {
            mods |= ModifierKeys.Shift;
        }

        // Mouse wParam doesn't carry Alt/Meta — check key state directly.
        if ((Win32.GetKeyState(Win32.VK_MENU) & 0x8000) != 0)
        {
            mods |= ModifierKeys.Alt;
        }

        return mods;
    }
}

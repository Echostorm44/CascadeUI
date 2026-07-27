using System.Runtime.InteropServices;

namespace Cascade.UI;

/// <summary>
/// Processes X11 and Wayland input events and converts them into Cascade UI
/// input events. Follows the same pattern as Win32Input — static methods that
/// translate platform-specific event data into framework-neutral types.
/// </summary>
internal static class LinuxInput
{
    // ── X11 Mouse ────────────────────────────────────────────────────

    /// <summary>
    /// Converts an X11 ButtonPress or ButtonRelease event into a mouse event.
    /// Scroll events (buttons 4-7) are handled separately by ProcessX11ScrollEvent.
    /// Returns null if the button event is a scroll event.
    /// </summary>
    internal static NativeMouseEvent? ProcessX11ButtonEvent(XButtonEvent ev, float dpiScale)
    {
        // Buttons 4-7 are scroll wheel events in X11.
        if (ev.button is >= X11Interop.Button4 and <= X11Interop.Button7)
        {
            return null;
        }

        NativeMouseEventType eventType = ev.type == X11Interop.ButtonPress
            ? NativeMouseEventType.MouseDown
            : NativeMouseEventType.MouseUp;

        NativeMouseButton button = MapX11Button(ev.button);
        ModifierKeys modifiers = MapX11Modifiers(ev.state);

        return new NativeMouseEvent
        {
            X = ev.x / dpiScale,
            Y = ev.y / dpiScale,
            Type = eventType,
            Button = button,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Converts an X11 MotionNotify event into a mouse move event.
    /// </summary>
    internal static NativeMouseEvent ProcessX11MotionEvent(XMotionEvent ev, float dpiScale)
    {
        ModifierKeys modifiers = MapX11Modifiers(ev.state);

        return new NativeMouseEvent
        {
            X = ev.x / dpiScale,
            Y = ev.y / dpiScale,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Converts an X11 EnterNotify or LeaveNotify event into a mouse enter/leave event.
    /// </summary>
    internal static NativeMouseEvent ProcessX11CrossingEvent(XCrossingEvent ev, float dpiScale)
    {
        NativeMouseEventType eventType = ev.type == X11Interop.EnterNotify
            ? NativeMouseEventType.MouseEnter
            : NativeMouseEventType.MouseLeave;

        return new NativeMouseEvent
        {
            X = ev.x / dpiScale,
            Y = ev.y / dpiScale,
            Type = eventType,
            Button = NativeMouseButton.None,
            Modifiers = MapX11Modifiers(ev.state)
        };
    }

    /// <summary>
    /// Converts an X11 ButtonPress event for buttons 4-7 into a scroll event.
    /// Returns null if the button is not a scroll button.
    /// </summary>
    internal static NativeScrollEvent? ProcessX11ScrollEvent(XButtonEvent ev, float dpiScale)
    {
        if (ev.button is < X11Interop.Button4 or > X11Interop.Button7)
        {
            return null;
        }

        // Only ButtonPress generates scroll events; ButtonRelease for scroll
        // buttons is ignored (X11 sends both).
        if (ev.type != X11Interop.ButtonPress)
        {
            return null;
        }

        float deltaX = 0f;
        float deltaY = 0f;

        switch (ev.button)
        {
            case X11Interop.Button4:
                deltaY = 1.0f; // Scroll up
                break;
            case X11Interop.Button5:
                deltaY = -1.0f; // Scroll down
                break;
            case X11Interop.Button6:
                deltaX = -1.0f; // Scroll left
                break;
            case X11Interop.Button7:
                deltaX = 1.0f; // Scroll right
                break;
        }

        return new NativeScrollEvent
        {
            X = ev.x / dpiScale,
            Y = ev.y / dpiScale,
            DeltaX = deltaX,
            DeltaY = deltaY,
            Modifiers = MapX11Modifiers(ev.state)
        };
    }

    // ── X11 Keyboard ─────────────────────────────────────────────────

    /// <summary>
    /// Converts an X11 KeyPress or KeyRelease event into a key event.
    /// </summary>
    internal static NativeKeyEvent? ProcessX11KeyEvent(XKeyEvent ev)
    {
        NativeKeyEventType eventType = ev.type == X11Interop.KeyPress
            ? NativeKeyEventType.KeyDown
            : NativeKeyEventType.KeyUp;

        nint keysym = X11Interop.XLookupKeysym(ref ev, 0);
        Key key = MapX11KeySym(keysym);
        ModifierKeys modifiers = MapX11Modifiers(ev.state);

        return new NativeKeyEvent
        {
            Key = key,
            Type = eventType,
            Modifiers = modifiers,
            Character = null
        };
    }

    /// <summary>
    /// Extracts the character from an X11 KeyPress event using XLookupString.
    /// Returns null if the event does not produce a printable character.
    /// </summary>
    internal static NativeKeyEvent? ProcessX11CharEvent(XKeyEvent ev)
    {
        if (ev.type != X11Interop.KeyPress)
        {
            return null;
        }

        // Allocate a small buffer for the character lookup.
        nint buffer = Marshal.AllocHGlobal(32);
        try
        {
            int byteCount = X11Interop.XLookupString(ref ev, buffer, 32, out nint _, 0);
            if (byteCount <= 0)
            {
                return null;
            }

            byte[] bytes = new byte[byteCount];
            Marshal.Copy(buffer, bytes, 0, byteCount);
            string text = System.Text.Encoding.UTF8.GetString(bytes);

            if (text.Length == 0)
            {
                return null;
            }

            char character = text[0];

            // Filter control characters (except Tab, Enter, Backspace).
            if (char.IsControl(character) &&
                character != '\t' &&
                character != '\r' &&
                character != '\b')
            {
                return null;
            }

            return new NativeKeyEvent
            {
                Key = Key.None,
                Type = NativeKeyEventType.KeyDown,
                Modifiers = MapX11Modifiers(ev.state),
                Character = character
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ── X11 Key Sym Mapping ──────────────────────────────────────────

    /// <summary>
    /// Maps an X11 KeySym to a Cascade UI Key value.
    /// </summary>
    internal static Key MapX11KeySym(nint keysym)
    {
        long ks = keysym.ToInt64();

        // Latin letters a-z (0x61-0x7A) and A-Z (0x41-0x5A) share the same keysyms.
        if (ks >= 0x61 && ks <= 0x7A)
        {
            return (Key)(Key.A + (int)(ks - 0x61));
        }

        if (ks >= 0x41 && ks <= 0x5A)
        {
            return (Key)(Key.A + (int)(ks - 0x41));
        }

        // Digit keys 0-9.
        if (ks >= 0x30 && ks <= 0x39)
        {
            return (Key)(Key.D0 + (int)(ks - 0x30));
        }

        // Function keys F1-F12 (XK_F1 = 0xFFBE, XK_F12 = 0xFFC9).
        if (ks >= 0xFFBE && ks <= 0xFFC9)
        {
            return (Key)(Key.F1 + (int)(ks - 0xFFBE));
        }

        // Numpad keys 0-9 (XK_KP_0 = 0xFFB0, XK_KP_9 = 0xFFB9).
        if (ks >= 0xFFB0 && ks <= 0xFFB9)
        {
            return (Key)(Key.NumPad0 + (int)(ks - 0xFFB0));
        }

        return ks switch
        {
            0xFF08 => Key.Backspace,    // XK_BackSpace
            0xFF09 => Key.Tab,          // XK_Tab
            0xFF0D => Key.Enter,        // XK_Return
            0xFF1B => Key.Escape,       // XK_Escape
            0x0020 => Key.Space,        // XK_space
            0xFF50 => Key.Home,         // XK_Home
            0xFF51 => Key.Left,         // XK_Left
            0xFF52 => Key.Up,           // XK_Up
            0xFF53 => Key.Right,        // XK_Right
            0xFF54 => Key.Down,         // XK_Down
            0xFF55 => Key.PageUp,       // XK_Prior / PageUp
            0xFF56 => Key.PageDown,     // XK_Next / PageDown
            0xFF57 => Key.End,          // XK_End
            0xFF63 => Key.Insert,       // XK_Insert
            0xFFFF => Key.Delete,       // XK_Delete
            0xFF61 => Key.PrintScreen,  // XK_Print
            0xFF14 => Key.ScrollLock,   // XK_Scroll_Lock
            0xFF13 => Key.Pause,        // XK_Pause
            0xFFE5 => Key.CapsLock,     // XK_Caps_Lock
            0xFF7F => Key.NumLock,      // XK_Num_Lock
            0xFFAA => Key.NumPadMultiply,  // XK_KP_Multiply
            0xFFAB => Key.NumPadAdd,       // XK_KP_Add
            0xFFAD => Key.NumPadSubtract,  // XK_KP_Subtract
            0xFFAE => Key.NumPadDecimal,   // XK_KP_Decimal
            0xFFAF => Key.NumPadDivide,    // XK_KP_Divide
            0xFF8D => Key.NumPadEnter,     // XK_KP_Enter
            0x003B => Key.Semicolon,    // semicolon
            0x003D => Key.Equals,       // equal
            0x002C => Key.Comma,        // comma
            0x002D => Key.Minus,        // minus
            0x002E => Key.Period,       // period
            0x002F => Key.Slash,        // slash
            0x0060 => Key.Backtick,     // grave
            0x005B => Key.LeftBracket,  // bracketleft
            0x005C => Key.Backslash,    // backslash
            0x005D => Key.RightBracket, // bracketright
            0x0027 => Key.Quote,        // apostrophe
            _      => Key.None
        };
    }

    /// <summary>
    /// Maps a Cascade UI Key value back to an X11 KeySym.
    /// Returns 0 if no mapping exists.
    /// </summary>
    internal static long MapKeyToX11KeySym(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return 0x61 + (int)(key - Key.A); // lowercase keysyms
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return 0x30 + (int)(key - Key.D0);
        }

        if (key >= Key.F1 && key <= Key.F12)
        {
            return 0xFFBE + (int)(key - Key.F1);
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return 0xFFB0 + (int)(key - Key.NumPad0);
        }

        return key switch
        {
            Key.Backspace       => 0xFF08,
            Key.Tab             => 0xFF09,
            Key.Enter           => 0xFF0D,
            Key.Escape          => 0xFF1B,
            Key.Space           => 0x0020,
            Key.Home            => 0xFF50,
            Key.Left            => 0xFF51,
            Key.Up              => 0xFF52,
            Key.Right           => 0xFF53,
            Key.Down            => 0xFF54,
            Key.PageUp          => 0xFF55,
            Key.PageDown        => 0xFF56,
            Key.End             => 0xFF57,
            Key.Insert          => 0xFF63,
            Key.Delete          => 0xFFFF,
            Key.PrintScreen     => 0xFF61,
            Key.ScrollLock      => 0xFF14,
            Key.Pause           => 0xFF13,
            Key.CapsLock        => 0xFFE5,
            Key.NumLock         => 0xFF7F,
            Key.NumPadMultiply  => 0xFFAA,
            Key.NumPadAdd       => 0xFFAB,
            Key.NumPadSubtract  => 0xFFAD,
            Key.NumPadDecimal   => 0xFFAE,
            Key.NumPadDivide    => 0xFFAF,
            Key.NumPadEnter     => 0xFF8D,
            Key.Semicolon       => 0x003B,
            Key.Equals          => 0x003D,
            Key.Comma           => 0x002C,
            Key.Minus           => 0x002D,
            Key.Period          => 0x002E,
            Key.Slash           => 0x002F,
            Key.Backtick        => 0x0060,
            Key.LeftBracket     => 0x005B,
            Key.Backslash       => 0x005C,
            Key.RightBracket    => 0x005D,
            Key.Quote           => 0x0027,
            _                   => 0
        };
    }

    // ── Modifier Key Mapping ─────────────────────────────────────────

    /// <summary>
    /// Maps X11 modifier state bits to Cascade ModifierKeys flags.
    /// </summary>
    internal static ModifierKeys MapX11Modifiers(uint state)
    {
        ModifierKeys mods = ModifierKeys.None;

        if ((state & X11Interop.ControlMask) != 0)
        {
            mods |= ModifierKeys.Ctrl;
        }

        if ((state & X11Interop.ShiftMask) != 0)
        {
            mods |= ModifierKeys.Shift;
        }

        if ((state & X11Interop.Mod1Mask) != 0)
        {
            mods |= ModifierKeys.Alt;
        }

        if ((state & X11Interop.Mod4Mask) != 0)
        {
            mods |= ModifierKeys.Meta;
        }

        return mods;
    }

    /// <summary>
    /// Maps Cascade ModifierKeys flags to X11 modifier state bits.
    /// </summary>
    internal static uint MapModifiersToX11(ModifierKeys modifiers)
    {
        uint result = 0;

        if ((modifiers & ModifierKeys.Ctrl) != 0)
        {
            result |= X11Interop.ControlMask;
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            result |= X11Interop.ShiftMask;
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            result |= X11Interop.Mod1Mask;
        }

        if ((modifiers & ModifierKeys.Meta) != 0)
        {
            result |= X11Interop.Mod4Mask;
        }

        return result;
    }

    // ── X11 Button Mapping ───────────────────────────────────────────

    /// <summary>
    /// Maps an X11 button number to a Cascade mouse button.
    /// </summary>
    internal static NativeMouseButton MapX11Button(uint button)
    {
        return button switch
        {
            X11Interop.Button1 => NativeMouseButton.Left,
            X11Interop.Button2 => NativeMouseButton.Middle,
            X11Interop.Button3 => NativeMouseButton.Right,
            _                  => NativeMouseButton.None
        };
    }
}

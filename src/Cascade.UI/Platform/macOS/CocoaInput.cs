namespace Cascade.UI;

/// <summary>
/// Processes macOS NSEvent objects and converts them into Cascade UI input
/// events. Translates Cocoa event types, key codes, modifier flags, and
/// mouse coordinates into the platform-independent Cascade input model.
/// </summary>
internal static class CocoaInput
{
    // ── Mouse ────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a mouse event from an NSEvent. Returns null if the event
    /// is not a mouse event.
    /// </summary>
    internal static NativeMouseEvent? ProcessMouseEvent(nint nsEvent, float scaleFactor)
    {
        if (nsEvent == 0)
        {
            return null;
        }

        ulong eventType = ObjC.MsgSendULong(nsEvent, ObjC.Type_Sel);
        NativeMouseEventType? cascadeType = GetMouseEventType(eventType);
        if (cascadeType is null)
        {
            return null;
        }

        // locationInWindow returns NSPoint in window coordinates (bottom-left origin).
        // We convert to top-left for Cascade's coordinate system.
        double rawX = ObjC.MsgSendDouble(nsEvent, ObjC.RegisterSelector("locationInWindow.x"));
        double rawY = ObjC.MsgSendDouble(nsEvent, ObjC.RegisterSelector("locationInWindow.y"));

        // For simplicity, read location via the paired doubles in the NSPoint.
        ObjC.MsgSendStret(out NSPoint location, nsEvent, ObjC.LocationInWindow);

        NativeMouseButton button = GetMouseButton(eventType, nsEvent);
        ModifierKeys modifiers = GetModifierKeys(nsEvent);

        return new NativeMouseEvent
        {
            X = (float)location.X,
            Y = (float)location.Y,
            Type = cascadeType.Value,
            Button = button,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Extracts a scroll event from an NSEvent. Returns null if the event
    /// is not a scroll wheel event.
    /// </summary>
    internal static NativeScrollEvent? ProcessScrollEvent(nint nsEvent, float scaleFactor)
    {
        if (nsEvent == 0)
        {
            return null;
        }

        ulong eventType = ObjC.MsgSendULong(nsEvent, ObjC.Type_Sel);
        if (eventType != ObjC.NSEventTypeScrollWheel)
        {
            return null;
        }

        ObjC.MsgSendStret(out NSPoint location, nsEvent, ObjC.LocationInWindow);

        double deltaX = ObjC.MsgSendDouble(nsEvent, ObjC.ScrollingDeltaX);
        double deltaY = ObjC.MsgSendDouble(nsEvent, ObjC.ScrollingDeltaY);

        // If the trackpad reports precise scrolling deltas, normalize them.
        bool precise = ObjC.MsgSendBool(nsEvent, ObjC.HasPreciseScrollingDeltas);
        if (!precise)
        {
            // Line-based scrolling — normalize to a reasonable step.
            deltaX *= 10.0;
            deltaY *= 10.0;
        }

        ModifierKeys modifiers = GetModifierKeys(nsEvent);

        return new NativeScrollEvent
        {
            X = (float)location.X,
            Y = (float)location.Y,
            DeltaX = (float)deltaX,
            DeltaY = (float)deltaY,
            Modifiers = modifiers
        };
    }

    // ── Keyboard ─────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a key event from an NSEvent. Returns null if the event
    /// is not a key event.
    /// </summary>
    internal static NativeKeyEvent? ProcessKeyEvent(nint nsEvent)
    {
        if (nsEvent == 0)
        {
            return null;
        }

        ulong eventType = ObjC.MsgSendULong(nsEvent, ObjC.Type_Sel);
        NativeKeyEventType? cascadeType = GetKeyEventType(eventType);
        if (cascadeType is null)
        {
            return null;
        }

        ushort keyCode = (ushort)ObjC.MsgSendULong(nsEvent, ObjC.KeyCode_Sel);
        Key key = MapKeyCode(keyCode);
        ModifierKeys modifiers = GetModifierKeys(nsEvent);

        char? character = null;
        if (cascadeType == NativeKeyEventType.KeyDown)
        {
            nint charsPtr = ObjC.MsgSend(nsEvent, ObjC.Characters);
            string? chars = ObjC.FromNSString(charsPtr);
            if (chars is { Length: > 0 })
            {
                char c = chars[0];
                // Filter out control characters (except Tab, Enter, Backspace).
                if (!char.IsControl(c) || c == '\t' || c == '\r' || c == '\b')
                {
                    character = c;
                }
            }
        }

        return new NativeKeyEvent
        {
            Key = key,
            Type = cascadeType.Value,
            Modifiers = modifiers,
            Character = character
        };
    }

    // ── macOS Key Code Mapping ───────────────────────────────────────

    /// <summary>
    /// Maps a macOS hardware key code to a Cascade UI <see cref="Key"/> value.
    /// macOS key codes are hardware scan codes, not virtual keys.
    /// </summary>
    internal static Key MapKeyCode(ushort keyCode)
    {
        return keyCode switch
        {
            // Letters (QWERTY layout)
            0x00 => Key.A,
            0x0B => Key.B,
            0x08 => Key.C,
            0x02 => Key.D,
            0x0E => Key.E,
            0x03 => Key.F,
            0x05 => Key.G,
            0x04 => Key.H,
            0x22 => Key.I,
            0x26 => Key.J,
            0x28 => Key.K,
            0x25 => Key.L,
            0x2E => Key.M,
            0x2D => Key.N,
            0x1F => Key.O,
            0x23 => Key.P,
            0x0C => Key.Q,
            0x0F => Key.R,
            0x01 => Key.S,
            0x11 => Key.T,
            0x20 => Key.U,
            0x09 => Key.V,
            0x0D => Key.W,
            0x07 => Key.X,
            0x10 => Key.Y,
            0x06 => Key.Z,

            // Digits
            0x1D => Key.D0,
            0x12 => Key.D1,
            0x13 => Key.D2,
            0x14 => Key.D3,
            0x15 => Key.D4,
            0x17 => Key.D5,
            0x16 => Key.D6,
            0x1A => Key.D7,
            0x1C => Key.D8,
            0x19 => Key.D9,

            // Function keys
            0x7A => Key.F1,
            0x78 => Key.F2,
            0x63 => Key.F3,
            0x76 => Key.F4,
            0x60 => Key.F5,
            0x61 => Key.F6,
            0x62 => Key.F7,
            0x64 => Key.F8,
            0x65 => Key.F9,
            0x6D => Key.F10,
            0x67 => Key.F11,
            0x6F => Key.F12,

            // Control keys
            0x33 => Key.Backspace,  // Delete (backspace)
            0x30 => Key.Tab,
            0x24 => Key.Enter,      // Return
            0x4C => Key.Enter,      // Numpad Enter
            0x39 => Key.CapsLock,
            0x35 => Key.Escape,
            0x31 => Key.Space,

            // Navigation
            0x74 => Key.PageUp,
            0x79 => Key.PageDown,
            0x77 => Key.End,
            0x73 => Key.Home,
            0x7B => Key.Left,
            0x7C => Key.Right,
            0x7E => Key.Up,
            0x7D => Key.Down,
            0x75 => Key.Delete,     // Forward Delete

            // Numpad
            0x52 => Key.NumPad0,
            0x53 => Key.NumPad1,
            0x54 => Key.NumPad2,
            0x55 => Key.NumPad3,
            0x56 => Key.NumPad4,
            0x57 => Key.NumPad5,
            0x58 => Key.NumPad6,
            0x59 => Key.NumPad7,
            0x5B => Key.NumPad8,
            0x5C => Key.NumPad9,
            0x43 => Key.NumPadMultiply,
            0x45 => Key.NumPadAdd,
            0x4E => Key.NumPadSubtract,
            0x41 => Key.NumPadDecimal,
            0x4B => Key.NumPadDivide,
            0x47 => Key.NumLock,    // Clear key on Mac numpad

            // Punctuation
            0x29 => Key.Semicolon,
            0x18 => Key.Equals,
            0x2B => Key.Comma,
            0x1B => Key.Minus,
            0x2F => Key.Period,
            0x2C => Key.Slash,
            0x32 => Key.Backtick,
            0x21 => Key.LeftBracket,
            0x2A => Key.Backslash,
            0x1E => Key.RightBracket,
            0x27 => Key.Quote,

            _ => Key.None
        };
    }

    /// <summary>
    /// Maps a Cascade UI <see cref="Key"/> value back to a macOS hardware key code.
    /// Returns 0xFFFF if no mapping exists.
    /// </summary>
    internal static ushort MapKeyToKeyCode(Key key)
    {
        return key switch
        {
            Key.A => 0x00,
            Key.B => 0x0B,
            Key.C => 0x08,
            Key.D => 0x02,
            Key.E => 0x0E,
            Key.F => 0x03,
            Key.G => 0x05,
            Key.H => 0x04,
            Key.I => 0x22,
            Key.J => 0x26,
            Key.K => 0x28,
            Key.L => 0x25,
            Key.M => 0x2E,
            Key.N => 0x2D,
            Key.O => 0x1F,
            Key.P => 0x23,
            Key.Q => 0x0C,
            Key.R => 0x0F,
            Key.S => 0x01,
            Key.T => 0x11,
            Key.U => 0x20,
            Key.V => 0x09,
            Key.W => 0x0D,
            Key.X => 0x07,
            Key.Y => 0x10,
            Key.Z => 0x06,

            Key.D0 => 0x1D,
            Key.D1 => 0x12,
            Key.D2 => 0x13,
            Key.D3 => 0x14,
            Key.D4 => 0x15,
            Key.D5 => 0x17,
            Key.D6 => 0x16,
            Key.D7 => 0x1A,
            Key.D8 => 0x1C,
            Key.D9 => 0x19,

            Key.F1  => 0x7A,
            Key.F2  => 0x78,
            Key.F3  => 0x63,
            Key.F4  => 0x76,
            Key.F5  => 0x60,
            Key.F6  => 0x61,
            Key.F7  => 0x62,
            Key.F8  => 0x64,
            Key.F9  => 0x65,
            Key.F10 => 0x6D,
            Key.F11 => 0x67,
            Key.F12 => 0x6F,

            Key.Backspace       => 0x33,
            Key.Tab             => 0x30,
            Key.Enter           => 0x24,
            Key.CapsLock        => 0x39,
            Key.Escape          => 0x35,
            Key.Space           => 0x31,
            Key.PageUp          => 0x74,
            Key.PageDown        => 0x79,
            Key.End             => 0x77,
            Key.Home            => 0x73,
            Key.Left            => 0x7B,
            Key.Right           => 0x7C,
            Key.Up              => 0x7E,
            Key.Down            => 0x7D,
            Key.Delete          => 0x75,

            Key.NumPad0         => 0x52,
            Key.NumPad1         => 0x53,
            Key.NumPad2         => 0x54,
            Key.NumPad3         => 0x55,
            Key.NumPad4         => 0x56,
            Key.NumPad5         => 0x57,
            Key.NumPad6         => 0x58,
            Key.NumPad7         => 0x59,
            Key.NumPad8         => 0x5B,
            Key.NumPad9         => 0x5C,
            Key.NumPadMultiply  => 0x43,
            Key.NumPadAdd       => 0x45,
            Key.NumPadSubtract  => 0x4E,
            Key.NumPadDecimal   => 0x41,
            Key.NumPadDivide    => 0x4B,
            Key.NumLock         => 0x47,

            Key.Semicolon       => 0x29,
            Key.Equals          => 0x18,
            Key.Comma           => 0x2B,
            Key.Minus           => 0x1B,
            Key.Period          => 0x2F,
            Key.Slash           => 0x2C,
            Key.Backtick        => 0x32,
            Key.LeftBracket     => 0x21,
            Key.Backslash       => 0x2A,
            Key.RightBracket    => 0x1E,
            Key.Quote           => 0x27,

            _ => 0xFFFF
        };
    }

    // ── Modifier Key Helpers ─────────────────────────────────────────

    /// <summary>
    /// Extracts Cascade modifier key flags from an NSEvent's modifierFlags.
    /// </summary>
    internal static ModifierKeys GetModifierKeys(nint nsEvent)
    {
        ulong flags = ObjC.MsgSendULong(nsEvent, ObjC.ModifierFlags);
        return MapModifierFlags(flags);
    }

    /// <summary>
    /// Maps macOS NSEventModifierFlags to Cascade <see cref="ModifierKeys"/>.
    /// On macOS, Command is the primary modifier (maps to Ctrl in Cascade),
    /// and Control is the secondary modifier (maps to Ctrl for platform
    /// consistency in cross-platform shortcuts).
    /// </summary>
    internal static ModifierKeys MapModifierFlags(ulong flags)
    {
        ModifierKeys mods = ModifierKeys.None;

        // macOS Command key maps to Cascade Ctrl for cross-platform shortcuts.
        if ((flags & ObjC.NSEventModifierFlagCommand) != 0)
        {
            mods |= ModifierKeys.Ctrl;
        }

        if ((flags & ObjC.NSEventModifierFlagShift) != 0)
        {
            mods |= ModifierKeys.Shift;
        }

        // macOS Option key maps to Cascade Alt.
        if ((flags & ObjC.NSEventModifierFlagOption) != 0)
        {
            mods |= ModifierKeys.Alt;
        }

        // macOS Control key maps to Cascade Meta.
        if ((flags & ObjC.NSEventModifierFlagControl) != 0)
        {
            mods |= ModifierKeys.Meta;
        }

        return mods;
    }

    // ── Private Helpers ──────────────────────────────────────────────

    private static NativeMouseEventType? GetMouseEventType(ulong eventType)
    {
        return eventType switch
        {
            ObjC.NSEventTypeLeftMouseDown     => NativeMouseEventType.MouseDown,
            ObjC.NSEventTypeLeftMouseUp       => NativeMouseEventType.MouseUp,
            ObjC.NSEventTypeRightMouseDown    => NativeMouseEventType.MouseDown,
            ObjC.NSEventTypeRightMouseUp      => NativeMouseEventType.MouseUp,
            ObjC.NSEventTypeOtherMouseDown    => NativeMouseEventType.MouseDown,
            ObjC.NSEventTypeOtherMouseUp      => NativeMouseEventType.MouseUp,
            ObjC.NSEventTypeMouseMoved        => NativeMouseEventType.MouseMove,
            ObjC.NSEventTypeLeftMouseDragged  => NativeMouseEventType.MouseMove,
            ObjC.NSEventTypeRightMouseDragged => NativeMouseEventType.MouseMove,
            ObjC.NSEventTypeOtherMouseDragged => NativeMouseEventType.MouseMove,
            ObjC.NSEventTypeMouseEntered      => NativeMouseEventType.MouseEnter,
            ObjC.NSEventTypeMouseExited       => NativeMouseEventType.MouseLeave,
            _ => null
        };
    }

    private static NativeMouseButton GetMouseButton(ulong eventType, nint nsEvent)
    {
        return eventType switch
        {
            ObjC.NSEventTypeLeftMouseDown     => NativeMouseButton.Left,
            ObjC.NSEventTypeLeftMouseUp       => NativeMouseButton.Left,
            ObjC.NSEventTypeLeftMouseDragged  => NativeMouseButton.Left,
            ObjC.NSEventTypeRightMouseDown    => NativeMouseButton.Right,
            ObjC.NSEventTypeRightMouseUp      => NativeMouseButton.Right,
            ObjC.NSEventTypeRightMouseDragged => NativeMouseButton.Right,
            ObjC.NSEventTypeOtherMouseDown    => MapOtherButton(nsEvent),
            ObjC.NSEventTypeOtherMouseUp      => MapOtherButton(nsEvent),
            ObjC.NSEventTypeOtherMouseDragged => MapOtherButton(nsEvent),
            _ => NativeMouseButton.None
        };
    }

    private static NativeMouseButton MapOtherButton(nint nsEvent)
    {
        long buttonNumber = ObjC.MsgSendLong(nsEvent, ObjC.ButtonNumber);
        return buttonNumber switch
        {
            2 => NativeMouseButton.Middle,
            _ => NativeMouseButton.None
        };
    }

    private static NativeKeyEventType? GetKeyEventType(ulong eventType)
    {
        return eventType switch
        {
            ObjC.NSEventTypeKeyDown => NativeKeyEventType.KeyDown,
            ObjC.NSEventTypeKeyUp   => NativeKeyEventType.KeyUp,
            _ => null
        };
    }
}

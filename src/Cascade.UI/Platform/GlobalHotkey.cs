namespace Cascade.UI;

/// <summary>
/// Represents a global hotkey registration. Global hotkeys fire even when
/// the app window does not have focus. Essential for clipboard managers,
/// launcher apps, screenshot tools, and tray-primary applications.
/// </summary>
public sealed class GlobalHotkey
{
    /// <summary>
    /// The key combination for this hotkey.
    /// </summary>
    public required Hotkey Hotkey { get; init; }

    /// <summary>
    /// A human-readable label for display in settings UI.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Handler invoked when the hotkey is pressed.
    /// </summary>
    public required Action OnPress { get; init; }
}

/// <summary>
/// A key combination consisting of zero or more modifier keys and a primary key.
/// </summary>
public readonly record struct Hotkey(ModifierKeys Modifiers, Key Key)
{
    /// <summary>
    /// Creates a hotkey from modifier flags and a primary key.
    /// </summary>
    public static Hotkey From(ModifierKeys modifiers, Key key)
    {
        return new Hotkey(modifiers, key);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var parts = new List<string>(4);

        if (Modifiers.HasFlag(ModifierKeys.Ctrl))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Meta))
        {
            parts.Add("Meta");
        }

        if (Key != Key.None)
        {
            parts.Add(FormatKeyName(Key));
        }

        return string.Join("+", parts);
    }

    private static string FormatKeyName(Key key)
    {
        return key switch
        {
            // Letter keys
            >= Key.A and <= Key.Z => key.ToString(),

            // Digit keys — strip the "D" prefix
            >= Key.D0 and <= Key.D9 => key.ToString()[1..],

            // Function keys
            >= Key.F1 and <= Key.F12 => key.ToString(),

            // NumPad keys — add space for readability
            Key.NumPad0 => "NumPad 0",
            Key.NumPad1 => "NumPad 1",
            Key.NumPad2 => "NumPad 2",
            Key.NumPad3 => "NumPad 3",
            Key.NumPad4 => "NumPad 4",
            Key.NumPad5 => "NumPad 5",
            Key.NumPad6 => "NumPad 6",
            Key.NumPad7 => "NumPad 7",
            Key.NumPad8 => "NumPad 8",
            Key.NumPad9 => "NumPad 9",
            Key.NumPadAdd => "NumPad +",
            Key.NumPadSubtract => "NumPad -",
            Key.NumPadMultiply => "NumPad *",
            Key.NumPadDivide => "NumPad /",
            Key.NumPadDecimal => "NumPad .",
            Key.NumPadEnter => "NumPad Enter",

            // Named keys — use friendly names
            Key.Escape => "Esc",
            Key.CapsLock => "Caps Lock",
            Key.Backspace => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "Page Up",
            Key.PageDown => "Page Down",
            Key.PrintScreen => "Print Screen",
            Key.ScrollLock => "Scroll Lock",
            Key.NumLock => "Num Lock",

            // Punctuation
            Key.Semicolon => ";",
            Key.Equals => "=",
            Key.Comma => ",",
            Key.Minus => "-",
            Key.Period => ".",
            Key.Slash => "/",
            Key.Backtick => "`",
            Key.LeftBracket => "[",
            Key.Backslash => "\\",
            Key.RightBracket => "]",
            Key.Quote => "'",

            // Everything else uses the enum name
            _ => key.ToString()
        };
    }
}

/// <summary>
/// Modifier key flags for hotkey combinations.
/// </summary>
[Flags]
public enum ModifierKeys
{
    /// <summary>No modifier keys.</summary>
    None = 0,

    /// <summary>Ctrl key (Command on macOS).</summary>
    Ctrl = 1 << 0,

    /// <summary>Shift key.</summary>
    Shift = 1 << 1,

    /// <summary>Alt key (Option on macOS).</summary>
    Alt = 1 << 2,

    /// <summary>Meta key (Windows key on Windows, Command on macOS).</summary>
    Meta = 1 << 3
}

/// <summary>
/// Keyboard key identifiers. Platform-independent — the framework maps
/// from platform-specific virtual key codes.
/// </summary>
public enum Key
{
    None,
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Escape, Tab, CapsLock, Space, Enter, Backspace, Delete,
    Insert, Home, End, PageUp, PageDown,
    Left, Right, Up, Down,
    PrintScreen, ScrollLock, Pause,
    NumLock, NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumPadAdd, NumPadSubtract, NumPadMultiply, NumPadDivide, NumPadDecimal, NumPadEnter,
    Semicolon, Equals, Comma, Minus, Period, Slash, Backtick,
    LeftBracket, Backslash, RightBracket, Quote
}

/// <summary>
/// Thrown when a global hotkey cannot be registered because it is already
/// registered by another application.
/// </summary>
public sealed class HotkeyConflictException : Exception
{
    /// <summary>The hotkey that could not be registered.</summary>
    public Hotkey Hotkey { get; }

    /// <summary>
    /// Creates a new <see cref="HotkeyConflictException"/>.
    /// </summary>
    public HotkeyConflictException(Hotkey hotkey)
        : base($"The hotkey {hotkey} is already registered by another application.")
    {
        Hotkey = hotkey;
    }

    /// <summary>
    /// Creates a new <see cref="HotkeyConflictException"/> with a custom message.
    /// </summary>
    public HotkeyConflictException(Hotkey hotkey, string message) : base(message)
    {
        Hotkey = hotkey;
    }

    /// <summary>
    /// Creates a new <see cref="HotkeyConflictException"/> with a custom message and inner exception.
    /// </summary>
    public HotkeyConflictException(Hotkey hotkey, string message, Exception innerException)
        : base(message, innerException)
    {
        Hotkey = hotkey;
    }
}

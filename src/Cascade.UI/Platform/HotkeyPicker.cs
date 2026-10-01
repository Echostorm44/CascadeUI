namespace Cascade.UI;

/// <summary>
/// A control for settings UIs that allows the user to record a hotkey
/// combination by pressing keys. Displays the current hotkey and enters
/// recording mode on focus/click.
/// </summary>
/// <remarks>
/// Click it (or press Enter/Space while it has focus) to record; the next key pressed together
/// with any modifiers becomes the hotkey and is passed to <see cref="OnChange"/>. While recording,
/// the field shows the modifiers being held. Escape cancels; moving focus away stops recording.
/// </remarks>
public class HotkeyPicker : Node
{
    /// <summary>
    /// The label displayed beside the picker field.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The currently assigned hotkey. Null if no hotkey is set.
    /// </summary>
    public Hotkey? Current { get; init; }

    /// <summary>
    /// Callback invoked when the user records a new hotkey combination.
    /// </summary>
    public Action<Hotkey>? OnChange { get; init; }

    /// <summary>Whether the picker is disabled (not focusable, cannot record).</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Text shown when no hotkey is set. Default: "None".</summary>
    public string Placeholder { get; init; } = "None";

    /// <summary>
    /// Whether the picker is waiting for a key. Lives on the node and is carried across re-renders by
    /// the reconciler; only meaningful while the picker has focus.
    /// </summary>
    internal bool IsRecording { get; set; }

    /// <summary>The field's text: the hotkey, the placeholder, or the recording prompt.</summary>
    internal string FieldText(bool focused, ModifierKeys held)
    {
        if (!focused || !IsRecording)
        {
            return Current?.ToString() ?? Placeholder;
        }
        return held == ModifierKeys.None ? "Press a shortcut…" : new Hotkey(held, Cascade.UI.Key.None) + "+…";
    }

    /// <summary>
    /// Handles a key-down while the picker has focus. Returns true when the key was consumed.
    /// </summary>
    internal bool HandleKey(Cascade.UI.Key key, ModifierKeys modifiers)
    {
        if (IsDisabled)
        {
            return false;
        }

        if (!IsRecording)
        {
            if (key is Cascade.UI.Key.Enter or Cascade.UI.Key.Space && modifiers == ModifierKeys.None)
            {
                IsRecording = true;
                return true;
            }
            return false;
        }

        if (key == Cascade.UI.Key.Escape && modifiers == ModifierKeys.None)
        {
            IsRecording = false;
            return true;
        }

        if (key == Cascade.UI.Key.None)
        {
            return true; // a bare modifier: keep waiting, the field shows what is held
        }

        IsRecording = false;
        OnChange?.Invoke(new Hotkey(modifiers, key));
        return true;
    }
}

namespace Cascade.UI;

/// <summary>
/// Free-text entry that produces a list of removable pill tags. Tags are
/// created by typing and pressing a delimiter key. Supports suggestions,
/// drag-to-reorder, and per-tag validation.
/// </summary>
public sealed class TagInput : Node
{
    /// <summary>
    /// Creates a tag input bound to a list of strings.
    /// </summary>
    /// <param name="value">Two-way binding to the tag list.</param>
    /// <param name="placeholder">Placeholder text shown when no tags and input is empty.</param>
    /// <param name="delimiter">Which key(s) create a new tag.</param>
    /// <param name="maxTags">Maximum number of tags allowed. Null for unlimited.</param>
    /// <param name="label">Optional label displayed above the input.</param>
    public TagInput(
        Bindable<IReadOnlyList<string>> value,
        LocKey placeholder = default,
        TagDelimiter delimiter = TagDelimiter.EnterAndComma,
        int? maxTags = null,
        LocKey label = default)
    {
        Value = value;
        Placeholder = placeholder;
        Delimiter = delimiter;
        MaxTags = maxTags;
        Label = label;
    }

    /// <summary>Two-way binding to the tag list.</summary>
    public Bindable<IReadOnlyList<string>> Value { get; }

    /// <summary>Placeholder text shown when no tags and input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Which key(s) create a new tag.</summary>
    public TagDelimiter Delimiter { get; }

    /// <summary>Maximum number of tags allowed.</summary>
    public int? MaxTags { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal Func<string, IEnumerable<string>>? SuggestionSyncSource { get; set; }
    internal Func<string, Task<IEnumerable<string>>>? SuggestionAsyncSource { get; set; }
    internal bool AllowDuplicateValues { get; set; }
    internal Action<string>? OnDuplicateHandler { get; set; }
    internal bool IsReorderEnabled { get; set; }
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    /// <summary>
    /// Runs per-tag validation for the given tag value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult ValidateTag(string tag)
    {
        foreach (var rule in ValidationRules)
        {
            var result = rule(tag);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }

    // ── Internal editing state ────────────────────────────────────

    /// <summary>Current text in the input field (not yet committed as a tag).</summary>
    internal string InputBuffer { get; set; } = "";

    /// <summary>Caret position within the input buffer.</summary>
    internal int CaretIndex { get; set; }

    /// <summary>Absolute viewport bounds, set by the painter for hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Bounds of the text input area within the control (viewport coords).</summary>
    internal Rect InputAreaBounds { get; set; }

    /// <summary>Bounds of each tag's remove (×) button (viewport coords). Index matches tag index.</summary>
    internal List<Rect> TagRemoveBounds { get; } = [];

    /// <summary>Index of the tag whose × button is hovered (-1 = none).</summary>
    internal int HoveredRemoveIndex { get; set; } = -1;

    /// <summary>Whether the control has focus.</summary>
    internal bool IsFocused { get; set; }

    /// <summary>
    /// When true, the next TagInput instance created should auto-restore focus.
    /// Set by AddTag/RemoveTagAt before the Invalidate-triggered re-render.
    /// </summary>
    internal static bool PendingFocusRestore { get; set; }

    /// <summary>
    /// Working copy of the tag list maintained by InputDispatcher across re-renders.
    /// Avoids Bindable staleness when FocusManager holds a stale node reference.
    /// </summary>
    internal List<string>? LiveTags { get; set; }

    /// <summary>
    /// Gets the current tag list — <see cref="LiveTags"/> if set (during editing),
    /// otherwise falls back to the Bindable snapshot <see cref="Value"/>.
    /// </summary>
    internal IReadOnlyList<string> CurrentTags => (IReadOnlyList<string>?)LiveTags ?? Value.Value;

    // ── Tag management methods ────────────────────────────────────

    internal void AddTag(string tag)
    {
        string trimmed = tag.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        var current = CurrentTags;

        // Check duplicates
        if (!AllowDuplicateValues && current.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            OnDuplicateHandler?.Invoke(trimmed);
            return;
        }

        // Check max tags
        if (MaxTags.HasValue && current.Count >= MaxTags.Value)
        {
            return;
        }

        // Validate
        var result = ValidateTag(trimmed);
        if (!result.IsValid)
        {
            return;
        }

        var newList = new List<string>(current) { trimmed };
        LiveTags = newList;
        PendingFocusRestore = true;
        Value.OnChange(newList);
        InputBuffer = "";
        CaretIndex = 0;
    }

    internal void RemoveTagAt(int index)
    {
        var current = CurrentTags;
        if (index < 0 || index >= current.Count)
        {
            return;
        }

        var newList = new List<string>(current);
        newList.RemoveAt(index);
        LiveTags = newList;
        PendingFocusRestore = true;
        Value.OnChange(newList);
    }
}

/// <summary>
/// Extension methods for <see cref="TagInput"/> providing fluent modifiers.
/// </summary>
public static class TagInputExtensions
{
    /// <summary>Adds a per-tag validation rule applied when each tag is created.</summary>
    public static TagInput Validate(this TagInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static TagInput ValidateOn(this TagInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Adds a suggestion source for autocomplete as the user types.</summary>
    public static TagInput Suggestions(this TagInput input, Func<string, IEnumerable<string>> source)
    {
        input.SuggestionSyncSource = source;
        return input;
    }

    /// <summary>Adds an async suggestion source for autocomplete.</summary>
    public static TagInput Suggestions(this TagInput input, Func<string, Task<IEnumerable<string>>> source)
    {
        input.SuggestionAsyncSource = source;
        return input;
    }

    /// <summary>Controls whether duplicate tags are allowed.</summary>
    public static TagInput AllowDuplicates(this TagInput input, bool allow, Action<string>? onDuplicate = null)
    {
        input.AllowDuplicateValues = allow;
        input.OnDuplicateHandler = onDuplicate;
        return input;
    }

    /// <summary>Enables drag-to-reorder for tags.</summary>
    public static TagInput ReorderEnabled(this TagInput input, bool enabled = true)
    {
        input.IsReorderEnabled = enabled;
        return input;
    }

    /// <summary>Disables the input.</summary>
    public static TagInput Disabled(this TagInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only.</summary>
    public static TagInput ReadOnly(this TagInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static TagInput AccessibleLabel(this TagInput input, LocKey label)
    {
        input.AccessibleLabelValue = label;
        return input;
    }
}

/// <summary>
/// Defines which key(s) create a new tag in a <see cref="TagInput"/>.
/// </summary>
public enum TagDelimiter
{
    /// <summary>Enter key only.</summary>
    Enter,

    /// <summary>Comma key only.</summary>
    Comma,

    /// <summary>Either Enter or Comma (default).</summary>
    EnterAndComma,

    /// <summary>Tab key (useful when comma is valid in a tag).</summary>
    Tab
}

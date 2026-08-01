namespace Cascade.UI;

/// <summary>
/// A text input variant that intercepts trigger characters (e.g., @ and #)
/// to open an autocomplete popover for mentions and hashtags. Confirmed
/// mentions render as inline pills.
/// </summary>
public sealed class MentionInput : Node
{
    /// <summary>
    /// Creates a mention-capable text input.
    /// </summary>
    /// <param name="value">Two-way binding to the raw text value including mention insert strings.</param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    /// <param name="triggers">
    /// The set of mention triggers, each defining a trigger character, source, rendering, and insert format.
    /// </param>
    /// <param name="label">Optional label displayed above the input.</param>
    public MentionInput(
        Bindable<string> value,
        LocKey placeholder = default,
        IReadOnlyList<IMentionTrigger>? triggers = null,
        LocKey label = default)
    {
        Value = value;
        Placeholder = placeholder;
        Triggers = triggers ?? [];
        Label = label;
    }

    /// <summary>Two-way binding to the raw text value.</summary>
    public Bindable<string> Value { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Mention triggers defining trigger characters and data sources.</summary>
    public IReadOnlyList<IMentionTrigger> Triggers { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal int? MaxLengthValue { get; set; }
    internal TimeSpan? DebounceDelay { get; set; }
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    // ── Internal editing state ────────────────────────────────────

    /// <summary>Absolute viewport bounds, set by the painter for hit testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>Whether the control has focus.</summary>
    internal bool IsFocused { get; set; }

    // ── Popup state ──────────────────────────────────────────────

    /// <summary>Whether the suggestion popup is currently open.</summary>
    internal bool IsPopupOpen { get; set; }

    /// <summary>The trigger that activated the current popup session.</summary>
    internal IMentionTrigger? ActiveTrigger { get; set; }

    /// <summary>Text typed after the trigger character (used to filter suggestions).</summary>
    internal string QueryText { get; set; } = "";

    /// <summary>Caret index where the trigger character was typed.</summary>
    internal int QueryStartIndex { get; set; }

    /// <summary>Current suggestion insert-texts matching the query.</summary>
    internal List<string> Suggestions { get; } = [];

    /// <summary>Index of the highlighted suggestion (-1 = none).</summary>
    internal int HighlightedIndex { get; set; } = -1;

    /// <summary>Popup bounds in viewport coordinates (set during painting).</summary>
    internal Rect PopupBounds { get; set; }

    /// <summary>Bounds of each suggestion item in viewport coordinates.</summary>
    internal List<Rect> SuggestionItemBounds { get; } = [];

    internal void OpenPopup(IMentionTrigger trigger, int queryStart)
    {
        ActiveTrigger = trigger;
        QueryStartIndex = queryStart;
        QueryText = "";
        IsPopupOpen = true;
        HighlightedIndex = 0;
        UpdateSuggestions();
    }

    internal void ClosePopup()
    {
        IsPopupOpen = false;
        ActiveTrigger = null;
        QueryText = "";
        Suggestions.Clear();
        SuggestionItemBounds.Clear();
        HighlightedIndex = -1;
    }

    internal void UpdateSuggestions()
    {
        Suggestions.Clear();
        SuggestionItemBounds.Clear();
        if (ActiveTrigger != null)
        {
            var results = ActiveTrigger.GetSuggestions(QueryText);
            Suggestions.AddRange(results);
        }
        HighlightedIndex = Suggestions.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        string currentValue = Value.Value ?? string.Empty;
        foreach (var rule in ValidationRules)
        {
            var result = rule(currentValue);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Non-generic interface for mention triggers, enabling heterogeneous
/// trigger lists in <see cref="MentionInput"/>.
/// </summary>
public interface IMentionTrigger
{
    /// <summary>The character that activates this trigger (e.g., '@' or '#').</summary>
    char TriggerChar { get; }

    /// <summary>
    /// Returns insert-text suggestions matching the given query.
    /// The query is the text typed after the trigger character.
    /// </summary>
    IReadOnlyList<string> GetSuggestions(string query);
}

/// <summary>
/// Defines a mention trigger with a specific data type for source items.
/// </summary>
/// <typeparam name="T">The type of items returned by the source.</typeparam>
public sealed class MentionTrigger<T> : IMentionTrigger
{
    /// <summary>
    /// Creates a typed mention trigger.
    /// </summary>
    /// <param name="trigger">The character that activates this trigger.</param>
    /// <param name="source">Function returning matching items for the typed query.</param>
    /// <param name="render">Function rendering each item in the popover.</param>
    /// <param name="insert">Function producing the text inserted into the input on selection.</param>
    public MentionTrigger(
        char trigger,
        Func<string, IEnumerable<T>> source,
        Func<T, Node> render,
        Func<T, string> insert)
    {
        TriggerChar = trigger;
        Source = source;
        Render = render;
        Insert = insert;
    }

    /// <inheritdoc/>
    public char TriggerChar { get; }

    /// <summary>Function returning matching items for the typed query.</summary>
    public Func<string, IEnumerable<T>> Source { get; }

    /// <summary>Function rendering each item in the popover.</summary>
    public Func<T, Node> Render { get; }

    /// <summary>Function producing the text inserted into the input on selection.</summary>
    public Func<T, string> Insert { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSuggestions(string query) =>
        Source(query).Select(item => Insert(item)).ToList();
}

/// <summary>
/// Extension methods for <see cref="MentionInput"/> providing fluent modifiers.
/// </summary>
public static class MentionInputExtensions
{
    /// <summary>Adds a validation rule.</summary>
    public static MentionInput Validate(this MentionInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static MentionInput ValidateOn(this MentionInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Sets the maximum character length.</summary>
    public static MentionInput MaxLength(this MentionInput input, int maxLength)
    {
        input.MaxLengthValue = maxLength;
        return input;
    }

    /// <summary>Debounces the OnChange callback.</summary>
    public static MentionInput Debounce(this MentionInput input, TimeSpan delay)
    {
        input.DebounceDelay = delay;
        return input;
    }

    /// <summary>Disables the input.</summary>
    public static MentionInput Disabled(this MentionInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only.</summary>
    public static MentionInput ReadOnly(this MentionInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static MentionInput AccessibleLabel(this MentionInput input, LocKey label)
    {
        input.LayoutData.A11yLabel = label.Resolve();
        return input;
    }
}

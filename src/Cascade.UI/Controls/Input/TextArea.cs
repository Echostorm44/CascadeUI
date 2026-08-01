namespace Cascade.UI;

/// <summary>
/// Multi-line text entry control. Shares all TextInput modifiers except masking.
/// Supports auto-grow, user resize, and character count display.
/// </summary>
public sealed class TextArea : Node
{
    /// <summary>
    /// Creates a multi-line text area bound to a string field.
    /// </summary>
    /// <param name="value">Two-way binding to the text value.</param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    /// <param name="minLines">Minimum visible lines determining initial height.</param>
    /// <param name="maxLines">Maximum visible lines before scrolling. Null for unbounded.</param>
    /// <param name="label">Optional label displayed above or beside the input.</param>
    public TextArea(
        Bindable<string> value,
        LocKey placeholder = default,
        int minLines = 3,
        int? maxLines = null,
        LocKey label = default)
    {
        Value = value;
        Placeholder = placeholder;
        MinLines = minLines;
        MaxLines = maxLines;
        Label = label;
    }

    /// <summary>Two-way binding to the text value.</summary>
    public Bindable<string> Value { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Minimum visible lines determining initial height.</summary>
    public int MinLines { get; internal set; }

    /// <summary>Maximum visible lines before scrolling.</summary>
    public int? MaxLines { get; internal set; }

    /// <summary>Optional label displayed above or beside the input.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal int? MaxLengthValue { get; set; }
    internal CountStyle? CharacterCountStyle { get; set; }
    internal int? FixedLines { get; set; }
    internal bool AutoGrowEnabled { get; set; }
    internal int? AutoGrowMinLines { get; set; }
    internal int? AutoGrowMaxLines { get; set; }
    internal ResizeDirection ResizeDir { get; set; } = ResizeDirection.None;
    internal Node PrefixNode { get; set; } = Node.Empty;
    internal Node SuffixNode { get; set; } = Node.Empty;
    internal TimeSpan? DebounceDelay { get; set; }
    internal Action<string>? OnChangeHandler { get; set; }
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    /// <summary>
    /// Absolute viewport-space bounds, set by the painter each frame.
    /// Used by InputDispatcher for mouse-to-caret hit testing.
    /// </summary>
    internal Rect AbsoluteBounds { get; set; }

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
/// Extension methods for <see cref="TextArea"/> providing fluent modifiers.
/// </summary>
public static class TextAreaExtensions
{
    /// <summary>Adds a validation rule to the text area.</summary>
    public static TextArea Validate(this TextArea input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static TextArea ValidateOn(this TextArea input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Sets the maximum character length.</summary>
    public static TextArea MaxLength(this TextArea input, int maxLength)
    {
        input.MaxLengthValue = maxLength;
        return input;
    }

    /// <summary>Shows a character count below the text area.</summary>
    public static TextArea ShowCharacterCount(this TextArea input, CountStyle style)
    {
        input.CharacterCountStyle = style;
        return input;
    }

    /// <summary>Sets a fixed number of visible lines.</summary>
    public static TextArea Lines(this TextArea input, int lines)
    {
        input.FixedLines = lines;
        input.MinLines = lines;
        input.MaxLines = lines;
        return input;
    }

    /// <summary>Enables auto-grow with optional bounds.</summary>
    public static TextArea AutoGrow(this TextArea input, int? minLines = null, int? maxLines = null)
    {
        input.AutoGrowEnabled = true;
        input.AutoGrowMinLines = minLines;
        input.AutoGrowMaxLines = maxLines;
        if (minLines.HasValue)
        {
            input.MinLines = minLines.Value;
        }

        if (maxLines.HasValue)
        {
            input.MaxLines = maxLines.Value;
        }

        return input;
    }

    /// <summary>Enables user-resizable handle.</summary>
    public static TextArea Resizable(this TextArea input, ResizeDirection direction)
    {
        input.ResizeDir = direction;
        return input;
    }

    /// <summary>Adds a prefix node rendered inside the input border.</summary>
    public static TextArea Prefix(this TextArea input, Node prefix)
    {
        input.PrefixNode = prefix;
        return input;
    }

    /// <summary>Adds a suffix node rendered inside the input border.</summary>
    public static TextArea Suffix(this TextArea input, Node suffix)
    {
        input.SuffixNode = suffix;
        return input;
    }

    /// <summary>Debounces the OnChange callback by the specified delay.</summary>
    public static TextArea Debounce(this TextArea input, TimeSpan delay)
    {
        input.DebounceDelay = delay;
        return input;
    }

    /// <summary>Registers a change handler that fires after debounce settles.</summary>
    public static TextArea OnChange(this TextArea input, Action<string> handler)
    {
        input.OnChangeHandler = handler;
        return input;
    }

    /// <summary>Disables the text area.</summary>
    public static TextArea Disabled(this TextArea input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the text area read-only.</summary>
    public static TextArea ReadOnly(this TextArea input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static TextArea AccessibleLabel(this TextArea input, LocKey label)
    {
        input.LayoutData.A11yLabel = label.Resolve();
        return input;
    }
}

/// <summary>
/// Style for displaying character count below a <see cref="TextArea"/>.
/// </summary>
public enum CountStyle
{
    /// <summary>Shows the current character count (e.g., "47").</summary>
    Current,

    /// <summary>Shows remaining characters (e.g., "233 remaining").</summary>
    Remaining,

    /// <summary>Shows current / maximum (e.g., "47 / 280").</summary>
    Fraction
}

/// <summary>
/// Direction in which a <see cref="TextArea"/> can be resized by the user.
/// </summary>
public enum ResizeDirection
{
    /// <summary>Not resizable.</summary>
    None,

    /// <summary>Resizable vertically only.</summary>
    Vertical,

    /// <summary>Resizable horizontally only.</summary>
    Horizontal,

    /// <summary>Resizable in both directions.</summary>
    Both
}

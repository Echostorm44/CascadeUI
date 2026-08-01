namespace Cascade.UI;

/// <summary>
/// Single-line text entry control. The foundational input control wrapping
/// the text editing engine with support for masking, validation, prefix/suffix
/// slots, and autocomplete.
/// </summary>
public sealed class TextInput : Node
{
    /// <summary>
    /// Creates a text input bound to a string field via two-way binding.
    /// </summary>
    /// <param name="value">Two-way binding to the text value.</param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    /// <param name="inputType">Semantic input type affecting keyboard and behavior.</param>
    /// <param name="label">Optional label displayed above or beside the input.</param>
    /// <param name="icon">Optional leading icon inside the input.</param>
    public TextInput(
        Bindable<string> value,
        LocKey placeholder = default,
        InputType inputType = InputType.Text,
        LocKey label = default,
        Icon icon = default)
    {
        Value = value;
        Placeholder = placeholder;
        InputType = inputType;
        Label = label;
        Icon = icon;
    }

    /// <summary>Two-way binding to the text value.</summary>
    public Bindable<string> Value { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; internal set; }

    /// <summary>Semantic input type affecting keyboard layout and behavior.</summary>
    public InputType InputType { get; }

    /// <summary>Optional label displayed above or beside the input.</summary>
    public LocKey Label { get; }

    /// <summary>Optional leading icon inside the input.</summary>
    public Icon Icon { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal int? MaxLengthValue { get; set; }
    internal MaskPattern? MaskPatternValue { get; set; }
    internal CustomMask? CustomMaskValue { get; set; }
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

    internal Func<string, IEnumerable<string>>? AutocompleteSyncSource { get; set; }
    internal Func<string, Task<IEnumerable<string>>>? AutocompleteAsyncSource { get; set; }
    internal ITypedAutocomplete? TypedAutocompleteSource { get; set; }

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
/// Non-generic interface for typed autocomplete sources,
/// enabling storage without generic type parameters on the control.
/// </summary>
internal interface ITypedAutocomplete
{
}

/// <summary>
/// Typed autocomplete source with custom rendering and selection.
/// </summary>
internal sealed class TypedAutocomplete<T> : ITypedAutocomplete
{
    internal TypedAutocomplete(
        Func<string, Task<IEnumerable<T>>> source,
        Func<T, Node> render,
        Func<T, string> select)
    {
        Source = source;
        Render = render;
        Select = select;
    }

    internal Func<string, Task<IEnumerable<T>>> Source { get; }
    internal Func<T, Node> Render { get; }
    internal Func<T, string> Select { get; }
}

/// <summary>
/// Extension methods for <see cref="TextInput"/> providing fluent modifiers.
/// </summary>
public static class TextInputExtensions
{
    /// <summary>Adds a validation rule to the input.</summary>
    public static TextInput Validate(this TextInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static TextInput ValidateOn(this TextInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Sets the maximum character length.</summary>
    public static TextInput MaxLength(this TextInput input, int maxLength)
    {
        input.MaxLengthValue = maxLength;
        return input;
    }

    /// <summary>Applies a mask pattern constraining input and display format.</summary>
    public static TextInput Mask(this TextInput input, MaskPattern mask)
    {
        input.MaskPatternValue = mask;
        return input;
    }

    /// <summary>Applies a mask pattern from a format string.</summary>
    public static TextInput Mask(this TextInput input, string pattern)
    {
        input.MaskPatternValue = new MaskPattern(pattern);
        return input;
    }

    /// <summary>Applies a custom mask with full format/parse control.</summary>
    public static TextInput Mask(this TextInput input, CustomMask mask)
    {
        input.CustomMaskValue = mask;
        return input;
    }

    /// <summary>Adds a prefix node rendered inside the input border.</summary>
    public static TextInput Prefix(this TextInput input, Node prefix)
    {
        input.PrefixNode = prefix;
        return input;
    }

    /// <summary>Adds a suffix node rendered inside the input border.</summary>
    public static TextInput Suffix(this TextInput input, Node suffix)
    {
        input.SuffixNode = suffix;
        return input;
    }

    /// <summary>Adds a placeholder override displayed inside the input.</summary>
    public static TextInput Placeholder(this TextInput input, LocKey placeholder)
    {
        input.Placeholder = placeholder;
        return input;
    }

    /// <summary>Debounces the OnChange callback by the specified delay.</summary>
    public static TextInput Debounce(this TextInput input, TimeSpan delay)
    {
        input.DebounceDelay = delay;
        return input;
    }

    /// <summary>Registers a change handler that fires after debounce settles.</summary>
    public static TextInput OnChange(this TextInput input, Action<string> handler)
    {
        input.OnChangeHandler = handler;
        return input;
    }

    /// <summary>Disables the input control.</summary>
    public static TextInput Disabled(this TextInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only (visible but not editable).</summary>
    public static TextInput ReadOnly(this TextInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static TextInput AccessibleLabel(this TextInput input, LocKey label)
    {
        input.LayoutData.A11yLabel = label.Resolve();
        return input;
    }

    /// <summary>Adds a string-based autocomplete suggestion source.</summary>
    public static TextInput Autocomplete(this TextInput input, Func<string, IEnumerable<string>> source)
    {
        input.AutocompleteSyncSource = source;
        return input;
    }

    /// <summary>Adds an async string-based autocomplete suggestion source.</summary>
    public static TextInput Autocomplete(this TextInput input, Func<string, Task<IEnumerable<string>>> source)
    {
        input.AutocompleteAsyncSource = source;
        return input;
    }

    /// <summary>Adds a typed autocomplete with custom rendering and selection.</summary>
    public static TextInput Autocomplete<T>(
        this TextInput input,
        Func<string, Task<IEnumerable<T>>> source,
        Func<T, Node> render,
        Func<T, string> select)
    {
        input.TypedAutocompleteSource = new TypedAutocomplete<T>(source, render, select);
        return input;
    }
}

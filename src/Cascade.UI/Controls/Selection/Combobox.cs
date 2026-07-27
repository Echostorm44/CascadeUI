namespace Cascade.UI;

/// <summary>
/// Non-generic interface for <see cref="Combobox{T}"/> used by the rendering
/// pipeline to paint combobox controls without requiring the generic type parameter.
/// </summary>
internal interface IComboboxNode
{
    LocKey Placeholder { get; }
    LocKey Label { get; }
    bool IsNodeDisabled { get; }
    bool IsOpen { get; set; }

    /// <summary>Display text for the current value when the dropdown is closed.</summary>
    string? DisplayText { get; }

    /// <summary>The current search/filter text typed by the user while the dropdown is open.</summary>
    string SearchText { get; set; }

    /// <summary>Number of options matching the current search filter.</summary>
    int FilteredOptionCount { get; }

    /// <summary>Gets the display label for a filtered option by index.</summary>
    string GetFilteredOptionLabel(int index);

    /// <summary>Selects the filtered option at the given index and closes the dropdown.</summary>
    void SelectFilteredIndex(int index);

    /// <summary>Commits the current search text as a free-form value and closes.</summary>
    void CommitText();

    /// <summary>Index of the currently highlighted (hovered) option, or -1 for none.</summary>
    int HighlightedIndex { get; set; }

    /// <summary>Scroll offset for long dropdown lists.</summary>
    int ScrollOffset { get; set; }

    /// <summary>Dropdown bounds in absolute coordinates for hit testing.</summary>
    Rect DropdownBounds { get; set; }

    /// <summary>Dropdown item height in logical pixels, set by the painter from theme.</summary>
    float DropdownItemHeight { get; set; }

    /// <summary>Toggles the dropdown open/closed state.</summary>
    void ToggleOpen();

    /// <summary>Closes the dropdown without selecting.</summary>
    void Close();
}

/// <summary>
/// An editable select — a text input that shows suggestions from a list but
/// also accepts values not in the list. Unlike <see cref="Select{T}"/>, the
/// value is not restricted to the options list.
/// </summary>
/// <typeparam name="T">The type of the bound value.</typeparam>
public sealed class Combobox<T> : Node, IComboboxNode where T : notnull
{
    /// <summary>
    /// Creates a combobox with a static list of options.
    /// </summary>
    /// <param name="value">Two-way binding to the selected or typed value.</param>
    /// <param name="options">Static list of suggested options.</param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    /// <param name="label">Optional label displayed above the combobox.</param>
    public Combobox(
        Bindable<T> value,
        IReadOnlyList<SelectOption<T>>? options = null,
        LocKey placeholder = default,
        LocKey label = default)
    {
        Value = value;
        StaticOptions = options;
        Placeholder = placeholder;
        Label = label;
    }

    /// <summary>Two-way binding to the selected or typed value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>Static list of suggested options. Null when using async options.</summary>
    public IReadOnlyList<SelectOption<T>>? StaticOptions { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal Func<string, Task<IEnumerable<SelectOption<T>>>>? AsyncOptionSource { get; set; }
    internal TimeSpan? DebounceDelay { get; set; }
    internal Func<T, Node>? OptionRenderer { get; set; }
    internal List<Func<T, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        T currentValue = Value.Value;
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

    // ── Runtime state for rendering and input ─────────────────────────

    bool IComboboxNode.IsNodeDisabled => IsDisabled;

    /// <summary>Whether the dropdown is currently open.</summary>
    internal bool IsOpen { get; set; }

    bool IComboboxNode.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <summary>Current search text typed while open.</summary>
    internal string SearchTextValue { get; set; } = "";

    string IComboboxNode.SearchText
    {
        get => SearchTextValue;
        set => SearchTextValue = value;
    }

    /// <summary>Index of the highlighted (hovered) option.</summary>
    internal int HighlightedIndex { get; set; } = -1;

    int IComboboxNode.HighlightedIndex
    {
        get => HighlightedIndex;
        set => HighlightedIndex = value;
    }

    /// <summary>Dropdown bounds set by the painter for hit testing.</summary>
    internal Rect DropdownBoundsValue { get; set; }

    Rect IComboboxNode.DropdownBounds
    {
        get => DropdownBoundsValue;
        set => DropdownBoundsValue = value;
    }

    /// <summary>Scroll offset for long dropdown lists.</summary>
    internal int ScrollOffsetValue { get; set; }

    int IComboboxNode.ScrollOffset
    {
        get => ScrollOffsetValue;
        set => ScrollOffsetValue = value;
    }

    /// <summary>Dropdown item height set by painter from theme.</summary>
    internal float DropdownItemHeightValue { get; set; } = 32f;

    float IComboboxNode.DropdownItemHeight
    {
        get => DropdownItemHeightValue;
        set => DropdownItemHeightValue = value;
    }

    void IComboboxNode.ToggleOpen()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            // Pre-populate search with current display text
            SearchTextValue = ((IComboboxNode)this).DisplayText ?? "";
            HighlightedIndex = -1;
            ScrollOffsetValue = 0;
        }
        else
        {
            SearchTextValue = "";
        }
    }

    void IComboboxNode.Close()
    {
        IsOpen = false;
        SearchTextValue = "";
        HighlightedIndex = -1;
        ScrollOffsetValue = 0;
    }

    string? IComboboxNode.DisplayText
    {
        get
        {
            T? val = Value.Value;
            if (val == null)
            {
                return null;
            }

            // Try to find a matching option label
            var options = StaticOptions;
            if (options is not null)
            {
                foreach (var opt in options)
                {
                    if (EqualityComparer<T>.Default.Equals(opt.Value, val))
                    {
                        return opt.Label.Resolve();
                    }
                }
            }

            return val.ToString();
        }
    }

    private IReadOnlyList<SelectOption<T>> GetFilteredOptions()
    {
        var options = StaticOptions;
        if (options is null || options.Count == 0)
        {
            return Array.Empty<SelectOption<T>>();
        }

        string search = SearchTextValue;
        if (string.IsNullOrEmpty(search))
        {
            return options;
        }

        var filtered = new List<SelectOption<T>>();
        foreach (var opt in options)
        {
            string label = opt.Label.Resolve();
            if (label.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(opt);
            }
        }

        return filtered;
    }

    int IComboboxNode.FilteredOptionCount => GetFilteredOptions().Count;

    string IComboboxNode.GetFilteredOptionLabel(int index)
    {
        var filtered = GetFilteredOptions();
        if (index < 0 || index >= filtered.Count)
        {
            return string.Empty;
        }

        return filtered[index].Label.Resolve();
    }

    void IComboboxNode.SelectFilteredIndex(int index)
    {
        var filtered = GetFilteredOptions();
        if (index < 0 || index >= filtered.Count)
        {
            return;
        }

        Value.OnChange(filtered[index].Value);
        IsOpen = false;
        SearchTextValue = "";
        HighlightedIndex = -1;
    }

    void IComboboxNode.CommitText()
    {
        // For string-typed comboboxes, commit the typed text directly.
        // For other types, try to find a matching option.
        string text = SearchTextValue;
        if (string.IsNullOrEmpty(text))
        {
            IsOpen = false;
            SearchTextValue = "";
            return;
        }

        // Try exact match first
        var options = StaticOptions;
        if (options is not null)
        {
            foreach (var opt in options)
            {
                if (string.Equals(opt.Label.Resolve(), text, StringComparison.OrdinalIgnoreCase))
                {
                    Value.OnChange(opt.Value);
                    IsOpen = false;
                    SearchTextValue = "";
                    return;
                }
            }
        }

        // For string type, commit the raw text
        if (typeof(T) == typeof(string) && text is T typedValue)
        {
            Value.OnChange(typedValue);
        }

        IsOpen = false;
        SearchTextValue = "";
    }
}

/// <summary>
/// Extension methods for <see cref="Combobox{T}"/> providing fluent modifiers.
/// </summary>
public static class ComboboxExtensions
{
    /// <summary>Sets an async option source loaded as the user types.</summary>
    public static Combobox<T> Options<T>(this Combobox<T> combobox, Func<string, Task<IEnumerable<SelectOption<T>>>> source)
        where T : notnull
    {
        combobox.AsyncOptionSource = source;
        return combobox;
    }

    /// <summary>Debounces the async option loading by the specified delay.</summary>
    public static Combobox<T> Debounce<T>(this Combobox<T> combobox, TimeSpan delay)
        where T : notnull
    {
        combobox.DebounceDelay = delay;
        return combobox;
    }

    /// <summary>Sets a custom renderer for options in the open dropdown.</summary>
    public static Combobox<T> RenderOption<T>(this Combobox<T> combobox, Func<T, Node> render)
        where T : notnull
    {
        combobox.OptionRenderer = render;
        return combobox;
    }

    /// <summary>Adds a validation rule.</summary>
    public static Combobox<T> Validate<T>(this Combobox<T> combobox, Func<T, ValidationResult> rule)
        where T : notnull
    {
        combobox.ValidationRules.Add(rule);
        return combobox;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Combobox<T> ValidateOn<T>(this Combobox<T> combobox, ValidationTrigger trigger)
        where T : notnull
    {
        combobox.ValidationTriggerMode = trigger;
        return combobox;
    }

    /// <summary>Disables the combobox.</summary>
    public static Combobox<T> Disabled<T>(this Combobox<T> combobox, bool disabled = true)
        where T : notnull
    {
        combobox.IsDisabled = disabled;
        return combobox;
    }

    /// <summary>Makes the combobox read-only.</summary>
    public static Combobox<T> ReadOnly<T>(this Combobox<T> combobox, bool readOnly = true)
        where T : notnull
    {
        combobox.IsReadOnly = readOnly;
        return combobox;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Combobox<T> AccessibleLabel<T>(this Combobox<T> combobox, LocKey label)
        where T : notnull
    {
        combobox.AccessibleLabelValue = label;
        return combobox;
    }
}

namespace Cascade.UI;

/// <summary>
/// Non-generic interface for <see cref="Select{T}"/> used by the rendering
/// pipeline to paint select controls without requiring the generic type parameter.
/// </summary>
internal interface ISelectNode
{
    LocKey Placeholder { get; }
    LocKey Label { get; }
    bool IsNodeDisabled { get; }
    bool IsOpen { get; set; }
    string? SelectedDisplayText { get; }

    /// <summary>Total number of selectable options (flat list or across all groups).</summary>
    int OptionCount { get; }

    /// <summary>Index of the currently highlighted (hovered) option, or -1 for none.</summary>
    int HighlightedIndex { get; set; }

    /// <summary>Index of the currently selected option, or -1 for none.</summary>
    int SelectedIndex { get; }

    /// <summary>Gets the display label for an option by flat index.</summary>
    string GetOptionLabel(int index);

    /// <summary>Selects the option at the given flat index and closes the dropdown.</summary>
    void SelectIndex(int index);

    /// <summary>Toggles the dropdown open/closed state.</summary>
    void ToggleOpen();

    /// <summary>Closes the dropdown without selecting.</summary>
    void Close();

    /// <summary>
    /// Dropdown bounds in absolute coordinates, set by the painter after rendering
    /// so the input dispatcher can perform hit testing on the dropdown area.
    /// </summary>
    Rect DropdownBounds { get; set; }

    /// <summary>
    /// Scroll offset (number of items scrolled from the top) for long dropdown lists.
    /// </summary>
    int ScrollOffset { get; set; }

    /// <summary>
    /// Dropdown item height in logical pixels, set by the painter from theme
    /// so the input dispatcher can compute which item was clicked/hovered.
    /// </summary>
    float DropdownItemHeight { get; set; }
}

/// <summary>
/// Single-item dropdown selection. Supports typed options, grouped options,
/// searchable filtering, and custom option rendering.
/// </summary>
/// <typeparam name="T">The type of the selectable value.</typeparam>
#pragma warning disable CA1716 // Type name Select matches a reserved keyword — intentional API name per spec
public sealed class Select<T> : Node, ISelectNode where T : notnull
#pragma warning restore CA1716
{
    /// <summary>
    /// Creates a select dropdown with a flat list of options.
    /// </summary>
    /// <param name="value">Two-way binding to the selected value.</param>
    /// <param name="options">The available options.</param>
    /// <param name="placeholder">Placeholder text shown when nothing is selected.</param>
    /// <param name="label">Optional label displayed above the select.</param>
    public Select(
        Bindable<T> value,
        IReadOnlyList<SelectOption<T>> options,
        LocKey placeholder = default,
        LocKey label = default)
    {
        Value = value;
        Options = options;
        Groups = null;
        Placeholder = placeholder;
        Label = label;
    }

    /// <summary>
    /// Creates a select dropdown with grouped options.
    /// </summary>
    /// <param name="value">Two-way binding to the selected value.</param>
    /// <param name="groups">The option groups.</param>
    /// <param name="placeholder">Placeholder text shown when nothing is selected.</param>
    /// <param name="label">Optional label displayed above the select.</param>
    public Select(
        Bindable<T> value,
        IReadOnlyList<SelectGroup<T>> groups,
        LocKey placeholder = default,
        LocKey label = default)
    {
        Value = value;
        Options = null;
        Groups = groups;
        Placeholder = placeholder;
        Label = label;
    }

    /// <summary>Two-way binding to the selected value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>Flat list of options. Null when using groups.</summary>
    public IReadOnlyList<SelectOption<T>>? Options { get; }

    /// <summary>Grouped options. Null when using a flat list.</summary>
    public IReadOnlyList<SelectGroup<T>>? Groups { get; }

    /// <summary>Placeholder text shown when nothing is selected.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool IsSearchable { get; set; }
    internal Func<string, Task<IEnumerable<SelectOption<T>>>>? AsyncSearchSource { get; set; }
    internal Func<T, Node>? OptionRenderer { get; set; }
    internal Func<T, Node>? SelectedRenderer { get; set; }
    internal List<Func<T, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    bool ISelectNode.IsNodeDisabled => IsDisabled;

    /// <summary>Whether the dropdown panel is currently open.</summary>
    internal bool IsOpen { get; set; }

    /// <summary>Index of the highlighted (hovered) option.</summary>
    internal int HighlightedIndex { get; set; } = -1;

    /// <summary>Dropdown bounds set by the painter for hit testing.</summary>
    internal Rect DropdownBoundsValue { get; set; }

    bool ISelectNode.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }
    int ISelectNode.HighlightedIndex
    {
        get => HighlightedIndex;
        set => HighlightedIndex = value;
    }
    Rect ISelectNode.DropdownBounds
    {
        get => DropdownBoundsValue;
        set => DropdownBoundsValue = value;
    }

    /// <summary>Scroll offset for long dropdown lists.</summary>
    internal int ScrollOffsetValue { get; set; }

    int ISelectNode.ScrollOffset
    {
        get => ScrollOffsetValue;
        set => ScrollOffsetValue = value;
    }

    /// <summary>Dropdown item height set by painter from theme.</summary>
    internal float DropdownItemHeightValue { get; set; } = 32f;

    float ISelectNode.DropdownItemHeight
    {
        get => DropdownItemHeightValue;
        set => DropdownItemHeightValue = value;
    }

    void ISelectNode.ToggleOpen()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            HighlightedIndex = -1;
            ScrollOffsetValue = 0;
        }
    }

    void ISelectNode.Close()
    {
        IsOpen = false;
        HighlightedIndex = -1;
        ScrollOffsetValue = 0;
    }

    int ISelectNode.OptionCount => GetFlatOptions().Count;

    int ISelectNode.SelectedIndex
    {
        get
        {
            T? val = Value.Value;
            if (val == null)
            {
                return -1;
            }

            var flat = GetFlatOptions();
            for (int i = 0; i < flat.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(flat[i].Value, val))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    string ISelectNode.GetOptionLabel(int index)
    {
        var flat = GetFlatOptions();
        if (index < 0 || index >= flat.Count)
        {
            return string.Empty;
        }

        return flat[index].Label.Resolve();
    }

    void ISelectNode.SelectIndex(int index)
    {
        var flat = GetFlatOptions();
        if (index < 0 || index >= flat.Count)
        {
            return;
        }

        Value.OnChange(flat[index].Value);
        IsOpen = false;
        HighlightedIndex = -1;
    }

    private IReadOnlyList<SelectOption<T>> GetFlatOptions()
    {
        if (Options is not null)
        {
            return Options;
        }

        if (Groups is not null)
        {
            var flat = new List<SelectOption<T>>();
            foreach (var group in Groups)
            {
                flat.AddRange(group.Options);
            }

            return flat;
        }

        return Array.Empty<SelectOption<T>>();
    }

    /// <summary>
    /// Returns the display text for the currently selected value, or null
    /// if nothing is selected. Used by <see cref="ISelectNode"/> for painting.
    /// </summary>
    string? ISelectNode.SelectedDisplayText
    {
        get
        {
            T? val = Value.Value;
            if (val == null)
            {
                return null;
            }

            // Try to find the matching option's label
            if (Options is not null)
            {
                foreach (var opt in Options)
                {
                    if (EqualityComparer<T>.Default.Equals(opt.Value, val))
                    {
                        return opt.Label.Resolve();
                    }
                }
            }
            else if (Groups is not null)
            {
                foreach (var group in Groups)
                {
                    foreach (var opt in group.Options)
                    {
                        if (EqualityComparer<T>.Default.Equals(opt.Value, val))
                        {
                            return opt.Label.Resolve();
                        }
                    }
                }
            }

            return val.ToString();
        }
    }

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
}

/// <summary>
/// A single option in a <see cref="Select{T}"/> dropdown.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class SelectOption<T>
{
    /// <summary>
    /// Creates a select option.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="label">The display label.</param>
    public SelectOption(T value, LocKey label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>The option value.</summary>
    public T Value { get; }

    /// <summary>The display label.</summary>
    public LocKey Label { get; }
}

/// <summary>
/// A group of options in a <see cref="Select{T}"/> dropdown with a header.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class SelectGroup<T>
{
    /// <summary>
    /// Creates an option group.
    /// </summary>
    /// <param name="header">The group header text.</param>
    /// <param name="options">The options in this group.</param>
    public SelectGroup(LocKey header, IEnumerable<SelectOption<T>> options)
    {
        Header = header;
        Options = options;
    }

    /// <summary>The group header text.</summary>
    public LocKey Header { get; }

    /// <summary>The options in this group.</summary>
    public IEnumerable<SelectOption<T>> Options { get; }
}

/// <summary>
/// Extension methods for <see cref="Select{T}"/> providing fluent modifiers.
/// </summary>
public static class SelectExtensions
{
    /// <summary>Makes the dropdown searchable with local substring matching.</summary>
    public static Select<T> Searchable<T>(this Select<T> select)
        where T : notnull
    {
        select.IsSearchable = true;
        return select;
    }

    /// <summary>Makes the dropdown searchable with an async search function.</summary>
    public static Select<T> Searchable<T>(this Select<T> select, Func<string, Task<IEnumerable<SelectOption<T>>>> search)
        where T : notnull
    {
        select.IsSearchable = true;
        select.AsyncSearchSource = search;
        return select;
    }

    /// <summary>Sets a custom renderer for options in the open dropdown.</summary>
    public static Select<T> RenderOption<T>(this Select<T> select, Func<T, Node> render)
        where T : notnull
    {
        select.OptionRenderer = render;
        return select;
    }

    /// <summary>Sets a custom renderer for the selected value in the closed field.</summary>
    public static Select<T> RenderSelected<T>(this Select<T> select, Func<T, Node> render)
        where T : notnull
    {
        select.SelectedRenderer = render;
        return select;
    }

    /// <summary>Adds a validation rule.</summary>
    public static Select<T> Validate<T>(this Select<T> select, Func<T, ValidationResult> rule)
        where T : notnull
    {
        select.ValidationRules.Add(rule);
        return select;
    }

    /// <summary>Sets when validation fires.</summary>
    public static Select<T> ValidateOn<T>(this Select<T> select, ValidationTrigger trigger)
        where T : notnull
    {
        select.ValidationTriggerMode = trigger;
        return select;
    }

    /// <summary>Disables the select.</summary>
    public static Select<T> Disabled<T>(this Select<T> select, bool disabled = true)
        where T : notnull
    {
        select.IsDisabled = disabled;
        return select;
    }

    /// <summary>Makes the select read-only.</summary>
    public static Select<T> ReadOnly<T>(this Select<T> select, bool readOnly = true)
        where T : notnull
    {
        select.IsReadOnly = readOnly;
        return select;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static Select<T> AccessibleLabel<T>(this Select<T> select, LocKey label)
        where T : notnull
    {
        select.LayoutData.A11yLabel = label.Resolve();
        return select;
    }
}

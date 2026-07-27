namespace Cascade.UI;

/// <summary>
/// A row (or column) of mutually exclusive toggle buttons. Exactly one can
/// be active at a time. Used for view selectors, alignment tools, time range
/// pickers, and any option set where all choices are visible simultaneously.
/// </summary>
/// <typeparam name="T">The type of the selectable value.</typeparam>
public sealed class ToggleGroup<T> : Node, IToggleGroup where T : notnull
{
    /// <summary>
    /// Creates a toggle group bound to a typed field.
    /// </summary>
    /// <param name="value">Two-way binding to the selected value.</param>
    /// <param name="options">The available toggle options.</param>
    /// <param name="label">Optional label displayed above the group.</param>
    public ToggleGroup(
        Bindable<T> value,
        IReadOnlyList<ToggleOption<T>> options,
        LocKey label = default)
    {
        Value = value;
        Options = options;
        Label = label;
    }

    /// <summary>Two-way binding to the selected value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>The available toggle options.</summary>
    public IReadOnlyList<ToggleOption<T>> Options { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal Orientation OrientationValue { get; set; } = Orientation.Horizontal;
    internal List<Func<T, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    // ── IToggleGroup implementation ──────────────────────────────────

    int IToggleGroup.OptionCount => Options.Count;

    string IToggleGroup.GetOptionLabel(int index)
    {
        var opt = Options[index];
        string resolved = opt.TextLabel.Resolve();
        return !string.IsNullOrEmpty(resolved) ? resolved : $"Option {index + 1}";
    }

    int IToggleGroup.SelectedIndex
    {
        get
        {
            T current = Value.Value;
            for (int i = 0; i < Options.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(Options[i].Value, current))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    void IToggleGroup.SelectIndex(int index)
    {
        if (index >= 0 && index < Options.Count && !IsDisabled && !IsReadOnly)
        {
            Value.OnChange(Options[index].Value);
        }
    }

    bool IToggleGroup.IsControlDisabled => IsDisabled;

    Rect IToggleGroup.AbsoluteBounds { get; set; }

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
/// A single option in a <see cref="ToggleGroup{T}"/>.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class ToggleOption<T>
{
    /// <summary>
    /// Creates a toggle option with a text label.
    /// </summary>
    /// <param name="value">The value this option represents.</param>
    /// <param name="label">The text label.</param>
    public ToggleOption(T value, LocKey label)
    {
        Value = value;
        TextLabel = label;
        NodeLabel = Node.Empty;
    }

    /// <summary>
    /// Creates a toggle option with a rich node label (e.g., an icon).
    /// </summary>
    /// <param name="value">The value this option represents.</param>
    /// <param name="label">The rich node label.</param>
    public ToggleOption(T value, Node label)
    {
        Value = value;
        TextLabel = default;
        NodeLabel = label;
    }

    /// <summary>The value this option represents.</summary>
    public T Value { get; }

    /// <summary>Text label. Default when using a rich node label.</summary>
    public LocKey TextLabel { get; }

    /// <summary>Rich node label. <see cref="Node.Empty"/> when using a text label.</summary>
    public Node NodeLabel { get; }
}

/// <summary>
/// Extension methods for <see cref="ToggleGroup{T}"/> providing fluent modifiers.
/// </summary>
public static class ToggleGroupExtensions
{
    /// <summary>Sets the layout orientation of the toggle buttons.</summary>
    public static ToggleGroup<T> Orientation<T>(this ToggleGroup<T> group, Orientation orientation)
        where T : notnull
    {
        group.OrientationValue = orientation;
        return group;
    }

    /// <summary>Adds a validation rule.</summary>
    public static ToggleGroup<T> Validate<T>(this ToggleGroup<T> group, Func<T, ValidationResult> rule)
        where T : notnull
    {
        group.ValidationRules.Add(rule);
        return group;
    }

    /// <summary>Sets when validation fires.</summary>
    public static ToggleGroup<T> ValidateOn<T>(this ToggleGroup<T> group, ValidationTrigger trigger)
        where T : notnull
    {
        group.ValidationTriggerMode = trigger;
        return group;
    }

    /// <summary>Disables all buttons in the group.</summary>
    public static ToggleGroup<T> Disabled<T>(this ToggleGroup<T> group, bool disabled = true)
        where T : notnull
    {
        group.IsDisabled = disabled;
        return group;
    }

    /// <summary>Makes all buttons in the group read-only.</summary>
    public static ToggleGroup<T> ReadOnly<T>(this ToggleGroup<T> group, bool readOnly = true)
        where T : notnull
    {
        group.IsReadOnly = readOnly;
        return group;
    }

    /// <summary>Sets the accessible label for the toggle group.</summary>
    public static ToggleGroup<T> AccessibleLabel<T>(this ToggleGroup<T> group, LocKey label)
        where T : notnull
    {
        group.AccessibleLabelValue = label;
        return group;
    }
}

/// <summary>
/// Non-generic interface for ToggleGroup to enable layout/paint/hit-testing
/// without knowing the type parameter.
/// </summary>
public interface IToggleGroup
{
    /// <summary>Number of toggle options.</summary>
    int OptionCount { get; }

    /// <summary>Gets the label text for an option by index.</summary>
    string GetOptionLabel(int index);

    /// <summary>Gets the index of the currently selected option, or -1.</summary>
    int SelectedIndex { get; }

    /// <summary>Selects the option at the given index.</summary>
    void SelectIndex(int index);

    /// <summary>Whether the control is disabled.</summary>
    bool IsControlDisabled { get; }

    /// <summary>
    /// Absolute bounds set by the painter for click coordinate mapping.
    /// </summary>
    Rect AbsoluteBounds { get; set; }
}

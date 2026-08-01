namespace Cascade.UI;

/// <summary>
/// Non-generic interface for RadioButton rendering and interaction.
/// Allows NodePainter and InputDispatcher to work without knowing T.
/// </summary>
internal interface IRadioButton
{
    /// <summary>Whether this radio button is currently selected.</summary>
    bool IsSelected { get; }

    /// <summary>Resolved label text. Empty when a rich node label is used.</summary>
    string LabelText { get; }

    /// <summary>Rich node label, or <see cref="Node.Empty"/> when a text label is used.</summary>
    Node NodeLabel { get; }

    /// <summary>Visual style of the radio button.</summary>
    RadioStyle Style { get; }

    /// <summary>Selects this radio button, updating the parent group's binding.</summary>
    void Select();
}

/// <summary>
/// Non-generic interface for RadioGroup, allowing layout, hit testing, and painting
/// to work without knowing the generic type parameter T.
/// </summary>
internal interface IRadioGroup
{
    /// <summary>The layout content node containing the RadioButton children.</summary>
    Node Content { get; }

    /// <summary>Whether the group is disabled.</summary>
    bool IsGroupDisabled { get; }
}

/// <summary>
/// Single selection from a set of mutually exclusive options. RadioButtons
/// must be used inside a RadioGroup. The group manages mutual exclusion
/// and keyboard navigation.
/// </summary>
/// <typeparam name="T">The type of the selectable value.</typeparam>
public sealed class RadioGroup<T> : Node, IRadioGroup where T : notnull
{
    /// <summary>
    /// Creates a radio group bound to a typed field.
    /// </summary>
    /// <param name="value">Two-way binding to the selected value.</param>
    /// <param name="content">
    /// Layout node containing <see cref="RadioButton{T}"/> children
    /// (typically a <see cref="Column"/> or <see cref="Row"/>).
    /// </param>
    public RadioGroup(
        Bindable<T> value,
        Node content)
    {
        Value = value;
        Content = content;
        WireRadioButtons(content);
    }

    /// <summary>Two-way binding to the selected value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>Layout node containing RadioButton children.</summary>
    public Node Content { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<T, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    // ── IRadioGroup implementation ───────────────────────────────────

    bool IRadioGroup.IsGroupDisabled => IsDisabled;

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

    /// <summary>
    /// Walks the content tree to find RadioButton children, wiring up
    /// their selection callbacks and initial IsSelected state.
    /// </summary>
    private void WireRadioButtons(Node node)
    {
        if (node is RadioButton<T> rb)
        {
            rb.IsSelected = EqualityComparer<T>.Default.Equals(rb.Value, Value.Value);
            rb.OnSelect = () => Value.OnChange(rb.Value);
            return;
        }

        // Walk into layout containers to find nested RadioButtons
        IReadOnlyList<Node>? children = node switch
        {
            Column col => col.Children,
            Row row    => row.Children,
            Stack s    => s.Children,
            Grid g     => g.Children,
            _          => null
        };

        if (children is not null)
        {
            foreach (var child in children)
            {
                WireRadioButtons(child);
            }
        }
    }
}

/// <summary>
/// An individual radio button option within a <see cref="RadioGroup{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the selectable value, matching the parent RadioGroup.</typeparam>
public sealed class RadioButton<T> : Node, IRadioButton where T : notnull
{
    /// <summary>
    /// Creates a radio button with a text label.
    /// </summary>
    /// <param name="value">The value this button represents.</param>
    /// <param name="label">Text label for the radio button.</param>
    /// <param name="style">Visual style of the radio button.</param>
    public RadioButton(
        T value,
        LocKey label,
        RadioStyle style = RadioStyle.Default)
    {
        Value = value;
        TextLabel = label;
        NodeLabel = Node.Empty;
        Style = style;
    }

    /// <summary>
    /// Creates a radio button with a rich node label (e.g., multi-line content or icons).
    /// </summary>
    /// <param name="value">The value this button represents.</param>
    /// <param name="label">Rich node content for the radio button label.</param>
    /// <param name="style">Visual style of the radio button.</param>
    public RadioButton(
        T value,
        Node label,
        RadioStyle style = RadioStyle.Default)
    {
        Value = value;
        TextLabel = default;
        NodeLabel = label;
        Style = style;
    }

    /// <summary>The value this button represents.</summary>
    public T Value { get; }

    /// <summary>Text label. Default when using a rich node label.</summary>
    public LocKey TextLabel { get; }

    /// <summary>Rich node label. <see cref="Node.Empty"/> when using a text label.</summary>
    public Node NodeLabel { get; }

    /// <summary>Visual style of the radio button.</summary>
    public RadioStyle Style { get; }

    // ── Internal state set by parent RadioGroup ──────────────────────

    /// <summary>Whether this button is currently selected. Set by RadioGroup during construction.</summary>
    internal bool IsSelected { get; set; }

    /// <summary>Callback to select this button. Set by RadioGroup during construction.</summary>
    internal Action? OnSelect { get; set; }

    // ── IRadioButton implementation ──────────────────────────────────

    bool IRadioButton.IsSelected => IsSelected;
    string IRadioButton.LabelText => TextLabel.Resolve();
    Node IRadioButton.NodeLabel => NodeLabel;
    RadioStyle IRadioButton.Style => Style;
    void IRadioButton.Select() => OnSelect?.Invoke();
}

/// <summary>
/// Extension methods for <see cref="RadioGroup{T}"/> providing fluent modifiers.
/// </summary>
public static class RadioGroupExtensions
{
    /// <summary>Adds a validation rule.</summary>
    public static RadioGroup<T> Validate<T>(this RadioGroup<T> group, Func<T, ValidationResult> rule)
        where T : notnull
    {
        group.ValidationRules.Add(rule);
        return group;
    }

    /// <summary>Sets when validation fires.</summary>
    public static RadioGroup<T> ValidateOn<T>(this RadioGroup<T> group, ValidationTrigger trigger)
        where T : notnull
    {
        group.ValidationTriggerMode = trigger;
        return group;
    }

    /// <summary>Disables all radio buttons in the group.</summary>
    public static RadioGroup<T> Disabled<T>(this RadioGroup<T> group, bool disabled = true)
        where T : notnull
    {
        group.IsDisabled = disabled;
        return group;
    }

    /// <summary>Makes all radio buttons in the group read-only.</summary>
    public static RadioGroup<T> ReadOnly<T>(this RadioGroup<T> group, bool readOnly = true)
        where T : notnull
    {
        group.IsReadOnly = readOnly;
        return group;
    }

    /// <summary>Sets the accessible label for the radio group.</summary>
    public static RadioGroup<T> AccessibleLabel<T>(this RadioGroup<T> group, LocKey label)
        where T : notnull
    {
        group.LayoutData.A11yLabel = label.Resolve();
        return group;
    }
}

/// <summary>
/// Visual style for a <see cref="RadioButton{T}"/>.
/// </summary>
public enum RadioStyle
{
    /// <summary>Standard radio button with dot indicator.</summary>
    Default,

    /// <summary>Selectable card with a border that becomes the primary color when selected.</summary>
    Card,

    /// <summary>Renders as a button-group (see <see cref="ToggleGroup{T}"/>).</summary>
    Button
}

namespace Cascade.UI;

/// <summary>
/// Non-generic interface for the rendering pipeline to interact with
/// <see cref="NumberInput{T}"/> without knowing the concrete numeric type.
/// </summary>
public interface INumberInput
{
    /// <summary>The current value formatted as a display string.</summary>
    string DisplayValue { get; }

    /// <summary>Increments the value by one step.</summary>
    void Increment();

    /// <summary>Decrements the value by one step.</summary>
    void Decrement();

    /// <summary>Whether the control is disabled.</summary>
    bool IsDisabled { get; }

    /// <summary>Whether the control is read-only.</summary>
    bool IsReadOnly { get; }

    /// <summary>Optional label text.</summary>
    LocKey Label { get; }

    /// <summary>Stepper button layout position.</summary>
    StepperPosition StepperPos { get; }

    /// <summary>Whether the value is at the minimum bound (disable decrement).</summary>
    bool IsAtMin { get; }

    /// <summary>Whether the value is at the maximum bound (disable increment).</summary>
    bool IsAtMax { get; }

    /// <summary>Absolute bounds for click coordinate mapping.</summary>
    Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Which stepper button is currently hovered: -1 = none, 0 = decrement, 1 = increment.
    /// Set by the input dispatcher during mouse-move events.
    /// </summary>
    int HoveredStepperButton { get; set; }

    /// <summary>
    /// Which stepper button is currently pressed: -1 = none, 0 = decrement, 1 = increment.
    /// Set by the input dispatcher on mouse-down, cleared on mouse-up.
    /// </summary>
    int PressedStepperButton { get; set; }
}

/// <summary>
/// Typed numeric entry control with up/down stepper buttons. Enforces
/// numeric input at the keyboard level. Generic over any numeric type
/// (<see cref="int"/>, <see cref="long"/>, <see cref="float"/>,
/// <see cref="double"/>, <see cref="decimal"/>).
/// </summary>
/// <typeparam name="T">The numeric type of the bound value.</typeparam>
public sealed class NumberInput<T> : Node, INumberInput where T : struct, IComparable<T>
{
    /// <summary>
    /// Creates a numeric input bound to a numeric field.
    /// </summary>
    /// <param name="value">Two-way binding to the numeric value.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="step">Increment/decrement step for stepper buttons and arrow keys.</param>
    /// <param name="format">
    /// .NET format string for display (e.g., "C2" for currency, "N0" for integer with separators).
    /// </param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    /// <param name="label">Optional label displayed above or beside the input.</param>
    public NumberInput(
        Bindable<T> value,
        T? min = null,
        T? max = null,
        T? step = null,
        string? format = null,
        LocKey placeholder = default,
        LocKey label = default)
    {
        Value = value;
        Min = min;
        Max = max;
        Step = step;
        Format = format;
        Placeholder = placeholder;
        Label = label;
    }

    /// <summary>Two-way binding to the numeric value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>Minimum allowed value.</summary>
    public T? Min { get; }

    /// <summary>Maximum allowed value.</summary>
    public T? Max { get; }

    /// <summary>Increment/decrement step.</summary>
    public T? Step { get; }

    /// <summary>.NET format string for display.</summary>
    public string? Format { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; }

    /// <summary>Optional label.</summary>
    public LocKey Label { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal List<Func<T, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal StepperPosition StepperPos { get; set; } = StepperPosition.Right;
    internal Node PrefixNode { get; set; } = Node.Empty;
    internal Node SuffixNode { get; set; } = Node.Empty;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }
    internal LocKey AccessibleLabelValue { get; set; }

    // ── INumberInput explicit interface members ───────────────────────

    StepperPosition INumberInput.StepperPos => StepperPos;
    bool INumberInput.IsDisabled => IsDisabled;
    bool INumberInput.IsReadOnly => IsReadOnly;
    Rect INumberInput.AbsoluteBounds { get; set; }
    int INumberInput.HoveredStepperButton { get; set; } = -1;
    int INumberInput.PressedStepperButton { get; set; } = -1;

    string INumberInput.DisplayValue
    {
        get
        {
            T val = Value.Value;
            if (Format != null)
            {
                return string.Format($"{{0:{Format}}}", val);
            }
            return val.ToString() ?? "";
        }
    }

    bool INumberInput.IsAtMin => Min.HasValue && Value.Value.CompareTo(Min.Value) <= 0;
    bool INumberInput.IsAtMax => Max.HasValue && Value.Value.CompareTo(Max.Value) >= 0;

    void INumberInput.Increment()
    {
        if (IsDisabled || IsReadOnly)
        {
            return;
        }
        T current = Value.Value;
        T step = Step ?? GetDefaultStep();
        T newVal = Add(current, step);
        Value.OnChange(Clamp(newVal));
    }

    void INumberInput.Decrement()
    {
        if (IsDisabled || IsReadOnly)
        {
            return;
        }
        T current = Value.Value;
        T step = Step ?? GetDefaultStep();
        T newVal = Subtract(current, step);
        Value.OnChange(Clamp(newVal));
    }

    private static T GetDefaultStep()
    {
        if (typeof(T) == typeof(int)) { return (T)(object)1; }
        if (typeof(T) == typeof(long)) { return (T)(object)1L; }
        if (typeof(T) == typeof(float)) { return (T)(object)1f; }
        if (typeof(T) == typeof(double)) { return (T)(object)1.0; }
        if (typeof(T) == typeof(decimal)) { return (T)(object)1m; }
        return default;
    }

    private static T Add(T a, T b)
    {
        if (typeof(T) == typeof(int)) { return (T)(object)((int)(object)a + (int)(object)b); }
        if (typeof(T) == typeof(long)) { return (T)(object)((long)(object)a + (long)(object)b); }
        if (typeof(T) == typeof(float)) { return (T)(object)((float)(object)a + (float)(object)b); }
        if (typeof(T) == typeof(double)) { return (T)(object)((double)(object)a + (double)(object)b); }
        if (typeof(T) == typeof(decimal)) { return (T)(object)((decimal)(object)a + (decimal)(object)b); }
        return a;
    }

    private static T Subtract(T a, T b)
    {
        if (typeof(T) == typeof(int)) { return (T)(object)((int)(object)a - (int)(object)b); }
        if (typeof(T) == typeof(long)) { return (T)(object)((long)(object)a - (long)(object)b); }
        if (typeof(T) == typeof(float)) { return (T)(object)((float)(object)a - (float)(object)b); }
        if (typeof(T) == typeof(double)) { return (T)(object)((double)(object)a - (double)(object)b); }
        if (typeof(T) == typeof(decimal)) { return (T)(object)((decimal)(object)a - (decimal)(object)b); }
        return a;
    }

    /// <summary>
    /// Clamps a value to the configured min/max bounds.
    /// </summary>
    internal T Clamp(T value)
    {
        if (Min.HasValue && value.CompareTo(Min.Value) < 0)
        {
            return Min.Value;
        }

        if (Max.HasValue && value.CompareTo(Max.Value) > 0)
        {
            return Max.Value;
        }

        return value;
    }

    /// <summary>
    /// Runs all registered validation rules against the given value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation(T value)
    {
        foreach (var rule in ValidationRules)
        {
            var result = rule(value);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Extension methods for <see cref="NumberInput{T}"/> providing fluent modifiers.
/// </summary>
public static class NumberInputExtensions
{
    /// <summary>Adds a validation rule.</summary>
    public static NumberInput<T> Validate<T>(this NumberInput<T> input, Func<T, ValidationResult> rule)
        where T : struct, IComparable<T>
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static NumberInput<T> ValidateOn<T>(this NumberInput<T> input, ValidationTrigger trigger)
        where T : struct, IComparable<T>
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Sets the stepper button position.</summary>
    public static NumberInput<T> StepperButtons<T>(this NumberInput<T> input, StepperPosition position)
        where T : struct, IComparable<T>
    {
        input.StepperPos = position;
        return input;
    }

    /// <summary>Adds a prefix node rendered inside the input border.</summary>
    public static NumberInput<T> Prefix<T>(this NumberInput<T> input, Node prefix)
        where T : struct, IComparable<T>
    {
        input.PrefixNode = prefix;
        return input;
    }

    /// <summary>Adds a suffix node rendered inside the input border.</summary>
    public static NumberInput<T> Suffix<T>(this NumberInput<T> input, Node suffix)
        where T : struct, IComparable<T>
    {
        input.SuffixNode = suffix;
        return input;
    }

    /// <summary>Disables the input.</summary>
    public static NumberInput<T> Disabled<T>(this NumberInput<T> input, bool disabled = true)
        where T : struct, IComparable<T>
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only.</summary>
    public static NumberInput<T> ReadOnly<T>(this NumberInput<T> input, bool readOnly = true)
        where T : struct, IComparable<T>
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static NumberInput<T> AccessibleLabel<T>(this NumberInput<T> input, LocKey label)
        where T : struct, IComparable<T>
    {
        input.AccessibleLabelValue = label;
        return input;
    }
}

/// <summary>
/// Position of the stepper (increment/decrement) buttons on a <see cref="NumberInput{T}"/>.
/// </summary>
public enum StepperPosition
{
    /// <summary>Up/down buttons on the right side of the input (default).</summary>
    Right,

    /// <summary>No stepper buttons — keyboard and scroll only.</summary>
    None,

    /// <summary>Minus button on the left, plus button on the right.</summary>
    Split
}

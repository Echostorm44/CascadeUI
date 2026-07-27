namespace Cascade.UI;

/// <summary>
/// A row of mutually exclusive segments rendered as a connected pill bar.
/// Selecting a segment highlights it and deselects the others.
/// Similar to iOS UISegmentedControl or Material ButtonToggleGroup.
/// </summary>
/// <typeparam name="T">The type of the selectable value.</typeparam>
public sealed class SegmentedControl<T> : Node, ISegmentedControl where T : notnull
{
    /// <summary>
    /// Creates a segmented control.
    /// </summary>
    /// <param name="value">Two-way binding to the selected value.</param>
    /// <param name="options">The available segments.</param>
    public SegmentedControl(
        Bindable<T> value,
        IReadOnlyList<SegmentOption<T>> options)
    {
        Value = value;
        Options = options;
    }

    /// <summary>Two-way binding to the selected value.</summary>
    public Bindable<T> Value { get; }

    /// <summary>The available segment options.</summary>
    public IReadOnlyList<SegmentOption<T>> Options { get; }

    // ── Internal modifier state ───────────────────────────────────────

    internal bool IsDisabled { get; set; }

    /// <summary>Disables all segments.</summary>
    public SegmentedControl<T> Disabled(bool disabled = true)
    {
        IsDisabled = disabled;
        return this;
    }

    // ── ISegmentedControl implementation ──────────────────────────────

    int ISegmentedControl.SegmentCount => Options.Count;

    string ISegmentedControl.GetSegmentLabel(int index) => Options[index].Label.Resolve();

    int ISegmentedControl.SelectedIndex
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

    void ISegmentedControl.SelectIndex(int index)
    {
        if (index >= 0 && index < Options.Count)
        {
            Value.OnChange(Options[index].Value);
        }
    }

    bool ISegmentedControl.IsControlDisabled => IsDisabled;

    Rect ISegmentedControl.AbsoluteBounds { get; set; }
}

/// <summary>
/// A single segment in a <see cref="SegmentedControl{T}"/>.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class SegmentOption<T>
{
    /// <summary>Creates a segment option with a text label.</summary>
    /// <param name="value">The value this segment represents.</param>
    /// <param name="label">The text label.</param>
    public SegmentOption(T value, LocKey label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>The value this segment represents.</summary>
    public T Value { get; }

    /// <summary>Text label for the segment.</summary>
    public LocKey Label { get; }
}

/// <summary>
/// Non-generic interface for SegmentedControl to enable layout/paint/hit-testing
/// without knowing the type parameter.
/// </summary>
public interface ISegmentedControl
{
    /// <summary>Number of segments.</summary>
    int SegmentCount { get; }

    /// <summary>Gets the label text for a segment by index.</summary>
    string GetSegmentLabel(int index);

    /// <summary>Gets the index of the currently selected segment, or -1.</summary>
    int SelectedIndex { get; }

    /// <summary>Selects the segment at the given index.</summary>
    void SelectIndex(int index);

    /// <summary>Whether the control is disabled.</summary>
    bool IsControlDisabled { get; }

    /// <summary>
    /// Absolute bounds set by the painter for click coordinate mapping.
    /// </summary>
    Rect AbsoluteBounds { get; set; }
}

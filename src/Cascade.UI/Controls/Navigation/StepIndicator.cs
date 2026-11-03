namespace Cascade.UI;

/// <summary>
/// A single step definition in a <see cref="StepIndicator"/>.
/// </summary>
#pragma warning disable CA1716 // Step is the spec-defined API name for this type
public record Step(string Label);
#pragma warning restore CA1716

/// <summary>
/// Visual style for the <see cref="StepIndicator"/>.
/// </summary>
public enum StepStyle
{
    /// <summary>Circles with step numbers connected by lines.</summary>
    Numbered,

    /// <summary>Small dots connected by lines.</summary>
    Dotted,

    /// <summary>Segmented progress bar.</summary>
    Bar,

    /// <summary>Vertical checklist with checkmarks for completed steps.</summary>
    Checklist
}

/// <summary>
/// A horizontal (or vertical) progress indicator for multi-step wizard flows.
/// Shows the current step, completed steps, and remaining steps.
/// </summary>
public sealed class StepIndicator : Node
{
    public StepIndicator(
        Bindable<int> currentStep,
        IReadOnlyList<Step> steps)
    {
        CurrentStep = currentStep;
        Steps = steps;
    }

    /// <summary>Two-way binding to the current zero-based step index.</summary>
    public Bindable<int> CurrentStep { get; }

    /// <summary>The step definitions in order.</summary>
    public IReadOnlyList<Step> Steps { get; }

    /// <summary>The visual style. Default is <see cref="StepStyle.Numbered"/>.</summary>
    internal StepStyle StyleSetting { get; set; } = StepStyle.Numbered;

    /// <summary>Predicate controlling which steps are clickable. Null means no steps are clickable.</summary>
    internal Func<int, bool>? ClickablePredicate { get; set; }

    /// <summary>Handler invoked when a clickable step is clicked.</summary>
    internal Action<int>? StepClickHandler { get; set; }

    /// <summary>
    /// Absolute bounds set by the painter for click coordinate mapping.
    /// </summary>
    internal Rect AbsoluteBounds { get; set; }

    // ── Geometry shared by layout, paint and click ────────────────────

    internal const float NumberedMarker = 28f;
    internal const float DotMarker = 12f;
    internal const float BarThickness = 6f;
    internal const float ChecklistMarker = 20f;
    internal const float ChecklistRow = 32f;
    internal const float ChecklistLabelGap = 10f;
    internal const float LabelGap = 6f;
    internal const float BarGap = 4f;

    /// <summary>Where one step is drawn: its marker (circle, dot or bar segment) and the area a click selects it in.</summary>
    internal readonly record struct StepSlot(Rect Marker, Rect Hit)
    {
        public float CenterX => Marker.X + Marker.Width / 2f;
        public float CenterY => Marker.Y + Marker.Height / 2f;
    }

    /// <summary>
    /// Lays the steps out for <paramref name="style"/> in a box <paramref name="width"/> wide, in the
    /// control's own coordinates. Numbered and Dotted spread their markers across the width with a line
    /// between; Bar splits the width into segments; Checklist stacks one row per step.
    /// </summary>
    internal static StepSlot[] Arrange(StepStyle style, int count, float width, float height)
    {
        var slots = new StepSlot[Math.Max(0, count)];
        if (count == 0)
        {
            return slots;
        }

        switch (style)
        {
            case StepStyle.Checklist:
                for (int i = 0; i < count; i++)
                {
                    float rowY = i * ChecklistRow;
                    var marker = new Rect(0f, rowY + (ChecklistRow - ChecklistMarker) / 2f, ChecklistMarker, ChecklistMarker);
                    slots[i] = new StepSlot(marker, new Rect(0f, rowY, width, ChecklistRow));
                }
                break;

            case StepStyle.Bar:
            {
                float segment = Math.Max(0f, (width - BarGap * (count - 1)) / count);
                for (int i = 0; i < count; i++)
                {
                    float x = i * (segment + BarGap);
                    slots[i] = new StepSlot(new Rect(x, 0f, segment, BarThickness), new Rect(x, 0f, segment + BarGap, height));
                }
                break;
            }

            default:
            {
                float size = style == StepStyle.Dotted ? DotMarker : NumberedMarker;
                float gap = count > 1 ? (width - count * size) / (count - 1) : 0f;
                float pitch = size + gap;
                for (int i = 0; i < count; i++)
                {
                    float x = i * pitch;
                    var marker = new Rect(x, 0f, size, size);
                    float hitHalf = Math.Max(size, pitch) / 2f;
                    float cx = x + size / 2f;
                    slots[i] = new StepSlot(marker, new Rect(cx - hitHalf, 0f, hitHalf * 2f, height));
                }
                break;
            }
        }

        return slots;
    }

    /// <summary>How tall the markers are for a horizontal style: labels sit <see cref="LabelGap"/> below.</summary>
    internal static float MarkerExtent(StepStyle style) => style switch
    {
        StepStyle.Dotted => DotMarker,
        StepStyle.Bar => BarThickness,
        _ => NumberedMarker,
    };

    /// <summary>The step a click at (<paramref name="x"/>, <paramref name="y"/>) in the control's coordinates selects, or -1.</summary>
    internal static int StepAt(StepStyle style, int count, float width, float height, float x, float y)
    {
        var slots = Arrange(style, count, width, height);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Hit.Contains(new Point(x, y)))
            {
                return i;
            }
        }
        return -1;
    }
}

/// <summary>
/// Fluent extension methods for <see cref="StepIndicator"/>.
/// </summary>
public static class StepIndicatorExtensions
{
    /// <summary>Sets the visual style of the step indicator.</summary>
    public static StepIndicator Style(this StepIndicator indicator, StepStyle style)
    {
        indicator.StyleSetting = style;
        return indicator;
    }

    /// <summary>
    /// Controls which steps are clickable. The predicate receives each step's
    /// zero-based index and returns whether clicking is allowed.
    /// </summary>
    public static StepIndicator Clickable(this StepIndicator indicator, Func<int, bool> predicate)
    {
        indicator.ClickablePredicate = predicate;
        return indicator;
    }

    /// <summary>Registers a callback invoked when a clickable step is clicked.</summary>
    public static StepIndicator OnStepClick(this StepIndicator indicator, Action<int> handler)
    {
        indicator.StepClickHandler = handler;
        return indicator;
    }
}

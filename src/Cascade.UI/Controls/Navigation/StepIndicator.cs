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

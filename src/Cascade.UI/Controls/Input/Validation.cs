using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// A control with <c>.Validate(rule)</c> rules: what the validation engine needs to run them at
/// the right moment and show the result.
/// </summary>
internal interface IValidatable
{
    /// <summary>When the rules run (<c>.ValidateOn</c>).</summary>
    ValidationTrigger ValidationTriggerMode { get; }

    /// <summary>Whether any rule was added.</summary>
    bool HasValidationRules { get; }

    /// <summary>Runs the rules against the current value: the first failure, or Ok.</summary>
    ValidationResult RunValidation();

    /// <summary>The value the rules look at, compared between frames to notice a change.</summary>
    object? ValidationValue { get; }

    /// <summary>The control's debounce (<c>.Debounce</c>), for <see cref="ValidationTrigger.Debounced"/>.</summary>
    TimeSpan? ValidationDebounce { get; }

    /// <summary>The control's validation progress, carried across re-renders.</summary>
    ValidationState? ValidationState { get; set; }
}

/// <summary>A control's validation progress: the result shown and what has changed since.</summary>
internal sealed class ValidationState
{
    /// <summary>The result on show: a message below the control unless it is Ok.</summary>
    internal ValidationResult? Result { get; set; }

    internal object? LastValue { get; set; }
    internal bool HasLastValue { get; set; }

    /// <summary>The value changed since the rules last ran.</summary>
    internal bool Dirty { get; set; }

    internal long LastChangeTimestamp { get; set; }
    internal bool WasFocused { get; set; }
}

/// <summary>
/// Runs controls' validation rules when their <see cref="ValidationTrigger"/> says to, and keeps the
/// result for layout and paint, which show its message below the control.
/// </summary>
/// <remarks>
/// <para>Layout calls <see cref="Update"/> for each control with rules, every frame, before measuring it:</para>
/// <list type="bullet">
/// <item><see cref="ValidationTrigger.Immediate"/> runs on every change.</item>
/// <item><see cref="ValidationTrigger.Debounced"/> runs once the value has been still for the control's
/// <c>.Debounce</c> delay (300 ms without one).</item>
/// <item><see cref="ValidationTrigger.Blur"/> runs when focus leaves a control whose value changed.</item>
/// <item><see cref="ValidationTrigger.Submit"/> waits for <see cref="FormScope.ValidateAll"/>, and
/// <see cref="ValidationTrigger.Manual"/> for <see cref="ValidationExtensions.Validate{T}(NodeRef{T})"/>.</item>
/// </list>
/// <para>Whatever the trigger, a control already showing a message re-checks on every change, so the
/// message goes as soon as the value is fixed.</para>
/// </remarks>
internal static class Validation
{
    /// <summary>Space between the control and its message.</summary>
    internal const float MessageGap = 4f;

    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>The message's font size: the theme's caption.</summary>
    internal static float MessageFontSize => LayoutSolver.SeparatorLabelFontSize > 0 ? LayoutSolver.SeparatorLabelFontSize : 12f;

    /// <summary>The height a shown message adds below the control.</summary>
    internal static float MessageHeight => MessageGap + MathF.Ceiling(MessageFontSize * 1.3f);

    internal static ValidationState StateOf(IValidatable control) => control.ValidationState ??= new ValidationState();

    /// <summary>Runs the rules if the trigger says it is time. Call once a frame, before measuring the control.</summary>
    internal static void Update(Node node, IValidatable control)
    {
        if (!control.HasValidationRules)
        {
            return;
        }

        var state = StateOf(control);
        object? value = control.ValidationValue;
        bool changed = state.HasLastValue && !Equals(value, state.LastValue);
        if (!state.HasLastValue || changed)
        {
            state.LastValue = value;
            state.HasLastValue = true;
        }
        if (changed)
        {
            state.Dirty = true;
            state.LastChangeTimestamp = Stopwatch.GetTimestamp();
        }

        bool focused = ReferenceEquals(FocusManager.FocusedElement, node);
        bool showingProblem = state.Result is { IsValid: false } || state.Result?.Status == ValidationStatus.Warning;

        switch (control.ValidationTriggerMode)
        {
            case ValidationTrigger.Immediate when changed:
                Run(control, state);
                break;

            case ValidationTrigger.Debounced when state.Dirty:
                var delay = control.ValidationDebounce ?? DefaultDebounce;
                if (Stopwatch.GetElapsedTime(state.LastChangeTimestamp) >= delay)
                {
                    Run(control, state);
                }
                else
                {
                    // Keep frames coming until the delay has passed.
                    ControlStateAnimator.SignalActiveTransition();
                }
                break;

            case ValidationTrigger.Blur when state.Dirty && state.WasFocused && !focused:
                Run(control, state);
                break;

            default:
                // Reward early: a field showing a problem re-checks as it is edited.
                if (changed && showingProblem)
                {
                    Run(control, state);
                }
                break;
        }

        state.WasFocused = focused;
    }

    /// <summary>Runs the rules now and shows the result.</summary>
    internal static ValidationResult Run(IValidatable control, ValidationState? state = null)
    {
        state ??= StateOf(control);
        var result = control.RunValidation();
        state.Result = result;
        state.Dirty = false;
        state.LastValue = control.ValidationValue;
        state.HasLastValue = true;
        SharedScheduler.Instance.RequestFrame();
        return result;
    }

    /// <summary>The result whose message is on show under the control, or null.</summary>
    internal static ValidationResult? Shown(Node node) =>
        node is IValidatable { ValidationState.Result: { Status: not ValidationStatus.Valid, Message.Length: > 0 } result }
            ? result
            : null;

    /// <summary>Keeps a control's validation progress when a re-render replaces it.</summary>
    internal static void TransferState(Node from, Node to)
    {
        if (from is IValidatable { ValidationState: { } state } && to is IValidatable target)
        {
            target.ValidationState = state;
        }
    }

    /// <summary>The controls with rules under <paramref name="root"/>, in tree order.</summary>
    internal static List<IValidatable> CollectFields(Node root)
    {
        var fields = new List<IValidatable>();
        void Walk(Node node)
        {
            if (node is IValidatable { HasValidationRules: true } field)
            {
                fields.Add(field);
            }
            foreach (var child in NodeDiffer.GetChildren(node))
            {
                Walk(child);
            }
        }
        Walk(root);
        return fields;
    }
}

/// <summary>Validating a control on demand.</summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Runs the control's rules now and shows the result under it, whatever its
    /// <see cref="ValidationTrigger"/> (the way to validate a <see cref="ValidationTrigger.Manual"/>
    /// field). Returns Ok when the control is not mounted or has no rules.
    /// </summary>
    public static ValidationResult Validate<T>(this NodeRef<T> control) where T : Node
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Node is IValidatable { HasValidationRules: true } field ? Validation.Run(field) : ValidationResult.Ok;
    }
}

namespace Cascade.UI;

/// <summary>
/// Defines how a control property transitions between states (hover, press,
/// focus, disabled). Used by the theme system to provide automatic Level 1
/// animations.
/// </summary>
public sealed class Transition
{
    /// <summary>No transition — instant state change.</summary>
    public static Transition None { get; } = new(AnimationModel.None);

    /// <summary>Creates a transition with the specified animation model.</summary>
    public Transition(AnimationModel model)
    {
        Model = model;
    }

    /// <summary>The animation model used for this transition.</summary>
    public AnimationModel Model { get; }

    /// <summary>
    /// Creates a <see cref="TransitionEngine"/> wired to the given scheduler
    /// and applies this transition's model to the specified property.
    /// </summary>
    internal void Apply(
        TransitionEngine engine,
        string propertyName,
        float fromValue,
        float toValue,
        Action<float> onProgress,
        Action? onComplete = null)
    {
        engine.BeginTransition(propertyName, fromValue, toValue, Model, onProgress, onComplete);
    }
}

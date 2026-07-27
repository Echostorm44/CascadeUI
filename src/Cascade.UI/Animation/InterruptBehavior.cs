namespace Cascade.UI;

/// <summary>
/// Controls how an in-flight animation reacts when a new target is set.
/// </summary>
public enum InterruptBehavior
{
    /// <summary>
    /// Default. Current position and velocity are preserved; the new animation
    /// blends naturally from the current physical state.
    /// </summary>
    Blend,

    /// <summary>
    /// Snap immediately to the new target with no transition.
    /// </summary>
    Cut,

    /// <summary>
    /// Finish the current animation to completion, then animate to the new target.
    /// </summary>
    Complete,
}

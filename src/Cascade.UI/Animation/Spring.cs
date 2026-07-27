namespace Cascade.UI;

// Spring presets are defined as AnimationModel.Spring.* in AnimationModel.cs.
// This file exists to match the WP-104 file list and provides additional
// spring-related utilities.

/// <summary>
/// Utility methods for spring-based animation configuration.
/// Named presets are available via <see cref="AnimationModel.Spring"/>.
/// </summary>
public static class SpringConfig
{
    /// <summary>
    /// Computes the approximate settling time for a spring with the given
    /// stiffness and damping (within 1% of target).
    /// </summary>
    public static Duration EstimateSettlingTime(float stiffness, float damping)
    {
        float seconds = SpringSolver.EstimateSettlingTime(stiffness, damping);
        return Duration.Ms((int)(seconds * 1000f));
    }
}

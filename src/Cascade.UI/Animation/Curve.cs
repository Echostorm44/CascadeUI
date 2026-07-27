namespace Cascade.UI;

// Curve presets are defined as AnimationModel.Ease/EaseIn/EaseOut/EaseInOut/Linear/Cubic
// in AnimationModel.cs. This file exists to match the WP-104 file list and provides
// additional curve-related utilities.

/// <summary>
/// Utility methods for curve-based animation configuration.
/// Named presets are available via <see cref="AnimationModel"/> factory methods.
/// </summary>
public static class CurveConfig
{
    /// <summary>
    /// Evaluates a cubic Bézier curve at the given time (0.0–1.0).
    /// Returns the eased progress value.
    /// </summary>
    public static float Evaluate(float t, float x1, float y1, float x2, float y2)
    {
        return CurveSolver.Evaluate(t, x1, y1, x2, y2);
    }
}

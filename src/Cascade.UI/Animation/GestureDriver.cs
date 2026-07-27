namespace Cascade.UI;

/// <summary>
/// Factory methods for gesture-driven animation drivers. Progress is tied
/// to user gestures such as drag position.
/// </summary>
public static class GestureDriver
{
    /// <summary>
    /// Creates a driver where progress maps to drag position along an axis.
    /// </summary>
    /// <param name="axis">The drag axis.</param>
    /// <param name="startPosition">Position where progress = 0.</param>
    /// <param name="endPosition">Position where progress = 1.</param>
    public static AnimationDriver Drag(DragAxis axis, float startPosition, float endPosition)
    {
        return new DragDriver(axis, startPosition, endPosition);
    }

    internal sealed class DragDriver : AnimationDriver
    {
        private readonly float start;
        private readonly float end;
        private float currentProgress;

        internal DragDriver(DragAxis axis, float startPosition, float endPosition)
        {
            Axis = axis;
            start = startPosition;
            end = endPosition;
        }

        public override float Progress => currentProgress;

        internal DragAxis Axis { get; }

        internal void UpdatePosition(float position)
        {
            float range = end - start;
            float p = MathF.Abs(range) > 1e-6f
                ? Math.Clamp((position - start) / range, 0f, 1f)
                : 0f;

            if (MathF.Abs(p - currentProgress) > 1e-6f)
            {
                currentProgress = p;
                NotifyProgressChanged(currentProgress);
            }
        }
    }
}

/// <summary>
/// The axis along which a drag gesture is tracked.
/// </summary>
public enum DragAxis
{
    /// <summary>Horizontal drag.</summary>
    X,

    /// <summary>Vertical drag.</summary>
    Y,
}

namespace Cascade.UI;

/// <summary>
/// Factory methods for scroll-driven animation drivers. Progress is tied
/// to the scroll position of a ScrollView.
/// </summary>
public static class ScrollDriver
{
    /// <summary>
    /// Creates a driver where progress maps to scroll position within
    /// the given offset range.
    /// </summary>
    /// <param name="startOffset">Scroll position where progress = 0.</param>
    /// <param name="endOffset">Scroll position where progress = 1.</param>
    public static AnimationDriver FromOffsets(float startOffset, float endOffset)
    {
        return new ScrollOffsetDriver(startOffset, endOffset);
    }

    internal sealed class ScrollOffsetDriver : AnimationDriver
    {
        private readonly float start;
        private readonly float end;
        private float currentProgress;

        internal ScrollOffsetDriver(float startOffset, float endOffset)
        {
            start = startOffset;
            end = endOffset;
        }

        public override float Progress => currentProgress;

        internal void UpdateOffset(float offset)
        {
            float range = end - start;
            float p = MathF.Abs(range) > 1e-6f
                ? Math.Clamp((offset - start) / range, 0f, 1f)
                : 0f;

            if (MathF.Abs(p - currentProgress) > 1e-6f)
            {
                currentProgress = p;
                NotifyProgressChanged(currentProgress);
            }
        }
    }
}

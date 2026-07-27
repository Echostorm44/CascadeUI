namespace Cascade.UI;

/// <summary>
/// Factory methods for value-driven animation drivers. Progress is derived
/// from a reactive value, mapped through input and output ranges.
/// </summary>
public static class ValueDriver
{
    /// <summary>
    /// Creates a driver whose progress is derived from a reactive value function,
    /// mapped from an input range to an output range (0.0–1.0).
    /// </summary>
    /// <param name="getValue">Function returning the current input value.</param>
    /// <param name="inputRange">The (min, max) input range.</param>
    /// <param name="outputRange">The (min, max) output progress range.</param>
    public static AnimationDriver From(
        Func<float> getValue,
        (float Min, float Max) inputRange,
        (float Min, float Max) outputRange)
    {
        return new MappedValueDriver(getValue, inputRange, outputRange);
    }

    internal sealed class MappedValueDriver : AnimationDriver
    {
        private readonly Func<float> getValue;
        private readonly float inputMin;
        private readonly float inputMax;
        private readonly float outputMin;
        private readonly float outputMax;
        private float currentProgress;

        internal MappedValueDriver(
            Func<float> valueFunc,
            (float Min, float Max) inputRange,
            (float Min, float Max) outputRange)
        {
            getValue = valueFunc;
            inputMin = inputRange.Min;
            inputMax = inputRange.Max;
            outputMin = outputRange.Min;
            outputMax = outputRange.Max;

            EvaluateProgress();

            SharedScheduler.Instance.Register(
                _ => EvaluateProgress(),
                () => false);
        }

        public override float Progress => currentProgress;

        private void EvaluateProgress()
        {
            float value = getValue();
            float inputRange = inputMax - inputMin;
            float normalized = MathF.Abs(inputRange) > 1e-6f
                ? Math.Clamp((value - inputMin) / inputRange, 0f, 1f)
                : 0f;

            float mapped = outputMin + normalized * (outputMax - outputMin);
            float p = Math.Clamp(mapped, 0f, 1f);

            if (MathF.Abs(p - currentProgress) > 1e-6f)
            {
                currentProgress = p;
                NotifyProgressChanged(currentProgress);
            }
        }
    }
}

namespace Cascade.UI;

/// <summary>
/// An animation driver with explicit, programmatic progress control.
/// Set <see cref="Progress"/> directly to drive bound animated values.
/// </summary>
public sealed class ManualDriver : AnimationDriver
{
    private float progress;

    /// <summary>
    /// Gets the current progress (0.0–1.0).
    /// </summary>
    public override float Progress => progress;

    /// <summary>
    /// Sets the progress (0.0–1.0), notifying all bound animated values.
    /// </summary>
    public void SetProgress(float value)
    {
        progress = Math.Clamp(value, 0f, 1f);
        NotifyProgressChanged(progress);
    }
}

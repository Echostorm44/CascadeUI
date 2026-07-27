namespace Cascade.UI;

/// <summary>
/// Controls how a keyframe animation repeats.
/// </summary>
public enum LoopMode
{
    /// <summary>Play once and stop.</summary>
    None,

    /// <summary>Loop from the beginning each time.</summary>
    Restart,

    /// <summary>Alternate between forward and backward playback.</summary>
    Reverse,
}

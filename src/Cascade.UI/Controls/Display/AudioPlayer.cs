namespace Cascade.UI;

/// <summary>
/// Audio playback with waveform visualization, metadata display, and playback
/// controls. Backed by FFmpeg — supports MP3, AAC, FLAC, OGG, WAV, AIFF,
/// Opus, and any other FFmpeg audio format.
/// </summary>
public sealed class AudioPlayer : Node
{
    /// <summary>Creates an audio player from a local file path.</summary>
    /// <param name="path">Local file path to the audio file.</param>
    public AudioPlayer(string path)
    {
        Path = path;
        BindableSource = null;
    }

    /// <summary>Creates an audio player bound to a changing source.</summary>
    /// <param name="source">Bindable path.</param>
    public AudioPlayer(Bindable<string> source)
    {
        Path = null;
        BindableSource = source;
    }

    /// <summary>Local file path, or null.</summary>
    public string? Path { get; }

    /// <summary>Bindable source, or null.</summary>
    public Bindable<string>? BindableSource { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal bool AutoPlayEnabled { get; set; }
    internal bool LoopEnabled { get; set; }
    internal AudioControls ControlsMode { get; set; } = AudioControls.Full;
    internal IReadOnlyList<float> PlaybackRateOptions { get; set; } = Array.Empty<float>();
    internal TimeSpan SkipAmountValue { get; set; }
    internal bool WaveformVisible { get; set; }
    internal int WaveformBars { get; set; } = 200;
    internal ColorValue? WaveformPlayedColor { get; set; }
    internal ColorValue? WaveformUnplayedColor { get; set; }
    internal WaveformStyle WaveformDisplayStyle { get; set; } = WaveformStyle.Bars;
    internal bool MetadataVisible { get; set; }
    internal MetadataLayout MetadataDisplayLayout { get; set; } = MetadataLayout.Compact;
    internal Action? OnPlayHandler { get; set; }
    internal Action? OnPauseHandler { get; set; }
    internal Action? OnEndedHandler { get; set; }
    internal Action<string>? OnErrorHandler { get; set; }

    // ── Playback ──────────────────────────────────────────────────────

    /// <summary>Enables or disables auto-play.</summary>
    public AudioPlayer AutoPlay(bool enabled)
    {
        AutoPlayEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables looping.</summary>
    public AudioPlayer Loop(bool enabled)
    {
        LoopEnabled = enabled;
        return this;
    }

    // ── Controls ──────────────────────────────────────────────────────

    /// <summary>Sets the control style.</summary>
    public AudioPlayer Controls(AudioControls controls)
    {
        ControlsMode = controls;
        return this;
    }

    /// <summary>Sets the available playback rate options.</summary>
    public AudioPlayer PlaybackRates(IReadOnlyList<float> rates)
    {
        PlaybackRateOptions = rates;
        return this;
    }

    /// <summary>Sets the skip amount for forward/back skip buttons.</summary>
    public AudioPlayer SkipAmount(TimeSpan amount)
    {
        SkipAmountValue = amount;
        return this;
    }

    // ── Waveform ──────────────────────────────────────────────────────

    /// <summary>Shows or hides the waveform visualization.</summary>
    public AudioPlayer ShowWaveform(bool enabled)
    {
        WaveformVisible = enabled;
        return this;
    }

    /// <summary>Configures the waveform visualization.</summary>
    public AudioPlayer Waveform(
        int bars = 200,
        ColorValue? played = null,
        ColorValue? unplayed = null,
        WaveformStyle style = WaveformStyle.Bars)
    {
        WaveformBars = bars;
        WaveformPlayedColor = played;
        WaveformUnplayedColor = unplayed;
        WaveformDisplayStyle = style;
        return this;
    }

    // ── Metadata ──────────────────────────────────────────────────────

    /// <summary>Shows or hides metadata (artist, title, album art from tags).</summary>
    public AudioPlayer ShowMetadata(bool enabled, MetadataLayout layout = MetadataLayout.Compact)
    {
        MetadataVisible = enabled;
        MetadataDisplayLayout = layout;
        return this;
    }

    // ── Events ────────────────────────────────────────────────────────

    /// <summary>Callback when playback starts.</summary>
    public AudioPlayer OnPlay(Action handler)
    {
        OnPlayHandler = handler;
        return this;
    }

    /// <summary>Callback when playback pauses.</summary>
    public AudioPlayer OnPause(Action handler)
    {
        OnPauseHandler = handler;
        return this;
    }

    /// <summary>Callback when playback reaches the end.</summary>
    public AudioPlayer OnEnded(Action handler)
    {
        OnEndedHandler = handler;
        return this;
    }

    /// <summary>Callback on playback error.</summary>
    public AudioPlayer OnError(Action<string> handler)
    {
        OnErrorHandler = handler;
        return this;
    }
}

/// <summary>
/// Audio control overlay style.
/// </summary>
public enum AudioControls
{
    /// <summary>Play/pause, seek bar, time, volume, playback rate, skip buttons.</summary>
    Full,

    /// <summary>Play/pause, seek bar, time only.</summary>
    Minimal,

    /// <summary>No controls — use programmatic API.</summary>
    None
}

/// <summary>
/// Waveform visualization style for <see cref="AudioPlayer"/>.
/// </summary>
public enum WaveformStyle
{
    /// <summary>Vertical amplitude bars.</summary>
    Bars,

    /// <summary>Reflected waveform above and below the center line.</summary>
    Mirror,

    /// <summary>Filled area waveform.</summary>
    Fill
}

/// <summary>
/// Layout for audio metadata display.
/// </summary>
public enum MetadataLayout
{
    /// <summary>Album art with title and artist inline.</summary>
    Compact,

    /// <summary>Large album art above, text below.</summary>
    Full,

    /// <summary>No metadata display.</summary>
    None
}

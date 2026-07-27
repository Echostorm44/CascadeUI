namespace Cascade.UI;

/// <summary>
/// Full-featured video playback backed by FFmpeg. Supports virtually every
/// container and codec. Hardware decoding is used when available.
/// </summary>
public sealed class VideoPlayer : Node
{
    /// <summary>Creates a video player from a local file path.</summary>
    /// <param name="path">Local file path to the video.</param>
    public VideoPlayer(string path)
    {
        Path = path;
        Url = null;
        BindableSource = null;
    }

    /// <summary>Creates a video player from a URL (streaming).</summary>
    /// <param name="url">The video URL.</param>
    /// <param name="streaming">Disambiguator (unused).</param>
    public VideoPlayer(string url, bool streaming)
    {
        Path = null;
        Url = url;
        BindableSource = null;
    }

    /// <summary>Creates a video player bound to a changing source.</summary>
    /// <param name="source">Bindable path or URL.</param>
    public VideoPlayer(Bindable<string> source)
    {
        Path = null;
        Url = null;
        BindableSource = source;
    }

    /// <summary>Local file path, or null.</summary>
    public string? Path { get; }

    /// <summary>Video URL, or null.</summary>
    public string? Url { get; }

    /// <summary>Bindable source, or null.</summary>
    public Bindable<string>? BindableSource { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal VideoControls ControlsMode { get; set; } = VideoControls.Full;
    internal bool AutoPlayEnabled { get; set; }
    internal bool LoopEnabled { get; set; }
    internal bool MutedEnabled { get; set; }
    internal TimeSpan StartPosition { get; set; }
    internal bool SeekThumbnailsEnabled { get; set; }
    internal TimeSpan? SeekThumbnailInterval { get; set; }
    internal bool SubtitlesEmbedded { get; set; } = true;
    internal IReadOnlyList<SubtitleTrack> ExternalSubtitles { get; set; } = Array.Empty<SubtitleTrack>();
    internal string? DefaultSubtitleName { get; set; }
    internal bool AudioTracksEnabled { get; set; }
    internal string? DefaultAudioTrackName { get; set; }
    internal IReadOnlyList<VideoChapter> ChapterList { get; set; } = Array.Empty<VideoChapter>();
    internal bool PictureInPictureEnabled { get; set; }
    internal Action? OnPlayHandler { get; set; }
    internal Action? OnPauseHandler { get; set; }
    internal Action? OnEndedHandler { get; set; }
    internal Action<TimeSpan>? OnTimeUpdateHandler { get; set; }
    internal Action<VideoError>? OnErrorHandler { get; set; }
    internal Action<VideoChapter>? OnChapterChangeHandler { get; set; }

    // ── Controls ──────────────────────────────────────────────────────

    /// <summary>Sets the control overlay style.</summary>
    public VideoPlayer Controls(VideoControls controls)
    {
        ControlsMode = controls;
        return this;
    }

    /// <summary>Enables or disables auto-play.</summary>
    public VideoPlayer AutoPlay(bool enabled)
    {
        AutoPlayEnabled = enabled;
        return this;
    }

    /// <summary>Enables or disables looping.</summary>
    public VideoPlayer Loop(bool enabled)
    {
        LoopEnabled = enabled;
        return this;
    }

    /// <summary>Mutes or unmutes audio.</summary>
    public VideoPlayer Muted(bool muted)
    {
        MutedEnabled = muted;
        return this;
    }

    /// <summary>Sets the initial playback position.</summary>
    public VideoPlayer StartAt(TimeSpan position)
    {
        StartPosition = position;
        return this;
    }

    // ── Seek thumbnails ───────────────────────────────────────────────

    /// <summary>Enables seek bar thumbnail previews.</summary>
    public VideoPlayer SeekThumbnails(bool enabled, TimeSpan? interval = null)
    {
        SeekThumbnailsEnabled = enabled;
        SeekThumbnailInterval = interval;
        return this;
    }

    // ── Subtitles ─────────────────────────────────────────────────────

    /// <summary>Configures subtitle tracks.</summary>
    public VideoPlayer Subtitles(bool embedded = true, IReadOnlyList<SubtitleTrack>? external = null)
    {
        SubtitlesEmbedded = embedded;
        ExternalSubtitles = external ?? Array.Empty<SubtitleTrack>();
        return this;
    }

    /// <summary>Sets the default subtitle track by name.</summary>
    public VideoPlayer DefaultSubtitleTrack(string name)
    {
        DefaultSubtitleName = name;
        return this;
    }

    // ── Audio tracks ──────────────────────────────────────────────────

    /// <summary>Enables embedded audio track selection.</summary>
    public VideoPlayer AudioTracks(bool embedded)
    {
        AudioTracksEnabled = embedded;
        return this;
    }

    /// <summary>Sets the default audio track by name.</summary>
    public VideoPlayer DefaultAudioTrack(string name)
    {
        DefaultAudioTrackName = name;
        return this;
    }

    // ── Chapters ──────────────────────────────────────────────────────

    /// <summary>Sets chapter markers displayed on the seek bar.</summary>
    public VideoPlayer Chapters(IReadOnlyList<VideoChapter> chapters)
    {
        ChapterList = chapters;
        return this;
    }

    // ── Picture-in-Picture ────────────────────────────────────────────

    /// <summary>Enables picture-in-picture support.</summary>
    public VideoPlayer PictureInPicture(bool enabled)
    {
        PictureInPictureEnabled = enabled;
        return this;
    }

    // ── Events ────────────────────────────────────────────────────────

    /// <summary>Callback when playback starts.</summary>
    public VideoPlayer OnPlay(Action handler)
    {
        OnPlayHandler = handler;
        return this;
    }

    /// <summary>Callback when playback pauses.</summary>
    public VideoPlayer OnPause(Action handler)
    {
        OnPauseHandler = handler;
        return this;
    }

    /// <summary>Callback when playback reaches the end.</summary>
    public VideoPlayer OnEnded(Action handler)
    {
        OnEndedHandler = handler;
        return this;
    }

    /// <summary>Callback on time position update.</summary>
    public VideoPlayer OnTimeUpdate(Action<TimeSpan> handler)
    {
        OnTimeUpdateHandler = handler;
        return this;
    }

    /// <summary>Callback on playback error.</summary>
    public VideoPlayer OnError(Action<VideoError> handler)
    {
        OnErrorHandler = handler;
        return this;
    }

    /// <summary>Callback on chapter change.</summary>
    public VideoPlayer OnChapterChange(Action<VideoChapter> handler)
    {
        OnChapterChangeHandler = handler;
        return this;
    }
}

/// <summary>
/// Video control overlay style.
/// </summary>
public enum VideoControls
{
    /// <summary>Play/pause, seek bar, volume, time, fullscreen, settings.</summary>
    Full,

    /// <summary>Play/pause, seek bar only.</summary>
    Minimal,

    /// <summary>No controls — use programmatic API.</summary>
    None
}

/// <summary>
/// An external subtitle track for <see cref="VideoPlayer"/>.
/// </summary>
public sealed class SubtitleTrack
{
    /// <summary>Creates a subtitle track.</summary>
    /// <param name="name">Display name (e.g., "English").</param>
    /// <param name="path">File path to the subtitle file (SRT, VTT, ASS, etc.).</param>
    public SubtitleTrack(string name, string path)
    {
        Name = name;
        Path = path;
    }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>File path to the subtitle file.</summary>
    public string Path { get; }
}

/// <summary>
/// A chapter marker in a <see cref="VideoPlayer"/>.
/// </summary>
public sealed class VideoChapter
{
    /// <summary>Creates a video chapter.</summary>
    /// <param name="title">Chapter title.</param>
    /// <param name="startTime">Start time of the chapter.</param>
    public VideoChapter(string title, TimeSpan startTime)
    {
        Title = title;
        StartTime = startTime;
    }

    /// <summary>Chapter title.</summary>
    public string Title { get; }

    /// <summary>Start time of the chapter.</summary>
    public TimeSpan StartTime { get; }
}

/// <summary>
/// A playback error from <see cref="VideoPlayer"/>.
/// </summary>
public sealed class VideoError
{
    /// <summary>Creates a video error.</summary>
    /// <param name="message">Error message.</param>
    public VideoError(string message)
    {
        Message = message;
    }

    /// <summary>Error message.</summary>
    public string Message { get; }
}

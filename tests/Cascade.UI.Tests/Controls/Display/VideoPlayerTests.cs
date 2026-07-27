#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class VideoPlayerTests
{
    [Test]
    public async Task Constructor_Path_StoresPath()
    {
        var path = "movie.mp4";
        var player = new VideoPlayer(path);

        string? actual = player.Path;
        await Assert.That(actual).IsEqualTo(path);
    }

    [Test]
    public async Task Constructor_Url_StoresUrl()
    {
        var url = "https://example.com/movie.mp4";
        var player = new VideoPlayer(url, streaming: true);

        string? actual = player.Url;
        await Assert.That(actual).IsEqualTo(url);
    }

    [Test]
    public async Task Constructor_Bindable_StoresBindable()
    {
        var source = new Bindable<string>("intro.mp4", _ => { });
        var player = new VideoPlayer(source);

        bool hasValue = player.BindableSource.HasValue;
        bool equals = player.BindableSource!.Value.Equals(source);
        await Assert.That(hasValue).IsTrue();
        await Assert.That(equals).IsTrue();
    }

    [Test]
    public async Task Controls_SetsMode()
    {
        var player = new VideoPlayer("movie.mp4").Controls(VideoControls.Minimal);

        var actual = player.ControlsMode;
        var expected = VideoControls.Minimal;
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task PlaybackFlags_SetValues()
    {
        var start = TimeSpan.FromSeconds(12);
        var player = new VideoPlayer("movie.mp4")
            .AutoPlay(true)
            .Loop(true)
            .Muted(true)
            .StartAt(start);

        bool autoplay = player.AutoPlayEnabled;
        bool loop = player.LoopEnabled;
        bool muted = player.MutedEnabled;
        var position = player.StartPosition;
        await Assert.That(autoplay).IsTrue();
        await Assert.That(loop).IsTrue();
        await Assert.That(muted).IsTrue();
        await Assert.That(position).IsEqualTo(start);
    }

    [Test]
    public async Task SeekThumbnails_SetsOptions()
    {
        var interval = TimeSpan.FromSeconds(10);
        var player = new VideoPlayer("movie.mp4").SeekThumbnails(true, interval);

        bool enabled = player.SeekThumbnailsEnabled;
        var actual = player.SeekThumbnailInterval;
        await Assert.That(enabled).IsTrue();
        await Assert.That(actual).IsEqualTo(interval);
    }

    [Test]
    public async Task Subtitles_SetsTracks()
    {
        var tracks = new[] { new SubtitleTrack("English", "en.srt") };
        var player = new VideoPlayer("movie.mp4").Subtitles(embedded: false, external: tracks);

        bool embedded = player.SubtitlesEmbedded;
        int count = player.ExternalSubtitles.Count;
        var expected = 1;
        await Assert.That(embedded).IsFalse();
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task AudioTracks_SetsDefaults()
    {
        var player = new VideoPlayer("movie.mp4")
            .AudioTracks(true)
            .DefaultAudioTrack("English 5.1");

        bool enabled = player.AudioTracksEnabled;
        string? track = player.DefaultAudioTrackName;
        var expected = "English 5.1";
        await Assert.That(enabled).IsTrue();
        await Assert.That(track).IsEqualTo(expected);
    }

    [Test]
    public async Task Chapters_SetsChapterList()
    {
        var chapters = new[]
        {
            new VideoChapter("Intro", TimeSpan.Zero),
            new VideoChapter("Part 1", TimeSpan.FromMinutes(3))
        };
        var player = new VideoPlayer("movie.mp4").Chapters(chapters);

        int count = player.ChapterList.Count;
        var expected = 2;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task PictureInPicture_SetsFlag()
    {
        var player = new VideoPlayer("movie.mp4").PictureInPicture(true);

        bool enabled = player.PictureInPictureEnabled;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task Events_SetHandlers()
    {
        var player = new VideoPlayer("movie.mp4")
            .OnPlay(() => { })
            .OnPause(() => { })
            .OnEnded(() => { })
            .OnTimeUpdate(_ => { })
            .OnError(_ => { })
            .OnChapterChange(_ => { });

        bool hasPlay = player.OnPlayHandler != null;
        bool hasPause = player.OnPauseHandler != null;
        bool hasEnded = player.OnEndedHandler != null;
        bool hasUpdate = player.OnTimeUpdateHandler != null;
        bool hasError = player.OnErrorHandler != null;
        bool hasChapter = player.OnChapterChangeHandler != null;
        await Assert.That(hasPlay).IsTrue();
        await Assert.That(hasPause).IsTrue();
        await Assert.That(hasEnded).IsTrue();
        await Assert.That(hasUpdate).IsTrue();
        await Assert.That(hasError).IsTrue();
        await Assert.That(hasChapter).IsTrue();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var player = new VideoPlayer("movie.mp4");
        var result = player
            .Controls(VideoControls.Full)
            .AutoPlay(false)
            .Loop(false);

        bool same = ReferenceEquals(player, result);
        await Assert.That(same).IsTrue();
    }
}

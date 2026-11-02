using System.Text.RegularExpressions;

namespace Cascade.UI.Tests.CI;

/// <summary>
/// Every fluent modifier must do something. Controls keep what a modifier sets in an internal
/// settable property; if nothing outside the control's own file ever reads it, the modifier is
/// stored and ignored (Label.FontFamily, Label.Decoration, Label.Selectable and Image.Fit all
/// were, unnoticed). This scans the framework source for such properties.
/// <see cref="NotYetImplemented"/> lists the ones found on 2026-11-02 that are still being built:
/// it may only shrink — implement one, and the test insists it leaves the list.
/// </summary>
public partial class ModifierCoverageTests
{
    private static readonly HashSet<string> NotYetImplemented = new(StringComparer.Ordinal)
    {
        "AudioPlayer.MetadataDisplayLayout",
        "AudioPlayer.MetadataVisible",
        "AudioPlayer.PlaybackRateOptions",
        "AudioPlayer.SkipAmountValue",
        "AudioPlayer.WaveformBars",
        "AudioPlayer.WaveformDisplayStyle",
        "AudioPlayer.WaveformPlayedColor",
        "AudioPlayer.WaveformUnplayedColor",
        "AudioPlayer.WaveformVisible",
        "Breadcrumb.SeparatorNode",
        "Calendar.OnDragCreate",
        "Calendar.OnDragMove",
        "Calendar.OnDragResize",
        "Card.ElevationOverride",
        "ColorPicker.DisplayFormats",
        "ColorPicker.EnableEyeDropper",
        "ColorPicker.MaxRecentColors",
        "ColorPicker.PickerModes",
        "ColorPicker.SavedSwatches",
        "ColorPicker.ShowOpacitySlider",
        "Combobox.AsyncOptionSource",
        "CommandPalette.CustomProviders",
        "DatePicker.HighlightedDatesTooltip",
        "EmojiPicker.MaxRecentCount",
        "EmojiPicker.RecentEmojiBind",
        "EmojiPicker.SkinToneBind",
        "Image.AutoRotateEnabled",
        "Image.DoubleTapZoom",
        "Image.ErrorNode",
        "Image.FadeInEnabled",
        "Image.LastResizeMode",
        "Image.MaxZoom",
        "Image.MinZoom",
        "Image.PlaceholderNode",
        "Image.ProcessingOperations",
        "Image.ProcessingPipeline",
        "Image.SvgColorMapOverride",
        "Image.SvgColorOverride",
        "Image.ZoomPanEnabled",
        "LinkButton.IsUnderlined",
        "Markdown.CodeBlockCopyButtonLabel",
        "Markdown.CodeBlockLineNumbers",
        "Markdown.CodeBlockMaxHeight",
        "Markdown.CodeBlockWrapLines",
        "Markdown.HeadingAnchorsEnabled",
        "Markdown.HeadingVisibleHandler",
        "Markdown.HtmlAllowed",
        "Markdown.ImagesBaseUrl",
        "Markdown.ImagesBorderRadius",
        "Markdown.ImagesLazyLoad",
        "Markdown.ImagesMaxWidth",
        "Markdown.LinkClickHandler",
        "Markdown.StreamingEnabled",
        "MultiSelect.ShowSelectAll",
        "NotificationBell.CustomRenderer",
        "NotificationBell.EmptyStateNode",
        "NotificationBell.EnableRingAnimation",
        "ProgressBar.ShowLabelEnabled",
        "PropertyGrid.UpdateIntervalValue",
        "QrCode.LogoBackgroundEnabled",
        "QrCode.LogoNode",
        "QrCode.LogoSizeFraction",
        "Rating.EmptyIcon",
        "Rating.FilledIcon",
        "Rating.TooltipLabels",
        "ScrollView.PersistScrollKey",
        "Select.AsyncSearchSource",
        "Select.SelectedRenderer",
        "Spinner.SpinnerColor",
        "StepIndicator.StyleSetting",
        "Tag.TagColorOverride",
        "TagInput.IsReorderEnabled",
        "TagInput.SuggestionAsyncSource",
        "TagInput.SuggestionSyncSource",
        "TextArea.AutoGrowEnabled",
        "TextArea.AutoGrowMaxLines",
        "TextArea.AutoGrowMinLines",
        "TextArea.ResizeDir",
        "TextInput.AutocompleteAsyncSource",
        "TextInput.AutocompleteSyncSource",
        "TextInput.CustomMaskValue",
        "TextInput.TypedAutocompleteSource",
        "TimePicker.PopupDisplayStyle",
        "Toggle.LabelPositionValue",
        "ToolBar.OrientationSetting",
        "ToolBar.OverflowSetting",
        "VideoPlayer.AudioTracksEnabled",
        "VideoPlayer.ChapterList",
        "VideoPlayer.DefaultAudioTrackName",
        "VideoPlayer.DefaultSubtitleName",
        "VideoPlayer.ExternalSubtitles",
        "VideoPlayer.MutedEnabled",
        "VideoPlayer.OnChapterChangeHandler",
        "VideoPlayer.OnTimeUpdateHandler",
        "VideoPlayer.PictureInPictureEnabled",
        "VideoPlayer.SeekThumbnailInterval",
        "VideoPlayer.SeekThumbnailsEnabled",
        "VideoPlayer.StartPosition",
        "VideoPlayer.SubtitlesEmbedded",
    };

    private static readonly string[] ControlFolders = ["Controls", "Navigation", "Dialog", "Responsive", "Scroll", "Imaging"];

    [GeneratedRegex(@"internal\s+[^;{=()]+?\s+([A-Z][A-Za-z0-9]+)\s*\{\s*get;\s*(?:internal\s+|private\s+)?set;\s*\}")]
    private static partial Regex ModifierState();

    [Test]
    public async Task EveryModifierSetting_IsReadOutsideItsControl()
    {
        var unread = FindUnreadModifierState();
        var unexpected = unread.Where(name => !NotYetImplemented.Contains(name)).Order().ToList();

        await Assert.That(unexpected).IsEmpty()
            .Because("these modifiers store a setting nothing uses: implement them (paint, layout or input must read it)");
    }

    [Test]
    public async Task NotYetImplementedList_OnlyNamesModifiersThatAreStillUnread()
    {
        var unread = FindUnreadModifierState();
        var done = NotYetImplemented.Where(name => !unread.Contains(name)).Order().ToList();

        await Assert.That(done).IsEmpty()
            .Because("these are implemented now: remove them from NotYetImplemented");
    }

    /// <summary>"Control.Property" for each internal settable property no other source file mentions.</summary>
    private static HashSet<string> FindUnreadModifierState()
    {
        string source = System.IO.Path.Combine(FindRepoRoot(), "src", "Cascade.UI");
        var files = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(f => f, File.ReadAllText);

        var unread = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string file, string text) in files)
        {
            string relative = System.IO.Path.GetRelativePath(source, file);
            if (!ControlFolders.Any(folder => relative.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            {
                continue;
            }
            foreach (Match match in ModifierState().Matches(text))
            {
                string name = match.Groups[1].Value;
                var word = new Regex($@"\b{name}\b");
                bool readElsewhere = files.Any(other => other.Key != file && word.IsMatch(other.Value));
                if (!readElsewhere && word.Count(text) <= 2)
                {
                    unread.Add($"{System.IO.Path.GetFileNameWithoutExtension(file)}.{name}");
                }
            }
        }
        return unread;
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(System.IO.Path.Combine(directory, ".github")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the repository root (the folder with .github).");
    }
}

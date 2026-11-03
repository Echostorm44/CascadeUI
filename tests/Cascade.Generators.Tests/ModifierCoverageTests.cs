using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cascade.Generators.Tests;

/// <summary>
/// Every fluent modifier must do something. A control keeps what a modifier sets in a property
/// with a restricted setter; if nothing anywhere reads that property, the modifier is stored and
/// ignored. Label.FontFamily, Label.Decoration, Label.Selectable and Image.Fit all were, unnoticed,
/// and so (2026-11-02) were the ~190 listed below. This compiles Cascade.UI's source and finds them
/// by symbol, not by name: a property is dead when a method returning its control assigns it and
/// no code reads it.
/// <see cref="NotYetImplemented"/> may only shrink - implement one and the test insists it leaves.
/// </summary>
public class ModifierCoverageTests
{
    private static readonly HashSet<string> NotYetImplemented = new(StringComparer.Ordinal)
    {
        "AudioPlayer.AutoPlayEnabled",
        "AudioPlayer.ControlsMode",
        "AudioPlayer.LoopEnabled",
        "AudioPlayer.MetadataDisplayLayout",
        "AudioPlayer.MetadataVisible",
        "AudioPlayer.OnEndedHandler",
        "AudioPlayer.OnErrorHandler",
        "AudioPlayer.OnPauseHandler",
        "AudioPlayer.OnPlayHandler",
        "AudioPlayer.PlaybackRateOptions",
        "AudioPlayer.SkipAmountValue",
        "AudioPlayer.WaveformBars",
        "AudioPlayer.WaveformDisplayStyle",
        "AudioPlayer.WaveformPlayedColor",
        "AudioPlayer.WaveformUnplayedColor",
        "AudioPlayer.WaveformVisible",
        "Calendar.OnDragCreate",
        "Calendar.OnDragMove",
        "Calendar.OnDragResize",
        "ColorPicker.DisplayFormats",
        "ColorPicker.EnableEyeDropper",
        "ColorPicker.MaxRecentColors",
        "ColorPicker.PickerModes",
        "ColorPicker.SavedSwatches",
        "ColorPicker.ShowOpacitySlider",
        "CommandPalette.CustomProviders",
        "DatePicker.HighlightedDatesTooltip",
        "EmojiPicker.Categories",
        "EmojiPicker.IsDisabled",
        "EmojiPicker.MaxRecentCount",
        "EmojiPicker.RecentEmojiBind",
        "EmojiPicker.SkinToneBind",
        "Image.AutoRotateEnabled",
        "Image.DoubleTapZoom",
        "Image.ErrorNode",
        "Image.FadeInEnabled",
        "Image.LazyLoadEnabled",
        "Image.MaxZoom",
        "Image.MinZoom",
        "Image.PlaceholderNode",
        "Image.ProcessingOperations",
        "Image.ProcessingPipeline",
        "Image.SvgColorMapOverride",
        "Image.SvgColorOverride",
        "Image.ZoomPanEnabled",
        "Markdown.AutoScrollMode",
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
        "MentionInput.MaxLengthValue",
        "NavigationBar.CollapseButtonPos",
        "NavigationBar.Collapsed",
        "NavigationBar.CollapseWidth",
        "NavigationBar.ExpandWidth",
        "NavigationBar.IsCollapsible",
        "NavigationBar.ShowCollapseButton",
        "NotificationBell.CustomRenderer",
        "NotificationBell.EmptyStateNode",
        "NotificationBell.EnableRingAnimation",
        "NotificationBell.IsDisabled",
        "NumberInput.PrefixNode",
        "NumberInput.SuffixNode",
        "QrCode.LogoBackgroundEnabled",
        "QrCode.LogoNode",
        "QrCode.LogoSizeFraction",
        "ScrollView.AccessibleLabel",
        "ScrollView.AutoScrollMode",
        "ScrollView.EndReachedThreshold",
        "ScrollView.IsFadeEdgesEnabled",
        "ScrollView.IsKeyboardScrollingEnabled",
        "ScrollView.IsPagingEnabled",
        "ScrollView.NestedScrollMode",
        "ScrollView.OnEndReachedHandler",
        "ScrollView.OnPageChangedHandler",
        "ScrollView.OnScrollEndHandler",
        "ScrollView.OnScrollHandler",
        "ScrollView.OnScrollStartHandler",
        "ScrollView.OverscrollMode",
        "ScrollView.PersistScrollKey",
        "ScrollView.PullToRefreshHandler",
        "ScrollView.PullToRefreshIndicator",
        "ScrollView.PullToRefreshIndicatorOffset",
        "ScrollView.PullToRefreshThreshold",
        "ScrollView.ScrollbarMode",
        "ScrollView.ScrollDirection",
        "ScrollView.ScrollSnap",
        "ScrollView.SnapAlignment",
        "TagInput.IsReorderEnabled",
        "TextArea.AutoGrowEnabled",
        "TextArea.AutoGrowMaxLines",
        "TextArea.AutoGrowMinLines",
        "TextArea.MaxLines",
        "TextArea.PrefixNode",
        "TextArea.ResizeDir",
        "TextArea.SuffixNode",
        "TextInput.CustomMaskValue",
        "TextInput.MaskPatternValue",
        "TextInput.PrefixNode",
        "TextInput.SuffixNode",
        "TimePicker.PopupDisplayStyle",
        "VideoPlayer.AudioTracksEnabled",
        "VideoPlayer.AutoPlayEnabled",
        "VideoPlayer.ChapterList",
        "VideoPlayer.ControlsMode",
        "VideoPlayer.DefaultAudioTrackName",
        "VideoPlayer.DefaultSubtitleName",
        "VideoPlayer.ExternalSubtitles",
        "VideoPlayer.LoopEnabled",
        "VideoPlayer.MutedEnabled",
        "VideoPlayer.OnChapterChangeHandler",
        "VideoPlayer.OnEndedHandler",
        "VideoPlayer.OnErrorHandler",
        "VideoPlayer.OnPauseHandler",
        "VideoPlayer.OnPlayHandler",
        "VideoPlayer.OnTimeUpdateHandler",
        "VideoPlayer.PictureInPictureEnabled",
        "VideoPlayer.SeekThumbnailInterval",
        "VideoPlayer.SeekThumbnailsEnabled",
        "VideoPlayer.StartPosition",
        "VideoPlayer.SubtitlesEmbedded",
    };

    private static readonly string[] ControlFolders = ["Controls", "Navigation", "Dialog", "Responsive", "Scroll", "Imaging", "Layout"];

    private static readonly Lazy<HashSet<string>> Dead = new(FindDeadModifiers);

    [TUnit.Core.Test]
    public async Task EveryModifierSetting_IsRead()
    {
        var unexpected = Dead.Value.Where(name => !NotYetImplemented.Contains(name)).Order().ToList();
        if (unexpected.Count > 0)
        {
            throw new InvalidOperationException("These modifiers store a setting nothing reads - implement them: "
                + string.Join(", ", unexpected));
        }
        await TUnit.Assertions.Assert.That(unexpected.Count).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task NotYetImplemented_OnlyListsModifiersThatAreStillUnread()
    {
        var done = NotYetImplemented.Where(name => !Dead.Value.Contains(name)).Order().ToList();
        if (done.Count > 0)
        {
            throw new InvalidOperationException("These are implemented now - remove them from NotYetImplemented: "
                + string.Join(", ", done));
        }
        await TUnit.Assertions.Assert.That(done.Count).IsEqualTo(0);
    }

    /// <summary>"Control.Property" for each fluent-set, never-read property of a Node subclass.</summary>
    private static HashSet<string> FindDeadModifiers()
    {
        string source = Path.Combine(FindRepoRoot(), "src", "Cascade.UI");
        char sep = Path.DirectorySeparatorChar;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["DEBUG", "CASCADE_DEVTOOLS"]);
        var trees = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal) && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parse, f))
            .ToList();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ModifierScan", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var node = compilation.GetTypeByMetadataName("Cascade.UI.Node")
            ?? throw new InvalidOperationException("Cascade.UI.Node not found in the source.");

        var candidates = new Dictionary<IPropertySymbol, string>(SymbolEqualityComparer.Default);
        var read = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        var setByModifier = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            string relative = Path.GetRelativePath(source, tree.FilePath);
            bool inControls = ControlFolders.Any(folder => relative.StartsWith(folder + sep, StringComparison.Ordinal));
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (!inControls
                    || model.GetDeclaredSymbol(declaration) is not IPropertySymbol property
                    || property.IsStatic
                    || property.SetMethod is null
                    || !(property.DeclaredAccessibility == Accessibility.Internal
                         || property.SetMethod.DeclaredAccessibility is Accessibility.Private or Accessibility.Internal)
                    || !DerivesFrom(property.ContainingType, node)
                    || declaration.AccessorList?.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null) == true)
                {
                    continue;
                }
                candidates[property] = $"{property.ContainingType.Name}.{property.Name}";
            }

            foreach (var identifier in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (model.GetSymbolInfo(identifier).Symbol is not IPropertySymbol property)
                {
                    continue;
                }
                SyntaxNode target = identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier ? access : identifier;
                if (target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                {
                    if (identifier.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method
                        && model.GetDeclaredSymbol(method) is IMethodSymbol { ReturnType: INamedTypeSymbol returned }
                        && SymbolEqualityComparer.Default.Equals(returned.OriginalDefinition, property.ContainingType.OriginalDefinition))
                    {
                        setByModifier.Add(property.OriginalDefinition);
                    }
                    continue;
                }
                read.Add(property.OriginalDefinition);
            }
        }

        return candidates
            .Where(c => setByModifier.Contains(c.Key) && !read.Contains(c.Key))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }
        return false;
    }

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, ".github")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the repository root (the folder with .github).");
    }
}

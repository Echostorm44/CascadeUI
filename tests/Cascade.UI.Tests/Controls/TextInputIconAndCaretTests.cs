namespace Cascade.UI.Tests.Controls;

/// <summary>
/// TextInput's leading icon moves where text starts, and a click places the caret by measuring the
/// text as painted (it used to assume a fixed padding and an average glyph width, ignoring icons).
/// </summary>
[NotInParallel([nameof(LayoutSolver.DefaultFontPath), "FocusManager"])]
public class TextInputIconAndCaretTests
{
    private static readonly Icon Search = new(["M11 17a6 6 0 1 0 0-12 6 6 0 0 0 0 12z"], new Size(24, 24), 16f, "Search");
    private string? savedFont;

    [Before(Test)]
    public void SetUp()
    {
        savedFont = LayoutSolver.DefaultFontPath;
        LayoutSolver.DefaultFontPath = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        FocusManager.Reset();
    }

    [After(Test)]
    public void TearDown()
    {
        LayoutSolver.DefaultFontPath = savedFont;
    }

    [Test]
    public async Task Icon_PushesTextStart_PastIconAndGap()
    {
        var theme = ThemeSwitcher.Current;
        var plain = new TextInput(new Bindable<string>("", _ => { }));
        var withIcon = new TextInput(new Bindable<string>("", _ => { }), icon: Search);

        float iconSize = TextInputMetrics.IconSize(withIcon, theme);

        await Assert.That(TextInputMetrics.ContentLeft(plain, theme)).IsEqualTo(theme.TextInput.PaddingH);
        await Assert.That(iconSize).IsEqualTo(16f); // the size the icon declares
        await Assert.That(TextInputMetrics.ContentLeft(withIcon, theme)).IsEqualTo(theme.TextInput.PaddingH + iconSize + TextInputMetrics.IconGap);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Click_PlacesCaretAtMeasuredCharacterBoundary(bool icon)
    {
        var theme = ThemeSwitcher.Current;
        var input = icon
            ? new TextInput(new Bindable<string>("", _ => { }), icon: Search)
            : new TextInput(new Bindable<string>("", _ => { }));
        input.AbsoluteBounds = new Rect(100, 50, 400, 36);
        const string text = "hello world";

        float prefix = TextLayoutEngine.Layout("hello", new TextLayoutOptions
        {
            FontPath = LayoutSolver.DefaultFontPath!,
            FontSize = TextInputMetrics.FontSize(theme),
            MaxWidth = float.PositiveInfinity,
            MaxLines = 1,
        }).Lines[0].Width;
        float textStart = 100 + TextInputMetrics.ContentLeft(input, theme);

        // Just right of "hello" → after the 'o'; on the icon or padding → start.
        await Assert.That(InputDispatcher.CaretIndexAt(input, text, textStart + prefix + 1f)).IsEqualTo(5);
        await Assert.That(InputDispatcher.CaretIndexAt(input, text, 101f)).IsEqualTo(0);
        await Assert.That(InputDispatcher.CaretIndexAt(input, text, 499f)).IsEqualTo(text.Length);
    }
}

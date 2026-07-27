#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class MarkdownTests
{
    [Test]
    public async Task Constructor_Source_StoresText()
    {
        var source = "# Title";
        var markdown = new Markdown(source);

        string? actual = markdown.Source;
        await Assert.That(actual).IsEqualTo(source);
    }

    [Test]
    public async Task Constructor_BindableSource_StoresBindable()
    {
        var bindable = new Bindable<string>("Hello", _ => { });
        var markdown = new Markdown(bindable);

        bool hasValue = markdown.BindableSource.HasValue;
        bool equals = markdown.BindableSource!.Value.Equals(bindable);
        await Assert.That(hasValue).IsTrue();
        await Assert.That(equals).IsTrue();
    }

    [Test]
    public async Task Constructor_Url_StoresUrl()
    {
        var url = "https://example.com/readme.md";
        var markdown = new Markdown(url, fromUrl: true);

        string? actual = markdown.SourceUrl;
        await Assert.That(actual).IsEqualTo(url);
    }

    [Test]
    public async Task CodeBlock_SetsOptions()
    {
        var markdown = new Markdown("code")
            .CodeBlock(
                syntaxHighlight: false,
                showLanguageLabel: false,
                showCopyButton: false,
                copyButtonLabel: "Copy Code",
                lineNumbers: true,
                maxHeight: 240,
                wrapLines: true);

        bool highlight = markdown.CodeBlockSyntaxHighlight;
        bool showLabel = markdown.CodeBlockShowLanguageLabel;
        bool showCopy = markdown.CodeBlockShowCopyButton;
        string label = markdown.CodeBlockCopyButtonLabel;
        bool lineNumbers = markdown.CodeBlockLineNumbers;
        float? maxHeight = markdown.CodeBlockMaxHeight;
        bool wrap = markdown.CodeBlockWrapLines;
        var expectedLabel = "Copy Code";
        var expectedHeight = 240f;
        await Assert.That(highlight).IsFalse();
        await Assert.That(showLabel).IsFalse();
        await Assert.That(showCopy).IsFalse();
        await Assert.That(label).IsEqualTo(expectedLabel);
        await Assert.That(lineNumbers).IsTrue();
        await Assert.That(maxHeight).IsEqualTo(expectedHeight);
        await Assert.That(wrap).IsTrue();
    }

    [Test]
    public async Task OnLinkClick_SetsHandler()
    {
        string? clicked = null;
        var markdown = new Markdown("link")
            .OnLinkClick(url => { clicked = url; });

        markdown.LinkClickHandler!.Invoke("https://cascade.dev");
        var expected = "https://cascade.dev";
        await Assert.That(clicked).IsEqualTo(expected);
    }

    [Test]
    public async Task Images_SetsOptions()
    {
        var markdown = new Markdown("image")
            .Images(lazyLoad: false, maxWidth: 320, borderRadius: 6);

        bool lazy = markdown.ImagesLazyLoad;
        float? maxWidth = markdown.ImagesMaxWidth;
        float? radius = markdown.ImagesBorderRadius;
        var expectedWidth = 320f;
        var expectedRadius = 6f;
        await Assert.That(lazy).IsFalse();
        await Assert.That(maxWidth).IsEqualTo(expectedWidth);
        await Assert.That(radius).IsEqualTo(expectedRadius);
    }

    [Test]
    public async Task BaseUrl_SetsBaseUrl()
    {
        var baseUrl = "https://example.com/docs/";
        var markdown = new Markdown("docs").BaseUrl(baseUrl);

        string? actual = markdown.ImagesBaseUrl;
        await Assert.That(actual).IsEqualTo(baseUrl);
    }

    [Test]
    public async Task StreamingMode_AndAutoScroll_SetFlags()
    {
        var markdown = new Markdown("stream")
            .StreamingMode(true)
            .AutoScroll(MarkdownAutoScrollMode.WhenAtBottom);

        bool streaming = markdown.StreamingEnabled;
        var mode = markdown.AutoScrollMode;
        var expectedMode = MarkdownAutoScrollMode.WhenAtBottom;
        await Assert.That(streaming).IsTrue();
        await Assert.That(mode).IsEqualTo(expectedMode);
    }

    [Test]
    public async Task AllowHtml_SetsFlag()
    {
        var markdown = new Markdown("html").AllowHtml(true);

        bool allowed = markdown.HtmlAllowed;
        await Assert.That(allowed).IsTrue();
    }

    [Test]
    public async Task ExtractHeadings_ReturnsHeadings()
    {
        var source = "# Title\n## Section\nNot a heading";
        var headings = MarkdownParser.ExtractHeadings(source);

        int count = headings.Count;
        var expectedCount = 2;
        string firstTitle = headings[0].Title;
        var expectedTitle = "Title";
        await Assert.That(count).IsEqualTo(expectedCount);
        await Assert.That(firstTitle).IsEqualTo(expectedTitle);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var markdown = new Markdown("content");
        var result = markdown
            .HeadingAnchors(true)
            .AllowHtml(false)
            .Images();

        bool same = ReferenceEquals(markdown, result);
        await Assert.That(same).IsTrue();
    }
}

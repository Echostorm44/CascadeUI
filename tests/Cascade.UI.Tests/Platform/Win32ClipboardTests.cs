using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for ClipboardContent availability tracking and ClipboardSnapshot.Formats.
/// These tests verify internal state without requiring a live Win32 clipboard.
/// </summary>
public class Win32ClipboardTests
{
    // ── ClipboardContent availability flags ──────────────────────────

    [Test]
    public async Task ClipboardContent_HasText_WhenTextAvailable()
    {
        ClipboardAvailability avail = new() { HasText = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasText).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_HasText_FalseWhenNotAvailable()
    {
        ClipboardAvailability avail = new() { HasText = false };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasText).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_HasHtml_WhenHtmlAvailable()
    {
        ClipboardAvailability avail = new() { HasHtml = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasHtml).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_HasRtf_AlwaysFalse()
    {
        ClipboardAvailability avail = new() { HasText = true, HasHtml = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasRtf).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_HasImage_WhenImageAvailable()
    {
        ClipboardAvailability avail = new() { HasImage = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasImage).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_HasFiles_WhenFilesAvailable()
    {
        ClipboardAvailability avail = new() { HasFiles = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasFiles).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_AllFlagsDefault_AllFalse()
    {
        ClipboardAvailability avail = new();
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.HasText).IsFalse();
        await Assert.That(content.HasHtml).IsFalse();
        await Assert.That(content.HasRtf).IsFalse();
        await Assert.That(content.HasImage).IsFalse();
        await Assert.That(content.HasFiles).IsFalse();
    }

    // ── AvailableFormats ─────────────────────────────────────────────

    [Test]
    public async Task ClipboardContent_AvailableFormats_EmptyWhenNothingAvailable()
    {
        ClipboardAvailability avail = new();
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.AvailableFormats.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClipboardContent_AvailableFormats_ContainsTextWhenTextSet()
    {
        ClipboardAvailability avail = new() { HasText = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.AvailableFormats.Count).IsEqualTo(1);
        await Assert.That(content.AvailableFormats[0]).IsEqualTo(ClipboardFormat.Text);
    }

    [Test]
    public async Task ClipboardContent_AvailableFormats_ContainsAllSetFormats()
    {
        ClipboardAvailability avail = new() { HasText = true, HasHtml = true, HasFiles = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.AvailableFormats.Count).IsEqualTo(3);
        await Assert.That(content.AvailableFormats).Contains(ClipboardFormat.Text);
        await Assert.That(content.AvailableFormats).Contains(ClipboardFormat.Html);
        await Assert.That(content.AvailableFormats).Contains(ClipboardFormat.Files);
    }

    [Test]
    public async Task ClipboardContent_AvailableFormats_DoesNotContainRtf()
    {
        ClipboardAvailability avail = new() { HasText = true, HasHtml = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        await Assert.That(content.AvailableFormats).DoesNotContain(ClipboardFormat.Rtf);
    }

    // ── Unsupported GetAsync methods return null ─────────────────────

    [Test]
    public async Task ClipboardContent_GetRtfAsync_ReturnsNull()
    {
        ClipboardAvailability avail = new();
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        string? result = await content.GetRtfAsync();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ClipboardContent_GetImageAsync_ReturnsNull()
    {
        ClipboardAvailability avail = new();
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        ImageData? result = await content.GetImageAsync();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ClipboardContent_GetRawAsync_ReturnsNull()
    {
        ClipboardAvailability avail = new();
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        byte[]? result = await content.GetRawAsync(ClipboardFormat.Custom("custom"));
        await Assert.That(result).IsNull();
    }

    // ── ClipboardSnapshot.Formats ────────────────────────────────────

    [Test]
    public async Task ClipboardSnapshot_Formats_DefaultIsEmpty()
    {
        ClipboardSnapshot snapshot = new();
        await Assert.That(snapshot.Formats.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClipboardSnapshot_Create_StoresFormats()
    {
        List<ClipboardFormat> formats = [ClipboardFormat.Text, ClipboardFormat.Html];
        ClipboardSnapshot snapshot = ClipboardSnapshot.Create(formats, "hello", "<b>hello</b>", null, null, null);
        await Assert.That(snapshot.Formats.Count).IsEqualTo(2);
        await Assert.That(snapshot.Formats[0]).IsEqualTo(ClipboardFormat.Text);
        await Assert.That(snapshot.Formats[1]).IsEqualTo(ClipboardFormat.Html);
    }

    [Test]
    public async Task ClipboardSnapshot_Create_StoresText()
    {
        List<ClipboardFormat> formats = [ClipboardFormat.Text];
        ClipboardSnapshot snapshot = ClipboardSnapshot.Create(formats, "my text", null, null, null, null);
        await Assert.That(snapshot.Text).IsEqualTo("my text");
    }

    [Test]
    public async Task ClipboardSnapshot_Create_StoresHtml()
    {
        List<ClipboardFormat> formats = [ClipboardFormat.Html];
        ClipboardSnapshot snapshot = ClipboardSnapshot.Create(formats, null, "<p>test</p>", null, null, null);
        await Assert.That(snapshot.Html).IsEqualTo("<p>test</p>");
    }

    [Test]
    public async Task ClipboardSnapshot_Create_StoresFiles()
    {
        List<ClipboardFormat> formats = [ClipboardFormat.Files];
        IReadOnlyList<string> files = ["C:\\file1.txt", "C:\\file2.txt"];
        ClipboardSnapshot snapshot = ClipboardSnapshot.Create(formats, null, null, null, null, files);
        await Assert.That(snapshot.Files).IsNotNull();
        await Assert.That(snapshot.Files!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClipboardSnapshot_Create_EmptyFormats_ProducesEmptyList()
    {
        ClipboardSnapshot snapshot = ClipboardSnapshot.Create([], null, null, null, null, null);
        await Assert.That(snapshot.Formats.Count).IsEqualTo(0);
    }

    // ── ClipboardChangedEventArgs ────────────────────────────────────

    [Test]
    public async Task ClipboardChangedEventArgs_Content_ReturnsConstructedContent()
    {
        ClipboardAvailability avail = new() { HasText = true };
        ClipboardContent content = ClipboardContent.FromWin32Availability(avail);
        var args = new ClipboardChangedEventArgs(content, 42u, ClipboardSource.OtherApp);
        await Assert.That(args.Content).IsEqualTo(content);
    }

    [Test]
    public async Task ClipboardChangedEventArgs_SequenceNumber_IsSet()
    {
        ClipboardContent content = ClipboardContent.FromWin32Availability(new ClipboardAvailability());
        var args = new ClipboardChangedEventArgs(content, 123u, ClipboardSource.OtherApp);
        await Assert.That(args.SequenceNumber).IsEqualTo(123u);
    }

    [Test]
    public async Task ClipboardChangedEventArgs_Source_IsSet()
    {
        ClipboardContent content = ClipboardContent.FromWin32Availability(new ClipboardAvailability());
        var args = new ClipboardChangedEventArgs(content, 0u, ClipboardSource.ThisApp);
        await Assert.That(args.Source).IsEqualTo(ClipboardSource.ThisApp);
    }
}

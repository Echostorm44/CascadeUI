using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Unit tests for the Linux platform wiring: Dispatcher triple-loop support,
/// DisplayServerDetector, LinuxClipboard utilities, LinuxFilePicker filter
/// building, and X11Window DPI parsing. All tests run without a display server.
/// </summary>
public class LinuxCoreTests
{
    // ── Dispatcher.IsInitialized ─────────────────────────────────────

    [Test]
    public async Task Dispatcher_IsInitialized_ReturnsFalseWhenAllLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        var savedLinux = Dispatcher.linuxLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop   = null;
        Dispatcher.linuxLoop   = null;

        try
        {
            await Assert.That(Dispatcher.IsInitialized).IsFalse();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop   = savedCocoa;
            Dispatcher.linuxLoop   = savedLinux;
        }
    }

    [Test]
    public async Task Dispatcher_LinuxLoop_IsNullByDefault()
    {
        var saved = Dispatcher.linuxLoop;
        Dispatcher.linuxLoop = null;

        try
        {
            await Assert.That(Dispatcher.LinuxLoop).IsEqualTo(null);
        }
        finally
        {
            Dispatcher.linuxLoop = saved;
        }
    }

    [Test]
    public async Task Dispatcher_IsOnUiThread_ReturnsFalseWhenAllLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        var savedLinux = Dispatcher.linuxLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop   = null;
        Dispatcher.linuxLoop   = null;

        try
        {
            await Assert.That(Dispatcher.IsOnUiThread).IsFalse();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop   = savedCocoa;
            Dispatcher.linuxLoop   = savedLinux;
        }
    }

    [Test]
    public async Task Dispatcher_Post_ThrowsWhenAllLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        var savedLinux = Dispatcher.linuxLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop   = null;
        Dispatcher.linuxLoop   = null;

        try
        {
            await Assert.That(() => Dispatcher.Post(() => { })).Throws<InvalidOperationException>();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop   = savedCocoa;
            Dispatcher.linuxLoop   = savedLinux;
        }
    }

    [Test]
    public async Task Dispatcher_InvokeAsync_ThrowsWhenAllLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        var savedLinux = Dispatcher.linuxLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop   = null;
        Dispatcher.linuxLoop   = null;

        try
        {
            await Assert.That(() => Dispatcher.InvokeAsync(() => { })).Throws<InvalidOperationException>();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop   = savedCocoa;
            Dispatcher.linuxLoop   = savedLinux;
        }
    }

    // ── DisplayServerDetector ────────────────────────────────────────

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeWayland_ReturnsWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display:     null,
            xdgSessionType: "wayland");

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeWayland_CaseInsensitive()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display:     null,
            xdgSessionType: "WAYLAND");

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeX11_ReturnsX11()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display:     null,
            xdgSessionType: "x11");

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    [Test]
    public async Task DetectFromEnvironment_WaylandDisplayEnvVar_ReturnsWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display:     null,
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_X11DisplayEnvVar_ReturnsX11()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display:     ":0",
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    [Test]
    public async Task DetectFromEnvironment_BothDisplayEnvVars_PrefersWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display:     ":0",
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_NoDisplays_ReturnsNone()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display:     null,
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.None);
    }

    [Test]
    public async Task DetectFromEnvironment_EmptyStrings_ReturnsNone()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "",
            x11Display:     "",
            xdgSessionType: "");

        await Assert.That(result).IsEqualTo(DisplayServer.None);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeOverridesWaylandDisplay()
    {
        // XDG_SESSION_TYPE=x11 should override WAYLAND_DISPLAY being set.
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display:     ":0",
            xdgSessionType: "x11");

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    [Test]
    public async Task AreWindowButtonsOnLeft_ReturnsFalse()
    {
        // Modern GNOME and KDE both use right-side window buttons.
        bool result = DisplayServerDetector.AreWindowButtonsOnLeft();
        await Assert.That(result).IsFalse();
    }

    // ── LinuxClipboard.ParseUriList ──────────────────────────────────

    [Test]
    public async Task ParseUriList_SingleFileUri_ReturnsOnePath()
    {
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList("file:///home/user/doc.txt\r\n");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("/home/user/doc.txt");
    }

    [Test]
    public async Task ParseUriList_MultipleFileUris_ReturnsAllPaths()
    {
        string uriList = "file:///home/user/a.txt\r\nfile:///home/user/b.txt\r\n";
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList(uriList);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo("/home/user/a.txt");
        await Assert.That(result[1]).IsEqualTo("/home/user/b.txt");
    }

    [Test]
    public async Task ParseUriList_CommentLinesSkipped()
    {
        string uriList = "# This is a comment\r\nfile:///tmp/file.txt\r\n";
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList(uriList);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("/tmp/file.txt");
    }

    [Test]
    public async Task ParseUriList_EmptyInput_ReturnsEmptyList()
    {
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList("");
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseUriList_OnlyComments_ReturnsEmptyList()
    {
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList("# comment1\n# comment2\n");
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseUriList_NonFileUri_IncludedAsIs()
    {
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList("https://example.com\r\n");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("https://example.com");
    }

    [Test]
    public async Task ParseUriList_UrlEncodedPath_DecodesCorrectly()
    {
        string uriList = "file:///home/user/my%20file.txt\r\n";
        IReadOnlyList<string> result = LinuxClipboard.ParseUriList(uriList);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("/home/user/my file.txt");
    }

    // ── LinuxClipboard.BuildUriList ──────────────────────────────────

    [Test]
    public async Task BuildUriList_SinglePath_ProducesFileUri()
    {
        string result = LinuxClipboard.BuildUriList(["/home/user/doc.txt"]);
        await Assert.That(result).Contains("file://");
        await Assert.That(result).Contains("home%2Fuser%2Fdoc.txt");
    }

    [Test]
    public async Task BuildUriList_MultiplePaths_EachOnOwnLine()
    {
        string result = LinuxClipboard.BuildUriList(["/tmp/a.txt", "/tmp/b.txt"]);
        string[] lines = result.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(2);
    }

    [Test]
    public async Task BuildUriList_EmptyList_ReturnsEmptyString()
    {
        string result = LinuxClipboard.BuildUriList([]);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task BuildUriList_RoundTrip_PreservesPath()
    {
        string[] originalPaths = ["/home/user/document.txt", "/tmp/file with spaces.txt"];
        string uriList = LinuxClipboard.BuildUriList(originalPaths);
        IReadOnlyList<string> roundTripped = LinuxClipboard.ParseUriList(uriList);

        await Assert.That(roundTripped.Count).IsEqualTo(2);
        await Assert.That(roundTripped[0]).IsEqualTo("/home/user/document.txt");
        await Assert.That(roundTripped[1]).IsEqualTo("/tmp/file with spaces.txt");
    }

    // ── LinuxFilePicker.BuildZenityFilter ────────────────────────────

    [Test]
    public async Task BuildZenityFilter_NullFilters_ReturnsEmpty()
    {
        string result = LinuxFilePicker.BuildZenityFilter(null);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task BuildZenityFilter_EmptyFilters_ReturnsEmpty()
    {
        string result = LinuxFilePicker.BuildZenityFilter([]);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task BuildZenityFilter_SingleFilter_ContainsLabelAndPattern()
    {
        var filters = new List<FileFilter>
        {
            new FileFilter("Text Files", "*.txt")
        };

        string result = LinuxFilePicker.BuildZenityFilter(filters);

        await Assert.That(result).Contains("Text Files");
        await Assert.That(result).Contains("*.txt");
        await Assert.That(result).Contains("--file-filter=");
    }

    [Test]
    public async Task BuildZenityFilter_MultipleFilters_ContainsAll()
    {
        var filters = new List<FileFilter>
        {
            new FileFilter("Text", "*.txt"),
            new FileFilter("Images", "*.png", "*.jpg")
        };

        string result = LinuxFilePicker.BuildZenityFilter(filters);

        await Assert.That(result).Contains("Text");
        await Assert.That(result).Contains("Images");
        await Assert.That(result).Contains("*.txt");
        await Assert.That(result).Contains("*.png");
        await Assert.That(result).Contains("*.jpg");
    }

    // ── LinuxFilePicker.BuildKdialogFilter ───────────────────────────

    [Test]
    public async Task BuildKdialogFilter_NullFilters_ReturnsEmpty()
    {
        string result = LinuxFilePicker.BuildKdialogFilter(null);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task BuildKdialogFilter_EmptyFilters_ReturnsEmpty()
    {
        string result = LinuxFilePicker.BuildKdialogFilter([]);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task BuildKdialogFilter_SingleFilter_ContainsPipeAndLabel()
    {
        var filters = new List<FileFilter>
        {
            new FileFilter("Documents", "*.doc", "*.docx")
        };

        string result = LinuxFilePicker.BuildKdialogFilter(filters);

        await Assert.That(result).Contains("|");
        await Assert.That(result).Contains("Documents");
        await Assert.That(result).Contains("*.doc");
        await Assert.That(result).Contains("*.docx");
    }

    [Test]
    public async Task BuildKdialogFilter_MultipleFilters_SeparatedByNewline()
    {
        var filters = new List<FileFilter>
        {
            new FileFilter("Text", "*.txt"),
            new FileFilter("Csv",  "*.csv")
        };

        string result = LinuxFilePicker.BuildKdialogFilter(filters);

        await Assert.That(result).Contains("\n");
        string[] lines = result.Split('\n');
        await Assert.That(lines.Length).IsEqualTo(2);
    }

    // ── X11Window.ParseXftDpi ────────────────────────────────────────

    [Test]
    public async Task ParseXftDpi_StandardDpiLine_Returns1xScale()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t96");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ParseXftDpi_HighDpiLine_Returns2xScale()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t192");
        await Assert.That(scale).IsEqualTo(2.0f);
    }

    [Test]
    public async Task ParseXftDpi_HiDpi144_Returns1Point5xScale()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t144");
        // 144 / 96 = 1.5
        await Assert.That(scale).IsEqualTo(1.5f);
    }

    [Test]
    public async Task ParseXftDpi_NoXftDpiLine_Returns1xScale()
    {
        float scale = X11Window.ParseXftDpi("Xft.rgba:\tnone\nXft.antialias:\t1");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ParseXftDpi_EmptyString_Returns1xScale()
    {
        float scale = X11Window.ParseXftDpi("");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    // ── ClipboardContent.FromLinuxAvailability ───────────────────────

    [Test]
    public async Task ClipboardContent_FromLinuxAvailability_SetsHasText()
    {
        var avail = new ClipboardAvailability { HasText = true, HasHtml = false, HasImage = false, HasFiles = false };
        ClipboardContent content = ClipboardContent.FromLinuxAvailability(avail);
        await Assert.That(content.HasText).IsTrue();
        await Assert.That(content.HasHtml).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_FromLinuxAvailability_SetsHasHtml()
    {
        var avail = new ClipboardAvailability { HasText = false, HasHtml = true, HasImage = false, HasFiles = false };
        ClipboardContent content = ClipboardContent.FromLinuxAvailability(avail);
        await Assert.That(content.HasHtml).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_FromLinuxAvailability_SetsHasFiles()
    {
        var avail = new ClipboardAvailability { HasText = false, HasHtml = false, HasImage = false, HasFiles = true };
        ClipboardContent content = ClipboardContent.FromLinuxAvailability(avail);
        await Assert.That(content.HasFiles).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_FromLinuxAvailability_HasRtfAlwaysFalse()
    {
        var avail = new ClipboardAvailability { HasText = true, HasHtml = true, HasImage = true, HasFiles = true };
        ClipboardContent content = ClipboardContent.FromLinuxAvailability(avail);
        await Assert.That(content.HasRtf).IsFalse();
    }
}

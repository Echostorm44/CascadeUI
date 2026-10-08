using System.Text;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Win32 clipboard: DIB decoding and CF_HTML encoding (pure), then round trips through the real
/// clipboard. The real-clipboard tests save whatever the user had on the clipboard and put it back.
/// </summary>
[NotInParallel("Clipboard")]
public class Win32ClipboardRoundTripTests
{
    [Test]
    public async Task CfHtml_RoundTripsNonAscii()
    {
        const string fragment = "<b>héllo — 日本</b>";
        byte[] cfHtml = Win32Clipboard.BuildCfHtml(fragment);

        // The header offsets are byte offsets; slicing the decoded string with them used to break here.
        await Assert.That(Win32Clipboard.ExtractHtmlFragment(cfHtml)).IsEqualTo(fragment);
    }

    [Test]
    public async Task Dib_32bppBitfields_UsesMasks()
    {
        // BITMAPINFOHEADER + 3 masks (R, G, B in the high bytes), 1×1 top-down.
        byte[] dib = new byte[40 + 12 + 4];
        WriteHeader(dib, width: 1, height: -1, bitCount: 32, compression: 3);
        BitConverter.GetBytes(0xFF000000u).CopyTo(dib, 40);
        BitConverter.GetBytes(0x00FF0000u).CopyTo(dib, 44);
        BitConverter.GetBytes(0x0000FF00u).CopyTo(dib, 48);
        BitConverter.GetBytes(0x11223344u).CopyTo(dib, 52);

        var image = Win32Clipboard.DecodeDib(dib)!;

        await Assert.That(image.Pixels).IsEquivalentTo(new byte[] { 0x11, 0x22, 0x33, 255 });
    }

    [Test]
    public async Task Dib_32bppRgbWithZeroAlpha_IsOpaque()
    {
        byte[] dib = new byte[40 + 8];
        WriteHeader(dib, width: 2, height: 1, bitCount: 32, compression: 0);
        dib[40] = 10; dib[41] = 20; dib[42] = 30;   // BGRA, alpha 0
        dib[44] = 40; dib[45] = 50; dib[46] = 60;

        var image = Win32Clipboard.DecodeDib(dib)!;

        await Assert.That(image.Pixels).IsEquivalentTo(new byte[] { 30, 20, 10, 255, 60, 50, 40, 255 });
    }

    [Test]
    public async Task Dib_V5_KeepsAlpha_AndFlipsBottomUp()
    {
        byte[] dib = new byte[124 + 8];
        WriteHeader(dib, width: 1, height: 2, bitCount: 32, compression: 3, headerSize: 124);
        BitConverter.GetBytes(0x00FF0000u).CopyTo(dib, 40);
        BitConverter.GetBytes(0x0000FF00u).CopyTo(dib, 44);
        BitConverter.GetBytes(0x000000FFu).CopyTo(dib, 48);
        BitConverter.GetBytes(0xFF000000u).CopyTo(dib, 52);
        // Bottom-up: the first row in memory is the bottom one.
        dib[124] = 1; dib[125] = 2; dib[126] = 3; dib[127] = 128;
        dib[128] = 4; dib[129] = 5; dib[130] = 6; dib[131] = 64;

        var image = Win32Clipboard.DecodeDib(dib)!;

        await Assert.That(image.Pixels).IsEquivalentTo(new byte[] { 6, 5, 4, 64, 3, 2, 1, 128 });
    }

    [Test]
    public async Task Write_AllFormats_ThenCaptureAndRestoreRaw()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var saved = Clipboard.CaptureRaw();
        try
        {
            byte[] rgba = [255, 0, 0, 128, 0, 255, 0, 255, 0, 0, 255, 0, 10, 20, 30, 40];
            var image = new ImageData { Pixels = rgba, Width = 2, Height = 2, Stride = 8 };
            bool written = await Clipboard.WriteAsync(new ClipboardContent
            {
                Text = "plain ✓",
                Html = "<i>rich ✓</i>",
                Rtf = @"{\rtf1 rich}",
                Image = image,
            });
            await Assert.That(written).IsTrue();
            await Assert.That(Win32Clipboard.IsOwnChange(Win32Clipboard.GetSequenceNumber())).IsTrue();

            await Assert.That(Win32Clipboard.GetText()).IsEqualTo("plain ✓");
            await Assert.That(Win32Clipboard.GetHtml()).IsEqualTo("<i>rich ✓</i>");
            await Assert.That(Win32Clipboard.GetRtf()).IsEqualTo(@"{\rtf1 rich}");
            await Assert.That(Win32Clipboard.GetImage()!.Pixels).IsEquivalentTo(rgba); // exact, alpha included (PNG)

            var raw = Clipboard.CaptureRaw()!;
            string[] names = [.. raw.Formats.Select(f => f.Name)];
            foreach (string expected in new[] { "CF_UNICODETEXT", "HTML Format", "Rich Text Format", "CF_DIBV5", "PNG" })
            {
                await Assert.That(names).Contains(expected);
            }
            await Assert.That(names).DoesNotContain("CF_TEXT"); // synthesized from Unicode text

            // Restore from the raw capture: same bytes back.
            await Clipboard.WriteTextAsync("something else");
            await Assert.That(Clipboard.WriteRaw(raw)).IsTrue();
            var again = Clipboard.CaptureRaw()!;
            foreach (var format in raw.Formats)
            {
                var copy = again.Find(format.Name);
                await Assert.That(copy).IsNotNull();
                await Assert.That(copy!.Data.ToArray()).IsEquivalentTo(format.Data.ToArray());
            }
        }
        finally
        {
            if (saved is not null)
            {
                Clipboard.WriteRaw(saved);
            }
        }
    }

    [Test]
    public async Task Capture_AnsiOnlyText_KeepsItAndTheUnicodeText()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var saved = Clipboard.CaptureRaw();
        try
        {
            // An app that puts only CF_TEXT on the clipboard (OneCommander's "Copy as Path"): Windows
            // lists it first and synthesizes CF_UNICODETEXT after it. Keeping only the first text format
            // dropped the Unicode text, so consumers that read it saw no text at all.
            byte[] ansi = Encoding.ASCII.GetBytes(@"C:\probe\path.txt" + "\0");
            await Assert.That(Clipboard.WriteRaw(new ClipboardRawSnapshot([new ClipboardRawFormat(1, "CF_TEXT", ansi)]))).IsTrue();

            var raw = Clipboard.CaptureRaw()!;
            string[] names = [.. raw.Formats.Select(f => f.Name)];

            await Assert.That(raw.Find("CF_TEXT")!.Data.ToArray()).IsEquivalentTo(ansi); // the original, byte for byte
            await Assert.That(Encoding.Unicode.GetString(raw.Find("CF_UNICODETEXT")!.Data.Span).TrimEnd('\0'))
                .IsEqualTo(@"C:\probe\path.txt");
            await Assert.That(names).DoesNotContain("CF_OEMTEXT"); // synthesized from CF_TEXT
        }
        finally
        {
            if (saved is not null)
            {
                Clipboard.WriteRaw(saved);
            }
        }
    }

    [Test]
    public async Task ExclusionMarkers_AreDetected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var saved = Clipboard.CaptureRaw();
        try
        {
            static ClipboardRawSnapshot With(string marker, byte[] data) => new(
            [
                new ClipboardRawFormat(13, "CF_UNICODETEXT", Encoding.Unicode.GetBytes("secret\0")),
                new ClipboardRawFormat(0xC000, marker, data),
            ]);

            Clipboard.WriteRaw(With("ExcludeClipboardContentFromMonitorProcessing", [0]));
            await Assert.That(Win32Clipboard.IsExcludedFromHistory()).IsTrue();

            Clipboard.WriteRaw(With("CanIncludeInClipboardHistory", BitConverter.GetBytes(0u)));
            await Assert.That(Win32Clipboard.IsExcludedFromHistory()).IsTrue();

            Clipboard.WriteRaw(With("CanIncludeInClipboardHistory", BitConverter.GetBytes(1u)));
            await Assert.That(Win32Clipboard.IsExcludedFromHistory()).IsFalse();
        }
        finally
        {
            if (saved is not null)
            {
                Clipboard.WriteRaw(saved);
            }
        }
    }

    private static void WriteHeader(byte[] dib, int width, int height, short bitCount, uint compression, int headerSize = 40)
    {
        BitConverter.GetBytes(headerSize).CopyTo(dib, 0);
        BitConverter.GetBytes(width).CopyTo(dib, 4);
        BitConverter.GetBytes(height).CopyTo(dib, 8);
        BitConverter.GetBytes((short)1).CopyTo(dib, 12);
        BitConverter.GetBytes(bitCount).CopyTo(dib, 14);
        BitConverter.GetBytes(compression).CopyTo(dib, 16);
    }
}

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for the Linux platform layer. These tests verify display server
/// detection logic, X11 key mapping, X11 modifier mapping, clipboard format
/// handling, and file picker filter building — all without requiring an actual
/// X11 or Wayland display.
/// </summary>
public class LinuxWindowTests
{
    // ── Display Server Detection ─────────────────────────────────────

    [Test]
    public async Task DetectFromEnvironment_WaylandDisplay_ReturnsWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display: null,
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_X11Display_ReturnsX11()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display: ":0",
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    [Test]
    public async Task DetectFromEnvironment_BothDisplays_PrefersWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display: ":0",
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_NoDisplays_ReturnsNone()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display: null,
            xdgSessionType: null);

        await Assert.That(result).IsEqualTo(DisplayServer.None);
    }

    [Test]
    public async Task DetectFromEnvironment_EmptyStrings_ReturnsNone()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "",
            x11Display: "",
            xdgSessionType: "");

        await Assert.That(result).IsEqualTo(DisplayServer.None);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeWayland_ReturnsWayland()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display: null,
            xdgSessionType: "wayland");

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeX11_ReturnsX11()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display: null,
            xdgSessionType: "x11");

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeCaseInsensitive()
    {
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: null,
            x11Display: null,
            xdgSessionType: "WAYLAND");

        await Assert.That(result).IsEqualTo(DisplayServer.Wayland);
    }

    [Test]
    public async Task DetectFromEnvironment_XdgSessionTypeOverridesDisplayVars()
    {
        // XDG_SESSION_TYPE=x11 should win even if WAYLAND_DISPLAY is set.
        DisplayServer result = DisplayServerDetector.DetectFromEnvironment(
            waylandDisplay: "wayland-0",
            x11Display: ":0",
            xdgSessionType: "x11");

        await Assert.That(result).IsEqualTo(DisplayServer.X11);
    }

    // ── Window Buttons Placement ─────────────────────────────────────

    [Test]
    public async Task AreWindowButtonsOnLeft_ReturnsFalse()
    {
        // Modern GNOME and KDE both use right-side buttons.
        bool result = DisplayServerDetector.AreWindowButtonsOnLeft();
        await Assert.That(result).IsFalse();
    }

    // ── X11 KeySym Mapping ───────────────────────────────────────────

    [Test]
    public async Task MapX11KeySym_LowercaseA_ReturnsKeyA()
    {
        Key result = LinuxInput.MapX11KeySym(0x61);
        await Assert.That(result).IsEqualTo(Key.A);
    }

    [Test]
    public async Task MapX11KeySym_UppercaseA_ReturnsKeyA()
    {
        Key result = LinuxInput.MapX11KeySym(0x41);
        await Assert.That(result).IsEqualTo(Key.A);
    }

    [Test]
    public async Task MapX11KeySym_LowercaseZ_ReturnsKeyZ()
    {
        Key result = LinuxInput.MapX11KeySym(0x7A);
        await Assert.That(result).IsEqualTo(Key.Z);
    }

    [Test]
    public async Task MapX11KeySym_Digit0_ReturnsD0()
    {
        Key result = LinuxInput.MapX11KeySym(0x30);
        await Assert.That(result).IsEqualTo(Key.D0);
    }

    [Test]
    public async Task MapX11KeySym_Digit9_ReturnsD9()
    {
        Key result = LinuxInput.MapX11KeySym(0x39);
        await Assert.That(result).IsEqualTo(Key.D9);
    }

    [Test]
    public async Task MapX11KeySym_F1_ReturnsF1()
    {
        Key result = LinuxInput.MapX11KeySym(0xFFBE);
        await Assert.That(result).IsEqualTo(Key.F1);
    }

    [Test]
    public async Task MapX11KeySym_F12_ReturnsF12()
    {
        Key result = LinuxInput.MapX11KeySym(0xFFC9);
        await Assert.That(result).IsEqualTo(Key.F12);
    }

    [Test]
    public async Task MapX11KeySym_Escape_ReturnsEscape()
    {
        Key result = LinuxInput.MapX11KeySym(0xFF1B);
        await Assert.That(result).IsEqualTo(Key.Escape);
    }

    [Test]
    public async Task MapX11KeySym_Enter_ReturnsEnter()
    {
        Key result = LinuxInput.MapX11KeySym(0xFF0D);
        await Assert.That(result).IsEqualTo(Key.Enter);
    }

    [Test]
    public async Task MapX11KeySym_Space_ReturnsSpace()
    {
        Key result = LinuxInput.MapX11KeySym(0x0020);
        await Assert.That(result).IsEqualTo(Key.Space);
    }

    [Test]
    public async Task MapX11KeySym_ArrowKeys_ReturnCorrectKeys()
    {
        await Assert.That(LinuxInput.MapX11KeySym(0xFF51)).IsEqualTo(Key.Left);
        await Assert.That(LinuxInput.MapX11KeySym(0xFF52)).IsEqualTo(Key.Up);
        await Assert.That(LinuxInput.MapX11KeySym(0xFF53)).IsEqualTo(Key.Right);
        await Assert.That(LinuxInput.MapX11KeySym(0xFF54)).IsEqualTo(Key.Down);
    }

    [Test]
    public async Task MapX11KeySym_NumPad0_ReturnsNumPad0()
    {
        Key result = LinuxInput.MapX11KeySym(0xFFB0);
        await Assert.That(result).IsEqualTo(Key.NumPad0);
    }

    [Test]
    public async Task MapX11KeySym_NumPad9_ReturnsNumPad9()
    {
        Key result = LinuxInput.MapX11KeySym(0xFFB9);
        await Assert.That(result).IsEqualTo(Key.NumPad9);
    }

    [Test]
    public async Task MapX11KeySym_NumPadEnter_ReturnsNumPadEnter()
    {
        Key result = LinuxInput.MapX11KeySym(0xFF8D);
        await Assert.That(result).IsEqualTo(Key.NumPadEnter);
    }

    [Test]
    public async Task MapX11KeySym_UnknownKey_ReturnsNone()
    {
        Key result = LinuxInput.MapX11KeySym(0xDEAD);
        await Assert.That(result).IsEqualTo(Key.None);
    }

    // ── Reverse KeySym Mapping ───────────────────────────────────────

    [Test]
    public async Task MapKeyToX11KeySym_RoundTrips_Letters()
    {
        for (long ks = 0x61; ks <= 0x7A; ks++)
        {
            Key key = LinuxInput.MapX11KeySym((nint)ks);
            long backToKs = LinuxInput.MapKeyToX11KeySym(key);
            await Assert.That(backToKs).IsEqualTo(ks);
        }
    }

    [Test]
    public async Task MapKeyToX11KeySym_RoundTrips_Digits()
    {
        for (long ks = 0x30; ks <= 0x39; ks++)
        {
            Key key = LinuxInput.MapX11KeySym((nint)ks);
            long backToKs = LinuxInput.MapKeyToX11KeySym(key);
            await Assert.That(backToKs).IsEqualTo(ks);
        }
    }

    [Test]
    public async Task MapKeyToX11KeySym_RoundTrips_FunctionKeys()
    {
        for (long ks = 0xFFBE; ks <= 0xFFC9; ks++)
        {
            Key key = LinuxInput.MapX11KeySym((nint)ks);
            long backToKs = LinuxInput.MapKeyToX11KeySym(key);
            await Assert.That(backToKs).IsEqualTo(ks);
        }
    }

    [Test]
    public async Task MapKeyToX11KeySym_None_ReturnsZero()
    {
        long result = LinuxInput.MapKeyToX11KeySym(Key.None);
        await Assert.That(result).IsEqualTo(0L);
    }

    // ── X11 Modifier Mapping ─────────────────────────────────────────

    [Test]
    public async Task MapX11Modifiers_Control_ReturnsCtrl()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(X11Interop.ControlMask);
        await Assert.That(result).IsEqualTo(ModifierKeys.Ctrl);
    }

    [Test]
    public async Task MapX11Modifiers_Shift_ReturnsShift()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(X11Interop.ShiftMask);
        await Assert.That(result).IsEqualTo(ModifierKeys.Shift);
    }

    [Test]
    public async Task MapX11Modifiers_Alt_ReturnsAlt()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(X11Interop.Mod1Mask);
        await Assert.That(result).IsEqualTo(ModifierKeys.Alt);
    }

    [Test]
    public async Task MapX11Modifiers_Super_ReturnsMeta()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(X11Interop.Mod4Mask);
        await Assert.That(result).IsEqualTo(ModifierKeys.Meta);
    }

    [Test]
    public async Task MapX11Modifiers_NoModifiers_ReturnsNone()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(0);
        await Assert.That(result).IsEqualTo(ModifierKeys.None);
    }

    [Test]
    public async Task MapX11Modifiers_Combined_CtrlShift()
    {
        ModifierKeys result = LinuxInput.MapX11Modifiers(X11Interop.ControlMask | X11Interop.ShiftMask);
        await Assert.That(result).IsEqualTo(ModifierKeys.Ctrl | ModifierKeys.Shift);
    }

    [Test]
    public async Task MapModifiersToX11_RoundTrips()
    {
        ModifierKeys original = ModifierKeys.Ctrl | ModifierKeys.Alt;
        uint x11Mods = LinuxInput.MapModifiersToX11(original);
        ModifierKeys roundTripped = LinuxInput.MapX11Modifiers(x11Mods);
        await Assert.That(roundTripped).IsEqualTo(original);
    }

    // ── X11 Button Mapping ───────────────────────────────────────────

    [Test]
    public async Task MapX11Button_Button1_ReturnsLeft()
    {
        NativeMouseButton result = LinuxInput.MapX11Button(X11Interop.Button1);
        await Assert.That(result).IsEqualTo(NativeMouseButton.Left);
    }

    [Test]
    public async Task MapX11Button_Button2_ReturnsMiddle()
    {
        NativeMouseButton result = LinuxInput.MapX11Button(X11Interop.Button2);
        await Assert.That(result).IsEqualTo(NativeMouseButton.Middle);
    }

    [Test]
    public async Task MapX11Button_Button3_ReturnsRight()
    {
        NativeMouseButton result = LinuxInput.MapX11Button(X11Interop.Button3);
        await Assert.That(result).IsEqualTo(NativeMouseButton.Right);
    }

    [Test]
    public async Task MapX11Button_ScrollButton_ReturnsNone()
    {
        NativeMouseButton result = LinuxInput.MapX11Button(X11Interop.Button4);
        await Assert.That(result).IsEqualTo(NativeMouseButton.None);
    }

    // ── Clipboard Format Handling ────────────────────────────────────

    [Test]
    public async Task ClipboardAvailability_DefaultsToFalse()
    {
        ClipboardAvailability availability = new();
        await Assert.That(availability.HasText).IsFalse();
        await Assert.That(availability.HasHtml).IsFalse();
        await Assert.That(availability.HasFiles).IsFalse();
        await Assert.That(availability.HasImage).IsFalse();
    }

    [Test]
    public async Task GetAtomNameForFormat_Text_ReturnsUtf8String()
    {
        string name = LinuxClipboard.GetAtomNameForFormat(ClipboardFormat.Text);
        await Assert.That(name).IsEqualTo("UTF8_STRING");
    }

    [Test]
    public async Task GetAtomNameForFormat_Html_ReturnsTextHtml()
    {
        string name = LinuxClipboard.GetAtomNameForFormat(ClipboardFormat.Html);
        await Assert.That(name).IsEqualTo("text/html");
    }

    [Test]
    public async Task GetAtomNameForFormat_Files_ReturnsUriList()
    {
        string name = LinuxClipboard.GetAtomNameForFormat(ClipboardFormat.Files);
        await Assert.That(name).IsEqualTo("text/uri-list");
    }

    [Test]
    public async Task GetMimeTypeForFormat_Text_ReturnsUtf8Mime()
    {
        string mime = LinuxClipboard.GetMimeTypeForFormat(ClipboardFormat.Text);
        await Assert.That(mime).IsEqualTo("text/plain;charset=utf-8");
    }

    [Test]
    public async Task GetMimeTypeForFormat_Image_ReturnsPng()
    {
        string mime = LinuxClipboard.GetMimeTypeForFormat(ClipboardFormat.Image);
        await Assert.That(mime).IsEqualTo("image/png");
    }

    [Test]
    public async Task GetMimeTypeForFormat_Custom_ReturnsApplicationPrefix()
    {
        string mime = LinuxClipboard.GetMimeTypeForFormat(ClipboardFormat.Custom("MyFormat"));
        await Assert.That(mime).IsEqualTo("application/x-cascade-MyFormat");
    }

    // ── URI List Parsing ─────────────────────────────────────────────

    [Test]
    public async Task ParseUriList_SingleFile_ReturnsPath()
    {
        IReadOnlyList<string> paths = LinuxClipboard.ParseUriList("file:///home/user/doc.txt\r\n");
        await Assert.That(paths.Count).IsEqualTo(1);
        await Assert.That(paths[0]).IsEqualTo("/home/user/doc.txt");
    }

    [Test]
    public async Task ParseUriList_MultipleFiles_ReturnsAllPaths()
    {
        string uriList = "file:///home/user/a.txt\r\nfile:///home/user/b.txt\r\n";
        IReadOnlyList<string> paths = LinuxClipboard.ParseUriList(uriList);
        await Assert.That(paths.Count).IsEqualTo(2);
        await Assert.That(paths[0]).IsEqualTo("/home/user/a.txt");
        await Assert.That(paths[1]).IsEqualTo("/home/user/b.txt");
    }

    [Test]
    public async Task ParseUriList_CommentsSkipped()
    {
        string uriList = "# comment\r\nfile:///home/user/doc.txt\r\n";
        IReadOnlyList<string> paths = LinuxClipboard.ParseUriList(uriList);
        await Assert.That(paths.Count).IsEqualTo(1);
        await Assert.That(paths[0]).IsEqualTo("/home/user/doc.txt");
    }

    [Test]
    public async Task ParseUriList_EmptyInput_ReturnsEmpty()
    {
        IReadOnlyList<string> paths = LinuxClipboard.ParseUriList("");
        await Assert.That(paths.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseUriList_EncodedSpaces_Decoded()
    {
        IReadOnlyList<string> paths = LinuxClipboard.ParseUriList("file:///home/user/my%20doc.txt\r\n");
        await Assert.That(paths.Count).IsEqualTo(1);
        await Assert.That(paths[0]).IsEqualTo("/home/user/my doc.txt");
    }

    [Test]
    public async Task BuildUriList_ProducesCorrectFormat()
    {
        string result = LinuxClipboard.BuildUriList(["/home/user/doc.txt"]);
        await Assert.That(result).Contains("file://");
        await Assert.That(result).Contains("doc.txt");
    }

    // ── File Picker Filter Building ──────────────────────────────────

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
    public async Task BuildZenityFilter_SingleFilter_ContainsLabel()
    {
        FileFilter[] filters = [new FileFilter("Images", "*.png", "*.jpg")];
        string result = LinuxFilePicker.BuildZenityFilter(filters);
        await Assert.That(result).Contains("Images");
        await Assert.That(result).Contains("*.png");
        await Assert.That(result).Contains("*.jpg");
    }

    [Test]
    public async Task BuildKdialogFilter_SingleFilter_ContainsPatternAndLabel()
    {
        FileFilter[] filters = [new FileFilter("Documents", "*.pdf")];
        string result = LinuxFilePicker.BuildKdialogFilter(filters);
        await Assert.That(result).Contains("*.pdf");
        await Assert.That(result).Contains("Documents");
    }

    [Test]
    public async Task BuildKdialogFilter_MultipleFilters_SeparatedByNewline()
    {
        FileFilter[] filters =
        [
            new FileFilter("Images", "*.png"),
            new FileFilter("All Files", "*.*")
        ];
        string result = LinuxFilePicker.BuildKdialogFilter(filters);
        await Assert.That(result).Contains("\n");
    }

    // ── X11 DPI Detection ────────────────────────────────────────────

    [Test]
    public async Task ParseXftDpi_Standard96_ReturnsScale1()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t96\n");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ParseXftDpi_HiDpi192_ReturnsScale2()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t192\n");
        await Assert.That(scale).IsEqualTo(2.0f);
    }

    [Test]
    public async Task ParseXftDpi_144Dpi_ReturnsScale1Point5()
    {
        float scale = X11Window.ParseXftDpi("Xft.dpi:\t144\n");
        await Assert.That(scale).IsEqualTo(1.5f);
    }

    [Test]
    public async Task ParseXftDpi_MissingXftDpi_ReturnsScale1()
    {
        float scale = X11Window.ParseXftDpi("Xft.antialias:\t1\n");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ParseXftDpi_EmptyString_ReturnsScale1()
    {
        float scale = X11Window.ParseXftDpi("");
        await Assert.That(scale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ParseXftDpi_MultipleResources_FindsDpi()
    {
        string resources = "Xft.antialias:\t1\nXft.hinting:\t1\nXft.dpi:\t120\nXft.rgba:\trgb\n";
        float scale = X11Window.ParseXftDpi(resources);
        await Assert.That(scale).IsEqualTo(120.0f / 96.0f);
    }

    // ── Event Loop ───────────────────────────────────────────────────

    [Test]
    public async Task EventLoop_Constructor_SetsMainThread()
    {
        LinuxEventLoop loop = new(DisplayServer.X11);
        await Assert.That(loop.IsOnMainThread).IsTrue();
        loop.Dispose();
    }

    [Test]
    public async Task EventLoop_Constructor_Wayland_SetsMainThread()
    {
        LinuxEventLoop loop = new(DisplayServer.Wayland);
        await Assert.That(loop.IsOnMainThread).IsTrue();
        loop.Dispose();
    }
}

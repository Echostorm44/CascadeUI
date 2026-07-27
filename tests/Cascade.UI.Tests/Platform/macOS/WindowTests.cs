using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

#pragma warning disable TUnitAssertions0005 // Tests intentionally verify compile-time constant values

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for the macOS Cocoa platform layer. These tests verify key code
/// mapping, modifier flag translation, clipboard format detection, and ObjC
/// selector registration logic without requiring an actual macOS runtime.
/// </summary>
public class CocoaWindowTests
{
    // ── Key Code Mapping ─────────────────────────────────────────────

    [Test]
    public async Task MapKeyCode_LetterA_ReturnsKeyA()
    {
        Key result = CocoaInput.MapKeyCode(0x00);
        await Assert.That(result).IsEqualTo(Key.A);
    }

    [Test]
    public async Task MapKeyCode_LetterZ_ReturnsKeyZ()
    {
        Key result = CocoaInput.MapKeyCode(0x06);
        await Assert.That(result).IsEqualTo(Key.Z);
    }

    [Test]
    public async Task MapKeyCode_LetterQ_ReturnsKeyQ()
    {
        Key result = CocoaInput.MapKeyCode(0x0C);
        await Assert.That(result).IsEqualTo(Key.Q);
    }

    [Test]
    public async Task MapKeyCode_Digit1_ReturnsD1()
    {
        Key result = CocoaInput.MapKeyCode(0x12);
        await Assert.That(result).IsEqualTo(Key.D1);
    }

    [Test]
    public async Task MapKeyCode_Digit0_ReturnsD0()
    {
        Key result = CocoaInput.MapKeyCode(0x1D);
        await Assert.That(result).IsEqualTo(Key.D0);
    }

    [Test]
    public async Task MapKeyCode_F1_ReturnsF1()
    {
        Key result = CocoaInput.MapKeyCode(0x7A);
        await Assert.That(result).IsEqualTo(Key.F1);
    }

    [Test]
    public async Task MapKeyCode_F12_ReturnsF12()
    {
        Key result = CocoaInput.MapKeyCode(0x6F);
        await Assert.That(result).IsEqualTo(Key.F12);
    }

    [Test]
    public async Task MapKeyCode_Escape_ReturnsEscape()
    {
        Key result = CocoaInput.MapKeyCode(0x35);
        await Assert.That(result).IsEqualTo(Key.Escape);
    }

    [Test]
    public async Task MapKeyCode_Return_ReturnsEnter()
    {
        Key result = CocoaInput.MapKeyCode(0x24);
        await Assert.That(result).IsEqualTo(Key.Enter);
    }

    [Test]
    public async Task MapKeyCode_NumpadEnter_ReturnsEnter()
    {
        Key result = CocoaInput.MapKeyCode(0x4C);
        await Assert.That(result).IsEqualTo(Key.Enter);
    }

    [Test]
    public async Task MapKeyCode_Space_ReturnsSpace()
    {
        Key result = CocoaInput.MapKeyCode(0x31);
        await Assert.That(result).IsEqualTo(Key.Space);
    }

    [Test]
    public async Task MapKeyCode_Tab_ReturnsTab()
    {
        Key result = CocoaInput.MapKeyCode(0x30);
        await Assert.That(result).IsEqualTo(Key.Tab);
    }

    [Test]
    public async Task MapKeyCode_Delete_ReturnsBackspace()
    {
        // macOS key code 0x33 is the Delete key (backspace)
        Key result = CocoaInput.MapKeyCode(0x33);
        await Assert.That(result).IsEqualTo(Key.Backspace);
    }

    [Test]
    public async Task MapKeyCode_ForwardDelete_ReturnsDelete()
    {
        Key result = CocoaInput.MapKeyCode(0x75);
        await Assert.That(result).IsEqualTo(Key.Delete);
    }

    [Test]
    public async Task MapKeyCode_ArrowKeys_ReturnCorrectKeys()
    {
        await Assert.That(CocoaInput.MapKeyCode(0x7B)).IsEqualTo(Key.Left);
        await Assert.That(CocoaInput.MapKeyCode(0x7C)).IsEqualTo(Key.Right);
        await Assert.That(CocoaInput.MapKeyCode(0x7E)).IsEqualTo(Key.Up);
        await Assert.That(CocoaInput.MapKeyCode(0x7D)).IsEqualTo(Key.Down);
    }

    [Test]
    public async Task MapKeyCode_NumPad0_ReturnsNumPad0()
    {
        Key result = CocoaInput.MapKeyCode(0x52);
        await Assert.That(result).IsEqualTo(Key.NumPad0);
    }

    [Test]
    public async Task MapKeyCode_NumPad9_ReturnsNumPad9()
    {
        Key result = CocoaInput.MapKeyCode(0x5C);
        await Assert.That(result).IsEqualTo(Key.NumPad9);
    }

    [Test]
    public async Task MapKeyCode_NavigationKeys_ReturnCorrectKeys()
    {
        await Assert.That(CocoaInput.MapKeyCode(0x73)).IsEqualTo(Key.Home);
        await Assert.That(CocoaInput.MapKeyCode(0x77)).IsEqualTo(Key.End);
        await Assert.That(CocoaInput.MapKeyCode(0x74)).IsEqualTo(Key.PageUp);
        await Assert.That(CocoaInput.MapKeyCode(0x79)).IsEqualTo(Key.PageDown);
    }

    [Test]
    public async Task MapKeyCode_Punctuation_ReturnCorrectKeys()
    {
        await Assert.That(CocoaInput.MapKeyCode(0x29)).IsEqualTo(Key.Semicolon);
        await Assert.That(CocoaInput.MapKeyCode(0x18)).IsEqualTo(Key.Equals);
        await Assert.That(CocoaInput.MapKeyCode(0x2B)).IsEqualTo(Key.Comma);
        await Assert.That(CocoaInput.MapKeyCode(0x1B)).IsEqualTo(Key.Minus);
        await Assert.That(CocoaInput.MapKeyCode(0x2F)).IsEqualTo(Key.Period);
        await Assert.That(CocoaInput.MapKeyCode(0x2C)).IsEqualTo(Key.Slash);
        await Assert.That(CocoaInput.MapKeyCode(0x32)).IsEqualTo(Key.Backtick);
        await Assert.That(CocoaInput.MapKeyCode(0x21)).IsEqualTo(Key.LeftBracket);
        await Assert.That(CocoaInput.MapKeyCode(0x2A)).IsEqualTo(Key.Backslash);
        await Assert.That(CocoaInput.MapKeyCode(0x1E)).IsEqualTo(Key.RightBracket);
        await Assert.That(CocoaInput.MapKeyCode(0x27)).IsEqualTo(Key.Quote);
    }

    [Test]
    public async Task MapKeyCode_UnknownKey_ReturnsNone()
    {
        Key result = CocoaInput.MapKeyCode(0xFF);
        await Assert.That(result).IsEqualTo(Key.None);
    }

    // ── Reverse Mapping ─────────────────────────────────────────────

    [Test]
    public async Task MapKeyToKeyCode_RoundTrips_Letters()
    {
        Key[] letterKeys =
        [
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I,
            Key.J, Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R,
            Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z
        ];

        foreach (Key key in letterKeys)
        {
            ushort keyCode = CocoaInput.MapKeyToKeyCode(key);
            Key roundTripped = CocoaInput.MapKeyCode(keyCode);
            await Assert.That(roundTripped).IsEqualTo(key);
        }
    }

    [Test]
    public async Task MapKeyToKeyCode_RoundTrips_Digits()
    {
        for (Key key = Key.D0; key <= Key.D9; key++)
        {
            ushort keyCode = CocoaInput.MapKeyToKeyCode(key);
            Key roundTripped = CocoaInput.MapKeyCode(keyCode);
            await Assert.That(roundTripped).IsEqualTo(key);
        }
    }

    [Test]
    public async Task MapKeyToKeyCode_RoundTrips_FunctionKeys()
    {
        for (Key key = Key.F1; key <= Key.F12; key++)
        {
            ushort keyCode = CocoaInput.MapKeyToKeyCode(key);
            Key roundTripped = CocoaInput.MapKeyCode(keyCode);
            await Assert.That(roundTripped).IsEqualTo(key);
        }
    }

    [Test]
    public async Task MapKeyToKeyCode_None_ReturnsMaxUshort()
    {
        ushort result = CocoaInput.MapKeyToKeyCode(Key.None);
        await Assert.That(result).IsEqualTo((ushort)0xFFFF);
    }

    [Test]
    public async Task MapKeyToKeyCode_RoundTrips_NavigationKeys()
    {
        Key[] navKeys = [Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.Left, Key.Right, Key.Up, Key.Down];

        foreach (Key key in navKeys)
        {
            ushort keyCode = CocoaInput.MapKeyToKeyCode(key);
            Key roundTripped = CocoaInput.MapKeyCode(keyCode);
            await Assert.That(roundTripped).IsEqualTo(key);
        }
    }

    [Test]
    public async Task MapKeyToKeyCode_RoundTrips_NumPadKeys()
    {
        for (Key key = Key.NumPad0; key <= Key.NumPad9; key++)
        {
            ushort keyCode = CocoaInput.MapKeyToKeyCode(key);
            Key roundTripped = CocoaInput.MapKeyCode(keyCode);
            await Assert.That(roundTripped).IsEqualTo(key);
        }
    }

    // ── Modifier Flags ──────────────────────────────────────────────

    [Test]
    public async Task MapModifierFlags_Command_MapsToCtrl()
    {
        ModifierKeys result = CocoaInput.MapModifierFlags(ObjC.NSEventModifierFlagCommand);
        await Assert.That(result).IsEqualTo(ModifierKeys.Ctrl);
    }

    [Test]
    public async Task MapModifierFlags_Shift_MapsToShift()
    {
        ModifierKeys result = CocoaInput.MapModifierFlags(ObjC.NSEventModifierFlagShift);
        await Assert.That(result).IsEqualTo(ModifierKeys.Shift);
    }

    [Test]
    public async Task MapModifierFlags_Option_MapsToAlt()
    {
        ModifierKeys result = CocoaInput.MapModifierFlags(ObjC.NSEventModifierFlagOption);
        await Assert.That(result).IsEqualTo(ModifierKeys.Alt);
    }

    [Test]
    public async Task MapModifierFlags_Control_MapsToMeta()
    {
        ModifierKeys result = CocoaInput.MapModifierFlags(ObjC.NSEventModifierFlagControl);
        await Assert.That(result).IsEqualTo(ModifierKeys.Meta);
    }

    [Test]
    public async Task MapModifierFlags_NoFlags_ReturnsNone()
    {
        ModifierKeys result = CocoaInput.MapModifierFlags(0);
        await Assert.That(result).IsEqualTo(ModifierKeys.None);
    }

    [Test]
    public async Task MapModifierFlags_CombinedFlags_ReturnsCombinedModifiers()
    {
        ulong flags = ObjC.NSEventModifierFlagCommand | ObjC.NSEventModifierFlagShift;
        ModifierKeys result = CocoaInput.MapModifierFlags(flags);
        await Assert.That(result).IsEqualTo(ModifierKeys.Ctrl | ModifierKeys.Shift);
    }

    [Test]
    public async Task MapModifierFlags_AllFlags_ReturnsAllModifiers()
    {
        ulong flags = ObjC.NSEventModifierFlagCommand
                     | ObjC.NSEventModifierFlagShift
                     | ObjC.NSEventModifierFlagOption
                     | ObjC.NSEventModifierFlagControl;
        ModifierKeys result = CocoaInput.MapModifierFlags(flags);
        ModifierKeys expected = ModifierKeys.Ctrl | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Meta;
        await Assert.That(result).IsEqualTo(expected);
    }

    // ── ObjC Constants Verification ─────────────────────────────────
    // Store const values in locals to avoid TUnitAssertions0005 (constant assertion)

    [Test]
    public async Task NSWindowStyleMask_BorderlessIsZero()
    {
        ulong value = ObjC.NSWindowStyleMaskBorderless;
        await Assert.That(value).IsEqualTo(0UL);
    }

    [Test]
    public async Task NSWindowStyleMask_TitledIsBit0()
    {
        ulong value = ObjC.NSWindowStyleMaskTitled;
        await Assert.That(value).IsEqualTo(1UL);
    }

    [Test]
    public async Task NSWindowStyleMask_ClosableIsBit1()
    {
        ulong value = ObjC.NSWindowStyleMaskClosable;
        await Assert.That(value).IsEqualTo(2UL);
    }

    [Test]
    public async Task NSWindowStyleMask_MiniaturizableIsBit2()
    {
        ulong value = ObjC.NSWindowStyleMaskMiniaturizable;
        await Assert.That(value).IsEqualTo(4UL);
    }

    [Test]
    public async Task NSWindowStyleMask_ResizableIsBit3()
    {
        ulong value = ObjC.NSWindowStyleMaskResizable;
        await Assert.That(value).IsEqualTo(8UL);
    }

    [Test]
    public async Task NSWindowStyleMask_FullSizeContentViewIsBit15()
    {
        ulong value = ObjC.NSWindowStyleMaskFullSizeContentView;
        await Assert.That(value).IsEqualTo(1UL << 15);
    }

    // ── NSEvent Type Constants ──────────────────────────────────────

    [Test]
    public async Task NSEventType_LeftMouseDown_IsOne()
    {
        ulong value = ObjC.NSEventTypeLeftMouseDown;
        await Assert.That(value).IsEqualTo(1UL);
    }

    [Test]
    public async Task NSEventType_KeyDown_IsTen()
    {
        ulong value = ObjC.NSEventTypeKeyDown;
        await Assert.That(value).IsEqualTo(10UL);
    }

    [Test]
    public async Task NSEventType_KeyUp_IsEleven()
    {
        ulong value = ObjC.NSEventTypeKeyUp;
        await Assert.That(value).IsEqualTo(11UL);
    }

    [Test]
    public async Task NSEventType_ScrollWheel_Is22()
    {
        ulong value = ObjC.NSEventTypeScrollWheel;
        await Assert.That(value).IsEqualTo(22UL);
    }

    // ── NSEvent Modifier Flag Constants ─────────────────────────────

    [Test]
    public async Task NSEventModifierFlag_Shift_IsBit17()
    {
        ulong value = ObjC.NSEventModifierFlagShift;
        await Assert.That(value).IsEqualTo(1UL << 17);
    }

    [Test]
    public async Task NSEventModifierFlag_Control_IsBit18()
    {
        ulong value = ObjC.NSEventModifierFlagControl;
        await Assert.That(value).IsEqualTo(1UL << 18);
    }

    [Test]
    public async Task NSEventModifierFlag_Option_IsBit19()
    {
        ulong value = ObjC.NSEventModifierFlagOption;
        await Assert.That(value).IsEqualTo(1UL << 19);
    }

    [Test]
    public async Task NSEventModifierFlag_Command_IsBit20()
    {
        ulong value = ObjC.NSEventModifierFlagCommand;
        await Assert.That(value).IsEqualTo(1UL << 20);
    }

    // ── Pasteboard Type Strings ─────────────────────────────────────

    [Test]
    public async Task PasteboardTypeString_IsCorrectUTI()
    {
        string value = ObjC.NSPasteboardTypeString;
        await Assert.That(value).IsEqualTo("public.utf8-plain-text");
    }

    [Test]
    public async Task PasteboardTypeHTML_IsCorrectUTI()
    {
        string value = ObjC.NSPasteboardTypeHTML;
        await Assert.That(value).IsEqualTo("public.html");
    }

    [Test]
    public async Task PasteboardTypeTIFF_IsCorrectUTI()
    {
        string value = ObjC.NSPasteboardTypeTIFF;
        await Assert.That(value).IsEqualTo("public.tiff");
    }

    [Test]
    public async Task PasteboardTypeFileURL_IsCorrectUTI()
    {
        string value = ObjC.NSPasteboardTypeFileURL;
        await Assert.That(value).IsEqualTo("public.file-url");
    }

    // ── Clipboard Availability Default ──────────────────────────────

    [Test]
    public async Task CocoaClipboardAvailability_DefaultsToFalse()
    {
        var availability = new CocoaClipboardAvailability();
        await Assert.That(availability.HasText).IsFalse();
        await Assert.That(availability.HasHtml).IsFalse();
        await Assert.That(availability.HasRtf).IsFalse();
        await Assert.That(availability.HasFiles).IsFalse();
        await Assert.That(availability.HasImage).IsFalse();
    }

    // ── NSRect Structure ────────────────────────────────────────────

    [Test]
    public async Task NSRect_Constructor_SetsFields()
    {
        var rect = new NSRect(10.0, 20.0, 800.0, 600.0);
        await Assert.That(rect.X).IsEqualTo(10.0);
        await Assert.That(rect.Y).IsEqualTo(20.0);
        await Assert.That(rect.Width).IsEqualTo(800.0);
        await Assert.That(rect.Height).IsEqualTo(600.0);
    }

    [Test]
    public async Task NSPoint_Constructor_SetsFields()
    {
        var point = new NSPoint(100.0, 200.0);
        await Assert.That(point.X).IsEqualTo(100.0);
        await Assert.That(point.Y).IsEqualTo(200.0);
    }

    [Test]
    public async Task NSSize_Constructor_SetsFields()
    {
        var size = new NSSize(1920.0, 1080.0);
        await Assert.That(size.Width).IsEqualTo(1920.0);
        await Assert.That(size.Height).IsEqualTo(1080.0);
    }

    // ── CocoaMonitorInfo Structure ──────────────────────────────────

    [Test]
    public async Task CocoaMonitorInfo_DefaultScaleFactor_IsZero()
    {
        var info = new CocoaMonitorInfo();
        await Assert.That(info.ScaleFactor).IsEqualTo(0f);
    }

    [Test]
    public async Task CocoaMonitorInfo_CanSetFields()
    {
        var info = new CocoaMonitorInfo
        {
            WorkArea = new Rect(0, 25, 1920, 1055),
            MonitorArea = new Rect(0, 0, 1920, 1080),
            ScaleFactor = 2.0f
        };

        await Assert.That(info.ScaleFactor).IsEqualTo(2.0f);
        await Assert.That(info.WorkArea.Width).IsEqualTo(1920f);
        await Assert.That(info.MonitorArea.Height).IsEqualTo(1080f);
    }

    // ── Modal Response Constants ────────────────────────────────────

    [Test]
    public async Task NSModalResponseOK_IsOne()
    {
        long value = ObjC.NSModalResponseOK;
        await Assert.That(value).IsEqualTo(1L);
    }

    [Test]
    public async Task NSModalResponseCancel_IsZero()
    {
        long value = ObjC.NSModalResponseCancel;
        await Assert.That(value).IsEqualTo(0L);
    }
}

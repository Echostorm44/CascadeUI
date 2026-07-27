using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for the Win32 platform layer. These tests verify input mapping,
/// clipboard format handling, and message loop dispatch logic without creating
/// actual Win32 windows.
/// </summary>
public class WindowTests
{
    // ── Virtual Key Mapping ──────────────────────────────────────────

    [Test]
    public async Task MapVirtualKey_LetterA_ReturnsKeyA()
    {
        Key result = Win32Input.MapVirtualKey(0x41);
        await Assert.That(result).IsEqualTo(Key.A);
    }

    [Test]
    public async Task MapVirtualKey_LetterZ_ReturnsKeyZ()
    {
        Key result = Win32Input.MapVirtualKey(0x5A);
        await Assert.That(result).IsEqualTo(Key.Z);
    }

    [Test]
    public async Task MapVirtualKey_Digit0_ReturnsD0()
    {
        Key result = Win32Input.MapVirtualKey(0x30);
        await Assert.That(result).IsEqualTo(Key.D0);
    }

    [Test]
    public async Task MapVirtualKey_Digit9_ReturnsD9()
    {
        Key result = Win32Input.MapVirtualKey(0x39);
        await Assert.That(result).IsEqualTo(Key.D9);
    }

    [Test]
    public async Task MapVirtualKey_F1_ReturnsF1()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_F1);
        await Assert.That(result).IsEqualTo(Key.F1);
    }

    [Test]
    public async Task MapVirtualKey_F12_ReturnsF12()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_F12);
        await Assert.That(result).IsEqualTo(Key.F12);
    }

    [Test]
    public async Task MapVirtualKey_Escape_ReturnsEscape()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_ESCAPE);
        await Assert.That(result).IsEqualTo(Key.Escape);
    }

    [Test]
    public async Task MapVirtualKey_Enter_ReturnsEnter()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_RETURN);
        await Assert.That(result).IsEqualTo(Key.Enter);
    }

    [Test]
    public async Task MapVirtualKey_Space_ReturnsSpace()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_SPACE);
        await Assert.That(result).IsEqualTo(Key.Space);
    }

    [Test]
    public async Task MapVirtualKey_ArrowKeys_ReturnCorrectKeys()
    {
        await Assert.That(Win32Input.MapVirtualKey(Win32.VK_LEFT)).IsEqualTo(Key.Left);
        await Assert.That(Win32Input.MapVirtualKey(Win32.VK_UP)).IsEqualTo(Key.Up);
        await Assert.That(Win32Input.MapVirtualKey(Win32.VK_RIGHT)).IsEqualTo(Key.Right);
        await Assert.That(Win32Input.MapVirtualKey(Win32.VK_DOWN)).IsEqualTo(Key.Down);
    }

    [Test]
    public async Task MapVirtualKey_NumPad0_ReturnsNumPad0()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_NUMPAD0);
        await Assert.That(result).IsEqualTo(Key.NumPad0);
    }

    [Test]
    public async Task MapVirtualKey_NumPad9_ReturnsNumPad9()
    {
        Key result = Win32Input.MapVirtualKey(Win32.VK_NUMPAD9);
        await Assert.That(result).IsEqualTo(Key.NumPad9);
    }

    [Test]
    public async Task MapVirtualKey_UnknownKey_ReturnsNone()
    {
        Key result = Win32Input.MapVirtualKey(0xFF);
        await Assert.That(result).IsEqualTo(Key.None);
    }

    // ── Reverse Mapping ─────────────────────────────────────────────

    [Test]
    public async Task MapKeyToVirtualKey_RoundTrips_Letters()
    {
        for (int vk = 0x41; vk <= 0x5A; vk++)
        {
            Key key = Win32Input.MapVirtualKey(vk);
            int backToVk = Win32Input.MapKeyToVirtualKey(key);
            await Assert.That(backToVk).IsEqualTo(vk);
        }
    }

    [Test]
    public async Task MapKeyToVirtualKey_RoundTrips_Digits()
    {
        for (int vk = 0x30; vk <= 0x39; vk++)
        {
            Key key = Win32Input.MapVirtualKey(vk);
            int backToVk = Win32Input.MapKeyToVirtualKey(key);
            await Assert.That(backToVk).IsEqualTo(vk);
        }
    }

    [Test]
    public async Task MapKeyToVirtualKey_RoundTrips_FunctionKeys()
    {
        for (int vk = Win32.VK_F1; vk <= Win32.VK_F12; vk++)
        {
            Key key = Win32Input.MapVirtualKey(vk);
            int backToVk = Win32Input.MapKeyToVirtualKey(key);
            await Assert.That(backToVk).IsEqualTo(vk);
        }
    }

    [Test]
    public async Task MapKeyToVirtualKey_None_ReturnsZero()
    {
        int result = Win32Input.MapKeyToVirtualKey(Key.None);
        await Assert.That(result).IsEqualTo(0);
    }

    // ── Mouse Message Processing ────────────────────────────────────

    [Test]
    public async Task ProcessMouseMessage_NonMouseMessage_ReturnsNull()
    {
        NativeMouseEvent? result = Win32Input.ProcessMouseMessage(
            Win32.WM_KEYDOWN, 0, 0, 1.0f);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ProcessMouseMessage_LeftButtonDown_ReturnsMouseDown()
    {
        nint lParam = MakeLParam(100, 200);
        NativeMouseEvent? result = Win32Input.ProcessMouseMessage(
            Win32.WM_LBUTTONDOWN, 0, lParam, 1.0f);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo(NativeMouseEventType.MouseDown);
        await Assert.That(result.Button).IsEqualTo(NativeMouseButton.Left);
        await Assert.That(result.X).IsEqualTo(100f);
        await Assert.That(result.Y).IsEqualTo(200f);
    }

    [Test]
    public async Task ProcessMouseMessage_MouseMove_ReturnsMouseMove()
    {
        nint lParam = MakeLParam(50, 75);
        NativeMouseEvent? result = Win32Input.ProcessMouseMessage(
            Win32.WM_MOUSEMOVE, 0, lParam, 1.0f);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo(NativeMouseEventType.MouseMove);
    }

    [Test]
    public async Task ProcessMouseMessage_DpiScaling_AppliesCorrectly()
    {
        nint lParam = MakeLParam(200, 400);
        NativeMouseEvent? result = Win32Input.ProcessMouseMessage(
            Win32.WM_LBUTTONDOWN, 0, lParam, 2.0f);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.X).IsEqualTo(100f);
        await Assert.That(result.Y).IsEqualTo(200f);
    }

    // ── Scroll Message Processing ───────────────────────────────────

    [Test]
    public async Task ProcessScrollMessage_NonScrollMessage_ReturnsNull()
    {
        NativeScrollEvent? result = Win32Input.ProcessScrollMessage(
            Win32.WM_KEYDOWN, 0, 0, 1.0f, 0);
        await Assert.That(result).IsNull();
    }

    // ── Character Message Processing ────────────────────────────────

    [Test]
    public async Task ProcessCharMessage_PrintableChar_ReturnsEvent()
    {
        NativeKeyEvent? result = Win32Input.ProcessCharMessage(
            Win32.WM_CHAR, (nuint)'A', 0);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Character).IsEqualTo('A');
    }

    [Test]
    public async Task ProcessCharMessage_Tab_ReturnsEvent()
    {
        NativeKeyEvent? result = Win32Input.ProcessCharMessage(
            Win32.WM_CHAR, (nuint)'\t', 0);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Character).IsEqualTo('\t');
    }

    [Test]
    public async Task ProcessCharMessage_ControlChar_ReturnsNull()
    {
        // NUL control character should be filtered
        NativeKeyEvent? result = Win32Input.ProcessCharMessage(
            Win32.WM_CHAR, 0x01, 0);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ProcessCharMessage_NonCharMessage_ReturnsNull()
    {
        NativeKeyEvent? result = Win32Input.ProcessCharMessage(
            Win32.WM_KEYDOWN, (nuint)'A', 0);
        await Assert.That(result).IsNull();
    }

    // ── Key Message Processing ──────────────────────────────────────

    [Test]
    public async Task ProcessKeyMessage_KeyDown_ReturnsKeyDownEvent()
    {
        NativeKeyEvent? result = Win32Input.ProcessKeyMessage(
            Win32.WM_KEYDOWN, (nuint)Win32.VK_ESCAPE, 0);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo(NativeKeyEventType.KeyDown);
        await Assert.That(result.Key).IsEqualTo(Key.Escape);
    }

    [Test]
    public async Task ProcessKeyMessage_KeyUp_ReturnsKeyUpEvent()
    {
        NativeKeyEvent? result = Win32Input.ProcessKeyMessage(
            Win32.WM_KEYUP, (nuint)Win32.VK_SPACE, 0);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Type).IsEqualTo(NativeKeyEventType.KeyUp);
        await Assert.That(result.Key).IsEqualTo(Key.Space);
    }

    [Test]
    public async Task ProcessKeyMessage_NonKeyMessage_ReturnsNull()
    {
        NativeKeyEvent? result = Win32Input.ProcessKeyMessage(
            Win32.WM_MOUSEMOVE, 0, 0);
        await Assert.That(result).IsNull();
    }

    // ── Message Loop ────────────────────────────────────────────────

    [Test]
    public async Task MessageLoop_Constructor_SetsMainThread()
    {
        var loop = new Win32MessageLoop();
        await Assert.That(loop.IsOnMainThread).IsTrue();
        loop.Dispose();
    }

    // ── Clipboard Format Availability ───────────────────────────────

    [Test]
    public async Task ClipboardAvailability_DefaultsToFalse()
    {
        var availability = new ClipboardAvailability();
        await Assert.That(availability.HasText).IsFalse();
        await Assert.That(availability.HasHtml).IsFalse();
        await Assert.That(availability.HasFiles).IsFalse();
        await Assert.That(availability.HasImage).IsFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static nint MakeLParam(int x, int y)
    {
        return (nint)((y << 16) | (x & 0xFFFF));
    }
}

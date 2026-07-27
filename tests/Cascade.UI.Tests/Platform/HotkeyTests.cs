namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Tests for <see cref="Hotkey.ToString()"/> formatting.
/// </summary>
public class HotkeyTests
{
    [Test]
    public async Task ToString_SingleKey_NoModifiers()
    {
        var hotkey = new Hotkey(ModifierKeys.None, Key.A);
        await Assert.That(hotkey.ToString()).IsEqualTo("A");
    }

    [Test]
    public async Task ToString_CtrlPlusKey()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl, Key.S);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+S");
    }

    [Test]
    public async Task ToString_CtrlShiftPlusKey()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl | ModifierKeys.Shift, Key.N);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+Shift+N");
    }

    [Test]
    public async Task ToString_AllModifiers()
    {
        var hotkey = new Hotkey(
            ModifierKeys.Ctrl | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Meta,
            Key.Delete);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+Shift+Alt+Meta+Delete");
    }

    [Test]
    public async Task ToString_FunctionKey()
    {
        var hotkey = new Hotkey(ModifierKeys.None, Key.F5);
        await Assert.That(hotkey.ToString()).IsEqualTo("F5");
    }

    [Test]
    public async Task ToString_DigitKey_StripsPrefix()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl, Key.D1);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+1");
    }

    [Test]
    public async Task ToString_EscapeKey_UsesShortName()
    {
        var hotkey = new Hotkey(ModifierKeys.None, Key.Escape);
        await Assert.That(hotkey.ToString()).IsEqualTo("Esc");
    }

    [Test]
    public async Task ToString_SpaceKey()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl, Key.Space);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+Space");
    }

    [Test]
    public async Task ToString_Punctuation_ShowsSymbol()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl, Key.Semicolon);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+;");
    }

    [Test]
    public async Task ToString_NumPadKey_HasSpace()
    {
        var hotkey = new Hotkey(ModifierKeys.None, Key.NumPad5);
        await Assert.That(hotkey.ToString()).IsEqualTo("NumPad 5");
    }

    [Test]
    public async Task ToString_NoKey_ShowsOnlyModifiers()
    {
        var hotkey = new Hotkey(ModifierKeys.Ctrl | ModifierKeys.Alt, Key.None);
        await Assert.That(hotkey.ToString()).IsEqualTo("Ctrl+Alt");
    }

    [Test]
    public async Task ToString_NoKeyNoModifiers_IsEmpty()
    {
        var hotkey = new Hotkey(ModifierKeys.None, Key.None);
        await Assert.That(hotkey.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task ToString_PageUp_HasSpace()
    {
        var hotkey = new Hotkey(ModifierKeys.Alt, Key.PageUp);
        await Assert.That(hotkey.ToString()).IsEqualTo("Alt+Page Up");
    }

    [Test]
    public async Task From_Factory_ProducesEquivalent()
    {
        var direct = new Hotkey(ModifierKeys.Ctrl, Key.C);
        var factory = Hotkey.From(ModifierKeys.Ctrl, Key.C);
        await Assert.That(direct).IsEqualTo(factory);
    }
}

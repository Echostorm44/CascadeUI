#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Typing into a NumberInput: a value typed and then left with Tab (or by the app clearing focus) is
/// saved and never shows up in the next field; Ctrl+A selects the value so typing, paste or Backspace
/// replaces it; paste keeps only the number's characters.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast", "Clipboard"])]
public class NumberInputKeyboardTests
{
    private sealed class Page : Component
    {
        internal int Width = 1920;
        internal int Height = 1080;

        protected override Node Render() => new Row(
            spacing: 10,
            children:
            [
                new NumberInput<int>(new Bindable<int>(Width, v => { Width = v; Invalidate(); }), min: 1, max: 20000, step: 1, format: "0").Width(120),
                new NumberInput<int>(new Bindable<int>(Height, v => { Height = v; Invalidate(); }), min: 1, max: 20000, step: 1, format: "0").Width(120),
            ]);
    }

    // Opens the page with the first field focused by Tab.
    private static (RenderHarness App, Page Page) Open()
    {
        FocusManager.ClearFocus();
        var page = new Page();
        var app = new RenderHarness(() => page, width: 400, height: 100);
        app.Frame();
        app.Press(Key.Tab);
        app.Frame();
        return (app, page);
    }

    // A key Windows also reports as a character message (WM_CHAR arrives as its own Key.None event).
    private static void PressWithChar(RenderHarness app, Key key, char ch, ModifierKeys modifiers = ModifierKeys.None)
    {
        app.Press(key, modifiers);
        app.Press(Key.None, character: ch);
    }

    private static void Type(RenderHarness app, string text)
    {
        foreach (char c in text)
        {
            app.Press(c is >= '0' and <= '9' ? Key.D0 + (c - '0') : Key.None, character: c);
        }
    }

    [Test]
    public async Task Tab_SavesTheTypedValue_AndTheNextFieldShowsItsOwn()
    {
        var (app, page) = Open();
        using (app)
        {
            Type(app, "3840");
            PressWithChar(app, Key.Tab, '\t');
            app.Frame();
            await Assert.That(page.Width).IsEqualTo(3840);
            await Assert.That(InputDispatcher.NumberEditBuffer).IsNull();

            // Typing in the second field edits its own value, and Shift+Tab saves that too.
            Type(app, "2160");
            PressWithChar(app, Key.Tab, '\t', ModifierKeys.Shift);
            app.Frame();
            await Assert.That(page.Height).IsEqualTo(2160);
            await Assert.That(page.Width).IsEqualTo(3840);
        }
    }

    [Test]
    public async Task TypingAfterEnterOrEscape_ReplacesTheValue()
    {
        var (app, page) = Open();
        using (app)
        {
            Type(app, "500");
            PressWithChar(app, Key.Enter, '\r');
            await Assert.That(page.Width).IsEqualTo(500);
            Type(app, "600");
            PressWithChar(app, Key.Enter, '\r');
            await Assert.That(page.Width).IsEqualTo(600);

            // Escape reverts the typed digit and leaves the field; Tab comes back to it.
            Type(app, "9");
            PressWithChar(app, Key.Escape, (char)27);
            app.Frame();
            await Assert.That(page.Width).IsEqualTo(600);
            await Assert.That(InputDispatcher.NumberEditBuffer).IsNull();
            PressWithChar(app, Key.Tab, '\t');
            app.Frame();
            Type(app, "700");
            PressWithChar(app, Key.Enter, '\r');
            await Assert.That(page.Width).IsEqualTo(700);
        }
    }

    [Test]
    public async Task ClearingFocus_SavesTheTypedValue()
    {
        var (app, page) = Open();
        using (app)
        {
            Type(app, "640");
            FocusManager.ClearFocus();
            app.Frame();
            await Assert.That(page.Width).IsEqualTo(640);
            await Assert.That(InputDispatcher.NumberEditBuffer).IsNull();
        }
    }

    [Test]
    public async Task CtrlA_ThenTyping_ReplacesTheValue()
    {
        var (app, page) = Open();
        using (app)
        {
            app.Press(Key.A, ModifierKeys.Ctrl);
            await Assert.That(InputDispatcher.NumberInputSelectAll).IsTrue();
            Type(app, "1280");
            await Assert.That(InputDispatcher.NumberEditBuffer).IsEqualTo("1280");
            app.Press(Key.Enter);
            await Assert.That(page.Width).IsEqualTo(1280);
        }
    }

    [Test]
    public async Task CtrlA_ThenBackspace_ClearsTheValue_AndArrowsCollapseTheSelection()
    {
        var (app, page) = Open();
        using (app)
        {
            app.Press(Key.A, ModifierKeys.Ctrl);
            app.Press(Key.Backspace);
            await Assert.That(InputDispatcher.NumberEditBuffer).IsEqualTo(string.Empty);
            Type(app, "800");
            app.Press(Key.Enter);
            await Assert.That(page.Width).IsEqualTo(800);

            // Ctrl+A then Left puts the caret before the value; typing inserts there.
            app.Press(Key.A, ModifierKeys.Ctrl);
            app.Press(Key.Left);
            await Assert.That(InputDispatcher.NumberInputSelectAll).IsFalse();
            Type(app, "1");
            app.Press(Key.Enter);
            await Assert.That(page.Width).IsEqualTo(1800);
        }
    }

    [Test]
    public async Task Paste_KeepsTheNumbersCharacters_AndReplacesASelectAll()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var (app, page) = Open();
        using (app)
        {
            await Clipboard.WriteTextAsync(" 2,560\r\n");
            app.Press(Key.A, ModifierKeys.Ctrl);
            app.Press(Key.V, ModifierKeys.Ctrl);
            await Assert.That(InputDispatcher.NumberEditBuffer).IsEqualTo("2560");
            app.Press(Key.Enter);
            await Assert.That(page.Width).IsEqualTo(2560);

            // Ctrl+A, Ctrl+C copies the value.
            app.Press(Key.A, ModifierKeys.Ctrl);
            app.Press(Key.C, ModifierKeys.Ctrl);
            var content = await Clipboard.GetContentAsync();
            await Assert.That(await content.GetTextAsync()).IsEqualTo("2560");
        }
    }
}

namespace Cascade.UI.Tests.Controls;

/// <summary>HotkeyPicker recording through the input dispatcher.</summary>
public class HotkeyPickerTests
{
    private InputDispatcher dispatcher = null!;
    private readonly List<Hotkey> recorded = [];

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher();
        FocusManager.Reset();
        recorded.Clear();
    }

    private HotkeyPicker Focused(Hotkey? current = null)
    {
        var picker = new HotkeyPicker { Current = current, OnChange = recorded.Add };
        dispatcher.SetRoot(new Column(children: [picker]));
        FocusManager.RequestFocus(picker);
        return picker;
    }

    [Test]
    public async Task Records_ModifiersPlusKey()
    {
        var picker = Focused(new Hotkey(ModifierKeys.Alt, Key.Space));
        await Assert.That(picker.FieldText(focused: true, ModifierKeys.None)).IsEqualTo("Alt+Space");

        KeyDown(Key.Enter, ModifierKeys.None);            // start recording
        KeyDown(Key.None, ModifierKeys.Ctrl);             // Ctrl alone: keep waiting
        await Assert.That(picker.IsRecording).IsTrue();
        await Assert.That(picker.FieldText(focused: true, ModifierKeys.Ctrl)).IsEqualTo("Ctrl+…");

        KeyDown(Key.Backtick, ModifierKeys.Ctrl);

        await Assert.That(recorded).IsEquivalentTo([new Hotkey(ModifierKeys.Ctrl, Key.Backtick)]);
        await Assert.That(picker.IsRecording).IsFalse();
    }

    [Test]
    public async Task Escape_Cancels()
    {
        var picker = Focused();
        KeyDown(Key.Space, ModifierKeys.None);
        KeyDown(Key.Escape, ModifierKeys.None);

        await Assert.That(picker.IsRecording).IsFalse();
        await Assert.That(recorded).IsEmpty();
        await Assert.That(picker.FieldText(focused: true, ModifierKeys.None)).IsEqualTo("None");
    }

    [Test]
    public async Task Tab_IsRecordable_WhileRecording()
    {
        var picker = Focused();
        KeyDown(Key.Enter, ModifierKeys.None);
        KeyDown(Key.Tab, ModifierKeys.Ctrl);

        await Assert.That(recorded).IsEquivalentTo([new Hotkey(ModifierKeys.Ctrl, Key.Tab)]);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(picker);
    }

    private void KeyDown(Key key, ModifierKeys modifiers)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = key, Type = NativeKeyEventType.KeyDown, Modifiers = modifiers });
    }

    [Test]
    [NotInParallel]
    public async Task Field_FitsTheRecordingPrompt_AtALargeFont()
    {
        string? saved = LayoutSolver.DefaultFontPath;
        float savedSize = LayoutSolver.BodyFontSize;
        LayoutSolver.DefaultFontPath = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        LayoutSolver.BodyFontSize = 22f;
        try
        {
            float prompt = TextLayoutEngine.Layout(HotkeyPicker.RecordingPrompt, new TextLayoutOptions { FontPath = LayoutSolver.DefaultFontPath!, FontSize = 22f }).BoundingBox.Width;

            float field = LayoutSolver.HotkeyPickerFieldWidth(new HotkeyPicker());

            await Assert.That(field).IsGreaterThanOrEqualTo(prompt + (ThemeSwitcher.Current.TextInput.PaddingH * 2f));
        }
        finally
        {
            LayoutSolver.DefaultFontPath = saved;
            LayoutSolver.BodyFontSize = savedSize;
        }
    }
}

namespace Cascade.UI.Tests.Core;

/// <summary>
/// KeyHandler dispatch: focus scoping, handlers anywhere in the reconciled tree, key-up,
/// modifier-change events, auto-repeat, and a consumed key-down not typing its character.
/// Key events mirror Win32: WM_KEYDOWN (Key, no character) then WM_CHAR (Key.None + character).
/// </summary>
public class KeyDispatchTests
{
    private InputDispatcher dispatcher = null!;

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher();
        FocusManager.Reset();
        Keyboard.Observe(ModifierKeys.None);
    }

    [Test]
    public async Task ConsumedKeyDown_DoesNotTypeItsCharacter()
    {
        string text = "";
        int fired = 0;
        var input = new TextInput(new Bindable<string>(text, v => text = v));
        var root = new KeyHandler(new Column(children: [input]),
            new KeyBinding(new Hotkey(ModifierKeys.Shift, Key.D1), () => fired++));
        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(input);

        KeyDown(Key.D1, ModifierKeys.Shift);
        Character('!', ModifierKeys.Shift);
        KeyDown(Key.A, ModifierKeys.None);
        Character('a', ModifierKeys.None);

        await Assert.That(fired).IsEqualTo(1);
        await Assert.That(text).IsEqualTo("a");
    }

    [Test]
    public async Task InnermostHandlerAroundFocus_Wins()
    {
        var calls = new List<string>();
        var input = new TextInput(new Bindable<string>("", _ => { }));
        var inner = new KeyHandler(input, new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.K), () => calls.Add("inner")));
        var sibling = new KeyHandler(new Label("other"), new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.K), () => calls.Add("sibling")));
        var outer = new KeyHandler(new Column(children: [sibling, inner]),
            new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.K), () => calls.Add("outer")),
            new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.L), () => calls.Add("outer-L")));
        dispatcher.SetRoot(outer);
        FocusManager.RequestFocus(input);

        KeyDown(Key.K, ModifierKeys.Ctrl);
        KeyDown(Key.L, ModifierKeys.Ctrl);

        // Ctrl+K: the handler around the focused input, not its sibling or the outer one.
        // Ctrl+L: only the outer handler binds it, and focus is inside it.
        await Assert.That(string.Join(",", calls)).IsEqualTo("inner,outer-L");
    }

    [Test]
    public async Task HandlerInsideScrollView_IsFound()
    {
        int fired = 0;
        var root = new ScrollView(new KeyHandler(new Label("content"),
            new KeyBinding(new Hotkey(ModifierKeys.None, Key.F5), () => fired++)));
        dispatcher.SetRoot(root);

        KeyDown(Key.F5, ModifierKeys.None);

        await Assert.That(fired).IsEqualTo(1);
    }

    [Test]
    public async Task KeyUp_ReachesHandler()
    {
        KeyEvent? released = null;
        var root = new KeyHandler(new Label("content")) { KeyUp = e => released = e };
        dispatcher.SetRoot(root);

        dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.Space, Type = NativeKeyEventType.KeyUp, Modifiers = ModifierKeys.None });

        await Assert.That(released).IsEqualTo(new KeyEvent(Key.Space, ModifierKeys.None, false));
    }

    [Test]
    public async Task ModifierDownAndUp_RaiseModifiersChanged()
    {
        var seen = new List<ModifierKeys>();
        void OnChanged(ModifierKeys m) => seen.Add(m);
        Keyboard.ModifiersChanged += OnChanged;
        try
        {
            dispatcher.SetRoot(new Label("content"));
            // Shift itself maps to Key.None; the event carries the post-event modifier state.
            KeyDown(Key.None, ModifierKeys.Shift);
            KeyDown(Key.None, ModifierKeys.Shift, repeat: true);
            dispatcher.HandleKeyEvent(new NativeKeyEvent { Key = Key.None, Type = NativeKeyEventType.KeyUp, Modifiers = ModifierKeys.None });
        }
        finally
        {
            Keyboard.ModifiersChanged -= OnChanged;
        }

        await Assert.That(string.Join(",", seen)).IsEqualTo("Shift,None");
    }

    [Test]
    public async Task NoRepeatBinding_IgnoresAutoRepeat()
    {
        int fired = 0;
        var root = new KeyHandler(new Label("content"),
            new KeyBinding(new Hotkey(ModifierKeys.None, Key.Enter), () => fired++, AllowRepeat: false));
        dispatcher.SetRoot(root);

        KeyDown(Key.Enter, ModifierKeys.None);
        KeyDown(Key.Enter, ModifierKeys.None, repeat: true);
        KeyDown(Key.Enter, ModifierKeys.None, repeat: true);

        await Assert.That(fired).IsEqualTo(1);
    }

    private void KeyDown(Key key, ModifierKeys modifiers, bool repeat = false)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = key,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = modifiers,
            IsRepeat = repeat,
        });
    }

    private void Character(char c, ModifierKeys modifiers)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.None,
            Character = c,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = modifiers,
        });
    }
}

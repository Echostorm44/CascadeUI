namespace Cascade.UI.Tests.Core;

// These tests drive focus and click routing through the static FocusManager.
// Cross-test isolation is handled suite-wide by the serial ParallelLimiter
// (see ParallelLimit.cs, WP-3516); the [Before(Test)] FocusManager.Reset below
// gives each test a clean focus baseline.
public class InputDispatcherTests
{
    private InputDispatcher dispatcher = null!;

    [Before(Test)]
    public void SetUp()
    {
        dispatcher = new InputDispatcher();
        FocusManager.Reset();
    }

    [Test]
    public async Task SetRoot_AcceptsNull_DoesNotThrow()
    {
        await Assert.That(() => dispatcher.SetRoot(null)).ThrowsNothing();
    }

    [Test]
    public async Task HandleMouseEvent_NoRoot_DoesNotThrow()
    {
        var evt = new NativeMouseEvent
        {
            X = 50,
            Y = 50,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        };

        await Assert.That(() => dispatcher.HandleMouseEvent(evt)).ThrowsNothing();
    }

    [Test]
    public async Task HandleMouseEvent_Move_FiresPointerEnter()
    {
        bool entered = false;
        var label = new Label("Test");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            PointerEnter = () => { entered = true; }
        };

        dispatcher.SetRoot(label);

        var evt = new NativeMouseEvent
        {
            X = 50,
            Y = 50,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        };

        dispatcher.HandleMouseEvent(evt);

        await Assert.That(entered).IsTrue();
    }

    [Test]
    public async Task HandleMouseEvent_MoveAway_FiresPointerLeave()
    {
        bool left = false;
        var label = new Label("Test");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            PointerLeave = () => { left = true; }
        };

        dispatcher.SetRoot(label);

        // First move into the node
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 50,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        });

        // Then move out of it
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 200, Y = 200,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(left).IsTrue();
    }

    [Test]
    public async Task HandleMouseEvent_ClickOnButton_InvokesOnClick()
    {
        bool clicked = false;
        var button = new Button("Click me", () => { clicked = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 120, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);

        // Mouse down
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 60, Y = 20,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        // Mouse up on same node
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 60, Y = 20,
            Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task HandleMouseEvent_DisabledButton_DoesNotInvoke()
    {
        bool clicked = false;
        var button = new Button("Disabled", () => { clicked = true; });
        button.IsDisabled = true;
        button.LayoutData.Bounds = new Rect(0, 0, 120, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 60, Y = 20,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 60, Y = 20,
            Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(clicked).IsFalse();
    }

    [Test]
    public async Task HandleMouseEvent_ClickOnGestureTap_InvokesTap()
    {
        bool tapped = false;
        var label = new Label("Tappable");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            Tap = () => { tapped = true; }
        };

        dispatcher.SetRoot(label);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 15,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 15,
            Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(tapped).IsTrue();
    }

    [Test]
    public async Task HandleMouseEvent_PointerDown_FiresPointerDownGesture()
    {
        PointerEventArgs? capturedArgs = null;
        var label = new Label("Pressable");
        label.LayoutData.Bounds = new Rect(10, 20, 100, 30);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            PointerDown = (args) => { capturedArgs = args; }
        };

        dispatcher.SetRoot(label);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 30,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(capturedArgs).IsNotNull();
        // Position should be relative to node bounds
        await Assert.That(capturedArgs!.Position.X).IsEqualTo(40f);
        await Assert.That(capturedArgs!.Position.Y).IsEqualTo(10f);
    }

    [Test]
    public async Task HandleMouseEvent_RightClick_InvokesContextMenu()
    {
        bool contextMenuFired = false;
        var label = new Label("Context");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            ContextMenu = () => { contextMenuFired = true; }
        };

        dispatcher.SetRoot(label);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 15,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Right,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(contextMenuFired).IsTrue();
    }

    [Test]
    public async Task HandleMouseEvent_MouseLeave_ClearsHover()
    {
        bool left = false;
        var label = new Label("Test");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 100);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            PointerLeave = () => { left = true; }
        };

        dispatcher.SetRoot(label);

        // Enter
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 50,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        });

        // Global mouse leave
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 0, Y = 0,
            Type = NativeMouseEventType.MouseLeave,
            Button = NativeMouseButton.None,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(left).IsTrue();
    }

    // ── Keyboard tests ────────────────────────────────────────────────

    [Test]
    public async Task HandleKeyEvent_NoRoot_DoesNotThrow()
    {
        var evt = new NativeKeyEvent
        {
            Key = Key.A,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.None
        };

        await Assert.That(() => dispatcher.HandleKeyEvent(evt)).ThrowsNothing();
    }

    [Test]
    public async Task HandleKeyEvent_Escape_ClearsFocus()
    {
        var button = new Button("Focus me", () => { });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);

        FocusManager.RequestFocus(button);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(button);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Escape,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(FocusManager.FocusedElement).IsNull();
    }

    [Test]
    public async Task HandleKeyEvent_EnterOnButton_InvokesClick()
    {
        bool clicked = false;
        var button = new Button("Click", () => { clicked = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);
        FocusManager.RequestFocus(button);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Enter,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task HandleKeyEvent_SpaceOnButton_InvokesClick()
    {
        bool clicked = false;
        var button = new Button("Click", () => { clicked = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);
        FocusManager.RequestFocus(button);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Space,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(clicked).IsTrue();
    }

    [Test]
    public async Task HandleKeyEvent_KeyUp_IsIgnored()
    {
        bool clicked = false;
        var button = new Button("Click", () => { clicked = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);
        FocusManager.RequestFocus(button);

        // KeyUp should be ignored
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Enter,
            Type = NativeKeyEventType.KeyUp,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(clicked).IsFalse();
    }

    [Test]
    public async Task HandleKeyEvent_MenuBarShortcut_InvokesActionWithoutOpening()
    {
        bool undone = false;
        var menuBar = new MenuBar(
            new Menu("Edit",
                MenuItem.Action("Undo",
                    Hotkey.From(ModifierKeys.Ctrl, Key.Z),
                    () => { undone = true; })));
        var root = new Column(children: [menuBar]);
        root.LayoutData.Bounds = new Rect(0, 0, 200, 200);
        root.LayoutData.IsVisible = true;

        dispatcher.SetRoot(root);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Z,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.Ctrl
        });

        await Assert.That(undone).IsTrue();
        await Assert.That(menuBar.IsOpen).IsFalse();
    }

    [Test]
    public async Task HandleKeyEvent_MenuBarShortcut_DisabledItem_DoesNotInvoke()
    {
        bool invoked = false;
        var menuBar = new MenuBar(
            new Menu("Edit",
                MenuItem.Action("Undo",
                    Hotkey.From(ModifierKeys.Ctrl, Key.Z),
                    () => { invoked = true; },
                    enabled: false)));
        var root = new Column(children: [menuBar]);
        root.LayoutData.Bounds = new Rect(0, 0, 200, 200);
        root.LayoutData.IsVisible = true;

        dispatcher.SetRoot(root);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Z,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.Ctrl
        });

        await Assert.That(invoked).IsFalse();
    }

    [Test]
    public async Task HandleKeyEvent_MenuBarShortcut_RequiresExactModifiers()
    {
        bool invoked = false;
        var menuBar = new MenuBar(
            new Menu("Edit",
                MenuItem.Action("Undo",
                    Hotkey.From(ModifierKeys.Ctrl, Key.Z),
                    () => { invoked = true; })));
        var root = new Column(children: [menuBar]);
        root.LayoutData.Bounds = new Rect(0, 0, 200, 200);
        root.LayoutData.IsVisible = true;

        dispatcher.SetRoot(root);

        // Ctrl+Shift+Z must not trigger a plain Ctrl+Z accelerator.
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Z,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.Ctrl | ModifierKeys.Shift
        });

        await Assert.That(invoked).IsFalse();
    }

    [Test]
    public async Task HandleKeyEvent_KeyBindingDispatched()
    {
        bool handlerCalled = false;
        var label = new Label("Content");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;

        var keyHandler = new KeyHandler(
            label,
            new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.S), () => { handlerCalled = true; }));
        keyHandler.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        keyHandler.LayoutData.IsVisible = true;

        dispatcher.SetRoot(keyHandler);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.S,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.Ctrl
        });

        await Assert.That(handlerCalled).IsTrue();
    }

    [Test]
    public async Task HandleKeyEvent_KeyBindingWithWhenFalse_NotDispatched()
    {
        bool handlerCalled = false;
        var label = new Label("Content");
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        label.LayoutData.IsVisible = true;

        var keyHandler = new KeyHandler(
            label,
            new KeyBinding(new Hotkey(ModifierKeys.Ctrl, Key.S), () => { handlerCalled = true; }, When: false));
        keyHandler.LayoutData.Bounds = new Rect(0, 0, 100, 30);
        keyHandler.LayoutData.IsVisible = true;

        dispatcher.SetRoot(keyHandler);

        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.S,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.Ctrl
        });

        await Assert.That(handlerCalled).IsFalse();
    }

    // ── Scroll tests ──────────────────────────────────────────────────

    [Test]
    public async Task HandleScrollEvent_NoRoot_DoesNotThrow()
    {
        var evt = new NativeScrollEvent
        {
            X = 50,
            Y = 50,
            DeltaX = 0,
            DeltaY = 120,
            Modifiers = ModifierKeys.None
        };

        await Assert.That(() => dispatcher.HandleScrollEvent(evt)).ThrowsNothing();
    }

    [Test]
    public async Task HandleScrollEvent_FiresScrollGesture()
    {
        Point? scrollDelta = null;
        var label = new Label("Scrollable");
        label.LayoutData.Bounds = new Rect(0, 0, 200, 200);
        label.LayoutData.IsVisible = true;
        label.LayoutData.GestureData = new GestureNodeData
        {
            Scroll = (delta) => { scrollDelta = delta; }
        };

        dispatcher.SetRoot(label);

        dispatcher.HandleScrollEvent(new NativeScrollEvent
        {
            X = 100,
            Y = 100,
            DeltaX = 0,
            DeltaY = 120,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(scrollDelta).IsNotNull();
        await Assert.That(scrollDelta!.Value.Y).IsEqualTo(120f);
    }

    // ── Click on different node than press ────────────────────────────

    [Test]
    public async Task HandleMouseEvent_UpOnDifferentNode_DoesNotTap()
    {
        bool tapped = false;
        var button = new Button("Don't click", () => { tapped = true; });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        var label = new Label("Other");
        label.LayoutData.Bounds = new Rect(110, 0, 100, 40);
        label.LayoutData.IsVisible = true;

        var row = new Row(children: [button, label]);
        row.LayoutData.Bounds = new Rect(0, 0, 220, 40);
        row.LayoutData.IsVisible = true;

        dispatcher.SetRoot(row);

        // Press on button
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 20,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        // Release on label (different node)
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 160, Y = 20,
            Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(tapped).IsFalse();
    }

    // ── Focus on mouse click ──────────────────────────────────────────

    [Test]
    public async Task HandleMouseEvent_ClickOnButton_FocusesButton()
    {
        var button = new Button("Focusable", () => { });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        dispatcher.SetRoot(button);

        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 50, Y = 20,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(button);
    }

    [Test]
    public async Task HandleMouseEvent_ClickOnEmptySpace_ClearsFocus()
    {
        var button = new Button("Focus", () => { });
        button.LayoutData.Bounds = new Rect(0, 0, 100, 40);
        button.LayoutData.IsVisible = true;

        var row = new Row(children: [button]);
        row.LayoutData.Bounds = new Rect(0, 0, 300, 40);
        row.LayoutData.IsVisible = true;

        dispatcher.SetRoot(row);

        // Click on button first
        FocusManager.RequestFocus(button);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(button);

        // Click on empty space (row area but not button)
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 200, Y = 20,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        await Assert.That(FocusManager.FocusedElement).IsNull();
    }

    [Test]
    public async Task HandleMouseEvent_DragSplitViewDivider_UpdatesPosition()
    {
        // Set up a horizontal SplitView: 320px wide, first pane 128px (40%), divider gap, second pane
        var first = new TestLeaf(128, 120);
        first.LayoutData.Bounds = new Rect(0, 0, 128, 120);
        first.LayoutData.IsVisible = true;

        var second = new TestLeaf(184, 120);
        second.LayoutData.Bounds = new Rect(136, 0, 184, 120);
        second.LayoutData.IsVisible = true;

        var split = new SplitView(first, second);
        split.LayoutData.Bounds = new Rect(0, 0, 320, 120);
        split.LayoutData.IsVisible = true;
        split.LayoutIndex = 0;

        dispatcher.SetRoot(split);

        // MouseDown in the divider gap (x=132 is between first [0-128] and second [136-320])
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 132, Y = 60,
            Type = NativeMouseEventType.MouseDown,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        // MouseMove to drag right by 40px
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 172, Y = 60,
            Type = NativeMouseEventType.MouseMove,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });

        // Verify the override was stored
        float? overrideSize = InputDispatcher.GetSplitViewOverride(0);
        await Assert.That(overrideSize).IsNotNull();
        // Original first width was 128, dragged right 40px → 168
        await Assert.That(overrideSize!.Value).IsEqualTo(168f);

        // MouseUp to end drag
        dispatcher.HandleMouseEvent(new NativeMouseEvent
        {
            X = 172, Y = 60,
            Type = NativeMouseEventType.MouseUp,
            Button = NativeMouseButton.Left,
            Modifiers = ModifierKeys.None
        });
    }

    // ── Tab focus traversal ───────────────────────────────────────────
    // Regression coverage for the NewUserForm report: Tab did nothing on a
    // fresh form (controls only joined the tab order after a click), and once
    // fields were reachable, tabbing between them displayed and committed the
    // previous field's text (the shared edit buffer was not re-seeded).

    private static TextInput BoundInput(Func<string> get, Action<string> set)
    {
        return new TextInput(new Bindable<string>(get(), set));
    }

    private void TypeChar(char c)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.A,
            Character = c,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = ModifierKeys.None
        });
    }

    private void PressTab(bool shift = false)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Tab,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = shift ? ModifierKeys.Shift : ModifierKeys.None
        });
    }

    [Test]
    public async Task Tab_MovesFocusToNextControl_WithoutPriorClick()
    {
        var first = new TextInput(new Bindable<string>("", _ => { }));
        var second = new TextInput(new Bindable<string>("", _ => { }));
        var root = new Column(children: [first, second]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(first);

        // Fresh tree — neither field was ever clicked, so previously nothing
        // was registered as focusable and Tab had nowhere to go.
        PressTab();

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(second);
    }

    [Test]
    public async Task ShiftTab_MovesFocusToPreviousControl()
    {
        var first = new TextInput(new Bindable<string>("", _ => { }));
        var second = new TextInput(new Bindable<string>("", _ => { }));
        var root = new Column(children: [first, second]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(second);

        PressTab(shift: true);

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(first);
    }

    [Test]
    public async Task Tab_SkipsDisabledControls()
    {
        var first = new TextInput(new Bindable<string>("", _ => { }));
        var disabled = new Button("Submit", () => { }).Disabled();
        var last = new TextInput(new Bindable<string>("", _ => { }));
        var root = new Column(children: [first, disabled, last]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(first);

        PressTab();

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(last);
    }

    [Test]
    public async Task Tab_WrapsAroundToFirstControl()
    {
        var first = new TextInput(new Bindable<string>("", _ => { }));
        var second = new TextInput(new Bindable<string>("", _ => { }));
        var root = new Column(children: [first, second]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(second);

        PressTab();

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(first);
    }

    [Test]
    public async Task Tab_ReSeedsEditBuffer_NoTextBleedBetweenFields()
    {
        string nameValue = "";
        string emailValue = "";
        var name = BoundInput(() => nameValue, v => nameValue = v);
        var email = BoundInput(() => emailValue, v => emailValue = v);
        var root = new Column(children: [name, email]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(name);

        TypeChar('J');
        TypeChar('o');

        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(email);

        TypeChar('a');
        TypeChar('b');

        // Each field keeps its own text — the email field must not inherit or
        // overwrite with the name field's buffer.
        await Assert.That(nameValue).IsEqualTo("Jo");
        await Assert.That(emailValue).IsEqualTo("ab");
    }

    [Test]
    public async Task Tab_IntoField_ShowsThatFieldsOwnValue()
    {
        // The focused field paints ActiveEditBuffer; after tabbing in it must
        // reflect the destination field's value, not the source field's.
        string aValue = "source-text";
        string bValue = "dest-text";
        var a = BoundInput(() => aValue, v => aValue = v);
        var b = BoundInput(() => bValue, v => bValue = v);
        var root = new Column(children: [a, b]);

        dispatcher.SetRoot(root);
        FocusManager.RequestFocus(a);
        // Prime the buffer for 'a' as a click/keystroke would.
        TypeChar('!');

        PressTab();

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(b);
        await Assert.That(InputDispatcher.ActiveEditBuffer).IsEqualTo("dest-text");
    }
}

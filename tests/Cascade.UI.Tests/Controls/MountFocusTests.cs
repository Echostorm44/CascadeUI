namespace Cascade.UI.Tests.Controls;

/// <summary>
/// .AutoFocus() and .InitialFocus() were stored but never read. A node asking for focus on mount
/// now gets it after its first layout — once, not on every re-render — and focus moved by code
/// re-points the text edit buffer like a click or Tab does.
/// </summary>
[NotInParallel(nameof(FocusManager))]
public class MountFocusTests
{
    [Before(Test)]
    public void SetUp()
    {
        FocusManager.Reset();
    }

    [Test]
    public async Task AutoFocus_FocusesOnMount_AndNotAgainAfterFocusMoves()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<AutoFocusForm>(800, 600);

        await Assert.That(FocusManager.FocusedElement).IsTypeOf<TextInput>();
        var first = (TextInput)FocusManager.FocusedElement!;
        await Assert.That(first.Placeholder.Resolve()).IsEqualTo("second");

        // Move focus away; a re-render must not pull it back.
        FocusManager.ClearFocus();
        AutoFocusForm.Instance!.Bump();
        orchestrator.Tick();

        await Assert.That(FocusManager.FocusedElement).IsNull();
    }

    [Test]
    public async Task AutoFocus_FieldMountedLater_TakesFocusFromTheMountedOne()
    {
        // ClipClop2: the search box (AutoFocus) stays mounted while Ctrl+K mounts a menu whose own
        // search box asks for focus — typing must then go to the menu's box.
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<OverlayForm>(800, 600);
        await Assert.That(((TextInput)FocusManager.FocusedElement!).Placeholder.Resolve()).IsEqualTo("search");

        OverlayForm.Instance!.Open(true);
        orchestrator.Tick();

        await Assert.That(FocusManager.FocusedElement).IsTypeOf<TextInput>();
        await Assert.That(((TextInput)FocusManager.FocusedElement!).Placeholder.Resolve()).IsEqualTo("menu");
    }

    [Test]
    public async Task InitialFocus_FocusesTheReferencedNode()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<TrapForm>(800, 600);

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(TrapForm.Instance!.Target.Node);
    }

    [Test]
    public async Task FocusMovedByCode_ShowsTheNewFieldsText()
    {
        string a = "";
        string b = "bee";
        var first = new TextInput(new Bindable<string>(a, v => a = v));
        var second = new TextInput(new Bindable<string>(b, v => b = v));
        var dispatcher = new InputDispatcher();
        dispatcher.SetRoot(new Column(children: [first, second]));
        FocusManager.RequestFocus(first);
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Type = NativeKeyEventType.KeyDown, Key = Key.None, Character = 'x' });

        FocusManager.RequestFocus(second);

        await Assert.That(InputDispatcher.ActiveEditBuffer).IsEqualTo("bee");
        dispatcher.HandleKeyEvent(new NativeKeyEvent { Type = NativeKeyEventType.KeyDown, Key = Key.None, Character = '!' });
        await Assert.That(b).IsEqualTo("bee!");
        await Assert.That(a).IsEqualTo("x");
    }

    private sealed class AutoFocusForm : Component
    {
        private int renders;

        public AutoFocusForm()
        {
            Instance = this;
        }

        public static AutoFocusForm? Instance { get; private set; }

        public void Bump()
        {
            renders++;
            Invalidate();
        }

        protected override Node Render()
        {
            return new Column(children:
            [
                new Label($"renders {renders}"),
                new TextInput(new Bindable<string>("", _ => { }), placeholder: "first"),
                new TextInput(new Bindable<string>("", _ => { }), placeholder: "second").AutoFocus(),
            ]);
        }
    }

    private sealed class OverlayForm : Component
    {
        private bool open;

        public OverlayForm()
        {
            Instance = this;
        }

        public static OverlayForm? Instance { get; private set; }

        public void Open(bool value)
        {
            open = value;
            Invalidate();
        }

        protected override Node Render()
        {
            Node main = new Column(children:
            [
                new TextInput(new Bindable<string>("", _ => { }), placeholder: "search").AutoFocus(),
                new Label("list"),
            ]);
            // The overlay slot is always there (Node.Empty when closed), so the search box keeps its
            // identity; swapping the root between main and a Stack would remount it as a new AutoFocus node.
            return new Stack(main, open
                ? new TextInput(new Bindable<string>("", _ => { }), placeholder: "menu").AutoFocus()
                : Node.Empty);
        }
    }

    private sealed class TrapForm : Component
    {
        public TrapForm()
        {
            Instance = this;
        }

        public static TrapForm? Instance { get; private set; }

        public NodeRef<TextInput> Target { get; } = new();

        protected override Node Render()
        {
            return new Column(children:
                [
                    new TextInput(new Bindable<string>("", _ => { })),
                    new TextInput(new Bindable<string>("", _ => { })).Ref(Target),
                ])
                .FocusTrap()
                .InitialFocus(Target);
        }
    }
}

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// <c>.FocusTrap()</c> on page content was stored and never read: Tab walked straight out of the
/// trapped subtree. While focus is inside a trap, Tab and Shift+Tab now cycle through the trap's
/// own tab stops. From outside, Tab walks the page as before and may enter the trap.
/// </summary>
[NotInParallel(nameof(FocusManager))]
public class FocusTrapTests
{
    private InputDispatcher dispatcher = null!;

    [Before(Test)]
    public void SetUp()
    {
        FocusManager.Reset();
        dispatcher = new InputDispatcher();
    }

    [After(Test)]
    public void TearDown()
    {
        FocusManager.Reset();
    }

    private static Button Btn(string label)
    {
        return new Button(label, onClick: () => { });
    }

    private void PressTab(bool shift = false)
    {
        dispatcher.HandleKeyEvent(new NativeKeyEvent
        {
            Key = Key.Tab,
            Type = NativeKeyEventType.KeyDown,
            Modifiers = shift ? ModifierKeys.Shift : ModifierKeys.None,
        });
    }

    [Test]
    public async Task Tab_InsideTrap_WrapsToTheTrapsFirstStop()
    {
        var before = Btn("before");
        var a = Btn("a");
        var b = Btn("b");
        var after = Btn("after");
        dispatcher.SetRoot(new Column(children: [before, new Column(children: [a, b]).FocusTrap(), after]));

        FocusManager.RequestFocus(a);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(b);

        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(a);
    }

    [Test]
    public async Task ShiftTab_InsideTrap_WrapsToTheTrapsLastStop()
    {
        var before = Btn("before");
        var a = Btn("a");
        var b = Btn("b");
        var after = Btn("after");
        dispatcher.SetRoot(new Column(children: [before, new Column(children: [a, b]).FocusTrap(), after]));

        FocusManager.RequestFocus(a);
        PressTab(shift: true);

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(b);
    }

    [Test]
    public async Task Tab_FromOutside_EntersTheTrap_ThenStaysInside()
    {
        var before = Btn("before");
        var a = Btn("a");
        var b = Btn("b");
        var after = Btn("after");
        dispatcher.SetRoot(new Column(children: [before, new Column(children: [a, b]).FocusTrap(), after]));

        FocusManager.RequestFocus(before);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(a);

        PressTab();
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(a);
    }

    [Test]
    public async Task Tab_OutsideTheTrap_WalksThePageNormally()
    {
        var before = Btn("before");
        var a = Btn("a");
        var after = Btn("after");
        dispatcher.SetRoot(new Column(children: [new Column(children: [a]).FocusTrap(), before, after]));

        FocusManager.RequestFocus(before);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(after);

        // Wrapping from the page's last stop lands on its first — which is in the trap.
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(a);
    }

    [Test]
    public async Task NestedTraps_TheInnermostApplies()
    {
        var outerA = Btn("outer-a");
        var innerA = Btn("inner-a");
        var innerB = Btn("inner-b");
        var outerB = Btn("outer-b");
        var inner = new Column(children: [innerA, innerB]).FocusTrap();
        dispatcher.SetRoot(new Column(children: [outerA, inner, outerB]).FocusTrap());

        FocusManager.RequestFocus(innerB);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(innerA);

        FocusManager.RequestFocus(outerB);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(outerA);
    }

    [Test]
    public async Task FocusTrapFalse_DoesNotTrap()
    {
        var a = Btn("a");
        var after = Btn("after");
        dispatcher.SetRoot(new Column(children: [new Column(children: [a]).FocusTrap(false), after]));

        FocusManager.RequestFocus(a);
        PressTab();

        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(after);
    }

    [Test]
    public async Task Trap_SkipsDisabledStopsInside()
    {
        var a = Btn("a");
        var disabled = new Button("disabled", onClick: () => { }).Disabled(true);
        var c = Btn("c");
        dispatcher.SetRoot(new Column(children: [Btn("before"), new Column(children: [a, disabled, c]).FocusTrap()]));

        FocusManager.RequestFocus(a);
        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(c);

        PressTab();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(a);
    }

    [Test]
    public async Task MountedTrapWithInitialFocus_TabCyclesInsideIt()
    {
        using var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<TrapPage>(800, 600);
        var page = TrapPage.Instance!;
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(page.Second.Node);

        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.Tab, Type = NativeKeyEventType.KeyDown });
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(page.First.Node);

        orchestrator.Input.HandleKeyEvent(new NativeKeyEvent { Key = Key.Tab, Type = NativeKeyEventType.KeyDown });
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(page.Second.Node);
    }

    private sealed class TrapPage : Component
    {
        public TrapPage()
        {
            Instance = this;
        }

        public static TrapPage? Instance { get; private set; }

        public NodeRef<TextInput> First { get; } = new();

        public NodeRef<TextInput> Second { get; } = new();

        protected override Node Render()
        {
            return new Column(children:
            [
                new Button("page", onClick: () => { }),
                new Column(children:
                    [
                        new TextInput(new Bindable<string>("", _ => { })).Ref(First),
                        new TextInput(new Bindable<string>("", _ => { })).Ref(Second),
                    ])
                    .FocusTrap()
                    .InitialFocus(Second),
                new Button("after", onClick: () => { }),
            ]);
        }
    }
}

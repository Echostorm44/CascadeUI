using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000 // Test components are managed by the test lifecycle
#pragma warning disable CA1812 // Test components are instantiated via reflection/test framework

namespace Cascade.UI.Tests;

public class ReconcilerTests
{
    // ── Test component stubs ─────────────────────────────────────

    private sealed class SimpleComponent : Component
    {
        private readonly Node content;
        public int RenderCount { get; private set; }

        public SimpleComponent(Node? content = null)
        {
            this.content = content ?? Node.Empty;
        }

        protected override Node Render()
        {
            RenderCount++;
            return this.content;
        }
    }

    private sealed class DifferentComponent : Component
    {
        protected override Node Render()
        {
            return new Label("different");
        }
    }

    private sealed class MountCountingPage : Component
    {
        public int MountCount { get; private set; }

        protected override Node Render()
        {
            return new Label("page");
        }

        protected override Task OnMounted()
        {
            MountCount++;
            return Task.CompletedTask;
        }
    }

    // ── NodeDiffer Tests ─────────────────────────────────────────

    [Test]
    public async Task Diff_NullOldTree_ProducesInsert()
    {
        var newTree = new Label("hello");
        var plan = NodeDiffer.Diff(null, newTree);

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Insert);
    }

    [Test]
    public async Task Diff_EmptyToLabel_ProducesInsert()
    {
        var plan = NodeDiffer.Diff(Node.Empty, new Label("hello"));

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Insert);
    }

    [Test]
    public async Task Diff_LabelToEmpty_ProducesDelete()
    {
        var plan = NodeDiffer.Diff(new Label("hello"), Node.Empty);

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Delete);
    }

    [Test]
    public async Task Diff_SameTypeLabels_ProducesUpdate()
    {
        var plan = NodeDiffer.Diff(new Label("old"), new Label("new"));

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Update);
    }

    [Test]
    public async Task Diff_DifferentTypes_ProducesReplace()
    {
        var oldTree = new Label("hello");
        var newTree = new Column(children: new Node[] { new Label("world") });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Replace);
    }

    [Test]
    public async Task Diff_ComponentSameType_ProducesReuseComponent()
    {
        using var old = new SimpleComponent();
        using var @new = new SimpleComponent();

        var plan = NodeDiffer.Diff(old, @new);

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.ReuseComponent);
    }

    [Test]
    public async Task Diff_ComponentDifferentType_ProducesReplace()
    {
        using var old = new SimpleComponent();
        using var @new = new DifferentComponent();

        var plan = NodeDiffer.Diff(old, @new);

        await Assert.That(plan.Count).IsEqualTo(1);
        await Assert.That(plan.Operations[0].Kind).IsEqualTo(DiffKind.Replace);
    }

    [Test]
    public async Task Diff_ColumnWithChildren_DiffsPositionally()
    {
        var oldTree = new Column(children: new Node[]
        {
            new Label("a"),
            new Label("b")
        });
        var newTree = new Column(children: new Node[]
        {
            new Label("a"),
            new Label("b"),
            new Label("c")
        });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        bool hasInsert = false;
        foreach (var op in plan.Operations)
        {
            if (op.Kind == DiffKind.Insert && op.Index == 2)
            {
                hasInsert = true;
            }
        }

        await Assert.That(hasInsert).IsTrue();
    }

    [Test]
    public async Task Diff_ColumnChildRemoved_ProducesDelete()
    {
        var oldTree = new Column(children: new Node[]
        {
            new Label("a"),
            new Label("b"),
            new Label("c")
        });
        var newTree = new Column(children: new Node[]
        {
            new Label("a"),
            new Label("b")
        });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        bool hasDelete = false;
        foreach (var op in plan.Operations)
        {
            if (op.Kind == DiffKind.Delete && op.Index == 2)
            {
                hasDelete = true;
            }
        }

        await Assert.That(hasDelete).IsTrue();
    }

    // ── Keyed children tests ─────────────────────────────────────

    [Test]
    public async Task Diff_KeyedChildren_DetectsMove()
    {
        var oldTree = new Column(children: new Node[]
        {
            new Label("a").Key("a"),
            new Label("b").Key("b"),
            new Label("c").Key("c")
        });
        var newTree = new Column(children: new Node[]
        {
            new Label("c").Key("c"),
            new Label("a").Key("a"),
            new Label("b").Key("b")
        });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        bool hasMove = false;
        foreach (var op in plan.Operations)
        {
            if (op.Kind == DiffKind.Move)
            {
                hasMove = true;
            }
        }

        await Assert.That(hasMove).IsTrue();
    }

    [Test]
    public async Task Diff_KeyedChildren_DetectsInsertionByKey()
    {
        var oldTree = new Column(children: new Node[]
        {
            new Label("a").Key("a"),
            new Label("b").Key("b")
        });
        var newTree = new Column(children: new Node[]
        {
            new Label("a").Key("a"),
            new Label("new").Key("new"),
            new Label("b").Key("b")
        });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        bool hasInsert = false;
        foreach (var op in plan.Operations)
        {
            if (op.Kind == DiffKind.Insert && op.Index == 1)
            {
                hasInsert = true;
            }
        }

        await Assert.That(hasInsert).IsTrue();
    }

    [Test]
    public async Task Diff_KeyedChildren_DetectsDeletionByKey()
    {
        var oldTree = new Column(children: new Node[]
        {
            new Label("a").Key("a"),
            new Label("b").Key("b"),
            new Label("c").Key("c")
        });
        var newTree = new Column(children: new Node[]
        {
            new Label("a").Key("a"),
            new Label("c").Key("c")
        });

        var plan = NodeDiffer.Diff(oldTree, newTree);

        bool hasDelete = false;
        foreach (var op in plan.Operations)
        {
            if (op.Kind == DiffKind.Delete)
            {
                hasDelete = true;
            }
        }

        await Assert.That(hasDelete).IsTrue();
    }

    // ── Reconciler tests ─────────────────────────────────────────

    [Test]
    public async Task Reconciler_MountTree_MountsComponents()
    {
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var hosts = new Dictionary<string, ComponentHost>();

        var tree = new Column(children: new Node[]
        {
            new SimpleComponent(),
            new Label("text")
        });

        reconciler.MountTree(tree, hosts, depth: 0);

        await Assert.That(hosts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Reconciler_MountTree_MountsNestedComponents()
    {
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var hosts = new Dictionary<string, ComponentHost>();

        var tree = new Column(children: new Node[]
        {
            new Row(children: new Node[]
            {
                new SimpleComponent(),
                new SimpleComponent()
            })
        });

        reconciler.MountTree(tree, hosts, depth: 0);

        await Assert.That(hosts.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Reconciler_UnmountAll_UnmountsAllHosts()
    {
        var scheduler = new RenderScheduler();
        var hosts = new Dictionary<string, ComponentHost>();

        var comp1 = new SimpleComponent();
        var comp2 = new SimpleComponent();
        var host1 = new ComponentHost(comp1, scheduler, treeDepth: 0);
        var host2 = new ComponentHost(comp2, scheduler, treeDepth: 0);
        host1.Mount();
        host2.Mount();
        hosts["a"] = host1;
        hosts["b"] = host2;

        Reconciler.UnmountAll(hosts);

        await Assert.That(host1.IsMounted).IsFalse();
        await Assert.That(host2.IsMounted).IsFalse();
        await Assert.That(hosts.Count).IsEqualTo(0);
    }

    // ── DiffPlan tests ───────────────────────────────────────────

    [Test]
    public async Task DiffPlan_EmptyByDefault()
    {
        var plan = new DiffPlan();

        await Assert.That(plan.Count).IsEqualTo(0);
        await Assert.That(plan.HasChanges).IsFalse();
    }

    [Test]
    public async Task DiffPlan_TracksOperations()
    {
        var plan = new DiffPlan();
        plan.Add(new DiffOperation(DiffKind.Insert, 0, null, new Label("a")));
        plan.Add(new DiffOperation(DiffKind.Delete, 1, new Label("b"), null));

        await Assert.That(plan.Count).IsEqualTo(2);
        await Assert.That(plan.HasChanges).IsTrue();
    }

    // ── Node.Key fluent method tests ─────────────────────────────

    [Test]
    public async Task Node_Key_SetsReconciliationKey()
    {
        var label = new Label("test").Key("my-key");

        await Assert.That(label.ReconciliationKey).IsEqualTo("my-key");
    }

    [Test]
    public async Task Node_Key_ReturnsSameNode()
    {
        var label = new Label("test");
        var result = label.Key("my-key");

        await Assert.That(result).IsSameReferenceAs(label);
    }

    // ── Node.Ref tests ───────────────────────────────────────────

    [Test]
    public async Task Node_Ref_SetsNodeOnRef()
    {
        var nodeRef = new NodeRef<Label>();
        var label = new Label("test").Ref(nodeRef);

        await Assert.That(nodeRef.Node).IsSameReferenceAs(label);
    }

    [Test]
    public async Task Node_Ref_ReturnsTypedNode()
    {
        var nodeRef = new NodeRef<Label>();
        var label = new Label("test");
        Label returned = label.Ref(nodeRef);

        await Assert.That(returned).IsSameReferenceAs(label);
    }

    [Test]
    public async Task NodeRef_SetMounted_SetsState()
    {
        var nodeRef = new NodeRef<Label>();
        var bounds = new Rect(10, 20, 100, 50);

        ((INodeRefInternal)nodeRef).SetMounted(bounds);

        await Assert.That(nodeRef.IsMounted).IsTrue();
        await Assert.That(nodeRef.Bounds).IsEqualTo(bounds);
    }

    [Test]
    public async Task NodeRef_WaitForMountAsync_CompletesImmediatelyIfMounted()
    {
        var nodeRef = new NodeRef<Label>();
        var bounds = new Rect(10, 20, 100, 50);
        ((INodeRefInternal)nodeRef).SetMounted(bounds);

        var result = await nodeRef.WaitForMountAsync(CancellationToken.None);

        await Assert.That(result).IsEqualTo(bounds);
    }

    [Test]
    public async Task NodeRef_WaitForMountAsync_WaitsUntilMounted()
    {
        var nodeRef = new NodeRef<Label>();
        var bounds = new Rect(10, 20, 100, 50);

        var waitTask = nodeRef.WaitForMountAsync(CancellationToken.None);

        await Assert.That(waitTask.IsCompleted).IsFalse();

        ((INodeRefInternal)nodeRef).SetMounted(bounds);

        var result = await waitTask;
        await Assert.That(result).IsEqualTo(bounds);
    }

    [Test]
    public async Task NodeRef_WaitForMountAsync_CancellationThrows()
    {
        var nodeRef = new NodeRef<Label>();
        using var cts = new CancellationTokenSource();

        var waitTask = nodeRef.WaitForMountAsync(cts.Token);
        await cts.CancelAsync();

        var threw = false;
        try
        {
            await waitTask;
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    // ── Interactive state transfer tests ─────────────────────────

    [Test, NotInParallel("FocusManager")]
    public async Task NotifyNodeReplaced_TransfersFocusDirectly()
    {
        FocusManager.Reset();

        var oldButton = new Button("Click", () => { });
        var newButton = new Button("Click", () => { });

        FocusManager.RequestFocus(oldButton);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(oldButton);

        FocusManager.NotifyNodeReplaced(oldButton, newButton);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(newButton);

        FocusManager.Reset();
    }

    [Test, NotInParallel("FocusManager")]
    public async Task Update_TransfersFocusToNewNode()
    {
        FocusManager.Reset();
        ControlStateAnimator.Reset();

        var oldButton = new Button("Click", () => { });
        var newButton = new Button("Click", () => { });

        // Focus the old button (simulating a click focus)
        FocusManager.RequestFocus(oldButton);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(oldButton);

        // Simulate reconciliation: diff produces Update, which transfers state
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldButton });
        var newTree = new Column(children: new Node[] { newButton });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // Focus should now point to the new button
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(newButton);

        FocusManager.Reset();
        ControlStateAnimator.Reset();
    }

    [Test]
    public async Task Update_TransfersHoverAndPressToNewNode()
    {
        ControlStateAnimator.Reset();

        var oldButton = new Button("Click", () => { });
        var newButton = new Button("Click", () => { });

        oldButton.IsHovered = true;
        oldButton.IsPressed = true;

        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldButton });
        var newTree = new Column(children: new Node[] { newButton });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        await Assert.That(newButton.IsHovered).IsTrue();
        await Assert.That(newButton.IsPressed).IsTrue();

        ControlStateAnimator.Reset();
    }

    [Test]
    public async Task Update_TransfersAnimationStateToNewNode()
    {
        ControlStateAnimator.Reset();

        var oldButton = new Button("Click", () => { });
        var newButton = new Button("Click", () => { });

        // Create animation state on old button by reconciling it
        oldButton.IsHovered = true;
        var anim = ControlStateAnimator.Reconcile(
            oldButton,
            AnimationModel.Spring.Snappy,
            AnimationModel.Spring.Snappy,
            isDisabled: false,
            isFocused: false);

        // Verify old button has animation state (Hover target should be 1)
        await Assert.That(anim.Hover.Target).IsEqualTo(1f);

        // Now reconcile old→new (simulating tree rebuild)
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldButton });
        var newTree = new Column(children: new Node[] { newButton });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // New button should inherit the animation state
        newButton.IsHovered = true;
        var newAnim = ControlStateAnimator.Reconcile(
            newButton,
            AnimationModel.Spring.Snappy,
            AnimationModel.Spring.Snappy,
            isDisabled: false,
            isFocused: false);

        // The hover target should still be 1 (transferred, not fresh)
        await Assert.That(newAnim.Hover.Target).IsEqualTo(1f);

        ControlStateAnimator.Reset();
    }

    [Test, NotInParallel("FocusManager")]
    public async Task Update_TransfersFocusableRegistration()
    {
        FocusManager.Reset();
        ControlStateAnimator.Reset();

        var oldButton = new Button("Click", () => { });
        var newButton = new Button("Click", () => { });

        // Register and focus old button (simulating click→focus→Tab)
        FocusManager.RequestFocus(oldButton);
        FocusManager.LastFocusWasKeyboard = true;

        // Reconcile
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldButton });
        var newTree = new Column(children: new Node[] { newButton });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // Focus should point to new button AND LastFocusWasKeyboard should be preserved
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(newButton);
        await Assert.That(FocusManager.LastFocusWasKeyboard).IsTrue();

        // MoveFocus should still work (focusableNodes should have new button)
        bool moved = FocusManager.MoveFocus(FocusDirection.Next);
        await Assert.That(moved).IsTrue();
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(newButton);

        FocusManager.Reset();
        ControlStateAnimator.Reset();
    }

    // ── InputDispatcher.NotifyNodeReplaced Tests ─────────────────

    [Test]
    public async Task TransferInteractiveState_UpdatesPressedNodeReference()
    {
        // Regression test: when a re-render happens between MouseDown and MouseUp,
        // the Reconciler replaces old nodes with new instances. InputDispatcher's
        // pressedNode must be updated to the new instance so that ReferenceEquals
        // in HandleMouseUp still matches and the click handler fires.

        bool clicked = false;
        var oldButton = new Button("Click", () => { clicked = true; });
        var newButton = new Button("Click", () => { clicked = true; });

        // Suppress unused warning — clicked is used to make the lambda non-trivial
        _ = clicked;

        // Simulate mouse-down on the old button
        oldButton.IsPressed = true;

        // TransferInteractiveState should update InputDispatcher's references
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var oldTree = new Column(children: new Node[] { oldButton });
        var newTree = new Column(children: new Node[] { newButton });

        reconciler.Reconcile(oldTree, newTree, oldHosts, newHosts, depth: 0);

        // After reconciliation, IsPressed should be transferred to the new button
        await Assert.That(newButton.IsPressed).IsTrue();
        await Assert.That(oldButton.IsPressed).IsTrue(); // old still has it (not cleared by reconciler)
    }

    // ── NavigationTransitionHost incoming-page mounting ──────────

    [Test]
    public async Task NavigationTransition_IncomingPage_MountsDuringTransition()
    {
        // Regression: the incoming page must be mounted (and rendered) WHILE the
        // transition runs, not only when it completes. If GetChildren doesn't
        // expose the host's IncomingPage, the reconciler never mounts it, so it
        // has no RenderedTree and paints nothing mid-transition (the fade "goes
        // dark", slides show nothing sliding in, the curtain reveals blank).
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var initialPage = new SimpleComponent(new Label("home"));
        reconciler.Reconcile(null, initialPage, oldHosts, newHosts, depth: 0);
        oldHosts = newHosts;
        newHosts = [];

        var incoming = new MountCountingPage();
        var host = new NavigationTransitionHost
        {
            IncomingPage = incoming,
            OutgoingTree = initialPage.RenderedTree,
        };
        reconciler.Reconcile(initialPage, host, oldHosts, newHosts, depth: 0);

        await Assert.That(incoming.RenderedTree).IsNotNull();
        await Assert.That(incoming.MountCount).IsEqualTo(1);
    }

    [Test]
    public async Task NavigationTransition_Completion_DoesNotRemountIncoming()
    {
        // Regression: at completion the navigator returns the incoming page
        // directly (host → page). The incoming survives that switch, so it must
        // be REUSED, not unmounted-and-remounted (which would fire OnMounted a
        // second time per navigation and reload page data).
        var scheduler = new RenderScheduler();
        var reconciler = new Reconciler(scheduler);
        var oldHosts = new Dictionary<string, ComponentHost>();
        var newHosts = new Dictionary<string, ComponentHost>();

        var initialPage = new SimpleComponent(new Label("home"));
        reconciler.Reconcile(null, initialPage, oldHosts, newHosts, depth: 0);
        oldHosts = newHosts;
        newHosts = [];

        var incoming = new MountCountingPage();
        var host = new NavigationTransitionHost
        {
            IncomingPage = incoming,
            OutgoingTree = initialPage.RenderedTree,
        };
        reconciler.Reconcile(initialPage, host, oldHosts, newHosts, depth: 0);
        oldHosts = newHosts;
        newHosts = [];

        // Transition completes: navigator returns the incoming page directly.
        reconciler.Reconcile(host, incoming, oldHosts, newHosts, depth: 0);

        await Assert.That(incoming.RenderedTree).IsNotNull();
        await Assert.That(incoming.MountCount).IsEqualTo(1);
    }
}

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000 // Test components are managed by the test lifecycle

namespace Cascade.UI.Tests;

public class ReactivityTests
{
    // ── Signal tracking tests ────────────────────────────────────

    [Test]
    public async Task SignalTracker_BeginEndTracking_SetsActiveScope()
    {
        await Assert.That(SignalTracker.IsTracking).IsFalse();

        var scope = SignalTracker.BeginTracking();
        await Assert.That(SignalTracker.IsTracking).IsTrue();

        SignalTracker.EndTracking();
        await Assert.That(SignalTracker.IsTracking).IsFalse();
    }

    [Test]
    public async Task SignalTracker_RecordRead_TracksSignals()
    {
        var signal = new SignalSource("test.field");

        var scope = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal);
        SignalTracker.EndTracking();

        await Assert.That(scope.ReadSignals.Count).IsEqualTo(1);
        await Assert.That(scope.ReadSignals.Contains(signal)).IsTrue();
    }

    [Test]
    public async Task SignalTracker_RecordRead_DeduplicatesSameSignal()
    {
        var signal = new SignalSource("test.field");

        var scope = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal);
        SignalTracker.RecordRead(signal);
        SignalTracker.EndTracking();

        await Assert.That(scope.ReadSignals.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SignalTracker_RecordRead_OutsideScope_DoesNotThrow()
    {
        var signal = new SignalSource("test.field");

        // Should not throw when no scope is active
        SignalTracker.RecordRead(signal);

        // Verify no scope is active (confirms we're not accidentally inside a scope)
        await Assert.That(SignalTracker.IsTracking).IsFalse();
    }

    // ── SignalSource subscription tests ──────────────────────────

    [Test]
    public async Task SignalSource_Subscribe_NotifiesOnWrite()
    {
        var signal = new SignalSource("test.counter");
        int notifyCount = 0;

        signal.Subscribe(() => { notifyCount++; });
        SignalTracker.NotifyWrite(signal);

        await Assert.That(notifyCount).IsEqualTo(1);
    }

    [Test]
    public async Task SignalSource_MultipleSubscribers_AllNotified()
    {
        var signal = new SignalSource("test.counter");
        int count1 = 0;
        int count2 = 0;

        signal.Subscribe(() => { count1++; });
        signal.Subscribe(() => { count2++; });
        SignalTracker.NotifyWrite(signal);

        await Assert.That(count1).IsEqualTo(1);
        await Assert.That(count2).IsEqualTo(1);
    }

    [Test]
    public async Task SignalSource_Unsubscribe_StopsNotifications()
    {
        var signal = new SignalSource("test.counter");
        int notifyCount = 0;
        void Handler() { notifyCount++; }

        signal.Subscribe(Handler);
        SignalTracker.NotifyWrite(signal);
        await Assert.That(notifyCount).IsEqualTo(1);

        signal.Unsubscribe(Handler);
        SignalTracker.NotifyWrite(signal);
        await Assert.That(notifyCount).IsEqualTo(1);
    }

    [Test]
    public async Task SignalSource_ClearSubscribers_RemovesAll()
    {
        var signal = new SignalSource("test.counter");
        int notifyCount = 0;

        signal.Subscribe(() => { notifyCount++; });
        signal.Subscribe(() => { notifyCount++; });
        signal.ClearSubscribers();
        SignalTracker.NotifyWrite(signal);

        await Assert.That(notifyCount).IsEqualTo(0);
    }

    // ── TrackingScope subscription management ────────────────────

    [Test]
    public async Task TrackingScope_ApplySubscriptions_SubscribesToReadSignals()
    {
        var signal = new SignalSource("test.counter");
        int notifyCount = 0;

        var scope = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal);
        SignalTracker.EndTracking();

        scope.ApplySubscriptions(() => { notifyCount++; }, previousScope: null);
        SignalTracker.NotifyWrite(signal);

        await Assert.That(notifyCount).IsEqualTo(1);
    }

    [Test]
    public async Task TrackingScope_ApplySubscriptions_UnsubscribesOldSignals()
    {
        var signal1 = new SignalSource("test.field1");
        var signal2 = new SignalSource("test.field2");
        int notifyCount = 0;
        void Handler() { notifyCount++; }

        // First render: reads signal1 and signal2
        var scope1 = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal1);
        SignalTracker.RecordRead(signal2);
        SignalTracker.EndTracking();
        scope1.ApplySubscriptions(Handler, previousScope: null);

        // Second render: reads only signal1 (signal2 no longer read)
        var scope2 = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal1);
        SignalTracker.EndTracking();
        scope2.ApplySubscriptions(Handler, previousScope: scope1);

        // signal2 change should NOT trigger notification
        notifyCount = 0;
        SignalTracker.NotifyWrite(signal2);
        await Assert.That(notifyCount).IsEqualTo(0);

        // signal1 change SHOULD trigger notification
        SignalTracker.NotifyWrite(signal1);
        await Assert.That(notifyCount).IsEqualTo(1);
    }

    [Test]
    public async Task TrackingScope_RemoveAllSubscriptions_CleansUp()
    {
        var signal = new SignalSource("test.counter");
        int notifyCount = 0;
        void Handler() { notifyCount++; }

        var scope = SignalTracker.BeginTracking();
        SignalTracker.RecordRead(signal);
        SignalTracker.EndTracking();
        scope.ApplySubscriptions(Handler, previousScope: null);

        scope.RemoveAllSubscriptions(Handler);
        SignalTracker.NotifyWrite(signal);

        await Assert.That(notifyCount).IsEqualTo(0);
    }

    // ── RenderScheduler tests ────────────────────────────────────

    [Test]
    public async Task RenderScheduler_MarkDirty_IncreasesDirtyCount()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        scheduler.MarkDirty(host);

        await Assert.That(scheduler.DirtyCount).IsEqualTo(1);
    }

    [Test]
    public async Task RenderScheduler_MarkDirty_CoalescesMultipleMarks()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        scheduler.MarkDirty(host);
        scheduler.MarkDirty(host);
        scheduler.MarkDirty(host);

        await Assert.That(scheduler.DirtyCount).IsEqualTo(1);
    }

    [Test]
    public async Task RenderScheduler_ProcessFrame_ClearsDirtySet()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        scheduler.MarkDirty(host);
        scheduler.ProcessFrame();

        await Assert.That(scheduler.DirtyCount).IsEqualTo(0);
    }

    [Test]
    public async Task RenderScheduler_ProcessFrame_RendersComponents()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        int initialRenderCount = component.RenderCount;
        scheduler.MarkDirty(host);
        scheduler.ProcessFrame();

        await Assert.That(component.RenderCount).IsGreaterThan(initialRenderCount);
    }

    [Test]
    public async Task RenderScheduler_FrameRequested_FiredOnFirstDirty()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        int frameRequestCount = 0;
        scheduler.FrameRequested += () => { frameRequestCount++; };

        scheduler.MarkDirty(host);

        await Assert.That(frameRequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task RenderScheduler_RemoveDirty_PreventsRender()
    {
        var scheduler = new RenderScheduler();
        var component = new TestComponent();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);
        host.Mount();

        int initialRenderCount = component.RenderCount;
        scheduler.MarkDirty(host);
        scheduler.RemoveDirty(host);
        scheduler.ProcessFrame();

        await Assert.That(component.RenderCount).IsEqualTo(initialRenderCount);
    }

    [Test]
    public async Task RenderScheduler_ParentRendersBeforeChild()
    {
        var renderOrder = new List<string>();
        var scheduler = new RenderScheduler();

        var parent = new OrderTrackingComponent("parent", renderOrder);
        var child = new OrderTrackingComponent("child", renderOrder);

        var parentHost = new ComponentHost(parent, scheduler, treeDepth: 0);
        var childHost = new ComponentHost(child, scheduler, treeDepth: 1);

        parentHost.Mount();
        childHost.Mount();

        renderOrder.Clear();

        // Mark child first, then parent - scheduler should still render parent first
        scheduler.MarkDirty(childHost);
        scheduler.MarkDirty(parentHost);
        scheduler.ProcessFrame();

        await Assert.That(renderOrder.Count).IsEqualTo(2);
        await Assert.That(renderOrder[0]).IsEqualTo("parent");
        await Assert.That(renderOrder[1]).IsEqualTo("child");
    }

    // ── Integration: signal change triggers re-render ────────────

    [Test]
    public async Task SignalChange_MarksDirtyInScheduler()
    {
        var scheduler = new RenderScheduler();
        var signal = new SignalSource("test.counter");
        var component = new SignalReadingComponent(signal);
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();

        // At this point the host should have subscribed to the signal
        // via the tracking scope in ExecuteRender.
        // Writing to the signal should mark the host dirty.
        SignalTracker.NotifyWrite(signal);

        await Assert.That(scheduler.DirtyCount).IsEqualTo(1);
    }

    [Test]
    public async Task SignalChange_ProcessFrame_ReRendersComponent()
    {
        var scheduler = new RenderScheduler();
        var signal = new SignalSource("test.counter");
        var component = new SignalReadingComponent(signal);
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        int renderCountAfterMount = component.RenderCount;

        SignalTracker.NotifyWrite(signal);
        scheduler.ProcessFrame();

        await Assert.That(component.RenderCount).IsGreaterThan(renderCountAfterMount);
    }

    [Test]
    public async Task MultipleSignalChanges_CoalesceIntoSingleRender()
    {
        var scheduler = new RenderScheduler();
        var signal1 = new SignalSource("test.field1");
        var signal2 = new SignalSource("test.field2");
        var component = new MultiSignalComponent(signal1, signal2);
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        int renderCountAfterMount = component.RenderCount;

        // Both signals change within the same "frame"
        SignalTracker.NotifyWrite(signal1);
        SignalTracker.NotifyWrite(signal2);

        // Only one ProcessFrame call
        scheduler.ProcessFrame();

        // Should have rendered exactly once more
        await Assert.That(component.RenderCount).IsEqualTo(renderCountAfterMount + 1);
    }

    // ── Test helpers ─────────────────────────────────────────────

    private sealed class TestComponent : Component
    {
        public int RenderCount { get; private set; }

        protected override Node Render()
        {
            RenderCount++;
            return Node.Empty;
        }
    }

    private sealed class OrderTrackingComponent : Component
    {
        private readonly string name;
        private readonly List<string> renderOrder;

        public OrderTrackingComponent(string name, List<string> renderOrder)
        {
            this.name = name;
            this.renderOrder = renderOrder;
        }

        protected override Node Render()
        {
            renderOrder.Add(name);
            return Node.Empty;
        }
    }

    private sealed class SignalReadingComponent : Component
    {
        private readonly SignalSource signal;
        public int RenderCount { get; private set; }

        public SignalReadingComponent(SignalSource signal)
        {
            this.signal = signal;
        }

        protected override Node Render()
        {
            RenderCount++;
            // Simulate reading a signal during render
            SignalTracker.RecordRead(signal);
            return Node.Empty;
        }
    }

    private sealed class MultiSignalComponent : Component
    {
        private readonly SignalSource signal1;
        private readonly SignalSource signal2;
        public int RenderCount { get; private set; }

        public MultiSignalComponent(SignalSource signal1, SignalSource signal2)
        {
            this.signal1 = signal1;
            this.signal2 = signal2;
        }

        protected override Node Render()
        {
            RenderCount++;
            SignalTracker.RecordRead(signal1);
            SignalTracker.RecordRead(signal2);
            return Node.Empty;
        }
    }

    // ── Component.Bind (two-way binding helper) ───────────────────

    [Test]
    public async Task ComponentBind_CarriesTheCurrentFieldValue()
    {
        var page = new BindTestComponent();
        var bind = page.MakeNameBind();
        await Assert.That(bind.Value).IsEqualTo("start");
    }

    [Test]
    public async Task ComponentBind_OnChange_WritesFieldThenInvalidates()
    {
        var page = new BindTestComponent();
        int invalidations = 0;
        page.InvalidateCallback = () => invalidations++;

        var bind = page.MakeNameBind();
        bind.OnChange("changed");

        // The setter ran (field updated) and the component was scheduled to re-render.
        await Assert.That(page.NameValue).IsEqualTo("changed");
        await Assert.That(invalidations).IsEqualTo(1);
    }

    [Test]
    public async Task ComponentBind_HighFrequencyChanges_CoalesceToOneReRenderPerFrame()
    {
        // The performance guarantee: binding a control to a value that changes very often
        // must NOT multiply render work. Each change marks the component dirty, but the
        // RenderScheduler dedups (HashSet), so N changes within a frame collapse to a
        // single re-render. CPU/GPU cost is bounded by frame rate, not by change rate.
        var scheduler = new RenderScheduler();
        var page = new BindTestComponent();
        var host = new ComponentHost(page, scheduler, treeDepth: 0);
        host.Mount();

        var bind = page.MakeNameBind();
        for (int i = 0; i < 10_000; i++)
        {
            bind.OnChange("v" + i);
        }

        // 10,000 changes → exactly one dirty component → one re-render this frame.
        await Assert.That(scheduler.DirtyCount).IsEqualTo(1);
    }

    // Exercises the framework-level Bind() promoted to Component (Bind(value, setter)).
    private sealed class BindTestComponent : Component
    {
        private string name = "start";
        public string NameValue => name;
        public Bindable<string> MakeNameBind() => Bind(name, v => name = v);
        protected override Node Render() => Node.Empty;
    }
}

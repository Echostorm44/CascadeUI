using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000 // Components in tests are disposed via ComponentHost.Unmount or test lifecycle

namespace Cascade.UI.Tests;

public class ComponentLifecycleTests
{
    // ── Test component that records lifecycle events ──────────────

    private sealed class LifecycleComponent : Component
    {
        private readonly List<string> events;

        public LifecycleComponent(List<string> events)
        {
            this.events = events;
        }

        protected override Node Render()
        {
            events.Add("Render");
            return Node.Empty;
        }

        protected override Task OnMounted()
        {
            events.Add("OnMounted");
            return Task.CompletedTask;
        }

        protected override void OnUnmounted()
        {
            events.Add("OnUnmounted");
        }

        protected override void OnBoundsChanged(Rect previousBounds, Rect currentBounds)
        {
            events.Add($"OnBoundsChanged:{previousBounds.Width}x{previousBounds.Height}->{currentBounds.Width}x{currentBounds.Height}");
        }
    }

    // ── Test component that records the LifetimeToken state ──────

    private sealed class TokenTrackingComponent : Component
    {
        public bool TokenCancelledBeforeUnmount { get; private set; }

        protected override Node Render()
        {
            return Node.Empty;
        }

        protected override void OnUnmounted()
        {
            TokenCancelledBeforeUnmount = LifetimeToken.IsCancellationRequested;
        }
    }

    // ── Test component that throws in Render ─────────────────────

    private sealed class ThrowingRenderComponent : Component
    {
        protected override Node Render()
        {
            throw new InvalidOperationException("Render failed");
        }
    }

    // ── Test component that throws in OnMounted ──────────────────

    private sealed class ThrowingMountedComponent : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }

        protected override Task OnMounted()
        {
            throw new InvalidOperationException("OnMounted failed");
        }
    }

    // ── Test component with async OnMounted ──────────────────────

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Used as type reference for async lifecycle testing.")]
    private sealed class AsyncMountedComponent : Component
    {
        public bool MountCompleted { get; private set; }
        public bool CancellationObserved { get; private set; }

        protected override Node Render()
        {
            return Node.Empty;
        }

        protected override async Task OnMounted()
        {
            try
            {
                await Delay(Duration.Ms(50), LifetimeToken);
                MountCompleted = true;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
            }
        }
    }

    // ── Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task Mount_CallsRender()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();

        await Assert.That(events).Contains("Render");
    }

    [Test]
    public async Task Mount_ThenCompleteMountAsync_CallsOnMounted()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.CompleteMountAsync();

        await Assert.That(events).Contains("OnMounted");
    }

    [Test]
    public async Task Unmount_CallsOnUnmounted()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.Unmount();

        await Assert.That(events).Contains("OnUnmounted");
    }

    [Test]
    public async Task Lifecycle_FollowsCorrectOrder()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.CompleteMountAsync();
        host.Unmount();

        await Assert.That(events.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(events[0]).IsEqualTo("Render");
        await Assert.That(events[1]).IsEqualTo("OnMounted");
        await Assert.That(events[events.Count - 1]).IsEqualTo("OnUnmounted");
    }

    [Test]
    public async Task LifetimeToken_CancelledBeforeOnUnmounted()
    {
        var component = new TokenTrackingComponent();
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.Unmount();

        await Assert.That(component.TokenCancelledBeforeUnmount).IsTrue();
    }

    [Test]
    public async Task Unmount_PreventedOnAlreadyUnmountedComponent()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.Unmount();

        int unmountCount = events.Count(e => e == "OnUnmounted");
        host.Unmount();
        int unmountCountAfter = events.Count(e => e == "OnUnmounted");

        await Assert.That(unmountCountAfter).IsEqualTo(unmountCount);
    }

    [Test]
    public async Task Mount_PreventedOnAlreadyMountedComponent()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();

        int renderCount = events.Count(e => e == "Render");
        host.Mount();
        int renderCountAfter = events.Count(e => e == "Render");

        await Assert.That(renderCountAfter).IsEqualTo(renderCount);
    }

    [Test]
    public async Task UpdateBounds_FiresOnBoundsChangedAfterFirstBounds()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();

        // First bounds set — should NOT fire OnBoundsChanged
        host.UpdateBounds(new Rect(0, 0, 100, 50));
        int boundsChangeCount = events.Count(e => e.StartsWith("OnBoundsChanged", StringComparison.Ordinal));
        await Assert.That(boundsChangeCount).IsEqualTo(0);

        // Second bounds set — SHOULD fire OnBoundsChanged
        host.UpdateBounds(new Rect(0, 0, 200, 100));
        boundsChangeCount = events.Count(e => e.StartsWith("OnBoundsChanged", StringComparison.Ordinal));
        await Assert.That(boundsChangeCount).IsEqualTo(1);
    }

    [Test]
    public async Task UpdateBounds_SameBoundsDoesNotFire()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.UpdateBounds(new Rect(0, 0, 100, 50));
        host.UpdateBounds(new Rect(0, 0, 100, 50));

        int boundsChangeCount = events.Count(e => e.StartsWith("OnBoundsChanged", StringComparison.Ordinal));
        await Assert.That(boundsChangeCount).IsEqualTo(0);
    }

    [Test]
    public async Task RenderError_CaughtFromRender()
    {
        var component = new ThrowingRenderComponent();
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();

        await Assert.That(host.RenderError).IsNotNull();
        await Assert.That(host.RenderError!.Message).IsEqualTo("Render failed");
    }

    [Test]
    public async Task RenderError_CaughtFromOnMounted()
    {
        var component = new ThrowingMountedComponent();
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        host.Mount();
        host.CompleteMountAsync();

        await Assert.That(host.RenderError).IsNotNull();
        await Assert.That(host.RenderError!.Message).IsEqualTo("OnMounted failed");
    }

    [Test]
    public async Task IsMounted_ReflectsState()
    {
        var events = new List<string>();
        var component = new LifecycleComponent(events);
        var scheduler = new RenderScheduler();
        var host = new ComponentHost(component, scheduler, treeDepth: 0);

        await Assert.That(host.IsMounted).IsFalse();

        host.Mount();
        await Assert.That(host.IsMounted).IsTrue();

        host.Unmount();
        await Assert.That(host.IsMounted).IsFalse();
    }

    [Test]
    public async Task Parallel_AwaitsAllTasks()
    {
        bool task1Ran = false;
        bool task2Ran = false;

        await Component_Parallel(CancellationToken.None,
            Task.Run(() => { task1Ran = true; }),
            Task.Run(() => { task2Ran = true; }));

        await Assert.That(task1Ran).IsTrue();
        await Assert.That(task2Ran).IsTrue();
    }

    [Test]
    public async Task Parallel_ThrowsOnCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var threw = false;
        try
        {
            await Component_Parallel(cts.Token, Task.CompletedTask);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Delay_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        var cancelled = false;
        try
        {
            await Component_Delay(Duration.Seconds(10), cts.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
    }

    // Expose the protected static helpers for testing
    private static Task Component_Parallel(CancellationToken ct, params Task[] tasks)
    {
        return ParallelHelper.Invoke(ct, tasks);
    }

    private static Task Component_Delay(Duration duration, CancellationToken ct)
    {
        return DelayHelper.Invoke(duration, ct);
    }

    /// <summary>
    /// Helper to call the protected static Parallel method for testing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Static methods are called; class exists to access protected Component members.")]
    private sealed class ParallelHelper : Component
    {
        public static async Task Invoke(CancellationToken ct, params Task[] tasks)
        {
            await Parallel(ct, tasks).ConfigureAwait(false);
        }

        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    /// <summary>
    /// Helper to call the protected static Delay method for testing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Static methods are called; class exists to access protected Component members.")]
    private sealed class DelayHelper : Component
    {
        public static async Task Invoke(Duration duration, CancellationToken ct)
        {
            await Delay(duration, ct).ConfigureAwait(false);
        }

        protected override Node Render()
        {
            return Node.Empty;
        }
    }
}

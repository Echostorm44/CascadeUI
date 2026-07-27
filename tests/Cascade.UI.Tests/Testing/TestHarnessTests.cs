using Cascade.UI;
using Cascade.UI.Testing;

namespace Cascade.UI.Tests.Testing;

/// <summary>Minimal concrete node for testing harness APIs that need new().</summary>
internal sealed class StubNode : Node { }

public class TestHarnessTests
{
    // ──────────────────────────────────────────────
    // TestHost tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task TestHost_DefaultViewport_Is1920x1080()
    {
        using var host = new TestHost();
        await Assert.That(host.ViewportWidth).IsEqualTo(1920f);
        await Assert.That(host.ViewportHeight).IsEqualTo(1080f);
    }

    [Test]
    public async Task TestHost_CustomDimensions_AreStored()
    {
        using var host = new TestHost(800, 600);
        await Assert.That(host.ViewportWidth).IsEqualTo(800f);
        await Assert.That(host.ViewportHeight).IsEqualTo(600f);
    }

    [Test]
    public async Task TestHost_Mount_CreatesAndTracksNode()
    {
        using var host = new TestHost();
        var node = host.Mount<StubNode>();
        await Assert.That(node).IsNotNull();
        await Assert.That(host.MountedNodes).HasCount().EqualTo(1);
    }

    [Test]
    public async Task TestHost_Mount_PreExistingNode_IsTracked()
    {
        using var host = new TestHost();
        var node = new StubNode();
        var result = host.Mount(node);
        await Assert.That(result).IsSameReferenceAs(node);
        await Assert.That(host.MountedNodes).HasCount().EqualTo(1);
    }

    [Test]
    public async Task TestHost_Mount_NullNode_Throws()
    {
        using var host = new TestHost();
        await Assert.That(() => host.Mount<StubNode>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task TestHost_MountedNodes_IsReadable()
    {
        using var host = new TestHost();
        host.Mount<StubNode>();
        host.Mount<StubNode>();
        await Assert.That(host.MountedNodes).HasCount().EqualTo(2);
    }

    [Test]
    public async Task TestHost_Render_DoesNotThrow()
    {
        using var host = new TestHost();
        host.Mount<StubNode>();
        await Assert.That(() => host.Render()).ThrowsNothing();
    }

    [Test]
    public async Task TestHost_Dispose_ClearsMountedNodes()
    {
        var host = new TestHost();
        host.Mount<StubNode>();
        host.Dispose();
        await Assert.That(host.MountedNodes).HasCount().EqualTo(0);
    }

    [Test]
    public async Task TestHost_Dispose_PreventsFurtherMounts()
    {
        var host = new TestHost();
        host.Dispose();
        await Assert.That(() => host.Mount<StubNode>()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task TestHost_MultipleMounts_AllTracked()
    {
        using var host = new TestHost();
        host.Mount<StubNode>();
        host.Mount(new StubNode());
        host.Mount<StubNode>();
        await Assert.That(host.MountedNodes).HasCount().EqualTo(3);
    }

    // ──────────────────────────────────────────────
    // ComponentTestHarness tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task ComponentTestHarness_Constructor_MountsComponent()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(harness.Component).IsNotNull();
    }

    [Test]
    public async Task ComponentTestHarness_Component_ReturnsMountedInstance()
    {
        var node = new StubNode();
        using var harness = new ComponentTestHarness<StubNode>(node);
        await Assert.That(harness.Component).IsSameReferenceAs(node);
    }

    [Test]
    public async Task ComponentTestHarness_Host_ReturnsUnderlyingHost()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(harness.Host).IsNotNull();
        await Assert.That(harness.Host.MountedNodes).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ComponentTestHarness_Render_DelegatesToHost()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(() => harness.Render()).ThrowsNothing();
    }

    [Test]
    public async Task ComponentTestHarness_Click_DoesNotThrow()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(() => harness.Click()).ThrowsNothing();
    }

    [Test]
    public async Task ComponentTestHarness_TypeText_DoesNotThrow()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(() => harness.TypeText("hello")).ThrowsNothing();
    }

    [Test]
    public async Task ComponentTestHarness_Focus_DoesNotThrow()
    {
        using var harness = new ComponentTestHarness<StubNode>();
        await Assert.That(() => harness.Focus()).ThrowsNothing();
    }

    [Test]
    public async Task ComponentTestHarness_Dispose_CleansUp()
    {
        var harness = new ComponentTestHarness<StubNode>();
        harness.Dispose();
        await Assert.That(harness.Host.MountedNodes).HasCount().EqualTo(0);
    }

    // ──────────────────────────────────────────────
    // NodeAssertions tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task NodeAssertions_Should_ReturnsAssertions()
    {
        var node = new StubNode();
        var assertions = node.Should();
        await Assert.That(assertions).IsNotNull();
        await Assert.That(assertions.Node).IsSameReferenceAs(node);
    }

    [Test]
    public async Task NodeAssertions_IsNotEmpty_SucceedsForNonEmptyNode()
    {
        var node = new StubNode();
        var result = node.Should().IsNotEmpty();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task NodeAssertions_IsNotEmpty_ThrowsForNodeEmpty()
    {
        await Assert.That(() => Node.Empty.Should().IsNotEmpty()).Throws<AssertionException>();
    }

    [Test]
    public async Task NodeAssertions_IsEmpty_SucceedsForNodeEmpty()
    {
        var result = Node.Empty.Should().IsEmpty();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task NodeAssertions_IsEmpty_ThrowsForNonEmptyNode()
    {
        var node = new StubNode();
        await Assert.That(() => node.Should().IsEmpty()).Throws<AssertionException>();
    }

    [Test]
    public async Task NodeAssertions_IsType_SucceedsForCorrectType()
    {
        var node = new StubNode();
        var result = node.Should().IsType<StubNode>();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task NodeAssertions_IsType_ThrowsForWrongType()
    {
        var node = new StubNode();
        await Assert.That(() => node.Should().IsType<Spacer>()).Throws<AssertionException>();
    }

    [Test]
    public async Task NodeAssertions_As_CastsSuccessfully()
    {
        var node = new StubNode();
        var result = node.Should().As<StubNode>();
        await Assert.That(result).IsSameReferenceAs(node);
    }

    // ──────────────────────────────────────────────
    // TestWindow tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task TestWindow_DefaultDimensions_Are1280x720()
    {
        var window = new TestWindow();
        await Assert.That(window.Width).IsEqualTo(1280f);
        await Assert.That(window.Height).IsEqualTo(720f);
    }

    [Test]
    public async Task TestWindow_Resize_UpdatesDimensions()
    {
        var window = new TestWindow();
        window.Resize(1920, 1080);
        await Assert.That(window.Width).IsEqualTo(1920f);
        await Assert.That(window.Height).IsEqualTo(1080f);
    }

    [Test]
    public async Task TestWindow_SimulateFocus_SetsIsFocusedTrue()
    {
        var window = new TestWindow();
        window.SimulateBlur();
        window.SimulateFocus();
        await Assert.That(window.IsFocused).IsTrue();
    }

    [Test]
    public async Task TestWindow_SimulateBlur_SetsIsFocusedFalse()
    {
        var window = new TestWindow();
        window.SimulateBlur();
        await Assert.That(window.IsFocused).IsFalse();
    }

    [Test]
    public async Task TestWindow_DisplayScale_DefaultIs1()
    {
        var window = new TestWindow();
        await Assert.That(window.DisplayScale).IsEqualTo(1.0f);
    }

    [Test]
    public async Task TestWindow_CustomTitle_IsStored()
    {
        var window = new TestWindow(title: "My App");
        await Assert.That(window.Title).IsEqualTo("My App");
    }

    // ──────────────────────────────────────────────
    // TestClock tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task TestClock_InitialElapsed_IsZero()
    {
        var clock = new TestClock();
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task TestClock_Advance_IncreasesElapsed()
    {
        var clock = new TestClock();
        clock.Advance(TimeSpan.FromSeconds(5));
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task TestClock_AdvanceMs_Works()
    {
        var clock = new TestClock();
        clock.AdvanceMs(250);
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task TestClock_AdvanceSec_Works()
    {
        var clock = new TestClock();
        clock.AdvanceSec(3);
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task TestClock_NegativeDuration_Throws()
    {
        var clock = new TestClock();
        await Assert.That(() => clock.Advance(TimeSpan.FromSeconds(-1))).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task TestClock_Reset_ReturnsToZero()
    {
        var clock = new TestClock();
        clock.AdvanceSec(10);
        clock.Reset();
        await Assert.That(clock.Elapsed).IsEqualTo(TimeSpan.Zero);
    }

    // ──────────────────────────────────────────────
    // SpecRefAttribute tests
    // ──────────────────────────────────────────────

    [Test]
    public async Task SpecRefAttribute_StoresDocumentAndSection()
    {
        var attr = new SpecRefAttribute("architecture.md", "Reactivity");
        await Assert.That(attr.Document).IsEqualTo("architecture.md");
        await Assert.That(attr.Section).IsEqualTo("Reactivity");
    }

    [Test]
    public async Task SpecRefAttribute_NullDocument_Throws()
    {
        await Assert.That(() => new SpecRefAttribute(null!, "Section")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SpecRefAttribute_NullSection_Throws()
    {
        await Assert.That(() => new SpecRefAttribute("Doc", null!)).Throws<ArgumentNullException>();
    }
}

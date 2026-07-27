#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("Popover")]
public sealed class PopoverTests
{
    private sealed class TestNode : Node
    {
    }

    private sealed class TestPopover : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    [Test]
    public async Task Show_WithAnchor_StoresAnchor()
    {
        var node = new TestNode();

        Popover.Show<TestPopover>(node);

        var request = Popover.LastRequest;
        bool same = ReferenceEquals(request!.Anchor, node);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Show_WithAnchor_StoresComponentType()
    {
        var node = new TestNode();

        Popover.Show<TestPopover>(node);

        var request = Popover.LastRequest;
        Type type = request!.ComponentType;
        await Assert.That(type).IsEqualTo(typeof(TestPopover));
    }

    [Test]
    public async Task Show_WithAnchor_UsesDefaultOptions()
    {
        var node = new TestNode();

        Popover.Show<TestPopover>(node);

        var request = Popover.LastRequest;
        var options = request!.Options;
        await Assert.That(options.PreferredSide).IsEqualTo(PopoverSide.Auto);
        await Assert.That(options.Dismissable).IsTrue();
    }

    [Test]
    public async Task Show_WithAnchor_StoresOptionOverrides()
    {
        var node = new TestNode();
        var options = new PopoverOptions
        {
            PreferredSide = PopoverSide.Bottom,
            OffsetX = 12,
            OffsetY = 24
        };

        Popover.Show<TestPopover>(node, options);

        var request = Popover.LastRequest;
        var stored = request!.Options;
        await Assert.That(stored.PreferredSide).IsEqualTo(PopoverSide.Bottom);
        await Assert.That(stored.OffsetX).IsEqualTo(12);
        await Assert.That(stored.OffsetY).IsEqualTo(24);
    }

    [Test]
    public async Task Show_WithPosition_StoresPosition()
    {
        var position = new Point(5, 7);

        Popover.Show<TestPopover>(position);

        var request = Popover.LastRequest;
        var stored = request!.Position;
        await Assert.That(stored).IsEqualTo(position);
    }

    [Test]
    public async Task Show_WithPosition_StoresComponentType()
    {
        var position = new Point(5, 7);

        Popover.Show<TestPopover>(position);

        var request = Popover.LastRequest;
        Type type = request!.ComponentType;
        await Assert.That(type).IsEqualTo(typeof(TestPopover));
    }

    [Test]
    public async Task Show_WithPosition_StoresOptionOverrides()
    {
        var position = new Point(5, 7);
        var options = new PopoverOptions
        {
            Dismissable = false,
            OffsetX = 3,
            OffsetY = 4
        };

        Popover.Show<TestPopover>(position, options);

        var request = Popover.LastRequest;
        var stored = request!.Options;
        await Assert.That(stored.Dismissable).IsFalse();
        await Assert.That(stored.OffsetX).IsEqualTo(3);
        await Assert.That(stored.OffsetY).IsEqualTo(4);
    }

    [Test]
    public async Task Show_WithPosition_ClearsAnchor()
    {
        var position = new Point(5, 7);

        Popover.Show<TestPopover>(position);

        var request = Popover.LastRequest;
        bool hasAnchor = request!.Anchor is not null;
        await Assert.That(hasAnchor).IsFalse();
    }
}

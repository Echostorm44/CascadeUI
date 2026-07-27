#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("ContextMenu")]
public sealed class ContextMenuTests
{
    private sealed class TestNode : Node
    {
    }

    [Test]
    public async Task Show_WithAnchor_StoresAnchor()
    {
        var node = new TestNode();
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(node, items);

        var request = ContextMenu.LastRequest;
        bool same = ReferenceEquals(request!.Anchor, node);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Show_WithAnchor_StoresItems()
    {
        var node = new TestNode();
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(node, items);

        var request = ContextMenu.LastRequest;
        bool same = ReferenceEquals(request!.Items, items);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Show_WithAnchor_ClearsPosition()
    {
        var node = new TestNode();
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(node, items);

        var request = ContextMenu.LastRequest;
        bool hasPosition = request!.Position.HasValue;
        await Assert.That(hasPosition).IsFalse();
    }

    [Test]
    public async Task Show_WithPosition_StoresPosition()
    {
        var position = new Point(12, 34);
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(position, items);

        var request = ContextMenu.LastRequest;
        var stored = request!.Position;
        await Assert.That(stored).IsEqualTo(position);
    }

    [Test]
    public async Task Show_WithPosition_StoresItems()
    {
        var position = new Point(12, 34);
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(position, items);

        var request = ContextMenu.LastRequest;
        bool same = ReferenceEquals(request!.Items, items);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task Show_WithPosition_ClearsAnchor()
    {
        var position = new Point(12, 34);
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };

        ContextMenu.Show(position, items);

        var request = ContextMenu.LastRequest;
        bool hasAnchor = request!.Anchor is not null;
        await Assert.That(hasAnchor).IsFalse();
    }

    [Test]
    public async Task Show_UpdatesLastRequest()
    {
        var node = new TestNode();
        var items = new[] { ContextMenuItem.Action("Open", () => { }) };
        ContextMenu.Show(node, items);

        var position = new Point(40, 50);
        var nextItems = new[] { ContextMenuItem.Action("Close", () => { }) };
        ContextMenu.Show(position, nextItems);

        var request = ContextMenu.LastRequest;
        var stored = request!.Position;
        await Assert.That(stored).IsEqualTo(position);
    }

    [Test]
    public async Task Show_StoresItemCount()
    {
        var node = new TestNode();
        var items = new[]
        {
            ContextMenuItem.Action("One", () => { }),
            ContextMenuItem.Action("Two", () => { })
        };

        ContextMenu.Show(node, items);

        int count = ContextMenu.LastRequest!.Items.Count;
        await Assert.That(count).IsEqualTo(2);
    }
}

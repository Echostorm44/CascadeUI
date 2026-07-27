#pragma warning disable CA2000, CA1812

using System.Collections.Generic;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

[NotInParallel("DragState")]
public sealed class DragDropTests
{
    [Test]
    public async Task DragState_Initial_IsNotDragging()
    {
        DragState.Reset();
        bool isDragging = DragState.IsDragging;
        object? data = DragState.DraggedData;
        Type? type = DragState.DraggedType;

        await Assert.That(isDragging).IsFalse();
        await Assert.That(data).IsNull();
        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task Draggable_StoresPayload()
    {
        DragState.Reset();
        var node = new TestNode().Draggable(new object());

        object? payload = node.LayoutData.DragData!.Payload;
        Type? payloadType = node.LayoutData.DragData!.PayloadType;

        await Assert.That(payload).IsNotNull();
        await Assert.That(payloadType).IsEqualTo(payload!.GetType());
    }

    [Test]
    public async Task DragHandle_SetsHandleFlag()
    {
        DragState.Reset();
        var node = new TestNode().DragHandle();
        bool isHandle = node.LayoutData.DragData!.IsHandle;

        await Assert.That(isHandle).IsTrue();
    }

    [Test]
    public async Task DragPreview_StoresPreviewNode()
    {
        DragState.Reset();
        var preview = new TestNode();
        var node = new TestNode().DragPreview(preview);

        Node? stored = node.LayoutData.DragData!.Preview;
        await Assert.That(stored).IsEqualTo(preview);
    }

    [Test]
    public async Task DragToOs_StoresExportFactory()
    {
        DragState.Reset();
        var files = new[] { new OsExportFile("path") };
        var node = new TestNode().DragToOs(() => new OsDragExport { Files = files });

        var factory = node.LayoutData.DragData!.ExportFactory!;
        IReadOnlyList<OsExportFile>? result = factory().Files;

        await Assert.That(result).IsEqualTo(files);
    }

    [Test]
    public async Task DropTarget_StoresCallbacks()
    {
        DragState.Reset();
        var node = new TestNode()
            .DropTarget(_ => true, (_, _) => { });

        bool isTarget = node.LayoutData.DragData!.IsDropTarget;
        bool acceptsSet = node.LayoutData.DragData!.Accepts is not null;
        bool dropSet = node.LayoutData.DragData!.OnDrop is not null;

        await Assert.That(isTarget).IsTrue();
        await Assert.That(acceptsSet).IsTrue();
        await Assert.That(dropSet).IsTrue();
    }

    [Test]
    public async Task DropFeedback_SetsKind()
    {
        DragState.Reset();
        var node = new TestNode()
            .DropFeedback(DragFeedbackKind.Highlight);

        var feedback = node.LayoutData.DragData!.Feedback;
        var expected = DragFeedbackKind.Highlight;
        await Assert.That(feedback).IsEqualTo(expected);
    }

    [Test]
    public async Task DropFeedbackBorder_SetsColorAndWidth()
    {
        DragState.Reset();
        var color = new ColorValue("#FF0000");
        float width = 2f;
        var node = new TestNode()
            .DropFeedback(color, width);

        var storedColor = node.LayoutData.DragData!.FeedbackBorderColor;
        float storedWidth = node.LayoutData.DragData!.FeedbackBorderWidth;

        await Assert.That(storedColor).IsEqualTo(color);
        await Assert.That(storedWidth).IsEqualTo(width);
    }

    [Test]
    public async Task DropFeedbackCustom_StoresBuilder()
    {
        DragState.Reset();
        var node = new TestNode()
            .DropFeedbackCustom(isOver => isOver ? new TestNode() : Node.Empty);

        bool hasBuilder = node.LayoutData.DragData!.CustomFeedbackBuilder is not null;
        await Assert.That(hasBuilder).IsTrue();
    }

    private sealed class TestNode : Node
    {
    }
}

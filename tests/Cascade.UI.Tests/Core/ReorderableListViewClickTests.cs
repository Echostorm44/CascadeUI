using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

#pragma warning disable CA2000

namespace Cascade.UI.Tests.Core;

/// <summary>
/// Regression: a per-row IconButton inside a reorderable ListView must still receive taps.
/// (QuickFixMyPics2's "X to remove" stopped working once the file list became a reorderable list.)
/// </summary>
public class ReorderableListViewClickTests
{
    // Mirrors QuickFixMyPics2's FileList: a reorderable ListView nested inside a Column (with a
    // header row above it) inside a padded/bordered "surface" Column, inside the padded root Column
    // — the deep nesting + a header offset above the list is what the minimal case missed.
    private sealed class ReorderRowButtonComponent : Component
    {
        public static int XClicks;
        private static readonly Icon X = new(["M18 6 6 18", "m6 6 12 12"], new Size(24, 24), 16f, "Remove");
        private readonly List<string> items = ["one", "two", "three"];

        private Node FileRow(string it) =>
            new Row(
                spacing: 12,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label(it).Expand(),
                    new IconButton(X, () => XClicks++),
                ]);

        private Node FileList() =>
            new Column(
                spacing: 12,
                children:
                [
                    new Row(children: [new Label($"{items.Count} items"), new Spacer(), new Button("Add", () => { }).Variant("ghost")]),
                    new ListView<string>(items.ToArray(), FileRow)
                        .ItemHeight(56f)
                        .Reorderable(true)
                        .OnReorder((_, _) => { })
                        .Expand(),
                ]);

        protected override Node Render() =>
            new Column(
                spacing: 20,
                children:
                [
                    new Column(children: [FileList().Expand()])
                        .Background(new ColorValue("#1C1C1E"))
                        .CornerRadius(14)
                        .Border(new ColorValue("#3A3A3C"), 1, 14)
                        .Padding(EdgeInsets.All(10))
                        .Expand(),
                    new Label("options"),
                ])
            .Padding(EdgeInsets.All(28));
    }

    private static void Click(FrameOrchestrator orch, float x, float y)
    {
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });
    }

    private static (float x, float y)? FindIconButtonPoint(Node rendered)
    {
        for (float y = 20; y < 590; y += 3)
        {
            for (float x = 780; x > 400; x -= 3)
            {
                if (HitTester.HitTest(rendered, x, y) is IconButton)
                {
                    return (x, y);
                }
            }
        }
        return null;
    }

    [Test]
    public async Task IconButtonInReorderableRow_HitTestsToTheButton()
    {
        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<ReorderRowButtonComponent>(800, 600);
        orch.Tick();

        var point = FindIconButtonPoint(orch.RootHost!.RenderedTree!);
        await Assert.That(point.HasValue).IsTrue(); // the X must be hit-testable at all
    }

    [Test]
    public async Task IconButtonInReorderableRow_ReceivesTap()
    {
        ReorderRowButtonComponent.XClicks = 0;
        using var orch = new FrameOrchestrator(() => { }, () => { });
        orch.MountRoot<ReorderRowButtonComponent>(800, 600);
        orch.Tick();

        var point = FindIconButtonPoint(orch.RootHost!.RenderedTree!);
        await Assert.That(point.HasValue).IsTrue();
        float x = point!.Value.x, y = point.Value.y;

        // Faithful to runtime: mouse-down marks the button pressed and requests a frame; that frame
        // rebuilds the virtualized row content, so the button under the release is a DIFFERENT
        // instance than at press. The tap must still fire.
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseDown, Button = NativeMouseButton.Left });
        orch.Tick(); // rebuilds ListView content (InvalidateContent) → new row instances
        orch.Input.HandleMouseEvent(new NativeMouseEvent { X = x, Y = y, Type = NativeMouseEventType.MouseUp, Button = NativeMouseButton.Left });

        await Assert.That(ReorderRowButtonComponent.XClicks).IsEqualTo(1);
    }
}

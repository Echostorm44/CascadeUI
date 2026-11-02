using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// A tag's layout guessed its text at 0.75 em a character with 16px padding while its paint measured
/// the text with 12px, then centred the smaller pill in the larger box. A row of tags was spaced by
/// each label's guessing error: wide gaps after some, tight after others. Layout and paint now share
/// one measurement, so the pill fills the box layout gave it.
/// </summary>
[NotInParallel(["FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class TagSizingTests
{
    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    [Test]
    public async Task EveryPill_FillsTheBoxLayoutGaveIt()
    {
        string[] labels = ["★ Favorites", "DI.FM", "RadioTunes", "JAZZRADIO", "Zen Radio", "ClassicalRadio", "1.FM"];
        Tag[] tags = [.. labels.Select(label => new Tag(label, new Bindable<bool>(true, _ => { })))];
        Tag removable = new("Removable", () => { });
        content = () => new Row(spacing: 8, children: [.. tags, removable]);

        using var backend = new EtchBackend();
        using var orchestrator = new FrameOrchestrator(() => { }, () => { })
        {
            Theme = new AppleTheme(ThemeMode.Dark),
            RenderBackend = backend,
        };
        orchestrator.BeginFrameCallback = () =>
        {
            backend.Reset();
            return (1UL, 1600u, 200u);
        };
        orchestrator.MountRoot<Host>(1600f, 200f);
        orchestrator.Tick();

        // Each tag paints one filled, rounded pill, left to right.
        var pills = backend.Commands
            .Where(op => op.Kind == EtchBackend.OpKind.DrawRect && op.Radius > 0f && op.Fill is not null)
            .ToList();
        Tag[] all = [.. tags, removable];
        await Assert.That(pills.Count).IsEqualTo(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            float laidOut = all[i].LayoutData.Bounds.Width;
            await Assert.That(pills[i].W).IsEqualTo(laidOut).Within(1f)
                .Because($"'{labels.ElementAtOrDefault(i) ?? "Removable"}' was given {laidOut}px and painted {pills[i].W}px.");
        }
    }
}

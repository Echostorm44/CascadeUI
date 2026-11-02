// CA2000: components built in Render() are owned by the framework (mounted or discarded by the reconciler).
#pragma warning disable CA2000

namespace Cascade.UI.Tests.Core;

/// <summary>
/// A child component takes its parent's new constructor arguments when the parent re-renders
/// (generated <see cref="ComponentInputs"/> adopters), while its state — mutable fields, and
/// readonly fields it creates itself — stays with the mounted instance.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ComponentInputsTests
{
    [Test]
    public async Task ReRenderedParent_PassesItsNewArguments_ToTheMountedChild()
    {
        InputsParent.Title = "first";
        InputsParent.Count = 1;
        using var app = Mount();
        var child = app.Child();
        await Assert.That(child.RenderedText).IsEqualTo("first 1");

        InputsParent.Title = "second";
        InputsParent.Count = 2;
        app.RerenderParent();

        await Assert.That(ReferenceEquals(app.Child(), child)).IsTrue().Because("the mounted child lives on");
        await Assert.That(child.RenderedText).IsEqualTo("second 2");
        await Assert.That(child.TitleProperty).IsEqualTo("second");
    }

    [Test]
    public async Task ReRenderedParent_KeepsTheChildsState_AndWhatItCreatedItself()
    {
        InputsParent.Title = "first";
        using var app = Mount();
        var child = app.Child();
        child.Clicks = 5;
        var created = child.Created;

        InputsParent.Title = "second";
        app.RerenderParent();

        await Assert.That(child.Clicks).IsEqualTo(5);
        await Assert.That(ReferenceEquals(child.Created, created)).IsTrue();
    }

    private static Harness Mount() => new();

    private sealed class Harness : IDisposable
    {
        private readonly FrameOrchestrator orchestrator = new(() => { }, () => { });

        public Harness()
        {
            orchestrator.MountRoot<InputsParent>(400, 200);
            orchestrator.Tick();
        }

        public InputsParent Parent => (InputsParent)orchestrator.RootHost!.Component;

        public InputsChild Child()
        {
            var column = (Column)Parent.RenderedTree!;
            var standIn = (Component)column.Children[0];
            return (InputsChild)(standIn.StandsInFor ?? standIn);
        }

        public void RerenderParent()
        {
            Parent.Refresh();
            orchestrator.Tick();
            orchestrator.Tick();
        }

        public void Dispose() => orchestrator.Dispose();
    }
}

/// <summary>A parent that passes its static data to a child (top-level so generated code can name it).</summary>
internal sealed class InputsParent : Component
{
    internal static string Title = "";
    internal static int Count;

    public void Refresh() => Invalidate();

    protected override Node Render() => new Column(children: [new InputsChild(Title, Count)]);
}

/// <summary>A child with inputs (a field and a property set from parameters) and state.</summary>
internal sealed class InputsChild : Component
{
    private readonly string title;
    private readonly int count;
    private readonly List<int> created;

    public InputsChild(string title, int count)
    {
        this.title = title;
        this.count = count;
        TitleProperty = title.Trim();
        created = new List<int>();
    }

    public string TitleProperty { get; }

    public int Clicks { get; set; }

    public List<int> Created => created;

    public string RenderedText => ((Label)RenderedTree!).Text!;

    protected override Node Render() => new Label($"{title} {count}");
}

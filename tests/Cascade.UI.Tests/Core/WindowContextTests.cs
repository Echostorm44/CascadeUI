// CA2000: components are owned by the framework once mounted.
#pragma warning disable CA2000

namespace Cascade.UI.Tests.Core;

/// <summary>
/// <see cref="WindowContext.Current"/> and <see cref="ResponsiveValue"/> reflect the real window:
/// they used to return a fixed 1280x720 "Expanded" context whatever the window, so a responsive
/// layout never responded. Reading them in Render() subscribes the component to changes.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class WindowContextTests
{
    [Test]
    public async Task Current_IsTheWindowsSizeAndSizeClass()
    {
        using var orchestrator = Mount<SizeReader>(500, 400);

        await Assert.That(SizeReader.LastSize).IsEqualTo(new Size(500, 400));
        await Assert.That(SizeReader.LastClass).IsEqualTo(WindowSizeClass.Compact);
        await Assert.That(SizeReader.LastColumns).IsEqualTo(1);
    }

    [Test]
    public async Task ResizingTheWindow_ReRendersItsReaders_WithTheNewSizeClass()
    {
        using var orchestrator = Mount<SizeReader>(500, 400);
        int before = SizeReader.Renders;

        orchestrator.HandleResize(1300, 800);
        orchestrator.Tick();

        await Assert.That(SizeReader.Renders).IsGreaterThan(before);
        await Assert.That(SizeReader.LastClass).IsEqualTo(WindowSizeClass.Large);
        await Assert.That(SizeReader.LastColumns).IsEqualTo(4);
    }

    [Test]
    public async Task ResizingTheWindow_LeavesComponentsThatDoNotReadIt()
    {
        using var orchestrator = Mount<NonReader>(500, 400);
        int before = NonReader.Renders;

        orchestrator.HandleResize(1300, 800);
        orchestrator.Tick();

        await Assert.That(NonReader.Renders).IsEqualTo(before);
    }

    [Test]
    public async Task DisplayScale_AndState_ComeFromTheWindow()
    {
        var orchestrator = new FrameOrchestrator(() => { }, () => { })
        {
            PixelRatio = 1.5f,
            WindowStateProvider = () => WindowState.Maximized,
            DisplaySizeProvider = () => new Size(2560, 1440),
        };
        using (orchestrator)
        {
            orchestrator.MountRoot<SizeReader>(900, 600);
            orchestrator.Tick();

            await Assert.That(SizeReader.LastContext!.DisplayScale).IsEqualTo(1.5f);
            await Assert.That(SizeReader.LastContext.State).IsEqualTo(WindowState.Maximized);
            await Assert.That(SizeReader.LastContext.DisplaySize).IsEqualTo(new Size(2560, 1440));
            await Assert.That(SizeReader.LastClass).IsEqualTo(WindowSizeClass.Expanded);
        }
    }

    private static FrameOrchestrator Mount<T>(float width, float height) where T : Component, new()
    {
        var orchestrator = new FrameOrchestrator(() => { }, () => { });
        orchestrator.MountRoot<T>(width, height);
        orchestrator.Tick();
        return orchestrator;
    }
}

/// <summary>Reads the window context in Render (top-level so its readonly-free shape is plain).</summary>
internal sealed class SizeReader : Component
{
    internal static int Renders;
    internal static Size LastSize;
    internal static WindowSizeClass LastClass;
    internal static int LastColumns;
    internal static WindowContext? LastContext;

    protected override Node Render()
    {
        Renders++;
        LastContext = WindowContext.Current;
        LastSize = LastContext.Size;
        LastClass = LastContext.SizeClass;
        LastColumns = ResponsiveValue.Of(1, medium: 2, expanded: 3, large: 4) is var columns ? (int)columns : 0;
        return new Label($"{LastSize}");
    }
}

/// <summary>Never reads the window context.</summary>
internal sealed class NonReader : Component
{
    internal static int Renders;

    protected override Node Render()
    {
        Renders++;
        return new Label("static");
    }
}

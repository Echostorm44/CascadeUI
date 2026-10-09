using System.Diagnostics;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// <c>--app Name</c> could not choose between two instances of one app (they register under the
/// same name), so the CLI and the MCP bridge drove whichever was focused. <c>Name#pid</c> and
/// <c>#pid</c> pick one process.
/// </summary>
public class InstanceSelectorTests
{
    [Test]
    [Arguments("ClipClop2", "ClipClop2", null)]
    [Arguments("ClipClop2#1234", "ClipClop2", 1234)]
    [Arguments("#1234", null, 1234)]
    [Arguments("Weird#name", "Weird#name", null)]
    [Arguments("Trailing#", "Trailing#", null)]
    [Arguments("Zero#0", "Zero#0", null)]
    [Arguments("Signed#-5", "Signed#-5", null)]
    [Arguments("A#B#42", "A#B", 42)]
    public async Task Parse_SplitsATrailingProcessId(string value, string? app, int? pid)
    {
        var selector = InstanceSelector.Parse(value);

        await Assert.That(selector.AppId).IsEqualTo(app);
        await Assert.That(selector.Pid).IsEqualTo(pid);
    }

    [Test]
    public async Task Parse_NullOrEmpty_SelectsNothing()
    {
        await Assert.That(InstanceSelector.Parse(null)).IsEqualTo(new InstanceSelector(null, null));
        await Assert.That(InstanceSelector.Parse("")).IsEqualTo(new InstanceSelector(null, null));
    }

    [Test]
    public async Task Format_IsWhatParseReadsBack()
    {
        var entry = new InstanceEntry { WindowId = "w", Port = 1, Title = "ClipClop2", Pid = 4321 };

        string text = InstanceSelector.Format(entry);

        await Assert.That(text).IsEqualTo("ClipClop2#4321");
        await Assert.That(InstanceSelector.Parse(text)).IsEqualTo(new InstanceSelector("ClipClop2", 4321));
    }

    [Test]
    public async Task ARowLeftByAnEarlierProcessWithTheSamePid_IsNotLive()
    {
        // Killed apps never unregister, and Windows reuses process ids: a row registered before
        // this process started is a dead app's, even though a process with its id is running.
        long started = new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
        var stale = new InstanceEntry { WindowId = "old", Port = 1, Title = "x", Pid = Environment.ProcessId, ActivatedAt = started - 60_000 };
        var current = new InstanceEntry { WindowId = "new", Port = 2, Title = "x", Pid = Environment.ProcessId, ActivatedAt = Now() };

        await Assert.That(SharedInstanceRegistry.IsLive(stale)).IsFalse();
        await Assert.That(SharedInstanceRegistry.IsLive(current)).IsTrue();
    }

    [Test]
    public async Task Register_DropsRowsOfExitedProcesses_SoTheMapDoesNotFillUp()
    {
        // Killed apps never unregister. Register used to keep their rows (only reads pruned), so
        // after a few dozen killed fixtures the 4 KB map was full, the next Register threw, and
        // McpHost swallowed it for the global registry: a live app missing from discovery.
        string app = $"selector-fill-{Guid.NewGuid():N}";
        using var dead = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        await dead.WaitForExitAsync();

        using var registry = new SharedInstanceRegistry(app);
        string longTitle = new('x', 200);
        for (int i = 0; i < 40; i++)
        {
            // Each row is ~300 bytes of JSON; 40 of them are three times the map's capacity.
            registry.Register(new InstanceEntry { WindowId = $"dead-{i}", Port = 2000 + i, Title = longTitle, Pid = dead.Id, ActivatedAt = Now() });
        }

        registry.Register(new InstanceEntry { WindowId = "live", Port = 3000, Title = app, Pid = Environment.ProcessId, ActivatedAt = Now() });

        var all = registry.FindAll();
        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].WindowId).IsEqualTo("live");
    }

    private static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    [Test]
    public async Task Resolve_WithAProcessId_PicksThatInstance_NotTheFocusedOne()
    {
        // Two live instances of one app: this test process (focused) and a child process.
        string app = $"selector-test-{Guid.NewGuid():N}";
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 >nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        try
        {
            // The registry is a named shared-memory map: it lives while a handle is open (as the
            // registering app keeps one), so hold this one across the lookups.
            using var registry = new SharedInstanceRegistry(app);
            registry.Register(new InstanceEntry { WindowId = "a", Port = 1001, Title = app, Pid = Environment.ProcessId, Focused = true, ActivatedAt = Now() + 2 });
            registry.Register(new InstanceEntry { WindowId = "b", Port = 1002, Title = app, Pid = child.Id, Focused = false, ActivatedAt = Now() + 1 });

            var byName = InstanceSelector.Parse(app).Resolve(app);
            var byPid = InstanceSelector.Parse($"{app}#{child.Id}").Resolve(app);
            var missing = InstanceSelector.Parse($"{app}#{int.MaxValue}").Resolve(app);

            await Assert.That(byName!.Port).IsEqualTo(1001);
            await Assert.That(byPid!.Port).IsEqualTo(1002);
            await Assert.That(missing).IsNull();
        }
        finally
        {
            child.Kill();
        }
    }
}

#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("CommandPalette")]
public class CommandPaletteTests
{
    private sealed class TestCommandProvider : ICommandProvider
    {
        public Task<IEnumerable<CommandResult>> SearchAsync(string query)
        {
            var results = new List<CommandResult>
            {
                new CommandResult { Label = "Test", Execute = () => { } }
            };
            return Task.FromResult<IEnumerable<CommandResult>>(results);
        }
    }

    [Test]
    public async Task Register_GlobalCommands_AddsToList()
    {
        CommandPalette.ResetAll();
        var cmd = new Command("Save");
        CommandPalette.Register(cmd);

        int count = CommandPalette.GlobalCommands.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Register_MultipleGlobalCommands()
    {
        CommandPalette.ResetAll();
        var cmd1 = new Command("Save");
        var cmd2 = new Command("Open");
        CommandPalette.Register(cmd1, cmd2);

        int count = CommandPalette.GlobalCommands.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Register_ScopedCommands_AddsToOwner()
    {
        CommandPalette.ResetAll();
        var owner = new object();
        var cmd = new Command("Format");
        CommandPalette.Register(owner, cmd);

        var scoped = CommandPalette.GetScopedCommands(owner);
        int count = scoped.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Unregister_RemovesScopedCommands()
    {
        CommandPalette.ResetAll();
        var owner = new object();
        CommandPalette.Register(owner, new Command("Test"));
        CommandPalette.Unregister(owner);

        var scoped = CommandPalette.GetScopedCommands(owner);
        int count = scoped.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Open_SetsIsOpen()
    {
        CommandPalette.ResetAll();
        CommandPalette.Open();

        bool open = CommandPalette.IsOpen;
        await Assert.That(open).IsTrue();
    }

    [Test]
    public async Task Close_ClearsIsOpen()
    {
        CommandPalette.ResetAll();
        CommandPalette.Open();
        CommandPalette.Close();

        bool open = CommandPalette.IsOpen;
        await Assert.That(open).IsFalse();
    }

    [Test]
    public async Task ClearRecent_EmptiesHistory()
    {
        CommandPalette.ResetAll();
        CommandPalette.ClearRecent();

        int count = CommandPalette.RecentCommands.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Command_StoresAllProperties()
    {
        var shortcut = new Hotkey(ModifierKeys.Ctrl, Key.P);
        var cmd = new Command("Run", shortcut: shortcut, category: "Debug");

        string label = cmd.Label;
        var sc = cmd.Shortcut;
        string? cat = cmd.Category;
        await Assert.That(label).IsEqualTo("Run");
        await Assert.That(sc).IsEqualTo(shortcut);
        await Assert.That(cat).IsEqualTo("Debug");
    }

    [Test]
    public async Task Command_CategoryIsOptional()
    {
        var cmd = new Command("Save");

        string? cat = cmd.Category;
        await Assert.That(cat).IsNull();
    }

    [Test]
    public async Task Providers_SetsCustomProviders()
    {
        var provider = new TestCommandProvider();
        var palette = new CommandPalette().Providers(provider);

        int count = palette.CustomProviders.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ResetAll_ClearsAllState()
    {
        CommandPalette.Register(new Command("Test"));
        CommandPalette.Open();
        CommandPalette.ResetAll();

        int globalCount = CommandPalette.GlobalCommands.Count;
        bool open = CommandPalette.IsOpen;
        await Assert.That(globalCount).IsEqualTo(0);
        await Assert.That(open).IsFalse();
    }

    [Test]
    public async Task Register_ScopedMultipleOwners_Independent()
    {
        CommandPalette.ResetAll();
        var owner1 = new object();
        var owner2 = new object();
        CommandPalette.Register(owner1, new Command("A"));
        CommandPalette.Register(owner2, new Command("B"), new Command("C"));

        var scoped1 = CommandPalette.GetScopedCommands(owner1);
        var scoped2 = CommandPalette.GetScopedCommands(owner2);
        int count1 = scoped1.Count;
        int count2 = scoped2.Count;
        await Assert.That(count1).IsEqualTo(1);
        await Assert.That(count2).IsEqualTo(2);
    }
}

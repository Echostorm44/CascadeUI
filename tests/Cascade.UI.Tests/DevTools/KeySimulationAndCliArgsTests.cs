using Cascade.UI.AI;
using Cascade.UI.DevTools;
using Cascade.UI.Tools.Commands;

namespace Cascade.UI.Tests.DevTools;

/// <summary>
/// The MCP key tool presses keys the way Win32 delivers them (down, character, up, modifier
/// release), and the CLI rejects options it does not know instead of silently dropping them.
/// </summary>
[NotInParallel(nameof(NodeTreeWalker))]
public class KeySimulationAndCliArgsTests
{
    [Before(Test)]
    public void SetUp()
    {
        FocusManager.Reset();
        Keyboard.Observe(ModifierKeys.None);
    }

    [Test]
    public async Task KeyPress_SendsDownCharacterUp_ThenReleasesModifiers()
    {
        var events = new List<string>();
        string text = "";
        var input = new TextInput(new Bindable<string>(text, v => text = v));
        var root = new KeyHandler(new Column(children: [input]))
        {
            KeyDown = e =>
            {
                events.Add($"down {e.Modifiers}+{e.Key}");
                return e.Key == Key.D1 && e.Modifiers == ModifierKeys.Shift; // consume Shift+1
            },
            KeyUp = e => { events.Add($"up {e.Modifiers}+{e.Key}"); },
        };
        var dispatcher = new InputDispatcher();
        dispatcher.SetRoot(root);
        NodeTreeWalker.SetInputDispatcher(dispatcher);
        FocusManager.RequestFocus(input);
        var modifierStates = new List<ModifierKeys>();
        Action<ModifierKeys> track = modifierStates.Add;
        Keyboard.ModifiersChanged += track;
        try
        {
            NodeTreeWalker.SimulateKeyPress(Key.D1, ModifierKeys.Shift, '!');
            NodeTreeWalker.SimulateKeyPress(Key.A, ModifierKeys.None, 'a');
        }
        finally
        {
            Keyboard.ModifiersChanged -= track;
        }

        await Assert.That(string.Join(" | ", events)).IsEqualTo(
            "down Shift+D1 | up Shift+D1 | up None+None | down None+A | up None+A");
        await Assert.That(text).IsEqualTo("a"); // the consumed Shift+1 did not type '!'
        await Assert.That(modifierStates).IsEquivalentTo(new[] { ModifierKeys.Shift, ModifierKeys.None });
    }

    [Test]
    [Arguments("screenshot", new[] { "--out", "x.png" }, "--out")]
    [Arguments("screenshot", new[] { "-o", "x.png", "--app", "Demo", "--scale", "2" }, null)]
    [Arguments("type", new[] { "--key", "K", "--ctrl", "--win" }, null)]
    [Arguments("type", new[] { "--key", "K", "--modifiers", "Ctrl" }, "--modifiers")]
    [Arguments("scroll", new[] { "--delta-y", "-120", "--x", "10" }, null)]
    [Arguments("find", new[] { "Save", "--by", "label", "--source-file", "MainView.cs" }, null)]
    [Arguments("find", new[] { "Save", "--bye", "label" }, "--bye")]
    public async Task EveryVerb_RejectsOptionsItsSpecDoesNotDeclare(string verb, string[] args, string? unknown)
    {
        McpCliVerbBinding binding = McpToolRegistry.FindByVerb(verb)!;

        await Assert.That(McpCommand.FindUnknownOption(binding.Verb, args)).IsEqualTo(unknown);
    }

    [Test]
    public async Task GenericVerb_ParsesDeclaredOptions()
    {
        McpCliVerbBinding inspect = McpToolRegistry.FindByVerb("inspect")!;
        using var quiet = new StringWriter();

        var args = McpCommand.ParseVerbArguments(inspect.Verb, ["Button:0", "--app", "Demo"], quiet);

        await Assert.That(args!.ContainsKey("node_id")).IsTrue();
    }
}

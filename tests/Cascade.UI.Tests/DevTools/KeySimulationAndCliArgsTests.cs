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
    public async Task CliArgs_UnknownOption_IsAnError()
    {
        McpCliVerbBinding screenshot = McpToolRegistry.FindByVerb("screenshot")!;
        using var error = new StringWriter();
        using var quiet = new StringWriter();

        var bad = McpCommand.ParseVerbArguments(screenshot.Verb, ["--out", "x.png"], error);
        var good = McpCommand.ParseVerbArguments(screenshot.Verb, ["--output", "x.png", "--app", "Demo"], quiet);

        await Assert.That(bad is null).IsTrue();
        await Assert.That(error.ToString()).Contains("Unknown option '--out'");
        await Assert.That(good!["__output"]?.GetValue<string>()).IsEqualTo("x.png");
    }

    [Test]
    public async Task CliArgs_TypeVerb_TakesKeyAndModifiers()
    {
        McpCliVerbBinding type = McpToolRegistry.FindByVerb("type")!;
        using var quiet = new StringWriter();

        var args = McpCommand.ParseVerbArguments(type.Verb, ["--key", "K", "--modifiers", "Ctrl"], quiet)!;

        await Assert.That(args["key"]?.GetValue<string>()).IsEqualTo("K");
        await Assert.That(args["modifiers"]?.GetValue<string>()).IsEqualTo("Ctrl");
        await Assert.That(args.ContainsKey("text")).IsFalse();
    }
}

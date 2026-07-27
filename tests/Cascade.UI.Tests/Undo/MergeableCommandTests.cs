using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class MergeableCommandTests
{
    [Test]
    public async Task MergeableCommand_MergesWithPrevious()
    {
        var stack = new UndoStack();
        var text = "";

        stack.Execute(new TypingCommand("a", () => text, v => text = v));
        stack.Execute(new TypingCommand("b", () => text, v => text = v));
        stack.Execute(new TypingCommand("c", () => text, v => text = v));

        await Assert.That(text).IsEqualTo("abc");
        await Assert.That(stack.UndoCount).IsEqualTo(1);

        stack.Undo();

        await Assert.That(text).IsEqualTo("");
    }

    [Test]
    public async Task MergeableCommand_DoesNotMergeWithDifferentType()
    {
        var stack = new UndoStack();
        var text = "";

        stack.Execute(new TypingCommand("a", () => text, v => text = v));
        stack.Execute(UndoCommand.Create("other", () => text += "!", () => text = text[..^1]));

        await Assert.That(stack.UndoCount).IsEqualTo(2);
    }

    [Test]
    public async Task MergeableCommand_RedoAfterMerge_Works()
    {
        var stack = new UndoStack();
        var text = "";

        stack.Execute(new TypingCommand("x", () => text, v => text = v));
        stack.Execute(new TypingCommand("y", () => text, v => text = v));

        stack.Undo();
        await Assert.That(text).IsEqualTo("");

        stack.Redo();
        await Assert.That(text).IsEqualTo("xy");
    }

    [Test]
    public async Task MergeableCommand_CanMerge_ReturnsTrue_ForSameType()
    {
        var cmd1 = new TypingCommand("a", () => "", _ => { });
        var cmd2 = new TypingCommand("b", () => "", _ => { });

        await Assert.That(cmd2.CanMerge(cmd1)).IsTrue();
    }

    [Test]
    public async Task MergeableCommand_CanMerge_ReturnsFalse_ForDifferentType()
    {
        var cmd1 = UndoCommand.Create("other", () => { }, () => { });
        var cmd2 = new TypingCommand("a", () => "", _ => { });

        await Assert.That(cmd2.CanMerge(cmd1)).IsFalse();
    }

    /// <summary>
    /// A simple mergeable typing command for testing. Each instance types a
    /// single character, and consecutive typing commands merge into one undo step.
    /// </summary>
    private sealed class TypingCommand : IMergeableCommand
    {
        private readonly string character;
        private readonly Func<string> getter;
        private readonly Action<string> setter;
        private string beforeText;
        private string afterText;

        public string Description => "Type";

        internal TypingCommand(string character, Func<string> getter, Action<string> setter)
        {
            this.character = character;
            this.getter = getter;
            this.setter = setter;
            beforeText = "";
            afterText = "";
        }

        public void Execute()
        {
            if (afterText.Length == 0)
            {
                beforeText = getter();
                afterText = beforeText + character;
            }

            setter(afterText);
        }

        public void Undo()
        {
            setter(beforeText);
        }

        public bool CanMerge(IUndoCommand previous)
        {
            return previous is TypingCommand;
        }

        public void Merge(IUndoCommand previous)
        {
            if (previous is TypingCommand prev)
            {
                prev.afterText = afterText;
            }
        }
    }
}

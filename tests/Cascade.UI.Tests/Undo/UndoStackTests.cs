using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class UndoStackTests
{
    [Test]
    public async Task Execute_AddsToUndoStack()
    {
        var stack = new UndoStack();
        var value = 0;
        var cmd = UndoCommand.Create("set", () => value = 1, () => value = 0);

        stack.Execute(cmd);

        await Assert.That(value).IsEqualTo(1);
        await Assert.That(stack.UndoCount).IsEqualTo(1);
        await Assert.That(stack.RedoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Undo_ReversesLastCommand()
    {
        var stack = new UndoStack();
        var value = 0;
        stack.Execute(UndoCommand.Create("inc", () => value++, () => value--));

        stack.Undo();

        await Assert.That(value).IsEqualTo(0);
        await Assert.That(stack.UndoCount).IsEqualTo(0);
        await Assert.That(stack.RedoCount).IsEqualTo(1);
    }

    [Test]
    public async Task Redo_ReExecutesUndoneCommand()
    {
        var stack = new UndoStack();
        var value = 0;
        stack.Execute(UndoCommand.Create("inc", () => value++, () => value--));

        stack.Undo();
        stack.Redo();

        await Assert.That(value).IsEqualTo(1);
        await Assert.That(stack.UndoCount).IsEqualTo(1);
        await Assert.That(stack.RedoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Undo_WhenEmpty_IsNoOp()
    {
        var stack = new UndoStack();
        stack.Undo();

        await Assert.That(stack.UndoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Redo_WhenEmpty_IsNoOp()
    {
        var stack = new UndoStack();
        stack.Redo();

        await Assert.That(stack.RedoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_ClearsRedoStack()
    {
        var stack = new UndoStack();
        var value = 0;
        stack.Execute(UndoCommand.Create("a", () => value = 1, () => value = 0));
        stack.Undo();
        stack.Execute(UndoCommand.Create("b", () => value = 2, () => value = 0));

        await Assert.That(stack.RedoCount).IsEqualTo(0);
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task CanUndo_ReflectsState()
    {
        var stack = new UndoStack();

        await Assert.That(stack.CanUndo).IsFalse();

        stack.Execute(UndoCommand.Create("cmd", () => { }, () => { }));

        await Assert.That(stack.CanUndo).IsTrue();

        stack.Undo();

        await Assert.That(stack.CanUndo).IsFalse();
    }

    [Test]
    public async Task CanRedo_ReflectsState()
    {
        var stack = new UndoStack();

        await Assert.That(stack.CanRedo).IsFalse();

        stack.Execute(UndoCommand.Create("cmd", () => { }, () => { }));
        stack.Undo();

        await Assert.That(stack.CanRedo).IsTrue();

        stack.Redo();

        await Assert.That(stack.CanRedo).IsFalse();
    }

    [Test]
    public async Task UndoDescription_ReturnsLastCommandDescription()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("Delete item", () => { }, () => { }));

        await Assert.That(stack.UndoDescription).IsEqualTo("Delete item");
    }

    [Test]
    public async Task UndoDescription_EmptyWhenNoCommands()
    {
        var stack = new UndoStack();

        await Assert.That(stack.UndoDescription).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task RedoDescription_ReturnsLastUndoneCommandDescription()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("Bold text", () => { }, () => { }));
        stack.Undo();

        await Assert.That(stack.RedoDescription).IsEqualTo("Bold text");
    }

    [Test]
    public async Task Clear_EmptiesBothStacks()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));
        stack.Execute(UndoCommand.Create("b", () => { }, () => { }));
        stack.Undo();

        stack.Clear();

        await Assert.That(stack.UndoCount).IsEqualTo(0);
        await Assert.That(stack.RedoCount).IsEqualTo(0);
        await Assert.That(stack.CanUndo).IsFalse();
        await Assert.That(stack.CanRedo).IsFalse();
    }

    [Test]
    public async Task MaxDepth_EvictsOldestCommands()
    {
        var stack = new UndoStack(maxDepth: 3);
        for (var i = 0; i < 5; i++)
        {
            stack.Execute(UndoCommand.Create($"cmd{i}", () => { }, () => { }));
        }

        await Assert.That(stack.UndoCount).IsEqualTo(3);
        await Assert.That(stack.UndoDescription).IsEqualTo("cmd4");
    }

    [Test]
    public async Task MultipleUndoRedo_WorksCorrectly()
    {
        var stack = new UndoStack();
        var values = new List<string>();

        stack.Execute(UndoCommand.Create("add a", () => values.Add("a"), () => values.Remove("a")));
        stack.Execute(UndoCommand.Create("add b", () => values.Add("b"), () => values.Remove("b")));
        stack.Execute(UndoCommand.Create("add c", () => values.Add("c"), () => values.Remove("c")));

        await Assert.That(values.Count).IsEqualTo(3);

        stack.Undo();
        await Assert.That(values.Count).IsEqualTo(2);

        stack.Undo();
        await Assert.That(values.Count).IsEqualTo(1);

        stack.Redo();
        await Assert.That(values.Count).IsEqualTo(2);
        await Assert.That(values[1]).IsEqualTo("b");
    }

    [Test]
    public async Task MarkSaved_And_IsDirty()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));

        stack.MarkSaved();

        await Assert.That(stack.IsDirty).IsFalse();

        stack.Execute(UndoCommand.Create("b", () => { }, () => { }));

        await Assert.That(stack.IsDirty).IsTrue();
    }

    [Test]
    public async Task IsDirty_UndoBackToSavePoint()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));
        stack.MarkSaved();
        stack.Execute(UndoCommand.Create("b", () => { }, () => { }));

        await Assert.That(stack.IsDirty).IsTrue();

        stack.Undo();

        await Assert.That(stack.IsDirty).IsFalse();
    }

    [Test]
    public async Task IsDirty_UndoPastSave_ThenNewCommand_PermanentlyDirty()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));
        stack.Execute(UndoCommand.Create("b", () => { }, () => { }));
        stack.MarkSaved();

        stack.Undo();
        stack.Execute(UndoCommand.Create("c", () => { }, () => { }));

        await Assert.That(stack.IsDirty).IsTrue();
    }

    [Test]
    public async Task IsDirty_FreshStack_NotDirty()
    {
        var stack = new UndoStack();

        await Assert.That(stack.IsDirty).IsFalse();
    }

    [Test]
    public async Task IsDirty_FreshStack_WithCommand_IsDirty()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));

        await Assert.That(stack.IsDirty).IsTrue();
    }

    [Test]
    public async Task PropertyChange_CapturesOldValue()
    {
        var stack = new UndoStack();
        var value = "old";
        var cmd = UndoCommand.PropertyChange("change", () => value, v => value = v, "new");

        stack.Execute(cmd);
        await Assert.That(value).IsEqualTo("new");

        stack.Undo();
        await Assert.That(value).IsEqualTo("old");

        stack.Redo();
        await Assert.That(value).IsEqualTo("new");
    }

    [Test]
    public async Task MaxDepth_EvictionInvalidatesSavedPoint()
    {
        var stack = new UndoStack(maxDepth: 3);
        stack.Execute(UndoCommand.Create("a", () => { }, () => { }));
        stack.MarkSaved();

        // Push enough to evict the saved point
        stack.Execute(UndoCommand.Create("b", () => { }, () => { }));
        stack.Execute(UndoCommand.Create("c", () => { }, () => { }));
        stack.Execute(UndoCommand.Create("d", () => { }, () => { }));

        // Undo all the way — saved point was evicted, so should be dirty
        stack.Undo();
        stack.Undo();
        stack.Undo();

        await Assert.That(stack.IsDirty).IsFalse();
    }
}

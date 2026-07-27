using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class BatchTests
{
    [Test]
    public async Task Batch_GroupsMultipleCommands()
    {
        var stack = new UndoStack();
        var value = 0;

        using (stack.BeginBatch("Batch op"))
        {
            stack.Execute(UndoCommand.Create("a", () => value += 10, () => value -= 10));
            stack.Execute(UndoCommand.Create("b", () => value += 20, () => value -= 20));
        }

        await Assert.That(value).IsEqualTo(30);
        await Assert.That(stack.UndoCount).IsEqualTo(1);
        await Assert.That(stack.UndoDescription).IsEqualTo("Batch op");
    }

    [Test]
    public async Task Batch_Undo_ReversesAllCommands()
    {
        var stack = new UndoStack();
        var values = new List<string>();

        using (stack.BeginBatch("Add items"))
        {
            stack.Execute(UndoCommand.Create("a", () => values.Add("a"), () => values.Remove("a")));
            stack.Execute(UndoCommand.Create("b", () => values.Add("b"), () => values.Remove("b")));
            stack.Execute(UndoCommand.Create("c", () => values.Add("c"), () => values.Remove("c")));
        }

        await Assert.That(values.Count).IsEqualTo(3);

        stack.Undo();

        await Assert.That(values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Batch_Redo_ReExecutesAllCommands()
    {
        var stack = new UndoStack();
        var value = 0;

        using (stack.BeginBatch("increment"))
        {
            stack.Execute(UndoCommand.Create("a", () => value++, () => value--));
            stack.Execute(UndoCommand.Create("b", () => value++, () => value--));
        }

        stack.Undo();
        await Assert.That(value).IsEqualTo(0);

        stack.Redo();
        await Assert.That(value).IsEqualTo(2);
    }

    [Test]
    public async Task Batch_Undo_ReversesInReverseOrder()
    {
        var stack = new UndoStack();
        var undoOrder = new List<string>();

        using (stack.BeginBatch("ordered"))
        {
            stack.Execute(UndoCommand.Create("1", () => { }, () => undoOrder.Add("1")));
            stack.Execute(UndoCommand.Create("2", () => { }, () => undoOrder.Add("2")));
            stack.Execute(UndoCommand.Create("3", () => { }, () => undoOrder.Add("3")));
        }

        stack.Undo();

        await Assert.That(undoOrder.Count).IsEqualTo(3);
        await Assert.That(undoOrder[0]).IsEqualTo("3");
        await Assert.That(undoOrder[1]).IsEqualTo("2");
        await Assert.That(undoOrder[2]).IsEqualTo("1");
    }

    [Test]
    public async Task NestedBatch_OnlyOutermostCreatesUndoStep()
    {
        var stack = new UndoStack();
        var value = 0;

        using (stack.BeginBatch("Outer"))
        {
            stack.Execute(UndoCommand.Create("a", () => value += 1, () => value -= 1));

            using (stack.BeginBatch("Inner"))
            {
                stack.Execute(UndoCommand.Create("b", () => value += 10, () => value -= 10));
                stack.Execute(UndoCommand.Create("c", () => value += 100, () => value -= 100));
            }

            stack.Execute(UndoCommand.Create("d", () => value += 1000, () => value -= 1000));
        }

        await Assert.That(value).IsEqualTo(1111);
        await Assert.That(stack.UndoCount).IsEqualTo(1);
        await Assert.That(stack.UndoDescription).IsEqualTo("Outer");

        stack.Undo();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyBatch_IsNoOp()
    {
        var stack = new UndoStack();

        using (stack.BeginBatch("empty"))
        {
            // No commands
        }

        await Assert.That(stack.UndoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Batch_ClearsRedoStack()
    {
        var stack = new UndoStack();
        stack.Execute(UndoCommand.Create("before", () => { }, () => { }));
        stack.Undo();

        await Assert.That(stack.RedoCount).IsEqualTo(1);

        using (stack.BeginBatch("batch"))
        {
            stack.Execute(UndoCommand.Create("new", () => { }, () => { }));
        }

        await Assert.That(stack.RedoCount).IsEqualTo(0);
    }

    [Test]
    public async Task Batch_WithSingleCommand_StillGroups()
    {
        var stack = new UndoStack();

        using (stack.BeginBatch("single"))
        {
            stack.Execute(UndoCommand.Create("only", () => { }, () => { }));
        }

        await Assert.That(stack.UndoCount).IsEqualTo(1);
        await Assert.That(stack.UndoDescription).IsEqualTo("single");
    }
}

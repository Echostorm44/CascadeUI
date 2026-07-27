using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

[NotInParallel("AppUndoStack")]
public class UndoIntegrationTests
{
    [Test]
    public async Task RegisterUndoStack_StoresStack()
    {
        var stack = new UndoStack();

        App.RegisterUndoStack(stack);

        await Assert.That(App.ActiveUndoStack).IsEqualTo(stack);

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task RegisterUndoStack_Null_ClearsStack()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);

        App.RegisterUndoStack(null);

        await Assert.That(App.ActiveUndoStack).IsNull();
    }

    [Test]
    public async Task PerformUndo_ReturnsFalse_WhenNoStackRegistered()
    {
        App.RegisterUndoStack(null);

        var result = App.PerformUndo();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PerformRedo_ReturnsFalse_WhenNoStackRegistered()
    {
        App.RegisterUndoStack(null);

        var result = App.PerformRedo();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PerformUndo_ReturnsFalse_WhenStackIsEmpty()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);

        var result = App.PerformUndo();

        await Assert.That(result).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformRedo_ReturnsFalse_WhenStackIsEmpty()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);

        var result = App.PerformRedo();

        await Assert.That(result).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformUndo_ExecutesUndo_OnActiveStack()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        var cmd = UndoCommand.Create("set to 1", () => value = 1, () => value = 0);
        stack.Execute(cmd);

        var result = App.PerformUndo();

        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo(0);
        await Assert.That(stack.CanUndo).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformRedo_ExecutesRedo_OnActiveStack()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        var cmd = UndoCommand.Create("set to 1", () => value = 1, () => value = 0);
        stack.Execute(cmd);
        stack.Undo();

        var result = App.PerformRedo();

        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo(1);
        await Assert.That(stack.CanRedo).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformUndo_ReturnsTrue_AndStackCanRedo_AfterUndo()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        stack.Execute(UndoCommand.Create("set to 42", () => { value = 42; }, () => { value = 0; }));

        var result = App.PerformUndo();

        await Assert.That(result).IsTrue();
        await Assert.That(stack.CanRedo).IsTrue();
        _ = value;

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformRedo_ReturnsTrue_AndStackCanUndo_AfterRedo()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        stack.Execute(UndoCommand.Create("set to 42", () => { value = 42; }, () => { value = 0; }));
        stack.Undo();

        var result = App.PerformRedo();

        await Assert.That(result).IsTrue();
        await Assert.That(stack.CanUndo).IsTrue();
        _ = value;

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformUndo_WhenNothingToUndo_ReturnsFalse_WithStackRegistered()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        stack.Execute(UndoCommand.Create("set to 1", () => { value = 1; }, () => { value = 0; }));
        stack.Undo();
        _ = value;

        var result = App.PerformUndo();

        await Assert.That(result).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task PerformRedo_WhenNothingToRedo_ReturnsFalse_WithStackRegistered()
    {
        var stack = new UndoStack();
        App.RegisterUndoStack(stack);
        var value = 0;
        stack.Execute(UndoCommand.Create("set to 1", () => { value = 1; }, () => { value = 0; }));
        _ = value;

        var result = App.PerformRedo();

        await Assert.That(result).IsFalse();

        App.RegisterUndoStack(null);
    }

    [Test]
    public async Task DataGrid_UndoEnabled_StoresValue()
    {
        IReadOnlyList<string> data = [];
        var binding = new Bindable<IReadOnlyList<string>>(data, _ => { });
        var columns = Array.Empty<DataGridColumn<string>>();
        var grid = new DataGrid<string>(binding, columns);

        grid.UndoEnabled(false);

        await Assert.That(grid.undoEnabledValue).IsFalse();
    }

    [Test]
    public async Task DataGrid_UndoDepth_StoresValue()
    {
        IReadOnlyList<string> data = [];
        var binding = new Bindable<IReadOnlyList<string>>(data, _ => { });
        var columns = Array.Empty<DataGridColumn<string>>();
        var grid = new DataGrid<string>(binding, columns);

        grid.UndoDepth(50);

        await Assert.That(grid.undoDepthValue).IsEqualTo(50);
    }
}

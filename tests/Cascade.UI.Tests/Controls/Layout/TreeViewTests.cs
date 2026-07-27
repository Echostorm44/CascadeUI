#pragma warning disable CA2000, CA1812
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class TreeViewTests
{
    private static TreeNode<string> MakeNode(string data, bool expanded = false, params TreeNode<string>[] children)
    {
        return new TreeNode<string>
        {
            Data = data,
            Children = children,
            Expanded = expanded
        };
    }

    private static TreeView<string> MakeTree(params TreeNode<string>[] items)
    {
        return new TreeView<string>(items, data => Node.Empty);
    }

    [Test]
    public async Task ConstructorSetsItemsAndRender()
    {
        var node = MakeNode("root");
        var tree = new TreeView<string>(
            new[] { node },
            data => Node.Empty);

        var expectedCount = 1;
        await Assert.That(tree.Items.Count).IsEqualTo(expectedCount);
        await Assert.That(tree.Render).IsNotNull();
    }

    [Test]
    public async Task TreeNodeRecordProperties()
    {
        var child = MakeNode("child");
        var root = MakeNode("root", true, child);

        var expectedData = "root";
        var expectedExpanded = true;
        var expectedChildCount = 1;
        await Assert.That(root.Data).IsEqualTo(expectedData);
        await Assert.That(root.Expanded).IsEqualTo(expectedExpanded);
        await Assert.That(root.Children.Count).IsEqualTo(expectedChildCount);
    }

    [Test]
    public async Task FixedItemHeightSetsData()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.ItemHeight(32f);

        var expected = 32f;
        await Assert.That(tree.LayoutData.TreeData).IsNotNull();
        await Assert.That(tree.LayoutData.TreeData!.FixedItemHeight).IsEqualTo(expected);
    }

    [Test]
    public async Task ItemHeightStrategySetsData()
    {
        var tree = MakeTree(MakeNode("a"));
        var strategy = Cascade.UI.ItemHeight.Dynamic;
        tree.ItemHeight(strategy);

        await Assert.That(tree.LayoutData.TreeData).IsNotNull();
        await Assert.That(tree.LayoutData.TreeData!.ItemHeightStrategy).IsEqualTo(strategy);
    }

    [Test]
    public async Task SelectionModeSetsData()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.SelectionMode(TreeSelectionMode.Multi);

        var expected = TreeSelectionMode.Multi;
        await Assert.That(tree.LayoutData.TreeData!.SelectionMode).IsEqualTo(expected);
    }

    [Test]
    public async Task OnSelectRegistersHandler()
    {
        TreeNode<string>? selected = null;
        var tree = MakeTree(MakeNode("a"));
        tree.OnSelect(node => { selected = node; });

        await Assert.That(tree.LayoutData.TreeData!.OnSelectHandler).IsNotNull();
    }

    [Test]
    public async Task SelectedBindSetsData()
    {
        var bind = new Bindable<IReadOnlyList<TreeNode<string>>>(
            Array.Empty<TreeNode<string>>(),
            _ => { });
        var tree = MakeTree(MakeNode("a"));
        tree.Selected(bind);

        await Assert.That(tree.LayoutData.TreeData!.SelectedBind).IsNotNull();
    }

    [Test]
    public async Task CheckboxModeEnablesTriState()
    {
        var bind = new Bindable<IReadOnlyList<TreeNode<string>>>(
            Array.Empty<TreeNode<string>>(),
            _ => { });
        var tree = MakeTree(MakeNode("a"));
        tree.CheckboxMode(bind, triState: true);

        var expected = true;
        await Assert.That(tree.LayoutData.TreeData!.TriStateCheckbox).IsEqualTo(expected);
        await Assert.That(tree.LayoutData.TreeData!.CheckedBind).IsNotNull();
    }

    [Test]
    public async Task OnCheckChangeRegistersHandler()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.OnCheckChange((node, isChecked) => { });

        await Assert.That(tree.LayoutData.TreeData!.OnCheckChangeHandler).IsNotNull();
    }

    [Test]
    public async Task InlineEditSetsEditTrigger()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.InlineEdit(TreeEditTrigger.F2);

        var expected = TreeEditTrigger.F2;
        await Assert.That(tree.LayoutData.TreeData!.EditTrigger).IsEqualTo(expected);
    }

    [Test]
    public async Task DraggableSetsDropIndicator()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.Draggable(dropIndicator: TreeDropIndicator.Highlight);

        var expected = TreeDropIndicator.Highlight;
        await Assert.That(tree.LayoutData.TreeData!.DropIndicator).IsEqualTo(expected);
    }

    [Test]
    public async Task ContextMenuRegistersFactory()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.ItemContextMenu(node => Array.Empty<ContextMenuItem>());

        await Assert.That(tree.LayoutData.TreeData!.ContextMenuFactory).IsNotNull();
    }

    [Test]
    public async Task OnDeleteRegistersHandler()
    {
        var tree = MakeTree(MakeNode("a"));
        tree.OnDelete(node => { });

        await Assert.That(tree.LayoutData.TreeData!.OnDeleteHandler).IsNotNull();
    }

    [Test]
    public async Task LazyLoadingConstructorSetsCallbacks()
    {
        var tree = new TreeView<string>(
            new[] { MakeNode("root") },
            data => Node.Empty,
            loadChildren: node => Task.FromResult<IReadOnlyList<TreeNode<string>>>(Array.Empty<TreeNode<string>>()),
            hasChildren: node => true);

        await Assert.That(tree.LoadChildren).IsNotNull();
        await Assert.That(tree.HasChildren).IsNotNull();
    }

    [Test]
    public async Task FluentChainingReturnsSameInstance()
    {
        var tree = MakeTree(MakeNode("a"));
        var result = tree.ItemHeight(24f).SelectionMode(TreeSelectionMode.None);

        await Assert.That(result).IsEqualTo(tree);
    }

    [Test]
    public async Task HierarchyWithMultipleLevels()
    {
        var grandchild = MakeNode("gc");
        var child = MakeNode("c", false, grandchild);
        var root = MakeNode("r", true, child);

        var expectedRootChildren = 1;
        var expectedChildChildren = 1;
        var expectedGrandchildChildren = 0;
        await Assert.That(root.Children.Count).IsEqualTo(expectedRootChildren);
        await Assert.That(root.Children[0].Children.Count).IsEqualTo(expectedChildChildren);
        await Assert.That(root.Children[0].Children[0].Children.Count).IsEqualTo(expectedGrandchildChildren);
    }
}

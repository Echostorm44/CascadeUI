using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// Structural comparison of node trees. Compares old vs new nodes by type
/// and key, identifying insertions, deletions, moves, and updates.
/// Produces a <see cref="DiffPlan"/> consumed by <see cref="Reconciler"/>.
/// </summary>
internal static class NodeDiffer
{
    /// <summary>
    /// Computes the diff between an old rendered tree and a new rendered tree.
    /// Returns a plan describing the minimal set of operations needed to
    /// transform the old tree into the new tree while reusing existing
    /// component instances.
    /// </summary>
    internal static DiffPlan Diff(Node? oldTree, Node newTree)
    {
        var plan = new DiffPlan();
        DiffNode(oldTree, newTree, plan, index: 0);
        return plan;
    }

    private static void DiffNode(Node? oldNode, Node newNode, DiffPlan plan, int index)
    {
        if (oldNode is null || oldNode is EmptyNodeMarker)
        {
            if (newNode is not EmptyNodeMarker)
            {
                plan.Add(new DiffOperation(DiffKind.Insert, index, null, newNode));
            }

            return;
        }

        if (newNode is EmptyNodeMarker)
        {
            plan.Add(new DiffOperation(DiffKind.Delete, index, oldNode, null));
            return;
        }

        if (!IsSameType(oldNode, newNode))
        {
            plan.Add(new DiffOperation(DiffKind.Replace, index, oldNode, newNode));
            return;
        }

        if (newNode is Component newComponent)
        {
            plan.Add(new DiffOperation(DiffKind.ReuseComponent, index, oldNode, newComponent));
            return;
        }

        plan.Add(new DiffOperation(DiffKind.Update, index, oldNode, newNode));
        DiffChildren(oldNode, newNode, plan);
    }

    private static void DiffChildren(Node oldNode, Node newNode, DiffPlan plan)
    {
        var oldChildren = GetChildren(oldNode);
        var newChildren = GetChildren(newNode);

        if (oldChildren.Count == 0 && newChildren.Count == 0)
        {
            return;
        }

        if (HasKeyedChildren(newChildren))
        {
            DiffKeyedChildren(oldChildren, newChildren, plan);
        }
        else
        {
            DiffPositionalChildren(oldChildren, newChildren, plan);
        }
    }

    private static void DiffPositionalChildren(
        IReadOnlyList<Node> oldChildren,
        IReadOnlyList<Node> newChildren,
        DiffPlan plan)
    {
        int commonLength = Math.Min(oldChildren.Count, newChildren.Count);

        for (int i = 0; i < commonLength; i++)
        {
            DiffNode(oldChildren[i], newChildren[i], plan, i);
        }

        for (int i = commonLength; i < newChildren.Count; i++)
        {
            plan.Add(new DiffOperation(DiffKind.Insert, i, null, newChildren[i]));
        }

        for (int i = commonLength; i < oldChildren.Count; i++)
        {
            plan.Add(new DiffOperation(DiffKind.Delete, i, oldChildren[i], null));
        }
    }

    private static void DiffKeyedChildren(
        IReadOnlyList<Node> oldChildren,
        IReadOnlyList<Node> newChildren,
        DiffPlan plan)
    {
        var oldKeyMap = BuildKeyMap(oldChildren);

        var matchedOldIndices = new HashSet<int>();

        for (int newIdx = 0; newIdx < newChildren.Count; newIdx++)
        {
            var newChild = newChildren[newIdx];
            var key = newChild.ReconciliationKey;

            if (key is not null && oldKeyMap.TryGetValue(key, out int oldIdx))
            {
                matchedOldIndices.Add(oldIdx);
                var oldChild = oldChildren[oldIdx];

                if (oldIdx != newIdx)
                {
                    plan.Add(new DiffOperation(DiffKind.Move, newIdx, oldChild, newChild));
                }
                else
                {
                    DiffNode(oldChild, newChild, plan, newIdx);
                }
            }
            else
            {
                plan.Add(new DiffOperation(DiffKind.Insert, newIdx, null, newChild));
            }
        }

        for (int i = 0; i < oldChildren.Count; i++)
        {
            if (!matchedOldIndices.Contains(i))
            {
                plan.Add(new DiffOperation(DiffKind.Delete, i, oldChildren[i], null));
            }
        }
    }

    private static Dictionary<string, int> BuildKeyMap(IReadOnlyList<Node> children)
    {
        var map = new Dictionary<string, int>(children.Count);
        for (int i = 0; i < children.Count; i++)
        {
            var key = children[i].ReconciliationKey;
            if (key is not null)
            {
                map[key] = i;
            }
        }

        return map;
    }

    private static bool HasKeyedChildren(IReadOnlyList<Node> children)
    {
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].ReconciliationKey is not null)
            {
                return true;
            }
        }

        return false;
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Method performs work based on runtime node type.")]
    internal static IReadOnlyList<Node> GetChildren(Node node)
    {
        return node switch
        {
            Row row => row.Children,
            Column col => col.Children,
            Stack stack => stack.Children,
            Grid grid => grid.Children,
            Center center => [center.Child],
            ScrollView scroll => [scroll.Content],
            SplitView split => [split.First, split.Second],
            Badge badge => [badge.Child],
            AnimatePresence ap => [ap.Child],
            Card card => [card.Content, card.Header, card.Footer, card.Media],
            Expander exp => [exp.HeaderNode, exp.Content],
            Accordion acc => acc.Sections,
            FormValidator fv => [fv.Content],
            KeyHandler kh => [kh.Content],
            StatusBar sb => [sb.Left, sb.Center, sb.Right],
            IRadioGroup rg => [rg.Content],
            IRadioButton rb when !rb.NodeLabel.IsLayoutEmpty => [rb.NodeLabel],
            // Only the incoming page is a live, reconcilable child — it must be
            // mounted/rendered during the transition so it paints (not just at
            // completion). The outgoing tree is a frozen snapshot painted with a
            // transform; it is not reconciled.
            NavigationTransitionHost nth when nth.IncomingPage is not null => [nth.IncomingPage],
            Component comp when comp.RenderedTree is not null => [comp.RenderedTree],
            _ => []
        };
    }

    private static bool IsSameType(Node old, Node @new)
    {
        if (old.GetType() != @new.GetType())
        {
            return false;
        }

        if (old.ReconciliationKey is not null || @new.ReconciliationKey is not null)
        {
            return old.ReconciliationKey == @new.ReconciliationKey;
        }

        return true;
    }

    /// <summary>
    /// Marker interface for identifying Node.Empty instances during diffing.
    /// </summary>
    internal interface EmptyNodeMarker;
}

/// <summary>
/// The kind of diff operation to apply during reconciliation.
/// </summary>
internal enum DiffKind
{
    /// <summary>A new node needs to be inserted at this position.</summary>
    Insert,

    /// <summary>An existing node needs to be removed from this position.</summary>
    Delete,

    /// <summary>A keyed node moved from one position to another.</summary>
    Move,

    /// <summary>A node of the same type exists and its properties should be updated.</summary>
    Update,

    /// <summary>A node of a different type replaced the old node. Unmount old, mount new.</summary>
    Replace,

    /// <summary>An existing Component instance should be reused (same type and key match).</summary>
    ReuseComponent
}

/// <summary>
/// A single diff operation describing one change between old and new trees.
/// </summary>
internal readonly record struct DiffOperation(
    DiffKind Kind,
    int Index,
    Node? OldNode,
    Node? NewNode);

/// <summary>
/// An ordered list of diff operations that describes how to transform
/// an old node tree into a new one.
/// </summary>
internal sealed class DiffPlan
{
    private readonly List<DiffOperation> operations = [];

    internal void Add(DiffOperation op)
    {
        operations.Add(op);
    }

    internal IReadOnlyList<DiffOperation> Operations => operations;

    internal int Count => operations.Count;

    internal bool HasChanges => operations.Count > 0;
}

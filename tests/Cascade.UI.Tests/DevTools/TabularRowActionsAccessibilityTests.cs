using Cascade.UI.DevTools;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests.Controls;

// The DevTools accessibility tree only exists where CASCADE_DEVTOOLS is defined; files under
// DevTools/ are excluded from plain Release builds of this project.
public partial class TabularRowActionsTests
{
    // ── Accessibility ────────────────────────────────────────────────

    [Test]
    public async Task Accessibility_RowsOnScreenExposeTheirActionsAsNamedButtons_WithKeyboardFocus()
    {
        var table = BuildTable();
        NodeTreeWalker.SetRoot(table);
        try
        {
            Click(100, RowCenter(1));
            Press(Key.Right);
            Paint(table);

            var tree = NodeTreeWalker.GetAccessibilityTree();
            await Assert.That(tree.Role).IsEqualTo(AccessibleRole.Table);
            // The header row comes first; then one row per row on screen, holding its cells, then its actions.
            await Assert.That(tree.Children[0].Label).IsEqualTo("Column headers");
            var rows = tree.Children.Where(c => c.Role == AccessibleRole.Row).Skip(1).ToList();
            await Assert.That(rows.Count).IsEqualTo(6);

            var row1 = rows[1];
            await Assert.That(row1.Label).IsEqualTo("row1");
            await Assert.That(row1.StateProperties["selected"]).IsEqualTo("true");
            await Assert.That(string.Join(",", row1.Children.Select(b => $"{b.Role}:{b.Label}"))).IsEqualTo("Cell:row1,Cell:B,Button:Open,Button:Delete");
            var buttons = row1.Children.Where(b => b.Role == AccessibleRole.Button).ToList();
            await Assert.That(buttons[0].Focused).IsTrue();
            await Assert.That(buttons[1].Focused).IsFalse();
            await Assert.That(buttons[1].Bounds).IsEqualTo(
                new Rect(SecondButtonCenterX - 12f, DataTop + RowHeight + 3f, ButtonSize, ButtonSize));

            await Assert.That(rows[4].Children.Where(b => b.Role == AccessibleRole.Button).ElementAt(1).Disabled).IsTrue();
            await Assert.That(rows[3].Children.Count(b => b.Role == AccessibleRole.Button)).IsEqualTo(1);
        }
        finally
        {
            NodeTreeWalker.SetRoot(new Label(""));
        }
    }
}

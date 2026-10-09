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
            var rows = tree.Children.Where(c => c.Role == AccessibleRole.Row).ToList();
            await Assert.That(rows.Count).IsEqualTo(6);

            var row1 = rows[1];
            await Assert.That(row1.Label).IsEqualTo("row1");
            await Assert.That(row1.StateProperties["selected"]).IsEqualTo("true");
            await Assert.That(string.Join(",", row1.Children.Select(b => $"{b.Role}:{b.Label}"))).IsEqualTo("Button:Open,Button:Delete");
            await Assert.That(row1.Children[0].Focused).IsTrue();
            await Assert.That(row1.Children[1].Focused).IsFalse();
            await Assert.That(row1.Children[1].Bounds).IsEqualTo(
                new Rect(SecondButtonCenterX - 12f, DataTop + RowHeight + 3f, ButtonSize, ButtonSize));

            await Assert.That(rows[4].Children[1].Disabled).IsTrue();
            await Assert.That(rows[3].Children.Count).IsEqualTo(1);
        }
        finally
        {
            NodeTreeWalker.SetRoot(new Label(""));
        }
    }
}

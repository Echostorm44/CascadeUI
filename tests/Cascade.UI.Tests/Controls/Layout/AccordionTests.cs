#pragma warning disable CA2000, CA1812
using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class AccordionTests
{
    [Test]
    public async Task ExpanderWithTextHeader()
    {
        var content = new TestLeaf(100, 100);
        var expander = new Expander("Section 1", content, expanded: true);

        var expectedHeader = "Section 1";
        var expectedExpanded = true;
        await Assert.That(expander.HeaderText.Value).IsEqualTo(expectedHeader);
        await Assert.That(expander.Expanded).IsEqualTo(expectedExpanded);
        await Assert.That(expander.Content).IsEqualTo(content);
    }

    [Test]
    public async Task ExpanderWithNodeHeader()
    {
        var header = new TestLeaf(50, 20);
        var content = new TestLeaf(100, 100);
        var expander = new Expander(header, content);

        await Assert.That(expander.HeaderNode).IsEqualTo(header);
        var expectedEmpty = true;
        await Assert.That(expander.HeaderText.Value is null or "").IsEqualTo(expectedEmpty);
    }

    [Test]
    public async Task ExpanderWithBindable()
    {
        var bind = new Bindable<bool>(true, _ => { });
        var content = new TestLeaf(100, 100);
        var expander = new Expander("Header", content, bind);

        await Assert.That(expander.ExpandedBind.Value).IsEqualTo(true);
    }

    [Test]
    public async Task ExpanderOnToggleRegistersHandler()
    {
        bool toggled = false;
        var expander = new Expander("Header", new TestLeaf(100, 100));
        expander.OnToggle(isOpen => { toggled = isOpen; });

        await Assert.That(expander.LayoutData.ExpanderData).IsNotNull();
        await Assert.That(expander.LayoutData.ExpanderData!.OnToggleHandler).IsNotNull();
    }

    [Test]
    public async Task ExpanderIdIsPreserved()
    {
        var expander = new Expander("Header", new TestLeaf(100, 100), id: "section-1");

        var expected = "section-1";
        await Assert.That(expander.Id).IsEqualTo(expected);
    }

    [Test]
    public async Task AccordionDefaultModeIsMultiOpen()
    {
        var section = new Expander("A", new TestLeaf(100, 100));
        var accordion = new Accordion(section);

        var expected = AccordionMode.MultiOpen;
        await Assert.That(accordion.Mode).IsEqualTo(expected);
    }

    [Test]
    public async Task AccordionSingleOpenMode()
    {
        var section = new Expander("A", new TestLeaf(100, 100));
        var accordion = new Accordion(AccordionMode.SingleOpen, section);

        var expected = AccordionMode.SingleOpen;
        await Assert.That(accordion.Mode).IsEqualTo(expected);
    }

    [Test]
    public async Task AccordionWithDefaultSection()
    {
        var s1 = new Expander("A", new TestLeaf(100, 100), id: "first");
        var s2 = new Expander("B", new TestLeaf(100, 100), id: "second");
        var accordion = new Accordion(AccordionMode.SingleOpen, "first", s1, s2);

        var expectedDefault = "first";
        var expectedCount = 2;
        await Assert.That(accordion.Default).IsEqualTo(expectedDefault);
        await Assert.That(accordion.Sections.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task AccordionSectionsAreAccessible()
    {
        var s1 = new Expander("A", new TestLeaf(100, 100));
        var s2 = new Expander("B", new TestLeaf(100, 100));
        var s3 = new Expander("C", new TestLeaf(100, 100));
        var accordion = new Accordion(s1, s2, s3);

        var expectedCount = 3;
        await Assert.That(accordion.Sections.Count).IsEqualTo(expectedCount);
        await Assert.That(accordion.Sections[0]).IsEqualTo(s1);
        await Assert.That(accordion.Sections[2]).IsEqualTo(s3);
    }

    [Test]
    public async Task AccordionDefaultIsNullWhenNotSpecified()
    {
        var accordion = new Accordion(new Expander("A", new TestLeaf(100, 100)));

        await Assert.That(accordion.Default).IsNull();
    }

    [Test]
    public async Task UncontrolledExpander_SeedsStateFromInitialValue()
    {
        var collapsed = new Expander("A", new TestLeaf(100, 100), expanded: false);
        var open = new Expander("B", new TestLeaf(100, 100), expanded: true);

        // IsExpanded is what layout/paint/input all read. For an uncontrolled
        // expander it must reflect the mutable ExpandedState seeded from the ctor.
        await Assert.That(collapsed.IsExpanded).IsFalse();
        await Assert.That(open.IsExpanded).IsTrue();
    }

    [Test]
    public async Task UncontrolledExpander_TogglingStateFlipsIsExpanded()
    {
        var expander = new Expander("A", new TestLeaf(100, 100), expanded: false);

        // Simulate the dispatcher's uncontrolled toggle. Before the fix this state
        // did not exist and the header click was a permanent no-op.
        expander.ExpandedState = !expander.ExpandedState;

        await Assert.That(expander.IsExpanded).IsTrue();
    }

    [Test]
    public async Task BoundExpander_IsExpanded_FollowsBindNotState()
    {
        var bind = new Bindable<bool>(true, _ => { });
        var expander = new Expander("A", new TestLeaf(100, 100), bind);

        // A bound expander ignores the uncontrolled ExpandedState entirely.
        expander.ExpandedState = false;

        await Assert.That(expander.IsExpanded).IsTrue();
    }

    [Test]
    public async Task ExpanderNodeHeaderUsesEmptyForTextHeaderPath()
    {
        var content = new TestLeaf(100, 100);
        var expander = new Expander("Text Header", content);

        await Assert.That(expander.HeaderNode).IsEqualTo(Node.Empty);
    }
}

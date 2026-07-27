#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SplitNavigatorTests
{
    private sealed class PaneAPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class PaneBPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class PaneCPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class DetailPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class OtherPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private static SplitNavigator CreateSplitNavigator(int paneCount = 2)
    {
        var panes = new List<Navigator>();
        for (int i = 0; i < paneCount; i++)
        {
            panes.Add(new Navigator(new PaneAPage()));
        }

        return new SplitNavigator(panes);
    }

    [Test]
    public async Task Create_WithNPanes_HasCorrectPaneCount()
    {
        int expectedCount = 3;
        var split = CreateSplitNavigator(expectedCount);

        int count = split.PaneCount;
        await Assert.That(count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task EachPane_HasIndependentStack()
    {
        var paneA = new Navigator(new PaneAPage());
        var paneB = new Navigator(new PaneBPage());
        var split = new SplitNavigator([paneA, paneB]);

        split[0].Push<DetailPage>();

        int depthA = split[0].StackDepth;
        int depthB = split[1].StackDepth;

        await Assert.That(depthA).IsEqualTo(2);
        await Assert.That(depthB).IsEqualTo(1);
    }

    [Test]
    public async Task PushOnOnePane_DoesNotAffectOthers()
    {
        var paneA = new Navigator(new PaneAPage());
        var paneB = new Navigator(new PaneBPage());
        var paneC = new Navigator(new PaneCPage());
        var split = new SplitNavigator([paneA, paneB, paneC]);

        split[1].Push<DetailPage>();
        split[1].Push<OtherPage>();

        bool aContainsDetail = split[0].Contains<DetailPage>();
        bool cContainsDetail = split[2].Contains<DetailPage>();
        bool bContainsDetail = split[1].Contains<DetailPage>();

        await Assert.That(aContainsDetail).IsFalse();
        await Assert.That(cContainsDetail).IsFalse();
        await Assert.That(bContainsDetail).IsTrue();
    }

    [Test]
    public async Task Indexer_ReturnsCorrectNavigator()
    {
        var paneA = new Navigator(new PaneAPage());
        var paneB = new Navigator(new PaneBPage());
        var split = new SplitNavigator([paneA, paneB]);

        var navA = split[0];
        var navB = split[1];

        bool aIsSame = ReferenceEquals(navA, paneA);
        bool bIsSame = ReferenceEquals(navB, paneB);

        await Assert.That(aIsSame).IsTrue();
        await Assert.That(bIsSame).IsTrue();
    }

    [Test]
    public async Task ActivePaneIndex_DefaultsToZero()
    {
        var split = CreateSplitNavigator(3);

        int active = split.ActivePaneIndex;
        await Assert.That(active).IsEqualTo(0);
    }

    [Test]
    public async Task SetActivePane_UpdatesActiveIndex()
    {
        var split = CreateSplitNavigator(3);
        split.SetActivePane(2);

        int active = split.ActivePaneIndex;
        await Assert.That(active).IsEqualTo(2);
    }

    [Test]
    public async Task Indexer_ThrowsOnInvalidIndex()
    {
        var split = CreateSplitNavigator(2);

        var action = () => { _ = split[5]; };
        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Render_ReturnsNonEmptyNode()
    {
        var split = CreateSplitNavigator(2);

        var rendered = split.InvokeRender();
        bool isNotEmpty = rendered != Node.Empty;
        await Assert.That(isNotEmpty).IsTrue();
    }

    [Test]
    public async Task PaneNavigation_PopWorksIndependently()
    {
        var paneA = new Navigator(new PaneAPage());
        var paneB = new Navigator(new PaneBPage());
        var split = new SplitNavigator([paneA, paneB]);

        split[0].Push<DetailPage>();
        split[0].Push<OtherPage>();
        split[0].Pop();

        int depthA = split[0].StackDepth;
        int depthB = split[1].StackDepth;

        await Assert.That(depthA).IsEqualTo(2);
        await Assert.That(depthB).IsEqualTo(1);
    }

    [Test]
    public async Task TwoPaneSplit_WithOptions_HasCorrectPaneCount()
    {
        var paneA = new Navigator(new PaneAPage());
        var paneB = new Navigator(new PaneBPage());
        var options = new SplitNavigatorOptions { Ratios = [0.3f] };
        var split = new SplitNavigator([paneA, paneB], options);

        int count = split.PaneCount;
        await Assert.That(count).IsEqualTo(2);
    }
}

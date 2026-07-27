#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class NavigationTests
{
    private sealed class HomePage : Component
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

    private sealed class SettingsPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class ProfilePage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private sealed class ResultPage : Component
    {
        protected override Node Render()
        {
            return Node.Empty;
        }
    }

    private static Navigator CreateNavigator(Component? initialPage = null)
    {
        return new Navigator(initialPage ?? new HomePage());
    }

    [Test]
    public async Task Push_AddsToStack()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();

        int depth = nav.StackDepth;
        await Assert.That(depth).IsEqualTo(2);
    }

    [Test]
    public async Task Pop_RemovesFromStack()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();
        nav.Pop();

        int depth = nav.StackDepth;
        await Assert.That(depth).IsEqualTo(1);
    }

    [Test]
    public async Task Replace_ReplacesCurrentPage()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();
        nav.Replace<SettingsPage>();

        int depth = nav.StackDepth;
        bool containsSettings = nav.Contains<SettingsPage>();
        bool containsDetail = nav.Contains<DetailPage>();

        await Assert.That(depth).IsEqualTo(2);
        await Assert.That(containsSettings).IsTrue();
        await Assert.That(containsDetail).IsFalse();
    }

    [Test]
    public async Task PopTo_PopsToSpecifiedPageType()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();
        nav.Push<SettingsPage>();
        nav.Push<ProfilePage>();
        nav.PopTo<DetailPage>();

        int depth = nav.StackDepth;
        await Assert.That(depth).IsEqualTo(2);
    }

    [Test]
    public async Task Reset_ClearsStackToNewRoot()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();
        nav.Push<SettingsPage>();
        nav.Reset<ProfilePage>();

        int depth = nav.StackDepth;
        bool containsProfile = nav.Contains<ProfilePage>();
        bool containsHome = nav.Contains<HomePage>();

        await Assert.That(depth).IsEqualTo(1);
        await Assert.That(containsProfile).IsTrue();
        await Assert.That(containsHome).IsFalse();
    }

    [Test]
    public async Task StackDepth_IsCorrectAfterOperations()
    {
        var nav = CreateNavigator();

        int initial = nav.StackDepth;
        await Assert.That(initial).IsEqualTo(1);

        nav.Push<DetailPage>();
        int afterPush = nav.StackDepth;
        await Assert.That(afterPush).IsEqualTo(2);

        nav.Push<SettingsPage>();
        int afterSecondPush = nav.StackDepth;
        await Assert.That(afterSecondPush).IsEqualTo(3);

        nav.Pop();
        int afterPop = nav.StackDepth;
        await Assert.That(afterPop).IsEqualTo(2);
    }

    [Test]
    public async Task CanGoBack_IsFalseWithSinglePage()
    {
        var nav = CreateNavigator();

        bool canGoBack = nav.CanGoBack;
        await Assert.That(canGoBack).IsFalse();
    }

    [Test]
    public async Task CanGoBack_IsTrueWithMultiplePages()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();

        bool canGoBack = nav.CanGoBack;
        await Assert.That(canGoBack).IsTrue();
    }

    [Test]
    public async Task Contains_FindsPushedPageType()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();

        bool containsDetail = nav.Contains<DetailPage>();
        bool containsHome = nav.Contains<HomePage>();
        bool containsSettings = nav.Contains<SettingsPage>();

        await Assert.That(containsDetail).IsTrue();
        await Assert.That(containsHome).IsTrue();
        await Assert.That(containsSettings).IsFalse();
    }

    [Test]
    public async Task Pop_DoesNothingOnSinglePage()
    {
        var nav = CreateNavigator();
        nav.Pop();

        int depth = nav.StackDepth;
        await Assert.That(depth).IsEqualTo(1);
    }

    [Test]
    public async Task PopTo_DoesNothingIfTypeNotInStack()
    {
        var nav = CreateNavigator();
        nav.Push<DetailPage>();
        nav.PopTo<SettingsPage>();

        int depth = nav.StackDepth;
        await Assert.That(depth).IsEqualTo(2);
    }

    [Test]
    public async Task PagePushed_EventFiredOnPush()
    {
        var nav = CreateNavigator();
        Component? pushed = null;
        nav.PagePushed += c => { pushed = c; };

        nav.Push<DetailPage>();

        bool isPushed = pushed is DetailPage;
        await Assert.That(isPushed).IsTrue();
    }

    [Test]
    public async Task PagePopped_EventFiredOnPop()
    {
        var nav = CreateNavigator();
        Component? popped = null;
        nav.PagePopped += c => { popped = c; };

        nav.Push<DetailPage>();
        nav.Pop();

        bool isPopped = popped is DetailPage;
        await Assert.That(isPopped).IsTrue();
    }

    [Test]
    public async Task PageResumed_EventFiredOnPop()
    {
        var nav = CreateNavigator();
        Component? resumed = null;
        nav.PageResumed += c => { resumed = c; };

        nav.Push<DetailPage>();
        nav.Pop();

        bool isResumed = resumed is HomePage;
        await Assert.That(isResumed).IsTrue();
    }

    [Test]
    public async Task PushForResultAsync_ReturnsResultWhenDelivered()
    {
        var nav = CreateNavigator();

        var task = nav.PushForResultAsync<ResultPage, string>(CancellationToken.None);

        int depthAfterPush = nav.StackDepth;
        await Assert.That(depthAfterPush).IsEqualTo(2);

        nav.ReturnResult<string>("hello");

        var result = await task;
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task PushForResultAsync_ReturnsNullOnCancellation()
    {
        var nav = CreateNavigator();
        using var cts = new CancellationTokenSource();

        var task = nav.PushForResultAsync<ResultPage, string>(cts.Token);
        await cts.CancelAsync();

        var result = await task;
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Render_ReturnsCurrentPageComponent()
    {
        var homePage = new HomePage();
        var nav = new Navigator(homePage);

        var rendered = nav.InvokeRender();

        bool isSame = ReferenceEquals(rendered, homePage);
        await Assert.That(isSame).IsTrue();
    }

    [Test]
    public async Task Render_SetsNavigationContext()
    {
        var nav = CreateNavigator();
        INavigator? captured = null;

        var previous = Navigation.CurrentNavigator;
        try
        {
            Navigation.CurrentNavigator = nav;
            captured = Navigation.CurrentNavigator;
        }
        finally
        {
            Navigation.CurrentNavigator = previous;
        }

        bool isSame = ReferenceEquals(captured, nav);
        await Assert.That(isSame).IsTrue();
    }
}

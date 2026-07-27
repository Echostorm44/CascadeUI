#pragma warning disable CA2000, CA1812

using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PageTransitionTests
{
    [Test]
    public async Task Slide_IsNotNull()
    {
        var transition = PageTransition.Slide;
        bool isNotNull = transition is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Fade_IsNotNull()
    {
        var transition = PageTransition.Fade;
        bool isNotNull = transition is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task None_IsNotNull()
    {
        var transition = PageTransition.None;
        bool isNotNull = transition is not null;
        await Assert.That(isNotNull).IsTrue();
    }

    [Test]
    public async Task Custom_CreatesTransitionWithModels()
    {
        var enter = AnimationModel.Spring.Standard;
        var exit = AnimationModel.Spring.Gentle;
        var transition = PageTransition.Custom(enter, exit);

        bool hasEnter = transition.EnterModel is not null;
        bool hasExit = transition.ExitModel is not null;

        await Assert.That(hasEnter).IsTrue();
        await Assert.That(hasExit).IsTrue();
    }

    [Test]
    public async Task Custom_EnterModelMatchesProvided()
    {
        var enter = AnimationModel.Spring.Snappy;
        var exit = AnimationModel.Spring.Gentle;
        var transition = PageTransition.Custom(enter, exit);

        bool isSameEnter = ReferenceEquals(transition.EnterModel, enter);
        bool isSameExit = ReferenceEquals(transition.ExitModel, exit);

        await Assert.That(isSameEnter).IsTrue();
        await Assert.That(isSameExit).IsTrue();
    }

    [Test]
    public async Task Curtain_CreatesTransitionWithFactory()
    {
        var enter = AnimationModel.Ease(Duration.Ms(300));
        var exit = AnimationModel.Ease(Duration.Ms(300));
        var transition = PageTransition.Curtain(
            Duration.Ms(500),
            (progress, size) => Node.Empty,
            enter,
            exit);

        bool hasCurtain = transition.CurtainFactory is not null;
        bool hasDuration = transition.CurtainDuration is not null;

        await Assert.That(hasCurtain).IsTrue();
        await Assert.That(hasDuration).IsTrue();
    }

    [Test]
    public async Task Curtain_FactoryReceivesProgressAndSize()
    {
        float capturedProgress = -1f;
        Size capturedSize = Size.Zero;

        var transition = PageTransition.Curtain(
            Duration.Ms(500),
            (progress, size) =>
            {
                capturedProgress = progress;
                capturedSize = size;
                return Node.Empty;
            },
            AnimationModel.Ease(Duration.Ms(300)),
            AnimationModel.Ease(Duration.Ms(300)));

        var factory = transition.CurtainFactory!;
        factory(0.5f, new Size(100, 200));

        float expectedProgress = 0.5f;
        float expectedWidth = 100f;
        float expectedHeight = 200f;

        await Assert.That(capturedProgress).IsEqualTo(expectedProgress);
        await Assert.That(capturedSize.Width).IsEqualTo(expectedWidth);
        await Assert.That(capturedSize.Height).IsEqualTo(expectedHeight);
    }

    [Test]
    public async Task TransitionManager_NoneTransition_CompletesImmediately()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        using var manager = new TransitionManager(engine);

        bool completed = false;
        manager.RunTransition(
            PageTransition.None,
            PageTransitionMode.Push,
            onComplete: () => { completed = true; });

        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task TransitionManager_NoneTransition_SetsProgressToOne()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        using var manager = new TransitionManager(engine);

        manager.RunTransition(PageTransition.None, PageTransitionMode.Push);

        float enterProgress = manager.EnterProgress;
        float exitProgress = manager.ExitProgress;
        float expected = 1f;

        await Assert.That(enterProgress).IsEqualTo(expected);
        await Assert.That(exitProgress).IsEqualTo(expected);
    }

    [Test]
    public async Task TransitionManager_CustomTransition_StartsTransitioning()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        using var manager = new TransitionManager(engine);

        var transition = PageTransition.Custom(
            AnimationModel.Spring.Standard,
            AnimationModel.Spring.Standard);

        manager.RunTransition(transition, PageTransitionMode.Push);

        bool isTransitioning = manager.IsTransitioning;
        await Assert.That(isTransitioning).IsTrue();
    }

    [Test]
    public async Task TransitionManager_Cancel_StopsTransition()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        using var manager = new TransitionManager(engine);

        var transition = PageTransition.Custom(
            AnimationModel.Spring.Standard,
            AnimationModel.Spring.Standard);

        manager.RunTransition(transition, PageTransitionMode.Push);
        manager.CancelCurrentTransition();

        bool isTransitioning = manager.IsTransitioning;
        float expected = 1f;

        await Assert.That(isTransitioning).IsFalse();
        await Assert.That(manager.EnterProgress).IsEqualTo(expected);
    }

    [Test]
    public async Task TransitionManager_Interruption_CancelsPreviousTransition()
    {
        var scheduler = new AnimationScheduler();
        var engine = new TransitionEngine(scheduler);
        using var manager = new TransitionManager(engine);

        var transition1 = PageTransition.Custom(
            AnimationModel.Spring.Standard,
            AnimationModel.Spring.Standard);

        var transition2 = PageTransition.Custom(
            AnimationModel.Spring.Snappy,
            AnimationModel.Spring.Snappy);

        manager.RunTransition(transition1, PageTransitionMode.Push);
        manager.RunTransition(transition2, PageTransitionMode.Push);

        bool isTransitioning = manager.IsTransitioning;
        await Assert.That(isTransitioning).IsTrue();
    }

    [Test]
    public async Task SlidePresets_AreDifferentInstances()
    {
        var slide = PageTransition.Slide;
        var slideLeft = PageTransition.SlideLeft;
        var slideRight = PageTransition.SlideRight;

        bool slideNotSameAsLeft = !ReferenceEquals(slide, slideLeft);
        bool leftNotSameAsRight = !ReferenceEquals(slideLeft, slideRight);

        await Assert.That(slideNotSameAsLeft).IsTrue();
        await Assert.That(leftNotSameAsRight).IsTrue();
    }

    [Test]
    public async Task Presets_CarryDistinctKinds()
    {
        // Every preset must be self-describing via Kind — the painter and
        // navigator select behaviour off Kind, not reference identity. If two
        // presets shared a Kind the wrong animation would play (the original
        // "everything slides right" bug: all presets were indistinguishable).
        var kinds = new[]
        {
            PageTransition.Slide.Kind,
            PageTransition.SlideLeft.Kind,
            PageTransition.SlideRight.Kind,
            PageTransition.SlideUp.Kind,
            PageTransition.SlideDown.Kind,
            PageTransition.Fade.Kind,
            PageTransition.None.Kind,
            PageTransition.Custom(AnimationModel.Spring.Standard, AnimationModel.Spring.Standard).Kind,
            PageTransition.Curtain(Duration.Ms(500), (_, _) => Node.Empty,
                AnimationModel.Spring.Standard, AnimationModel.Spring.Standard).Kind,
        };

        int distinct = kinds.Distinct().Count();
        await Assert.That(distinct).IsEqualTo(kinds.Length);
    }

    [Test]
    public async Task CrossFade_IsFadeKindWithDuration()
    {
        var transition = PageTransition.CrossFade(Duration.Ms(2500));

        await Assert.That(transition.Kind).IsEqualTo(PageTransitionKind.Fade);
        await Assert.That(transition.FadeDuration.HasValue).IsTrue();
        await Assert.That(transition.FadeDuration!.Value.TotalMilliseconds).IsEqualTo(2500d);
    }

    [Test]
    public async Task Fade_HasNoExplicitDuration()
    {
        await Assert.That(PageTransition.Fade.FadeDuration.HasValue).IsFalse();
    }

    [Test]
    public async Task Presets_MapToExpectedKind()
    {
        await Assert.That(PageTransition.Slide.Kind).IsEqualTo(PageTransitionKind.Slide);
        await Assert.That(PageTransition.SlideUp.Kind).IsEqualTo(PageTransitionKind.SlideUp);
        await Assert.That(PageTransition.SlideDown.Kind).IsEqualTo(PageTransitionKind.SlideDown);
        await Assert.That(PageTransition.Fade.Kind).IsEqualTo(PageTransitionKind.Fade);
        await Assert.That(PageTransition.None.Kind).IsEqualTo(PageTransitionKind.None);
    }
}

#pragma warning disable CA1812 // instantiated via Navigator
public class NavigatorTransitionOverrideTests
{
    private sealed class HomePage : Component
    {
        protected override Node Render() => Node.Empty;
    }

    private sealed class DetailPage : Component
    {
        protected override Node Render() => Node.Empty;
    }

    [Test]
    public async Task Push_WithoutOverride_UsesNavigatorDefault()
    {
        using var nav = new Navigator(new HomePage(), PageTransition.Slide);
        nav.Push<DetailPage>();

        await Assert.That(nav.ActiveTransition).IsEqualTo(PageTransition.Slide);
        await Assert.That(nav.IsTransitioning).IsTrue();
    }

    [Test]
    public async Task Push_Transition_OverridesNavigatorDefault()
    {
        // The reported bug: every push used the navigator default regardless of
        // intent. The per-push override must win.
        using var nav = new Navigator(new HomePage(), PageTransition.Slide);
        nav.Push<DetailPage>().Transition(PageTransition.SlideUp);

        await Assert.That(nav.ActiveTransition).IsEqualTo(PageTransition.SlideUp);
        await Assert.That(nav.IsTransitioning).IsTrue();
    }

    [Test]
    public async Task Push_TransitionNone_SkipsAnimation()
    {
        using var nav = new Navigator(new HomePage(), PageTransition.Slide);
        nav.Push<DetailPage>().Transition(PageTransition.None);

        await Assert.That(nav.ActiveTransition).IsEqualTo(PageTransition.None);
        await Assert.That(nav.IsTransitioning).IsFalse();
    }

    [Test]
    public async Task Pop_Transition_OverridesNavigatorDefault()
    {
        using var nav = new Navigator(new HomePage(), PageTransition.Slide);
        nav.Push<DetailPage>();
        nav.Pop().Transition(PageTransition.Fade);

        await Assert.That(nav.ActiveTransition).IsEqualTo(PageTransition.Fade);
    }

    [Test]
    public async Task NavigatorDefault_SlideUp_AppliesToPush()
    {
        using var nav = new Navigator(new HomePage(), PageTransition.SlideUp);
        nav.Push<DetailPage>();

        await Assert.That(nav.ActiveTransition).IsEqualTo(PageTransition.SlideUp);
    }
}

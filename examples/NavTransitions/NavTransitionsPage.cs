// Golden Example 07 — Navigation Transitions
//
// Demonstrates the Navigation system: Push/Pop between pages with
// different PageTransition types. MainPage has buttons that push
// FooPage using different transitions. FooPage pops back.

#pragma warning disable CA1812 // Internal class is never instantiated
#pragma warning disable CA1852 // Type can be sealed
#pragma warning disable CA2000 // Dispose objects before losing scope

using Cascade.UI;
using static Cascade.UI.CanvasFactory;

namespace NavTransitions;

// ── Main Page ─────────────────────────────────────────────────────────────────

internal class MainPage : Component
{
    protected override Node Render() =>
        new Navigator(
            initialPage: new TransitionMenu(),
            transition: PageTransition.Slide
        );
}

// ── Transition Menu ──────────────────────────────────────────────────────────

internal class TransitionMenu : Component
{
    protected override Node Render() =>
        new Center(
            child: new Column(
                spacing: 24,
                children:
                [
                    new Label("Navigation Transitions")
                        .FontSize(28)
                        .Bold(),

                    new Label("Each button pushes FooPage with a different transition.")
                        .FontSize(14)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),

                    new Separator(),

                    // Default Slide — the navigator's default: push slides left,
                    // pop slides right. Shown explicitly for contrast.
                    new Button(
                        "Default Slide →",
                        onClick: () => { Navigation.Push<FooPage>("Default Slide"); }
                    ),

                    // Fade — a slow, deliberate crossfade so the dissolve is easy
                    // to watch: the old page fades out, then the new fades in.
                    // CrossFade(duration) sets the timing; plain PageTransition.Fade
                    // is the quick default for everyday navigation.
                    new Button(
                        "Fade",
                        onClick: () =>
                        {
                            Navigation.Push<FooPage>("Fade")
                                .Transition(PageTransition.CrossFade(Duration.Ms(5000)));
                        }
                    ),

                    // SlideUp — incoming from bottom, outgoing to top
                    new Button(
                        "Slide Up ↑",
                        onClick: () =>
                        {
                            Navigation.Push<FooPage>("Slide Up")
                                .Transition(PageTransition.SlideUp);
                        }
                    ),

                    // SlideDown — incoming from top, outgoing to bottom
                    new Button(
                        "Slide Down ↓",
                        onClick: () =>
                        {
                            Navigation.Push<FooPage>("Slide Down")
                                .Transition(PageTransition.SlideDown);
                        }
                    ),

                    new Separator(),

                    new Label("Custom transitions compose from keyframes or a curtain factory.")
                        .FontSize(12)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),

                    // Dissolve — the old page breaks into patches that wink out
                    // in scattered order, revealing the new page through the gaps.
                    new Button(
                        "Dissolve",
                        onClick: () =>
                        {
                            Navigation.Push<FooPage>("Dissolve")
                                .Transition(PageTransition.Dissolve(Duration.Ms(4000)));
                        }
                    ),

                    // Curtain Wipe — the dramatic example
                    new Button(
                        "Custom: Curtain Wipe",
                        onClick: () =>
                        {
                            Navigation.Push<FooPage>("Curtain Wipe")
                                .Transition(Transitions.CurtainWipe);
                        }
                    ),
                ]
            ).Padding(40)
        );
}

// ── Foo Page ──────────────────────────────────────────────────────────────────

internal class FooPage : Component
{
    private readonly string transitionName;

    public FooPage(string transitionName)
    {
        this.transitionName = transitionName;
    }

    protected override Node Render() =>
        new Center(
            child: new Column(
                spacing: 20,
                children:
                [
                    new Label("Foo Page")
                        .FontSize(28)
                        .Bold(),

                    new Label($"Arrived via: {transitionName}")
                        .FontSize(16)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),

                    new Label("Pop returns to the menu. The menu was never unmounted.")
                        .FontSize(12)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),

                    new Spacer(20),

                    new Button(
                        "← Back to Menu",
                        onClick: () => { Navigation.Pop(); }
                    ),
                ]
            ).Padding(40)
        );
}

// ── Custom Transition Definitions ─────────────────────────────────────────────
//
// A transition is a value — defined once here and referenced per-push via
// .Transition(...). CurtainWipe supplies a factory node composited above both pages.

internal static class Transitions
{
    // Curtain Wipe: a colored bar sweeps left-to-right, then splits
    // top/bottom to reveal the new page.
    public static readonly PageTransition CurtainWipe = PageTransition.Curtain(
        duration: Duration.Ms(900),
        curtain: (progress, size) => CurtainFrame(progress, size),
        enter: AnimationModel.Linear(Duration.Ms(0)),
        exit: AnimationModel.Linear(Duration.Ms(0))
    );

    private static Node CurtainFrame(float progress, Size size)
    {
        var curtainColor = ThemeSwitcher.ActiveColors.Surface;

        if (progress <= 0.5f)
        {
            return CoverPhase(progress, size, curtainColor);
        }
        else
        {
            return RevealPhase(progress, size, curtainColor);
        }
    }

    // Phase 1: bar grows from left to full width
    private static Node CoverPhase(float progress, Size size, ColorValue color)
    {
        float phaseProgress = progress / 0.5f;
        float easedProgress = EaseInOut(phaseProgress);
        float barWidth = size.Width * easedProgress;

        return Canvas(
            size: size,
            onDraw: (ctx, s) =>
            {
                ctx.DrawRect(
                    new Rect(0, 0, barWidth, s.Height),
                    fill: color
                );
            }
        );
    }

    // Phase 2: curtain splits top/bottom revealing new page
    private static Node RevealPhase(float progress, Size size, ColorValue color)
    {
        float phaseProgress = (progress - 0.5f) / 0.5f;
        float easedProgress = EaseInOut(phaseProgress);

        float midY = size.Height * 0.5f;
        float topOffset = -midY * easedProgress;
        float bottomOffset = midY * easedProgress;

        return Canvas(
            size: size,
            onDraw: (ctx, s) =>
            {
                ctx.DrawRect(
                    new Rect(0, topOffset, s.Width, midY),
                    fill: color
                );
                ctx.DrawRect(
                    new Rect(0, midY + bottomOffset, s.Width, midY),
                    fill: color
                );
            }
        );
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);
}

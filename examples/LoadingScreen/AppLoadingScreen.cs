// Golden Example 06 — App Loading Screen
//
// StarfieldCanvas fills the background. As each initialization task completes,
// the warp factor advances — stars accelerate in step with real progress.
// Demonstrates coordinating a visual effect's intensity with real async progress,
// sequential initialization steps, AnimatedValue, and ProgressBar.

using Cascade.UI;
using static Cascade.UI.CanvasFactory;

#pragma warning disable CA1031 // General catch for initialization failure display
#pragma warning disable CA5394 // Random is not insecure for visual effects

namespace LoadingScreen;

internal sealed class AppLoadingScreen : Component
{
    private string currentTask    = "Starting up...";
    private int    completedSteps;
    private int    totalSteps;
    private bool   loadingFailed;
    private string failureMessage = "";

    private float Progress => totalSteps > 0
        ? (float)completedSteps / totalSteps
        : 0f;

    // Opacity state — driven directly since AnimatedValue requires framework integration.
    private float statusOpacity;
    private float progressOpacity;

    // ── Inline starfield state ────────────────────────────────────────
    private struct Star
    {
        public float Angle;
        public float Radius;
        public float Speed;
        public float PrevRadius;
        public float Brightness;
    }

    private static readonly ColorValue White     = new("#FFFFFF");
    private static readonly ColorValue Black     = new("#000000");
    private static readonly ColorValue BlueWhite = new("#B0D0FF");
    private static readonly ColorValue WarmWhite = new("#FFEEDD");

    private const int   StarCount = 200;
    private const float BaseSpeed = 30f;
    private const float MaxSpeed  = 1400f;

    private Star[] stars = [];
    private float  warpFactor;
    private Size   canvasSize;

    protected override async Task OnMounted()
    {
        InitStars();

        // Make status visible immediately.
        statusOpacity = 1f;
        progressOpacity = 1f;
        Invalidate();

        await RunInitializationAsync();
    }

    private void InitStars()
    {
        stars = new Star[StarCount];
        var rng = new Random();
        float maxInitialRadius = 600f;

        for (int i = 0; i < stars.Length; i++)
        {
            float r = (float)rng.NextDouble();
            float radius = r * r * maxInitialRadius;
            float speed  = BaseSpeed * (0.3f + (float)rng.NextDouble() * 1.2f);
            speed *= 0.8f + radius / maxInitialRadius * 0.5f;

            stars[i] = new Star
            {
                Angle      = (float)(rng.NextDouble() * Math.Tau),
                Radius     = radius,
                Speed      = speed,
                PrevRadius = radius,
                Brightness = 0.4f + (float)rng.NextDouble() * 0.6f
            };
        }
    }

    private void SetWarpProgress(float progress)
    {
        warpFactor = Math.Clamp(progress, 0f, 1f);
    }

    // ── Initialization sequence ───────────────────────────────────────

    private async Task RunInitializationAsync()
    {
        var steps = new (string Label, Func<CancellationToken, Task> Work)[]
        {
            ("Checking for updates...",     SimulateStepAsync),
            ("Opening database...",         SimulateStepAsync),
            ("Applying migrations...",      SimulateStepAsync),
            ("Loading user preferences...", SimulateStepAsync),
            ("Validating license...",       SimulateStepAsync),
            ("Syncing product catalog...",  SimulateLongStepAsync),
            ("Loading cached assets...",    SimulateStepAsync),
            ("Connecting to services...",   SimulateStepAsync),
        };

        totalSteps = steps.Length;
        Invalidate();

        try
        {
            foreach (var (label, work) in steps)
            {
                currentTask = label;
                Invalidate();

                await work(LifetimeToken);

                completedSteps++;
                SetWarpProgress(Progress);
                Invalidate();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            loadingFailed  = true;
            failureMessage = ex.Message;
            Invalidate();
            return;
        }

        await CompleteLoadingAsync();
    }

    private async Task CompleteLoadingAsync()
    {
        SetWarpProgress(1f);
        currentTask = "Ready.";
        Invalidate();

        await Delay(Duration.Ms(600), LifetimeToken);

        // Fade out — set opacity to 0.
        statusOpacity = 0f;
        progressOpacity = 0f;
        Invalidate();

        await Delay(Duration.Ms(300), LifetimeToken);

        // In a real app: Navigation.Replace<MainShell>();
        currentTask = "Application Loaded";
        statusOpacity = 1f;
        Invalidate();
    }

    // ── Render ────────────────────────────────────────────────────────

    protected override Node Render()
    {
        if (loadingFailed)
        {
            return FailureView();
        }

        return new Stack(children:
        [
            StarfieldNode(),

            new Center(
                new Column(spacing: 20, children:
                [
                    new Label("MyApp")
                        .FontSize(32)
                        .Bold()
                        .Color(White)
                        .Opacity(statusOpacity),

                    StatusArea(),
                ])
            ),
        ]);
    }

    private Node StarfieldNode()
    {
        return Canvas(
            size:    Size.Fill,
            onDraw:  DrawStars,
            onFrame: UpdateStars
        ).Background(Black);
    }

    private Node StatusArea()
    {
        return new Column(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new ProgressBar(Progress)
                .Width(280)
                .Opacity(progressOpacity),

            new Label(currentTask)
                .FontSize(12)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
                .Opacity(statusOpacity),
        ]);
    }

    private Node FailureView()
    {
        return new Stack(children:
        [
            StarfieldNode(),

            new Center(
                new Column(spacing: 20, crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    new Label("Startup Failed")
                        .FontSize(24)
                        .Bold()
                        .Color(White),

                    new Label(failureMessage)
                        .FontSize(14)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),

                    new Button(
                        label: "Quit",
                        onClick: () => { Environment.Exit(0); }
                    ),
                ])
                .Padding(40)
            ),
        ]);
    }

    // ── Starfield rendering ───────────────────────────────────────────

    private void UpdateStars(float dt)
    {
        if (canvasSize.Width < 1f || canvasSize.Height < 1f)
        {
            return;
        }

        float peakAcceleration = MaxSpeed * 2f;
        float acceleration     = warpFactor * peakAcceleration;

        var center    = new Point(canvasSize.Width * 0.5f, canvasSize.Height * 0.5f);
        float maxRadius = MathF.Sqrt(center.X * center.X + center.Y * center.Y) + 20f;
        var rng = Random.Shared;

        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].PrevRadius = stars[i].Radius;
            stars[i].Speed      = Math.Min(stars[i].Speed + acceleration * dt, MaxSpeed);
            stars[i].Radius    += stars[i].Speed * dt;

            if (stars[i].Radius >= maxRadius)
            {
                stars[i].Angle      = (float)(rng.NextDouble() * Math.Tau);
                stars[i].Radius     = (float)(rng.NextDouble() * 3f);
                stars[i].PrevRadius = stars[i].Radius;
                stars[i].Speed      = BaseSpeed * (0.4f + (float)rng.NextDouble() * 0.6f);
                stars[i].Brightness = 0.4f + (float)rng.NextDouble() * 0.6f;
            }
        }
    }

    private void DrawStars(DrawContext ctx, Size size)
    {
        canvasSize = size;
        var center = new Point(size.Width * 0.5f, size.Height * 0.5f);
        float halfDiag = MathF.Sqrt(center.X * center.X + center.Y * center.Y);

        for (int i = 0; i < stars.Length; i++)
        {
            ref readonly var star = ref stars[i];

            float cosA = MathF.Cos(star.Angle);
            float sinA = MathF.Sin(star.Angle);

            var current  = new Point(center.X + cosA * star.Radius,     center.Y + sinA * star.Radius);
            var previous = new Point(center.X + cosA * star.PrevRadius, center.Y + sinA * star.PrevRadius);

            float speedRatio   = star.Speed / MaxSpeed;
            float distRatio    = star.Radius / halfDiag;
            float streakLength = star.Radius - star.PrevRadius;

            float alpha = Math.Clamp(distRatio * 1.5f + speedRatio * 0.5f, 0.05f, 1f) * star.Brightness;

            if (streakLength < 1f)
            {
                float dotSize = 0.5f + distRatio * 1f;
                ctx.DrawCircle(current, radius: dotSize, fill: White.Opacity(alpha * 0.8f));
            }
            else
            {
                float lineWidth = 0.5f + speedRatio * 1.2f;

                ColorValue color;
                if (speedRatio < 0.4f)
                {
                    color = ColorValue.Lerp(BlueWhite, White, speedRatio * 2.5f);
                }
                else
                {
                    color = ColorValue.Lerp(White, WarmWhite, (speedRatio - 0.4f) * 1.67f);
                }

                ctx.DrawLine(
                    from:   previous,
                    to:     current,
                    stroke: new Stroke(color.Opacity(alpha), lineWidth, StrokeCap.Round, StrokeJoin.Round));
            }
        }
    }

    // ── Mock initialization steps ─────────────────────────────────────

    private static async Task SimulateStepAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400 + Random.Shared.Next(300)), ct);
    }

    private static async Task SimulateLongStepAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(800 + Random.Shared.Next(500)), ct);
    }
}

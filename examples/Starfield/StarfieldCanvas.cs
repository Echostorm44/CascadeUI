// Golden Example 04 — Starfield Canvas Animation
//
// Classic starfield screensaver: stars flow steadily outward from center,
// growing slightly larger and brighter as they approach the edges.
// Stars stay round — no streaking. Smooth, continuous flow with no bursting.
//
// Demonstrates:
//   - Canvas node with onDraw and onFrame callbacks
//   - Struct arrays for zero-allocation per-frame star state
//   - Depth-based perspective projection for natural star motion

using Cascade.UI;
using static Cascade.UI.CanvasFactory;

#pragma warning disable CA5394 // Random is not insecure for visual effects

namespace Starfield;

internal sealed class StarfieldCanvas : Component
{
    internal sealed record Config
    {
        public int   StarCount { get; init; } = 800;
        public float Speed     { get; init; } = 150f;
        public float MaxDepth  { get; init; } = 600f;

        public static Config Default  => new();
        public static Config Gentle   => new() { Speed = 80f };
        public static Config Dramatic => new() { Speed = 280f, StarCount = 1200 };
    }

    // Each star lives in 3D: X,Y are random screen-space offsets from center,
    // Z is depth (MaxDepth = far away, 0 = at the viewer).
    private struct Star
    {
        public float X;
        public float Y;
        public float Z;
        public float Brightness;
    }

    private static readonly ColorValue White = new("#FFFFFF");
    private static readonly ColorValue Black = new("#000000");

    private readonly Config config;
    private Star[]          stars = [];
    private Size            canvasSize;

    public StarfieldCanvas() : this(null) { }

    public StarfieldCanvas(Config? config)
    {
        this.config = config ?? Config.Default;
    }

    protected override Task OnMounted()
    {
        stars = new Star[config.StarCount];
        var rng = new Random();

        for (int i = 0; i < stars.Length; i++)
        {
            ResetStar(ref stars[i], rng, randomizeDepth: true);
        }

        return Task.CompletedTask;
    }

    private void ResetStar(ref Star star, Random rng, bool randomizeDepth)
    {
        // Small X,Y spread — perspective projection will fan them out naturally
        star.X          = ((float)rng.NextDouble() * 2f - 1f) * 150f;
        star.Y          = ((float)rng.NextDouble() * 2f - 1f) * 150f;
        star.Z          = randomizeDepth ? (float)(rng.NextDouble() * 0.95 + 0.05) * config.MaxDepth : config.MaxDepth;
        star.Brightness = 0.6f + (float)rng.NextDouble() * 0.4f;
    }

    protected override Node Render() =>
        Canvas(
            size:    Size.Fill,
            onDraw:  DrawStars,
            onFrame: UpdateStars
        )
        .Background(Black)
        .ClipToBounds(true);

    private void UpdateStars(float dt)
    {
        if (canvasSize.Width < 1f || canvasSize.Height < 1f)
        {
            return;
        }

        float speed = config.Speed;
        var   rng   = Random.Shared;

        for (int i = 0; i < stars.Length; i++)
        {
            // Move star toward viewer at constant speed
            stars[i].Z -= speed * dt;

            // When star passes the viewer, recycle it at the far end
            if (stars[i].Z <= 1f)
            {
                ResetStar(ref stars[i], rng, randomizeDepth: false);
            }
        }
    }

    private void DrawStars(DrawContext ctx, Size size)
    {
        canvasSize = size;
        float cx = size.Width * 0.5f;
        float cy = size.Height * 0.5f;
        // Focal length controls how quickly stars spread from center
        float focalLength = MathF.Min(size.Width, size.Height) * 0.8f;

        for (int i = 0; i < stars.Length; i++)
        {
            ref readonly var star = ref stars[i];

            if (star.Z <= 1f)
            {
                continue;
            }

            // Classic perspective: screen_pos = focal_length * world_pos / depth
            float invZ = focalLength / star.Z;
            float sx = cx + star.X * invZ;
            float sy = cy + star.Y * invZ;

            // Skip if off-screen
            if (sx < -3f || sx > size.Width + 3f || sy < -3f || sy > size.Height + 3f)
            {
                continue;
            }

            // Depth ratio: 0 = far, 1 = close
            float depthRatio = 1f - star.Z / config.MaxDepth;
            float dr2 = depthRatio * depthRatio;

            // Size: tiny pinpoint far away, slightly larger up close
            float dotRadius = 0.3f + dr2 * 2.0f;

            // Brightness: very dim far away, bright up close
            float alpha = (0.12f + dr2 * 0.88f) * star.Brightness;

            ctx.DrawCircle(new Point(sx, sy), radius: dotRadius, fill: White.Opacity(alpha));
        }
    }
}

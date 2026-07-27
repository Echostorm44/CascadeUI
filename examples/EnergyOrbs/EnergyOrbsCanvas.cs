// Golden Example 05 — Energy Orbs Canvas Animation
//
// 3D particle explosion: a white-hot sphere detonates, spraying thousands of
// sub-pixel sparks outward in all directions with realistic per-particle physics.
// Each particle has its own drag, gravity sensitivity, and speed — creating the
// chaotic, organic spread of a real firework. Particles are projected from 3D
// to 2D with perspective so the explosion reads as a true sphere in space.
//
// Demonstrates:
//   - 3D physics projected to 2D with perspective depth
//   - Per-particle drag and gravity for organic, non-uniform motion
//   - Sub-pixel particle rendering at GPU speed
//   - Deterministic noise for reproducible chaos without random state
//   - DrawBlurredRoundedRect for glowing sparks
//   - Layered center glow that fades with the explosion

using Cascade.UI;
using static Cascade.UI.CanvasFactory;
namespace EnergyOrbs;

internal sealed class EnergyOrbsCanvas : Component
{
    internal sealed record Config
    {
        public int   ParticleCount  { get; init; } = 12000;
        public float ExplosionTime  { get; init; } = 3.5f;
        public float PauseTime      { get; init; } = 0.3f;
        public float MinSpeed       { get; init; } = 100f;
        public float MaxSpeed       { get; init; } = 900f;
        public float MinDrag        { get; init; } = 0.6f;
        public float MaxDrag        { get; init; } = 3.5f;
        public float MinGravity     { get; init; } = 15f;
        public float MaxGravity     { get; init; } = 80f;
        public float FocalLength    { get; init; } = 600f;
        public float MinDotSize     { get; init; } = 0.4f;
        public float MaxDotSize     { get; init; } = 0.9f;
        public float LaunchStagger  { get; init; } = 0.06f;

        public static Config Default => new();
    }

    private struct Particle
    {
        public float VelX, VelY, VelZ;
        public float Drag;
        public float Gravity;
        public float Size;
        public float LaunchDelay;
    }

    private static readonly ColorValue Black   = new("#000000");
    private static readonly ColorValue White   = new("#FFFFFF");
    private static readonly ColorValue HotPink = new("#FF99DD");
    private static readonly ColorValue Magenta = new("#FF44BB");
    private static readonly ColorValue Blue    = new("#88BBFF");
    private static readonly ColorValue DeepB   = new("#5588EE");

    private readonly Config config;
    private Particle[]      particles = [];
    private float           elapsed;
    private Size            canvasSize;

    public EnergyOrbsCanvas() : this(null) { }

    public EnergyOrbsCanvas(Config? config)
    {
        this.config = config ?? Config.Default;
    }

    protected override Task OnMounted()
    {
        particles = new Particle[config.ParticleCount];

        for (int i = 0; i < particles.Length; i++)
        {
            float seed = i * 1.618033988f;

            // Random direction on unit sphere using deterministic noise
            // Spherical coordinates: theta (azimuth), phi (inclination)
            float theta = Noise(seed + 100f) * MathF.Tau;
            float cosPhi = 1f - 2f * Noise(seed + 200f);
            float sinPhi = MathF.Sqrt(MathF.Max(0f, 1f - cosPhi * cosPhi));

            float dirX = sinPhi * MathF.Cos(theta);
            float dirY = sinPhi * MathF.Sin(theta);
            float dirZ = cosPhi;

            // Speed: cubed distribution — lots of slow particles, few fast ones
            float speedNoise = Noise(seed + 300f);
            float speedT = speedNoise * speedNoise;
            float speed = config.MinSpeed + speedT * (config.MaxSpeed - config.MinSpeed);

            // Per-particle drag: fast particles get less drag (they punch through)
            float dragNoise = Noise(seed + 400f);
            float drag = config.MinDrag + (1f - speedT * 0.6f) * dragNoise
                         * (config.MaxDrag - config.MinDrag);

            // Per-particle gravity: heavier particles fall more
            float gravNoise = Noise(seed + 500f);
            float gravity = config.MinGravity + gravNoise * (config.MaxGravity - config.MinGravity);

            // Size: faster particles are smaller (they're more energetic fragments)
            float sizeNoise = Noise(seed + 600f);
            float size = config.MinDotSize + sizeNoise
                         * (config.MaxDotSize - config.MinDotSize);

            float launchDelay = Noise(seed + 700f) * config.LaunchStagger;

            particles[i] = new Particle
            {
                VelX        = dirX * speed,
                VelY        = dirY * speed,
                VelZ        = dirZ * speed,
                Drag        = drag,
                Gravity     = gravity,
                Size        = size,
                LaunchDelay = launchDelay
            };
        }

        return Task.CompletedTask;
    }

    protected override Node Render() =>
        Canvas(
            size:    Size.Fill,
            onDraw:  Draw,
            onFrame: Update
        )
        .Background(Black)
        .ClipToBounds(true);

    private void Update(float dt)
    {
        if (canvasSize.Width < 1f || canvasSize.Height < 1f)
        {
            return;
        }

        elapsed += dt;
    }

    private void Draw(DrawContext ctx, Size size)
    {
        canvasSize = size;
        var center = new Point(size.Width * 0.5f, size.Height * 0.5f);

        float totalCycle = config.ExplosionTime + config.PauseTime;
        float cycleTime = elapsed % totalCycle;
        float t = cycleTime;

        if (t > config.ExplosionTime)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            DrawParticle(ctx, center, ref particles[i], t);
        }

        // Center glow — white-hot at detonation, fades quickly
        float glowFade = MathF.Max(0f, 1f - t / (config.ExplosionTime * 0.3f));
        glowFade *= glowFade * glowFade;
        if (glowFade > 0.01f)
        {
            DrawCenterGlow(ctx, center, glowFade);
        }
    }

    private static void DrawCenterGlow(DrawContext ctx, Point center, float intensity)
    {
        // Outer magenta halo
        float magR = 50f * intensity;
        if (magR > 2f)
        {
            ctx.DrawBlurredRoundedRect(
                new Rect(center.X - magR, center.Y - magR, magR * 2f, magR * 2f),
                Magenta.Opacity(0.12f * intensity),
                radius: magR, blurSigma: magR * 0.5f);
        }

        // Hot pink mid glow
        float hotR = 30f * intensity;
        if (hotR > 2f)
        {
            ctx.DrawBlurredRoundedRect(
                new Rect(center.X - hotR, center.Y - hotR, hotR * 2f, hotR * 2f),
                HotPink.Opacity(0.25f * intensity),
                radius: hotR, blurSigma: hotR * 0.4f);
        }

        // White-hot core
        float coreR = 12f * intensity;
        if (coreR > 1f)
        {
            ctx.DrawBlurredRoundedRect(
                new Rect(center.X - coreR, center.Y - coreR, coreR * 2f, coreR * 2f),
                White.Opacity(0.8f * intensity),
                radius: coreR, blurSigma: coreR * 0.25f);
        }

        // Blazing pinpoint
        float pinR = 4f * intensity;
        if (pinR > 0.5f)
        {
            ctx.DrawBlurredRoundedRect(
                new Rect(center.X - pinR, center.Y - pinR, pinR * 2f, pinR * 2f),
                White.Opacity(intensity),
                radius: pinR, blurSigma: pinR * 0.15f);
        }
    }

    private void DrawParticle(DrawContext ctx, Point center, ref Particle p, float time)
    {
        float t = time - p.LaunchDelay;
        if (t < 0f)
        {
            return;
        }

        // Per-particle physics with individual drag
        float drag = p.Drag;
        float expDrag = MathF.Exp(-drag * t);
        float posFactor = (1f - expDrag) / drag;

        // Gravity with drag applied — creates arcs, not "stop and drop"
        // Integral of g*(1-e^(-d*t))/d gives: g/d * (t - (1-e^(-d*t))/d)
        float gravPosFactor = (t - posFactor) / drag;

        // 3D position
        float x3d = p.VelX * posFactor;
        float y3d = p.VelY * posFactor + p.Gravity * gravPosFactor;
        float z3d = p.VelZ * posFactor;

        // Perspective projection
        float depth = 1f / (1f + z3d / config.FocalLength);

        // Clamp depth: don't let far-away particles vanish, don't let close ones explode
        if (depth < 0.1f)
        {
            return;
        }
        if (depth > 3f)
        {
            depth = 3f;
        }

        float px = center.X + x3d * depth;
        float py = center.Y + y3d * depth;

        // Cull offscreen
        if (px < -10f || px > canvasSize.Width + 10f ||
            py < -10f || py > canvasSize.Height + 10f)
        {
            return;
        }

        // Behind camera — don't draw
        if (depth < 0.05f)
        {
            return;
        }

        // Fade curve: quick flash-in, hold, then fade out
        float lifeFrac = t / config.ExplosionTime;
        float fade;
        if (lifeFrac < 0.01f)
        {
            fade = lifeFrac / 0.01f;
        }
        else if (lifeFrac > 0.7f)
        {
            fade = MathF.Max(0f, (1f - lifeFrac) / 0.3f);
        }
        else
        {
            fade = 1f;
        }

        // Depth affects brightness: closer = brighter, further = dimmer
        float depthBrightness = MathF.Min(depth * 1.2f, 1.3f);
        float alpha = fade * depthBrightness;
        if (alpha < 0.03f)
        {
            return;
        }

        // Color based on distance from center: white-hot core → pink → magenta → blue edge
        float dx = px - center.X;
        float dy = py - center.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float maxDist = MathF.Max(canvasSize.Width, canvasSize.Height) * 0.5f;
        float dr = MathF.Min(dist / maxDist, 1f);

        ColorValue color;
        if (dr < 0.06f)
        {
            color = ColorValue.Lerp(White, HotPink, dr / 0.06f);
        }
        else if (dr < 0.18f)
        {
            color = ColorValue.Lerp(HotPink, Magenta, (dr - 0.06f) / 0.12f);
        }
        else if (dr < 0.45f)
        {
            color = ColorValue.Lerp(Magenta, Blue, (dr - 0.18f) / 0.27f);
        }
        else
        {
            float tt = MathF.Min((dr - 0.45f) / 0.55f, 1f);
            color = ColorValue.Lerp(Blue, DeepB, tt);
        }

        // Dot size scaled by depth (perspective), clamped to stay tiny
        float dotSize = p.Size * depth;
        if (dotSize < 0.4f)
        {
            dotSize = 0.4f;
        }
        if (dotSize > 1.0f)
        {
            dotSize = 1.0f;
        }

        // Boost alpha aggressively for sub-pixel particles
        float sizeBoost = 1f + MathF.Max(0f, 1f - dotSize) * 1.5f;
        float finalAlpha = MathF.Min(alpha * sizeBoost, 1f);

        // Minimal blur for maximum sharpness at tiny sizes
        ctx.DrawBlurredRoundedRect(
            new Rect(px - dotSize, py - dotSize, dotSize * 2f, dotSize * 2f),
            color.Opacity(finalAlpha),
            radius: dotSize, blurSigma: dotSize * 0.15f);
    }

    private static float Noise(float seed)
    {
        float v = MathF.Sin(seed * 127.1f + 311.7f) * 43758.5453f;
        return v - MathF.Floor(v);
    }
}

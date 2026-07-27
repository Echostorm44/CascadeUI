#pragma warning disable CA5394 // Particle effects use non-cryptographic randomness intentionally for performance

namespace Cascade.UI;

/// <summary>
/// Particle system engine. Manages a pool of particles with physics simulation
/// including gravity, drag, turbulence, and lifetime expiration.
/// </summary>
internal sealed class ParticleEngine
{
    private const int MaxParticles = 2000;

    private readonly ParticleState[] particles;
    private int activeCount;
    private readonly Random rng = new();

    internal ParticleEngine()
    {
        particles = new ParticleState[MaxParticles];
    }

    /// <summary>Number of currently alive particles.</summary>
    internal int ActiveCount => activeCount;

    /// <summary>True if any particles are alive.</summary>
    internal bool HasActiveParticles => activeCount > 0;

    /// <summary>
    /// Emits particles using the given configuration.
    /// </summary>
    internal void Emit(int count, EmitterConfig config)
    {
        for (int i = 0; i < count; i++)
        {
            if (activeCount >= MaxParticles)
            {
                break;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                break;
            }

            ref var p = ref particles[slot];
            p.IsAlive = true;
            p.Age = 0f;
            p.Lifetime = config.Lifetime + RandomRange(-config.LifetimeVariance, config.LifetimeVariance);
            p.PositionX = config.OriginX + RandomRange(-config.SpawnRadius, config.SpawnRadius);
            p.PositionY = config.OriginY + RandomRange(-config.SpawnRadius, config.SpawnRadius);

            float angle = config.DirectionAngle + RandomRange(-config.SpreadAngle * 0.5f, config.SpreadAngle * 0.5f);
            float speed = config.Speed + RandomRange(-config.SpeedVariance, config.SpeedVariance);
            p.VelocityX = MathF.Cos(angle) * speed;
            p.VelocityY = MathF.Sin(angle) * speed;

            p.Scale = config.InitialScale + RandomRange(-config.ScaleVariance, config.ScaleVariance);
            p.Opacity = config.InitialOpacity;
            p.Rotation = RandomRange(0f, config.InitialRotationRange);
            p.RotationVelocity = config.TumbleSpeed > 0f
                ? RandomRange(-config.TumbleSpeed, config.TumbleSpeed)
                : 0f;
            p.ColorIndex = count > 0 ? rng.Next(0, config.ColorCount) : 0;

            activeCount++;
        }
    }

    /// <summary>
    /// Advances the simulation by the given time delta.
    /// </summary>
    internal void Advance(float deltaTime, PhysicsConfig physics)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ref var p = ref particles[i];
            if (!p.IsAlive)
            {
                continue;
            }

            p.Age += deltaTime;
            if (p.Age >= p.Lifetime)
            {
                p.IsAlive = false;
                activeCount--;
                continue;
            }

            // Gravity
            p.VelocityY += physics.Gravity * deltaTime;

            // Drag
            if (physics.Drag > 0f)
            {
                float dragFactor = 1f - physics.Drag * deltaTime;
                dragFactor = MathF.Max(dragFactor, 0f);
                p.VelocityX *= dragFactor;
                p.VelocityY *= dragFactor;
            }

            // Turbulence (simplified noise)
            if (physics.Turbulence > 0f)
            {
                float turbX = SimplexNoise(p.PositionX * 0.01f + p.Age) * physics.Turbulence * deltaTime;
                float turbY = SimplexNoise(p.PositionY * 0.01f + p.Age + 100f) * physics.Turbulence * deltaTime;
                p.VelocityX += turbX;
                p.VelocityY += turbY;
            }

            // Integration
            p.PositionX += p.VelocityX * deltaTime;
            p.PositionY += p.VelocityY * deltaTime;

            // Rotation
            p.Rotation += p.RotationVelocity * deltaTime;

            // Fade and scale over lifetime
            float lifeFraction = p.Age / p.Lifetime;
            if (physics.FadeOut && lifeFraction > physics.FadeStart)
            {
                float fadeProg = (lifeFraction - physics.FadeStart) / (1f - physics.FadeStart);
                p.Opacity = (1f - fadeProg) * physics.InitialOpacity;
            }

            if (physics.ShrinkOut && lifeFraction > physics.ShrinkStart)
            {
                float shrinkProg = (lifeFraction - physics.ShrinkStart) / (1f - physics.ShrinkStart);
                p.Scale = (1f - shrinkProg) * physics.InitialScale;
            }
        }
    }

    /// <summary>
    /// Reads the state of all active particles into the provided callback.
    /// Used for rendering without allocating a snapshot array.
    /// </summary>
    internal void ForEachAlive(Action<ParticleSnapshot> visitor)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            ref var p = ref particles[i];
            if (!p.IsAlive)
            {
                continue;
            }

            visitor(new ParticleSnapshot(
                p.PositionX, p.PositionY,
                p.VelocityX, p.VelocityY,
                p.Scale, p.Opacity, p.Rotation,
                p.ColorIndex, p.Age / p.Lifetime));
        }
    }

    /// <summary>
    /// Removes all particles.
    /// </summary>
    internal void Clear()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].IsAlive = false;
        }
        activeCount = 0;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].IsAlive)
            {
                return i;
            }
        }
        return -1;
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>
    /// Simple hash-based pseudo-noise function (faster than full simplex).
    /// </summary>
    private static float SimplexNoise(float x)
    {
        int xi = (int)MathF.Floor(x);
        float frac = x - xi;
        float smooth = frac * frac * (3f - 2f * frac);
        float h0 = HashFloat(xi);
        float h1 = HashFloat(xi + 1);
        return h0 + (h1 - h0) * smooth;
    }

    private static float HashFloat(int n)
    {
        n = (n << 13) ^ n;
        n = n * (n * n * 15731 + 789221) + 1376312589;
        return 1f - (n & 0x7fffffff) / 1073741824f;
    }

    internal struct ParticleState
    {
        internal bool IsAlive;
        internal float Age;
        internal float Lifetime;
        internal float PositionX;
        internal float PositionY;
        internal float VelocityX;
        internal float VelocityY;
        internal float Scale;
        internal float Opacity;
        internal float Rotation;
        internal float RotationVelocity;
        internal int ColorIndex;
    }

    internal readonly record struct ParticleSnapshot(
        float X, float Y,
        float VelocityX, float VelocityY,
        float Scale, float Opacity, float Rotation,
        int ColorIndex, float LifeFraction);

    internal sealed class EmitterConfig
    {
        internal float OriginX { get; init; }
        internal float OriginY { get; init; }
        internal float SpawnRadius { get; init; }
        internal float Speed { get; init; }
        internal float SpeedVariance { get; init; }
        internal float DirectionAngle { get; init; }
        internal float SpreadAngle { get; init; } = MathF.PI * 2f;
        internal float Lifetime { get; init; } = 2f;
        internal float LifetimeVariance { get; init; }
        internal float InitialScale { get; init; } = 1f;
        internal float ScaleVariance { get; init; }
        internal float InitialOpacity { get; init; } = 1f;
        internal float InitialRotationRange { get; init; }
        internal float TumbleSpeed { get; init; }
        internal int ColorCount { get; init; } = 1;
    }

    internal sealed class PhysicsConfig
    {
        internal float Gravity { get; init; }
        internal float Drag { get; init; }
        internal float Turbulence { get; init; }
        internal bool FadeOut { get; init; } = true;
        internal float FadeStart { get; init; } = 0.6f;
        internal float InitialOpacity { get; init; } = 1f;
        internal bool ShrinkOut { get; init; }
        internal float ShrinkStart { get; init; } = 0.8f;
        internal float InitialScale { get; init; } = 1f;
    }
}

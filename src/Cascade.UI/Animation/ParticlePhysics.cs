namespace Cascade.UI;

/// <summary>
/// Defines the physics simulation for particles: gravity, velocity,
/// rotation, lifetime, and other physical properties.
/// </summary>
public sealed class ParticlePhysics
{
    private ParticlePhysics()
    {
    }

    internal float GravityValue { get; private set; }
    internal float SpeedValue { get; private set; }
    internal float SpeedVarianceValue { get; private set; }
    internal float SpreadAngleValue { get; private set; } = MathF.PI * 2f;
    internal float DirectionAngleValue { get; private set; }
    internal float TumbleSpeedValue { get; private set; }
    internal float DragValue { get; private set; }
    internal float TurbulenceValue { get; private set; }
    internal float LifetimeValue { get; private set; } = 2f;
    internal bool FadeOutEnabled { get; private set; } = true;
    internal bool ShrinkEnabled { get; private set; }

    /// <summary>
    /// Confetti preset: particles fall with gravity, spread outward, and tumble.
    /// </summary>
    public static ParticlePhysics Confetti(float gravity = 400f, float spread = 200f, bool tumble = true)
    {
        return new ParticlePhysics
        {
            GravityValue = gravity,
            SpeedValue = spread,
            SpeedVarianceValue = spread * 0.5f,
            SpreadAngleValue = MathF.PI * 2f,
            DirectionAngleValue = -MathF.PI / 2f,
            TumbleSpeedValue = tumble ? 6f : 0f,
            DragValue = 0.5f,
            TurbulenceValue = 30f,
            LifetimeValue = 3f,
            FadeOutEnabled = true,
            ShrinkEnabled = false,
        };
    }

    /// <summary>
    /// Explosion preset: particles shoot outward from a point.
    /// </summary>
    public static ParticlePhysics Explosion(float speed = 300f, float gravity = 200f)
    {
        return new ParticlePhysics
        {
            GravityValue = gravity,
            SpeedValue = speed,
            SpeedVarianceValue = speed * 0.3f,
            SpreadAngleValue = MathF.PI * 2f,
            DirectionAngleValue = 0f,
            TumbleSpeedValue = 0f,
            DragValue = 1f,
            LifetimeValue = 1.5f,
            FadeOutEnabled = true,
            ShrinkEnabled = true,
        };
    }

    /// <summary>
    /// Drift preset: particles drift upward and fade.
    /// </summary>
    public static ParticlePhysics Drift(float speed = 30f, Duration? lifetime = null)
    {
        float life = lifetime.HasValue
            ? (float)(lifetime.Value.TotalMilliseconds / 1000.0)
            : 5f;

        return new ParticlePhysics
        {
            GravityValue = -20f,
            SpeedValue = speed,
            SpeedVarianceValue = speed * 0.5f,
            SpreadAngleValue = MathF.PI / 3f,
            DirectionAngleValue = -MathF.PI / 2f,
            TumbleSpeedValue = 0.5f,
            DragValue = 0.2f,
            TurbulenceValue = 10f,
            LifetimeValue = life,
            FadeOutEnabled = true,
            ShrinkEnabled = false,
        };
    }

    /// <summary>
    /// Rain preset: particles fall at an angle.
    /// </summary>
    public static ParticlePhysics Rain(Angle angle, float speed = 400f)
    {
        return new ParticlePhysics
        {
            GravityValue = 0f,
            SpeedValue = speed,
            SpeedVarianceValue = speed * 0.1f,
            SpreadAngleValue = MathF.PI / 12f,
            DirectionAngleValue = angle.InRadians,
            TumbleSpeedValue = 0f,
            DragValue = 0f,
            LifetimeValue = 3f,
            FadeOutEnabled = true,
            ShrinkEnabled = false,
        };
    }

    internal ParticleEngine.PhysicsConfig ToPhysicsConfig()
    {
        return new ParticleEngine.PhysicsConfig
        {
            Gravity = GravityValue,
            Drag = DragValue,
            Turbulence = TurbulenceValue,
            FadeOut = FadeOutEnabled,
            FadeStart = 0.6f,
            InitialOpacity = 1f,
            ShrinkOut = ShrinkEnabled,
            ShrinkStart = 0.8f,
            InitialScale = 1f,
        };
    }

    internal ParticleEngine.EmitterConfig ToEmitterConfig(int colorCount, Point? origin)
    {
        float originX = origin?.X ?? 0f;
        float originY = origin?.Y ?? 0f;

        return new ParticleEngine.EmitterConfig
        {
            OriginX = originX,
            OriginY = originY,
            Speed = SpeedValue,
            SpeedVariance = SpeedVarianceValue,
            DirectionAngle = DirectionAngleValue,
            SpreadAngle = SpreadAngleValue,
            Lifetime = LifetimeValue,
            LifetimeVariance = LifetimeValue * 0.2f,
            InitialScale = 1f,
            InitialOpacity = 1f,
            InitialRotationRange = TumbleSpeedValue > 0f ? MathF.PI * 2f : 0f,
            TumbleSpeed = TumbleSpeedValue,
            ColorCount = colorCount,
        };
    }
}

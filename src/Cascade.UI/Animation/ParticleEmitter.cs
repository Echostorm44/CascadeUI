namespace Cascade.UI;

/// <summary>
/// Defines how particles are spawned in a particle effect.
/// </summary>
public sealed class ParticleEmitter
{
    private ParticleEmitter()
    {
    }

    internal EmissionPattern Pattern { get; private set; }
    internal int EmitCount { get; private set; }
    internal int EmitRate { get; private set; }
    internal Duration PulseInterval { get; private set; }
    internal Point? EmitOrigin { get; private set; }

    /// <summary>
    /// Emits all particles at once in a single burst.
    /// </summary>
    /// <param name="count">Number of particles to emit.</param>
    public static ParticleEmitter Burst(int count)
    {
        return new ParticleEmitter
        {
            Pattern = EmissionPattern.Burst,
            EmitCount = count,
        };
    }

    /// <summary>
    /// Emits particles continuously at the given rate.
    /// </summary>
    /// <param name="rate">Particles per second.</param>
    public static ParticleEmitter Continuous(int rate)
    {
        return new ParticleEmitter
        {
            Pattern = EmissionPattern.Continuous,
            EmitRate = rate,
        };
    }

    /// <summary>
    /// Emits bursts of particles on a repeating interval.
    /// </summary>
    /// <param name="count">Particles per burst.</param>
    /// <param name="every">Time between bursts.</param>
    public static ParticleEmitter Pulse(int count, Duration every)
    {
        return new ParticleEmitter
        {
            Pattern = EmissionPattern.Pulse,
            EmitCount = count,
            PulseInterval = every,
        };
    }

    /// <summary>
    /// Sets the world-space origin point of the emitter. Particles spawn
    /// from this position regardless of the Particles node's layout position.
    /// </summary>
    public ParticleEmitter Origin(Point screenPoint)
    {
        EmitOrigin = screenPoint;
        return this;
    }

    internal enum EmissionPattern
    {
        Burst,
        Continuous,
        Pulse,
    }
}

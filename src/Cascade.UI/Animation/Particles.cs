namespace Cascade.UI;

/// <summary>
/// Factory for creating particle effect nodes. GPU-accelerated particle
/// systems for celebrations, ambient effects, and visual feedback.
/// </summary>
public static class ParticleFactory
{
    /// <summary>
    /// Creates a particle effect node.
    /// </summary>
    /// <param name="emitter">How particles are spawned (burst, continuous, pulse).</param>
    /// <param name="shape">The visual shape of each particle.</param>
    /// <param name="colors">Color palette — each particle is randomly assigned one.</param>
    /// <param name="physics">Physics simulation (gravity, velocity, rotation, etc.).</param>
    public static Node Particles(
        ParticleEmitter emitter,
        ParticleShape shape,
        ColorValue[] colors,
        ParticlePhysics physics)
    {
        return new ParticleNode(emitter, shape, colors, physics);
    }

    internal sealed class ParticleNode : Node
    {
        internal ParticleNode(
            ParticleEmitter emitter,
            ParticleShape shape,
            ColorValue[] colors,
            ParticlePhysics physics)
        {
            Emitter = emitter;
            Shape = shape;
            Colors = colors;
            Physics = physics;
            Engine = new ParticleEngine();
            PhysicsConfig = physics.ToPhysicsConfig();
            EmitterConfig = physics.ToEmitterConfig(colors.Length, emitter.EmitOrigin);
        }

        internal ParticleEmitter Emitter { get; }
        internal ParticleShape Shape { get; }
        internal ColorValue[] Colors { get; }
        internal ParticlePhysics Physics { get; }
        internal ParticleEngine Engine { get; }
        internal ParticleEngine.PhysicsConfig PhysicsConfig { get; }
        internal ParticleEngine.EmitterConfig EmitterConfig { get; }
    }
}

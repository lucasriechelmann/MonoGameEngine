using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGameEngine.Engine.Physics;

namespace MonoGameEngine.Engine;

/// <summary>How a <see cref="Rigidbody2D"/> takes part in the simulation.</summary>
public enum RigidbodyType2D
{
    /// <summary>Never moves. Colliders without a rigidbody are static too.</summary>
    Static,

    /// <summary>Moved by gravity, forces and collisions.</summary>
    Dynamic,

    /// <summary>Moved only by its velocity. Pushes dynamic bodies but is not pushed back.</summary>
    Kinematic,
}

/// <summary>
/// Makes the entity part of the physics simulation. Add a <see cref="BoxCollider2D"/> or
/// <see cref="CircleCollider2D"/> to give it a shape. The simulation runs while the scene runs, and moves the
/// entity's <see cref="Transform"/>. Settings are read when the scene starts.
/// </summary>
public sealed class Rigidbody2D : Component
{
    /// <summary>Static, dynamic or kinematic.</summary>
    [Tooltip("Dynamic bodies fall and collide. Static bodies never move. Kinematic bodies move only by velocity.")]
    public RigidbodyType2D BodyType { get; set; } = RigidbodyType2D.Dynamic;

    /// <summary>Multiplies the scene gravity for this body. 0 floats, 1 is normal.</summary>
    [Range(-5, 5), Tooltip("Multiplies the scene gravity for this body. 0 floats, 1 is normal.")]
    public float GravityScale { get; set; } = 1f;

    /// <summary>Slows linear movement, like air resistance.</summary>
    [Range(0, 10), Tooltip("Slows movement, like air resistance.")]
    public float LinearDamping { get; set; }

    /// <summary>Slows rotation.</summary>
    [Range(0, 10), Tooltip("Slows rotation.")]
    public float AngularDamping { get; set; } = 0.05f;

    /// <summary>Prevents the body from rotating.</summary>
    [Tooltip("Prevents the body from rotating.")]
    public bool FixedRotation { get; set; }

    /// <summary>Velocity in pixels per second while the scene runs.</summary>
    [JsonIgnore]
    public Vector2 LinearVelocity
    {
        get => Body is { } body ? PhysicsWorld2D.ToPixels(body.LinearVelocity) : Vector2.Zero;
        set { if (Body is { } body) body.LinearVelocity = PhysicsWorld2D.ToMeters(value); }
    }

    /// <summary>Rotation speed in radians per second while the scene runs.</summary>
    [JsonIgnore]
    public float AngularVelocity
    {
        get => Body?.AngularVelocity ?? 0f;
        set { if (Body is { } body) body.AngularVelocity = value; }
    }

    /// <summary>Pushes the body continuously. <paramref name="force"/> is in pixels per second squared times mass.</summary>
    public void AddForce(Vector2 force) => Body?.ApplyForce(PhysicsWorld2D.ToMeters(force));

    /// <summary>Gives the body an instant push.</summary>
    public void AddImpulse(Vector2 impulse) => Body?.ApplyLinearImpulse(PhysicsWorld2D.ToMeters(impulse));

    /// <summary>Moves the body to a world position immediately, keeping its velocity.</summary>
    public void Teleport(Vector2 worldPosition)
    {
        if (Body is not { } body) return;
        body.SetTransform(PhysicsWorld2D.ToMeters(worldPosition), body.Rotation);
        Entity.Transform.WorldPosition = worldPosition;
    }

    private nkast.Aether.Physics2D.Dynamics.Body? Body => Scene?.Physics?.Find(Entity);

    /// <inheritdoc />
    protected internal override void OnAwake() => Scene?.GetOrCreatePhysics().Acquire(Entity);

    /// <inheritdoc />
    protected internal override void OnDestroy() => Scene?.Physics?.Release(Entity);
}

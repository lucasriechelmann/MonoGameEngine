using Microsoft.Xna.Framework;
using MonoGameEngine.Engine.Physics;
using nkast.Aether.Physics2D.Dynamics;

namespace MonoGameEngine.Engine;

/// <summary>
/// Base class for collision shapes. An entity with a collider and no <see cref="Rigidbody2D"/> is a static
/// obstacle. Shapes are scaled by the entity's world scale. Settings are read when the scene starts.
/// </summary>
public abstract class Collider2D : Component
{
    private Fixture? _fixture;

    /// <summary>Shape centre relative to the entity, in pixels (before scale).</summary>
    [Tooltip("Shape centre relative to the entity, in pixels.")]
    public Vector2 Offset { get; set; }

    /// <summary>A trigger reports overlaps with <c>OnTriggerEnter</c> but does not block movement.</summary>
    [Tooltip("A trigger reports overlaps but does not block movement.")]
    public bool IsTrigger { get; set; }

    /// <summary>Mass per area. Heavier shapes push lighter ones.</summary>
    [Range(0, 20), Tooltip("Mass per area. Heavier shapes push lighter ones.")]
    public float Density { get; set; } = 1f;

    /// <summary>0 is slippery, 1 is grippy.</summary>
    [Range(0, 1), Tooltip("0 is slippery, 1 is grippy.")]
    public float Friction { get; set; } = 0.3f;

    /// <summary>0 does not bounce, 1 bounces back with full speed.</summary>
    [Range(0, 1), Tooltip("0 does not bounce, 1 bounces back with full speed.")]
    public float Bounciness { get; set; }

    /// <summary>The size of the sprite on this entity (one frame of an animated sheet) in pixels, or zero.</summary>
    protected Vector2 SpriteSize()
    {
        var sprite = Entity.GetComponent<SpriteRenderer>();
        var texture = sprite?.GetTexture();
        if (sprite == null || texture == null) return Vector2.Zero;
        var frame = sprite.GetSourceRectangle(texture);
        return new Vector2(frame.Width, frame.Height);
    }

    /// <summary>Creates the Aether fixture for this shape on <paramref name="body"/>.</summary>
    internal abstract Fixture CreateFixture(Body body);

    /// <summary>Applies the shared settings to a new fixture and wires up collision events.</summary>
    internal Fixture Configure(Fixture fixture)
    {
        fixture.Friction = Friction;
        fixture.Restitution = Bounciness;
        fixture.IsSensor = IsTrigger;
        fixture.Tag = this;
        fixture.OnCollision += (self, other, _) =>
        {
            if (self.Tag is Collider2D mine && other.Tag is Collider2D theirs && Scene?.Physics is { } physics)
                physics.QueueContact(mine.Entity, theirs.Entity, mine.IsTrigger || theirs.IsTrigger);
            return true;
        };
        return fixture;
    }

    /// <inheritdoc />
    protected internal override void OnAwake()
    {
        var physics = Scene?.GetOrCreatePhysics();
        if (physics == null) return;
        _fixture = CreateFixture(physics.Acquire(Entity));
    }

    /// <inheritdoc />
    protected internal override void OnDestroy()
    {
        var physics = Scene?.Physics;
        if (physics == null) return;
        if (_fixture != null && physics.Find(Entity) is { } body) body.Remove(_fixture);
        _fixture = null;
        physics.Release(Entity);
    }

    /// <summary>Entity scale used for the shape (absolute values).</summary>
    protected Vector2 AbsoluteScale()
    {
        var scale = Entity.Transform.WorldScale;
        return new Vector2(MathF.Abs(scale.X), MathF.Abs(scale.Y));
    }
}

/// <summary>A rectangular collision shape. A size of zero fits the sprite on the same entity.</summary>
public sealed class BoxCollider2D : Collider2D
{
    /// <summary>Width and height in pixels before scale. Zero means "fit the sprite".</summary>
    [Tooltip("Width and height in pixels. Zero fits the sprite on this entity.")]
    public Vector2 Size { get; set; } = Vector2.Zero;

    /// <summary>The shape size in pixels, after the sprite fallback and the entity scale.</summary>
    public Vector2 GetWorldSize()
    {
        var size = Size == Vector2.Zero ? SpriteSize() : Size;
        return size * AbsoluteScale();
    }

    /// <summary>The four world-space corners (for drawing in the editor).</summary>
    public Vector2[] GetWorldCorners()
    {
        var half = GetWorldSize() * 0.5f;
        var transform = Entity.Transform;
        var centre = Offset * AbsoluteScale();
        float c = MathF.Cos(transform.WorldRotation), s = MathF.Sin(transform.WorldRotation);

        Vector2 Place(float x, float y)
        {
            var p = centre + new Vector2(x, y);
            return transform.WorldPosition + new Vector2(p.X * c - p.Y * s, p.X * s + p.Y * c);
        }
        return [Place(-half.X, -half.Y), Place(half.X, -half.Y), Place(half.X, half.Y), Place(-half.X, half.Y)];
    }

    internal override Fixture CreateFixture(Body body)
    {
        var size = GetWorldSize();
        if (size.X <= 0f || size.Y <= 0f)
        {
            Log.Warning($"BoxCollider2D on '{Entity.Name}' has no size and no sprite to fit; it will not collide.");
            size = new Vector2(1f, 1f);
        }
        var offset = Offset * AbsoluteScale();
        return Configure(body.CreateRectangle(size.X / PhysicsWorld2D.PixelsPerMeter, size.Y / PhysicsWorld2D.PixelsPerMeter,
            Density, PhysicsWorld2D.ToMeters(offset)));
    }
}

/// <summary>A circular collision shape. A radius of zero fits the sprite on the same entity.</summary>
public sealed class CircleCollider2D : Collider2D
{
    /// <summary>Radius in pixels before scale. Zero means "fit the sprite".</summary>
    [Range(0, 5000), Tooltip("Radius in pixels. Zero fits the sprite on this entity.")]
    public float Radius { get; set; }

    /// <summary>The circle radius in pixels, after the sprite fallback and the entity scale.</summary>
    public float GetWorldRadius()
    {
        float radius = Radius > 0f ? Radius : MathF.Min(SpriteSize().X, SpriteSize().Y) * 0.5f;
        var scale = AbsoluteScale();
        return radius * MathF.Max(scale.X, scale.Y);
    }

    /// <summary>The circle centre in world space (for drawing in the editor).</summary>
    public Vector2 GetWorldCenter() => Entity.Transform.TransformPoint(Offset);

    internal override Fixture CreateFixture(Body body)
    {
        float radius = GetWorldRadius();
        if (radius <= 0f)
        {
            Log.Warning($"CircleCollider2D on '{Entity.Name}' has no radius and no sprite to fit; it will not collide.");
            radius = 1f;
        }
        var offset = Offset * AbsoluteScale();
        return Configure(body.CreateCircle(radius / PhysicsWorld2D.PixelsPerMeter, Density, PhysicsWorld2D.ToMeters(offset)));
    }
}

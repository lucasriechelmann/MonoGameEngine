using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;
using AetherVector = nkast.Aether.Physics2D.Common.Vector2;

namespace MonoGameEngine.Engine.Physics;

/// <summary>
/// The physics simulation of one running scene, backed by Aether.Physics2D. It is created when the first physics
/// component wakes up and released when the scene stops. World units are pixels; the simulation works in metres
/// at <see cref="PixelsPerMeter"/> pixels per metre. Aether types never leave the Engine.
/// </summary>
internal sealed class PhysicsWorld2D
{
    /// <summary>Pixels per simulated metre.</summary>
    public const float PixelsPerMeter = 100f;

    private const float FixedStep = 1f / 60f;
    private const int MaxStepsPerFrame = 5;

    private sealed class Entry
    {
        public required Body Body;
        public required Rigidbody2D? Rigidbody;
        public int Users;
    }

    private readonly World _world;
    private readonly Dictionary<Entity, Entry> _entries = new();
    private readonly List<(Entity Self, Entity Other, bool Trigger)> _events = new();
    private float _accumulator;

    public PhysicsWorld2D(Vector2 gravityInPixels)
    {
        _world = new World(ToMeters(gravityInPixels));
    }

    /// <summary>Gravity in pixels per second squared.</summary>
    public Vector2 Gravity
    {
        get => ToPixels(_world.Gravity);
        set => _world.Gravity = ToMeters(value);
    }

    /// <summary>Returns the entity's body, creating it on first use, and counts the caller as a user of it.</summary>
    public Body Acquire(Entity entity)
    {
        if (_entries.TryGetValue(entity, out var existing))
        {
            existing.Users++;
            return existing.Body;
        }

        var rigidbody = entity.GetComponent<Rigidbody2D>();
        var transform = entity.Transform;
        var body = _world.CreateBody(ToMeters(transform.WorldPosition), transform.WorldRotation,
            rigidbody == null ? BodyType.Static : ToBodyType(rigidbody.BodyType));
        body.Tag = entity;
        if (rigidbody != null)
        {
            body.LinearDamping = rigidbody.LinearDamping;
            body.AngularDamping = rigidbody.AngularDamping;
            body.FixedRotation = rigidbody.FixedRotation;
        }

        _entries[entity] = new Entry { Body = body, Rigidbody = rigidbody, Users = 1 };
        return body;
    }

    /// <summary>The entity's body, or null if it has none.</summary>
    public Body? Find(Entity entity) => _entries.TryGetValue(entity, out var entry) ? entry.Body : null;

    /// <summary>Gives a user of the body back. The body is removed when nobody uses it any more.</summary>
    public void Release(Entity entity)
    {
        if (!_entries.TryGetValue(entity, out var entry) || --entry.Users > 0) return;
        _world.Remove(entry.Body);
        _entries.Remove(entity);
    }

    /// <summary>Queues a collision for the entity; delivered after the step.</summary>
    public void QueueContact(Entity self, Entity other, bool trigger) => _events.Add((self, other, trigger));

    /// <summary>Advances the simulation with a fixed time step and copies the result to the transforms.</summary>
    public void Step(float deltaSeconds)
    {
        SyncStaticBodiesFromTransforms();

        _accumulator += Math.Min(deltaSeconds, FixedStep * MaxStepsPerFrame);
        while (_accumulator >= FixedStep)
        {
            ApplyGravityScale();
            _world.Step(FixedStep);
            _accumulator -= FixedStep;
        }

        SyncTransformsFromBodies();
        DispatchContacts();
    }

    private void SyncStaticBodiesFromTransforms()
    {
        foreach (var (entity, entry) in _entries)
        {
            if (entry.Body.BodyType != BodyType.Static) continue;
            var transform = entity.Transform;
            var position = ToMeters(transform.WorldPosition);
            if (entry.Body.Position != position || entry.Body.Rotation != transform.WorldRotation)
                entry.Body.SetTransform(position, transform.WorldRotation);
        }
    }

    private void ApplyGravityScale()
    {
        foreach (var entry in _entries.Values)
        {
            var rigidbody = entry.Rigidbody;
            if (rigidbody == null || entry.Body.BodyType != BodyType.Dynamic || rigidbody.GravityScale == 1f) continue;
            entry.Body.ApplyForce(_world.Gravity * entry.Body.Mass * (rigidbody.GravityScale - 1f));
        }
    }

    private void SyncTransformsFromBodies()
    {
        foreach (var (entity, entry) in _entries)
        {
            if (entry.Body.BodyType == BodyType.Static) continue;
            var transform = entity.Transform;
            transform.WorldPosition = ToPixels(entry.Body.Position);
            transform.WorldRotation = entry.Body.Rotation;
        }
    }

    private void DispatchContacts()
    {
        if (_events.Count == 0) return;
        var pending = _events.ToArray(); // components may add contacts while handling these
        _events.Clear();

        foreach (var (self, other, trigger) in pending)
        {
            if (self.Scene == null) continue;
            var components = self.Components;
            for (int i = 0; i < components.Count; i++)
            {
                if (trigger) components[i].OnTriggerEnter(other);
                else components[i].OnCollisionEnter(other);
            }
        }
    }

    /// <summary>Removes every body.</summary>
    public void Clear()
    {
        foreach (var entry in _entries.Values) _world.Remove(entry.Body);
        _entries.Clear();
        _events.Clear();
    }

    public static AetherVector ToMeters(Vector2 pixels) => new(pixels.X / PixelsPerMeter, pixels.Y / PixelsPerMeter);

    public static Vector2 ToPixels(AetherVector meters) => new(meters.X * PixelsPerMeter, meters.Y * PixelsPerMeter);

    private static BodyType ToBodyType(RigidbodyType2D type) => type switch
    {
        RigidbodyType2D.Dynamic => BodyType.Dynamic,
        RigidbodyType2D.Kinematic => BodyType.Kinematic,
        _ => BodyType.Static,
    };
}

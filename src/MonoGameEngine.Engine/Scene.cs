using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGameEngine.Engine.Physics;

namespace MonoGameEngine.Engine;

/// <summary>A collection of entities arranged as a hierarchy.</summary>
public sealed class Scene
{
    private readonly List<Entity> _roots = new();
    private readonly List<Entity> _all = new();
    private readonly Dictionary<Guid, Entity> _byId = new();
    private readonly List<Entity> _pendingDestroy = new();
    private Vector2 _gravity = new(0f, 980f);

    /// <summary>Display name, stored in the scene file.</summary>
    public string Name { get; set; } = "Untitled";

    /// <summary>Loads assets for components such as <see cref="SpriteRenderer"/>. May be null in tests.</summary>
    [JsonIgnore]
    public AssetManager? Assets { get; set; }

    /// <summary>Gravity for physics in pixels per second squared. Y points down.</summary>
    [Tooltip("Gravity in pixels per second squared. Y points down.")]
    public Vector2 Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            if (Physics != null) Physics.Gravity = value;
        }
    }

    /// <summary>The running physics simulation, or null while no physics component is awake.</summary>
    internal PhysicsWorld2D? Physics { get; private set; }

    internal PhysicsWorld2D GetOrCreatePhysics() => Physics ??= new PhysicsWorld2D(_gravity);

    /// <summary>Drops the physics simulation (the runner calls this when the scene stops).</summary>
    internal void ReleasePhysics()
    {
        Physics?.Clear();
        Physics = null;
    }

    /// <summary>Entities without a parent, in order.</summary>
    public IReadOnlyList<Entity> Roots => _roots;

    /// <summary>Every entity in the scene, in no particular order. Safe to index in update loops.</summary>
    public IReadOnlyList<Entity> Entities => _all;

    internal List<Entity> RootList => _roots;

    /// <summary>Creates and attaches a new entity.</summary>
    public Entity CreateEntity(string name = "Entity", Entity? parent = null) =>
        CreateEntity(Guid.NewGuid(), name, parent);

    /// <summary>Creates and attaches a new entity with a given id (used when loading).</summary>
    public Entity CreateEntity(Guid id, string name, Entity? parent = null)
    {
        var entity = new Entity(id, name);
        if (!Add(entity, parent)) throw new InvalidOperationException("Could not attach the new entity.");
        return entity;
    }

    /// <summary>Finds an entity by id, or returns <c>null</c>.</summary>
    public Entity? Find(Guid id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Attaches a detached entity (and its subtree). <paramref name="index"/> is the final position
    /// among the new siblings; negative appends. Returns false if it is already in a scene or the parent is foreign.
    /// </summary>
    public bool Add(Entity entity, Entity? parent = null, int index = -1)
    {
        if (entity.Scene != null || (parent != null && parent.Scene != this)) return false;

        var list = parent?.ChildList ?? _roots;
        if (index < 0 || index > list.Count) list.Add(entity);
        else list.Insert(index, entity);
        entity.Parent = parent;
        Register(entity);
        entity.Transform.MarkDirty();
        return true;
    }

    /// <summary>Detaches an entity and its subtree from this scene without running destroy callbacks.</summary>
    public bool Remove(Entity entity)
    {
        if (entity.Scene != this) return false;
        (entity.Parent?.ChildList ?? _roots).Remove(entity);
        entity.Parent = null;
        Unregister(entity);
        entity.Transform.MarkDirty();
        return true;
    }

    /// <summary>Removes entities queued with <see cref="Entity.Destroy"/>, running destroy callbacks.</summary>
    public void FlushDestroyed()
    {
        if (_pendingDestroy.Count == 0) return;
        for (int i = 0; i < _pendingDestroy.Count; i++)
        {
            var entity = _pendingDestroy[i];
            if (entity.Scene != this) continue;
            entity.RunDestroyCallbacks();
            Remove(entity);
        }
        _pendingDestroy.Clear();
    }

    internal void QueueDestroy(Entity entity)
    {
        if (!_pendingDestroy.Contains(entity)) _pendingDestroy.Add(entity);
    }

    private void Register(Entity entity)
    {
        if (!_byId.TryAdd(entity.Id, entity))
            throw new InvalidOperationException($"An entity with id {entity.Id} already exists in the scene.");
        entity.Scene = this;
        _all.Add(entity);
        for (int i = 0; i < entity.ChildList.Count; i++) Register(entity.ChildList[i]);
    }

    private void Unregister(Entity entity)
    {
        entity.Scene = null;
        _byId.Remove(entity.Id);
        _all.Remove(entity);
        for (int i = 0; i < entity.ChildList.Count; i++) Unregister(entity.ChildList[i]);
    }
}

namespace MonoGameEngine.Engine;

/// <summary>A node in the scene hierarchy that holds components. Create entities with <see cref="Scene.CreateEntity"/>.</summary>
public sealed class Entity
{
    private readonly List<Entity> _children = new();
    private readonly List<Component> _components = new();

    internal Entity(Guid id, string name)
    {
        Id = id;
        Name = name;
        Transform = new Transform();
        Attach(Transform, -1);
    }

    /// <summary>Stable identity. Preserved across save and load.</summary>
    public Guid Id { get; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    /// <summary>Whether this entity (and its children) take part in update and draw.</summary>
    public bool Active { get; set; } = true;

    /// <summary>The scene this entity belongs to, or <c>null</c> when detached.</summary>
    public Scene? Scene { get; internal set; }

    /// <summary>The parent entity, or <c>null</c> for a root entity.</summary>
    public Entity? Parent { get; internal set; }

    /// <summary>Direct children, in order.</summary>
    public IReadOnlyList<Entity> Children => _children;

    /// <summary>Attached components. The <see cref="Transform"/> is always first.</summary>
    public IReadOnlyList<Component> Components => _components;

    /// <summary>The transform every entity has.</summary>
    public Transform Transform { get; }

    internal List<Entity> ChildList => _children;

    /// <summary>True when this entity and all its ancestors are active.</summary>
    public bool ActiveInHierarchy
    {
        get
        {
            for (var e = this; e != null; e = e.Parent)
            {
                if (!e.Active) return false;
            }
            return true;
        }
    }

    /// <summary>True when <paramref name="other"/> is an ancestor of this entity.</summary>
    public bool IsDescendantOf(Entity other)
    {
        for (var e = Parent; e != null; e = e.Parent)
        {
            if (e == other) return true;
        }
        return false;
    }

    /// <summary>
    /// Moves this entity under <paramref name="newParent"/> (or to the scene root when <c>null</c>).
    /// <paramref name="index"/> is the final position among the new siblings; negative appends.
    /// Local transform values are kept. Returns false if the move is invalid (cycle, other scene).
    /// </summary>
    public bool SetParent(Entity? newParent, int index = -1)
    {
        var scene = Scene;
        if (scene == null || newParent == this) return false;
        if (newParent != null && (newParent.Scene != scene || newParent.IsDescendantOf(this))) return false;

        var oldList = Parent?._children ?? scene.RootList;
        var newList = newParent?._children ?? scene.RootList;
        oldList.Remove(this);
        if (index < 0 || index > newList.Count) index = newList.Count;
        newList.Insert(index, this);
        Parent = newParent;
        Transform.MarkDirty();
        return true;
    }

    /// <summary>Index of this entity among its siblings.</summary>
    public int SiblingIndex => (Parent?._children ?? Scene?.RootList)?.IndexOf(this) ?? -1;

    /// <summary>Adds a new component of type <typeparamref name="T"/>.</summary>
    public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

    /// <summary>Adds a new component. Adding <see cref="Transform"/> returns the existing one.</summary>
    public Component AddComponent(Type type)
    {
        if (!typeof(Component).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException($"{type.Name} is not a concrete Component type.", nameof(type));
        if (type == typeof(Transform)) return Transform;

        var component = (Component)Activator.CreateInstance(type)!;
        Attach(component, -1);
        return component;
    }

    /// <summary>Re-attaches a component that was removed earlier (used by undo).</summary>
    public bool InsertComponent(Component component, int index = -1)
    {
        if (component.Entity != null || component is Transform) return false;
        Attach(component, index);
        return true;
    }

    /// <summary>Returns the first component of type <typeparamref name="T"/>, or <c>null</c>.</summary>
    public T? GetComponent<T>() where T : Component
    {
        for (int i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T match) return match;
        }
        return null;
    }

    /// <summary>Removes a component. The transform cannot be removed.</summary>
    public bool RemoveComponent(Component component)
    {
        if (component == Transform || component.Entity != this) return false;
        if (component.Awoken) component.OnDestroy();
        component.Awoken = false;
        component.Started = false;
        _components.Remove(component);
        component.Entity = null!;
        return true;
    }

    /// <summary>Queues this entity (and its children) for removal at the end of the current update.</summary>
    public void Destroy() => Scene?.QueueDestroy(this);

    internal void RunDestroyCallbacks()
    {
        for (int i = 0; i < _components.Count; i++)
        {
            var c = _components[i];
            if (c.Awoken) c.OnDestroy();
            c.Awoken = false;
            c.Started = false;
        }
        for (int i = 0; i < _children.Count; i++) _children[i].RunDestroyCallbacks();
    }

    private void Attach(Component component, int index)
    {
        component.Entity = this;
        if (index < 0 || index > _components.Count) _components.Add(component);
        else _components.Insert(index, component);
    }
}

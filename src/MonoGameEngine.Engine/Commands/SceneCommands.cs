using System.Reflection;
using Microsoft.Xna.Framework;

namespace MonoGameEngine.Engine.Commands;

/// <summary>Changes one property on a component or entity.</summary>
public sealed class SetPropertyCommand(object target, PropertyInfo property, object? oldValue, object? newValue)
    : ICommand
{
    /// <inheritdoc />
    public string Name => $"Set {property.Name}";

    /// <summary>Builds a command for the named public property of <paramref name="target"/>.</summary>
    public static SetPropertyCommand For(object target, string propertyName, object? oldValue, object? newValue) =>
        new(target, target.GetType().GetProperty(propertyName)
                    ?? throw new ArgumentException($"No property '{propertyName}'.", nameof(propertyName)),
            oldValue, newValue);

    /// <inheritdoc />
    public void Do() => property.SetValue(target, newValue);

    /// <inheritdoc />
    public void Undo() => property.SetValue(target, oldValue);
}

/// <summary>Records that an entity was created (the entity is already in the scene).</summary>
public sealed class CreateEntityCommand : ICommand
{
    private readonly Scene _scene;
    private readonly Entity _entity;
    private readonly Entity? _parent;
    private readonly int _index;

    /// <summary>Captures the entity's current place in the hierarchy.</summary>
    public CreateEntityCommand(Entity entity)
    {
        _scene = entity.Scene ?? throw new ArgumentException("The entity must be in a scene.", nameof(entity));
        _entity = entity;
        _parent = entity.Parent;
        _index = entity.SiblingIndex;
    }

    /// <inheritdoc />
    public string Name => $"Create {_entity.Name}";

    /// <inheritdoc />
    public void Do() => _scene.Add(_entity, _parent, _index);

    /// <inheritdoc />
    public void Undo() => _scene.Remove(_entity);
}

/// <summary>Deletes an entity with its subtree. Undo puts it back in the same place.</summary>
public sealed class DeleteEntityCommand : ICommand
{
    private readonly Scene _scene;
    private readonly Entity _entity;
    private readonly Entity? _parent;
    private readonly int _index;

    /// <summary>Captures the entity's current place in the hierarchy.</summary>
    public DeleteEntityCommand(Entity entity)
    {
        _scene = entity.Scene ?? throw new ArgumentException("The entity must be in a scene.", nameof(entity));
        _entity = entity;
        _parent = entity.Parent;
        _index = entity.SiblingIndex;
    }

    /// <inheritdoc />
    public string Name => $"Delete {_entity.Name}";

    /// <inheritdoc />
    public void Do() => _scene.Remove(_entity);

    /// <inheritdoc />
    public void Undo() => _scene.Add(_entity, _parent, _index);
}

/// <summary>Moves an entity to a new parent and position, keeping its world transform.</summary>
public sealed class ReparentEntityCommand : ICommand
{
    private readonly Entity _entity;
    private readonly Entity? _newParent;
    private readonly int _newIndex;
    private readonly Entity? _oldParent;
    private readonly int _oldIndex;
    private readonly (Vector2 Position, float Rotation, Vector2 Scale) _oldLocal;
    private (Vector2 Position, float Rotation, Vector2 Scale)? _newLocal;

    /// <summary>Captures the current place and local transform, to restore on undo.</summary>
    public ReparentEntityCommand(Entity entity, Entity? newParent, int newIndex = -1)
    {
        _entity = entity;
        _newParent = newParent;
        _newIndex = newIndex;
        _oldParent = entity.Parent;
        _oldIndex = entity.SiblingIndex;
        var t = entity.Transform;
        _oldLocal = (t.Position, t.Rotation, t.Scale);
    }

    /// <inheritdoc />
    public string Name => $"Move {_entity.Name}";

    /// <summary>False if the move would be invalid (a cycle), in which case <see cref="Do"/> does nothing.</summary>
    public bool IsValid =>
        _newParent != _entity && (_newParent == null || !_newParent.IsDescendantOf(_entity));

    /// <inheritdoc />
    public void Do()
    {
        if (!IsValid) return;
        var t = _entity.Transform;
        if (_newLocal is { } local)
        {
            _entity.SetParent(_newParent, _newIndex);
            (t.Position, t.Rotation, t.Scale) = local;
            return;
        }

        var (position, rotation, scale) = (t.WorldPosition, t.WorldRotation, t.WorldScale);
        if (!_entity.SetParent(_newParent, _newIndex)) return;
        t.WorldPosition = position;
        t.WorldRotation = rotation;
        t.WorldScale = scale;
        _newLocal = (t.Position, t.Rotation, t.Scale);
    }

    /// <inheritdoc />
    public void Undo()
    {
        _entity.SetParent(_oldParent, _oldIndex);
        var t = _entity.Transform;
        (t.Position, t.Rotation, t.Scale) = _oldLocal;
    }
}

/// <summary>Records that a component was added (it is already attached).</summary>
public sealed class AddComponentCommand(Component component) : ICommand
{
    private readonly Entity _entity = component.Entity;
    private readonly int _index = component.Entity.Components.ToList().IndexOf(component);

    /// <inheritdoc />
    public string Name => $"Add {component.GetType().Name}";

    /// <inheritdoc />
    public void Do() => _entity.InsertComponent(component, _index);

    /// <inheritdoc />
    public void Undo() => _entity.RemoveComponent(component);
}

/// <summary>Removes a component. Undo re-attaches the same instance with its values.</summary>
public sealed class RemoveComponentCommand(Component component) : ICommand
{
    private readonly Entity _entity = component.Entity;
    private readonly int _index = component.Entity.Components.ToList().IndexOf(component);

    /// <inheritdoc />
    public string Name => $"Remove {component.GetType().Name}";

    /// <inheritdoc />
    public void Do() => _entity.RemoveComponent(component);

    /// <inheritdoc />
    public void Undo() => _entity.InsertComponent(component, _index);
}

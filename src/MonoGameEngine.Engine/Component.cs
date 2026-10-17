using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEngine.Engine;

/// <summary>
/// Base class for behaviour attached to an <see cref="Entity"/>. Public read/write properties are
/// serialized and shown in the editor Inspector. Components need a public parameterless constructor.
/// </summary>
public abstract class Component
{
    internal bool Awoken;
    internal bool Started;

    /// <summary>Disabled components skip <c>OnStart</c>, <c>OnUpdate</c> and <c>OnDraw</c>.</summary>
    [HideInInspector]
    public bool Enabled { get; set; } = true;

    /// <summary>The entity this component is attached to. Valid while attached.</summary>
    public Entity Entity { get; internal set; } = null!;

    /// <summary>The scene of the owning entity, or <c>null</c> when detached.</summary>
    public Scene? Scene => Entity?.Scene;

    /// <summary>Called once when the scene starts running, before any <c>OnStart</c>.</summary>
    protected internal virtual void OnAwake() { }

    /// <summary>Called once before the first update after <c>OnAwake</c>.</summary>
    protected internal virtual void OnStart() { }

    /// <summary>Called every frame while the scene runs.</summary>
    protected internal virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>Called every frame to draw. The sprite batch is already begun.</summary>
    protected internal virtual void OnDraw(SpriteBatch spriteBatch) { }

    /// <summary>Called when the component or its entity is destroyed, or the scene stops.</summary>
    protected internal virtual void OnDestroy() { }
}

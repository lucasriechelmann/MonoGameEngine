using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEngine.Engine;

/// <summary>
/// Runs a <see cref="Scene"/>: drives the component lifecycle and draws it. Used by the game, and by
/// the editor for play mode (the editor also uses <see cref="Draw"/> alone for edit mode).
/// </summary>
public sealed class SceneRunner(Scene scene)
{
    /// <summary>The scene being run.</summary>
    public Scene Scene { get; } = scene;

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Runs <c>OnAwake</c> then <c>OnStart</c> on all active components.</summary>
    public void Start()
    {
        IsRunning = true;
        RunPendingLifecycle();
    }

    /// <summary>Advances the scene by one frame. Starts the runner if needed.</summary>
    public void Update(GameTime gameTime)
    {
        if (!IsRunning) Start();
        RunPendingLifecycle();

        var entities = Scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;
            var components = entity.Components;
            for (int j = 0; j < components.Count; j++)
            {
                var c = components[j];
                if (c.Enabled && c.Started) c.OnUpdate(gameTime);
            }
        }
        Scene.FlushDestroyed();
    }

    /// <summary>Runs <c>OnDestroy</c> on all awake components and resets the lifecycle so the scene can start again.</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        var entities = Scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var components = entities[i].Components;
            for (int j = 0; j < components.Count; j++)
            {
                var c = components[j];
                if (c.Awoken) c.OnDestroy();
                c.Awoken = false;
                c.Started = false;
            }
        }
        IsRunning = false;
    }

    /// <summary>
    /// Draws all active components. Uses the first enabled <see cref="Camera2D"/> unless
    /// <paramref name="viewOverride"/> is given (the editor passes its own view).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Point viewportSize, Matrix? viewOverride = null)
    {
        var view = viewOverride ?? FindCameraView(viewportSize);
        spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied, SamplerState.LinearClamp,
            null, null, null, view);

        var entities = Scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;
            var components = entity.Components;
            for (int j = 0; j < components.Count; j++)
            {
                if (components[j].Enabled) components[j].OnDraw(spriteBatch);
            }
        }
        spriteBatch.End();
    }

    private Matrix FindCameraView(Point viewportSize)
    {
        var entities = Scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;
            var camera = entity.GetComponent<Camera2D>();
            if (camera is { Enabled: true }) return camera.GetViewMatrix(viewportSize);
        }
        return Matrix.Identity;
    }

    private void RunPendingLifecycle()
    {
        var entities = Scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;
            var components = entity.Components;
            for (int j = 0; j < components.Count; j++)
            {
                var c = components[j];
                if (c.Awoken) continue;
                c.Awoken = true;
                c.OnAwake();
            }
        }
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;
            var components = entity.Components;
            for (int j = 0; j < components.Count; j++)
            {
                var c = components[j];
                if (c.Started || !c.Enabled || !c.Awoken) continue;
                c.Started = true;
                c.OnStart();
            }
        }
    }
}

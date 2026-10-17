using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Commands;
using MonoGameEngine.Engine.Serialization;

namespace MonoGameEngine.Editor.State;

/// <summary>Whether the editor is editing or running the scene.</summary>
public enum PlayMode
{
    /// <summary>Editing. Changes are undoable.</summary>
    Edit,

    /// <summary>Running a copy of the scene.</summary>
    Playing,

    /// <summary>Running copy is frozen.</summary>
    Paused,
}

/// <summary>
/// Everything the panels share: the scene being edited, selection, undo history and play mode.
/// Contains no drawing code. While playing, the scene runs as a copy made from a JSON snapshot, so
/// stopping simply discards the copy and the edited scene is exactly as it was.
/// </summary>
public sealed class EditorState
{
    private SceneRunner _editRunner;

    /// <summary>Creates state with an empty scene.</summary>
    public EditorState(AssetManager assets)
    {
        Assets = assets;
        EditScene = NewEmptyScene();
        _editRunner = new SceneRunner(EditScene);
        History.Changed += () => { if (Mode == PlayMode.Edit) Dirty = true; };
    }

    /// <summary>Loads textures and resolves asset paths.</summary>
    public AssetManager Assets { get; }

    /// <summary>The scene being edited.</summary>
    public Scene EditScene { get; private set; }

    /// <summary>The running copy while playing or paused.</summary>
    public Scene? PlayScene { get; private set; }

    /// <summary>The scene the panels show: the running copy while playing, else the edited scene.</summary>
    public Scene ActiveScene => PlayScene ?? EditScene;

    /// <summary>Runs the play copy.</summary>
    public SceneRunner? PlayRunner { get; private set; }

    /// <summary>Draws the active scene (and, while playing, runs it).</summary>
    public SceneRunner ViewRunner => PlayRunner ?? _editRunner;

    /// <summary>Undo and redo stacks for the edited scene.</summary>
    public CommandHistory History { get; } = new();

    /// <summary>The Scene view's own camera.</summary>
    public SceneViewCamera ViewCamera { get; } = new();

    /// <summary>Current play mode.</summary>
    public PlayMode Mode { get; private set; } = PlayMode.Edit;

    /// <summary>True when edits are recorded for undo.</summary>
    public bool IsEditing => Mode == PlayMode.Edit;

    /// <summary>Absolute path of the open scene file, or null for an unsaved scene.</summary>
    public string? ScenePath { get; private set; }

    /// <summary>True when there are changes since the last save.</summary>
    public bool Dirty { get; private set; }

    /// <summary>Relative asset path currently being dragged from the Assets panel.</summary>
    public string? DraggedAsset { get; set; }

    /// <summary>Selected entity id. Survives play and stop because both scenes share ids.</summary>
    public Guid? SelectedId { get; private set; }

    /// <summary>The selected entity in the active scene, or null.</summary>
    public Entity? Selected => SelectedId is { } id ? ActiveScene.Find(id) : null;

    /// <summary>Raised after a scene was loaded, created or saved (the app persists settings).</summary>
    public event Action? SceneFileChanged;

    /// <summary>Selects an entity, or clears the selection.</summary>
    public void Select(Entity? entity) => SelectedId = entity?.Id;

    // ---- undoable edits -------------------------------------------------------------------------

    /// <summary>Applies a command. While playing it is applied but not recorded.</summary>
    public void Execute(ICommand command)
    {
        if (IsEditing) History.Execute(command);
        else command.Do();
    }

    /// <summary>Records a command that was already applied (live edits such as a gizmo drag). Ignored while playing.</summary>
    public void Record(ICommand command)
    {
        if (IsEditing) History.Record(command);
    }

    /// <summary>Undoes the last edit.</summary>
    public void Undo()
    {
        if (IsEditing) History.Undo();
    }

    /// <summary>Redoes the last undone edit.</summary>
    public void Redo()
    {
        if (IsEditing) History.Redo();
    }

    /// <summary>Creates an empty entity.</summary>
    public Entity CreateEmpty(Entity? parent = null, Vector2? worldPosition = null) =>
        Create("Entity", parent, worldPosition, null);

    /// <summary>Creates an entity with a <see cref="SpriteRenderer"/>.</summary>
    public Entity CreateSprite(string texturePath, Entity? parent = null, Vector2? worldPosition = null)
    {
        var name = string.IsNullOrEmpty(texturePath) ? "Sprite" : Path.GetFileNameWithoutExtension(texturePath);
        return Create(name, parent, worldPosition, e => e.AddComponent<SpriteRenderer>().TexturePath = texturePath);
    }

    /// <summary>Creates an entity with a <see cref="Camera2D"/>.</summary>
    public Entity CreateCamera(Entity? parent = null, Vector2? worldPosition = null) =>
        Create("Camera", parent, worldPosition, e => e.AddComponent<Camera2D>());

    /// <summary>Deletes an entity and its children.</summary>
    public void Delete(Entity entity) => Execute(new DeleteEntityCommand(entity));

    /// <summary>Copies an entity and selects the copy.</summary>
    public Entity Duplicate(Entity entity)
    {
        var copy = SceneSerializer.Clone(entity);
        Record(new CreateEntityCommand(copy));
        Select(copy);
        return copy;
    }

    /// <summary>Moves an entity under a new parent. <paramref name="index"/> is the final sibling position.</summary>
    public bool Reparent(Entity entity, Entity? parent, int index = -1)
    {
        var command = new ReparentEntityCommand(entity, parent, index);
        if (!command.IsValid) return false;
        Execute(command);
        return true;
    }

    // ---- files ----------------------------------------------------------------------------------

    /// <summary>Starts a new empty scene.</summary>
    public void NewScene()
    {
        Stop();
        UseScene(NewEmptyScene(), null);
        Log.Info("New scene.");
    }

    /// <summary>Opens a scene file. Returns false and logs on failure.</summary>
    public bool OpenScene(string path)
    {
        try
        {
            var scene = SceneSerializer.Load(path, Assets);
            Stop();
            UseScene(scene, Path.GetFullPath(path));
            Log.Info($"Opened {Path.GetFileName(path)}.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not open '{path}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Saves to the current file. Returns false if the scene has no file yet.</summary>
    public bool Save() => ScenePath != null && SaveAs(ScenePath);

    /// <summary>Saves to a file and makes it the current one.</summary>
    public bool SaveAs(string path)
    {
        if (!path.EndsWith(SceneSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
            path += SceneSerializer.FileExtension;
        try
        {
            if (IsEditing || PlayScene == null) EditScene.Name = Path.GetFileName(path).Replace(SceneSerializer.FileExtension, "");
            SceneSerializer.Save(EditScene, path);
            ScenePath = Path.GetFullPath(path);
            Dirty = false;
            Log.Info($"Saved {Path.GetFileName(path)}.");
            SceneFileChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not save '{path}': {ex.Message}");
            return false;
        }
    }

    // ---- play mode ------------------------------------------------------------------------------

    /// <summary>Starts playing a copy of the scene, or resumes when paused.</summary>
    public void Play()
    {
        if (Mode == PlayMode.Paused)
        {
            Mode = PlayMode.Playing;
            return;
        }
        if (Mode != PlayMode.Edit) return;

        try
        {
            var snapshot = SceneSerializer.Serialize(EditScene);
            PlayScene = SceneSerializer.Deserialize(snapshot, Assets);
            PlayRunner = new SceneRunner(PlayScene);
            PlayRunner.Start();
            Mode = PlayMode.Playing;
            Log.Info("Play.");
        }
        catch (Exception ex)
        {
            Log.Error($"Play failed: {ex.Message}");
            Stop();
        }
    }

    /// <summary>Freezes the running copy.</summary>
    public void Pause()
    {
        if (Mode == PlayMode.Playing) Mode = PlayMode.Paused;
    }

    /// <summary>Stops playing and discards the copy; the edited scene is untouched.</summary>
    public void Stop()
    {
        if (Mode == PlayMode.Edit) return;
        try { PlayRunner?.Stop(); }
        catch (Exception ex) { Log.Error($"Error while stopping: {ex.Message}"); }
        PlayRunner = null;
        PlayScene = null;
        Mode = PlayMode.Edit;
        Log.Info("Stop.");
    }

    /// <summary>Advances the running copy. Call once per frame.</summary>
    public void Update(GameTime gameTime)
    {
        if (Mode != PlayMode.Playing || PlayRunner == null) return;
        try
        {
            PlayRunner.Update(gameTime);
        }
        catch (Exception ex)
        {
            Log.Error($"Exception in play mode: {ex.Message}");
            Stop();
        }
    }

    // ---- helpers --------------------------------------------------------------------------------

    private Entity Create(string name, Entity? parent, Vector2? worldPosition, Action<Entity>? setup)
    {
        var entity = ActiveScene.CreateEntity(name, parent);
        setup?.Invoke(entity);
        if (worldPosition is { } position) entity.Transform.WorldPosition = position;
        Record(new CreateEntityCommand(entity));
        Select(entity);
        return entity;
    }

    private Scene NewEmptyScene() => new() { Name = "Untitled", Assets = Assets };

    private void UseScene(Scene scene, string? path)
    {
        EditScene = scene;
        _editRunner = new SceneRunner(scene);
        ScenePath = path;
        History.Clear();
        Dirty = false;
        SelectedId = null;
        SceneFileChanged?.Invoke();
    }
}

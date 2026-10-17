using ImGuiNET;
using MonoGameEngine.Editor.State;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>What the Project menu can do. The editor window implements these because opening a project replaces the editor state.</summary>
/// <param name="New">Create a project from the template.</param>
/// <param name="Open">Choose a project file to open.</param>
/// <param name="OpenRecent">Open a recently used project by path.</param>
/// <param name="Settings">Show the Project Setup dialog for the open project.</param>
/// <param name="Rebuild">Rebuild the game code and reload it, keeping the open scene.</param>
/// <param name="Recent">Recently opened project paths, newest first.</param>
public sealed record ProjectActions(Action New, Action Open, Action<string> OpenRecent, Action Settings, Action Rebuild, Func<IReadOnlyList<string>> Recent);

/// <summary>Main menu bar (File, Edit, GameObject, View, Project), the Play / Pause / Stop toolbar, and global shortcuts.</summary>
public sealed class MainMenu(EditorState state, EditorSettings settings, FileDialog dialog, ProjectActions project, Action exit)
{
    private Action? _afterDiscardCheck;
    private bool _openUnsavedPopup;

    /// <summary>Draws the menu bar and any popups it owns.</summary>
    public void Draw()
    {
        HandleShortcuts();

        if (ImGui.BeginMainMenuBar())
        {
            DrawFileMenu();
            DrawEditMenu();
            DrawCreateMenu();
            DrawViewMenu();
            DrawProjectMenu();
            DrawPlayControls();
            ImGui.EndMainMenuBar();
        }
        DrawUnsavedPopup();
    }

    private void DrawFileMenu()
    {
        if (!ImGui.BeginMenu("File")) return;
        if (ImGui.MenuItem("New", "Ctrl+N")) Guarded(state.NewScene);
        if (ImGui.MenuItem("Open...", "Ctrl+O")) Guarded(ShowOpenDialog);
        ImGui.Separator();
        if (ImGui.MenuItem(Icons.Save + "  Save", "Ctrl+S")) SaveOrAsk(null);
        if (ImGui.MenuItem("Save As...")) ShowSaveDialog(null);
        ImGui.Separator();
        if (ImGui.MenuItem("Exit")) Guarded(exit);
        ImGui.EndMenu();
    }

    private void DrawEditMenu()
    {
        if (!ImGui.BeginMenu("Edit")) return;
        var history = state.History;
        if (ImGui.MenuItem(history.CanUndo ? $"{Icons.Undo}  Undo {history.UndoName}" : $"{Icons.Undo}  Undo", "Ctrl+Z", false, state.IsEditing && history.CanUndo))
            state.Undo();
        if (ImGui.MenuItem(history.CanRedo ? $"{Icons.Redo}  Redo {history.RedoName}" : $"{Icons.Redo}  Redo", "Ctrl+Y", false, state.IsEditing && history.CanRedo))
            state.Redo();
        ImGui.Separator();
        var selected = state.Selected;
        if (ImGui.MenuItem(Icons.ContentCopy + "  Duplicate", "Ctrl+D", false, selected != null)) state.Duplicate(selected!);
        if (ImGui.MenuItem(Icons.Delete + "  Delete", "Del", false, selected != null)) state.Delete(selected!);
        ImGui.EndMenu();
    }

    private void DrawCreateMenu()
    {
        if (!ImGui.BeginMenu("GameObject")) return;
        var parent = state.Selected;
        if (ImGui.MenuItem("Create Empty")) state.CreateEmpty();
        if (ImGui.MenuItem("Create Sprite")) state.CreateSprite("");
        if (ImGui.MenuItem("Create Camera")) state.CreateCamera();
        ImGui.Separator();
        if (ImGui.MenuItem("Create Empty Child", null, false, parent != null)) state.CreateEmpty(parent);
        ImGui.EndMenu();
    }

    private void DrawViewMenu()
    {
        if (!ImGui.BeginMenu("View")) return;
        bool grid = settings.ShowGrid;
        if (ImGui.MenuItem("Grid", null, ref grid)) settings.ShowGrid = grid;
        bool demo = settings.ShowDemoWindow;
        if (ImGui.MenuItem("ImGui Demo Window", null, ref demo)) settings.ShowDemoWindow = demo;
        if (ImGui.MenuItem("Reset Scene View")) state.ViewCamera.Reset();
        ImGui.EndMenu();
    }

    private void DrawProjectMenu()
    {
        if (!ImGui.BeginMenu("Project")) return;
        if (ImGui.MenuItem(Icons.Add + "  New Project...")) Guarded(project.New);
        if (ImGui.MenuItem(Icons.FolderOpen + "  Open Project...")) Guarded(project.Open);
        if (ImGui.BeginMenu("Open Recent"))
        {
            var recent = project.Recent();
            if (recent.Count == 0) ImGui.TextDisabled("(none)");
            foreach (var path in recent)
            {
                if (ImGui.MenuItem(Path.GetFileNameWithoutExtension(path) + "##" + path)) Guarded(() => project.OpenRecent(path));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(path);
            }
            ImGui.EndMenu();
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Project Settings...")) project.Settings();
        if (ImGui.MenuItem(Icons.Refresh + "  Rebuild and Reload Game Code", "Ctrl+B")) project.Rebuild();
        ImGui.EndMenu();
    }

    private void DrawPlayControls()
    {
        const float width = 92f;
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - width * 3 - ImGui.GetStyle().ItemSpacing.X * 2) * 0.5f);

        bool playing = state.Mode == PlayMode.Playing;
        bool paused = state.Mode == PlayMode.Paused;

        if (playing) ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.2f, 0.55f, 0.25f, 1f));
        if (ImGui.Button(Icons.PlayArrow + " Play", new Num.Vector2(width, 0))) state.Play();
        if (playing) ImGui.PopStyleColor();

        if (paused) ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.7f, 0.55f, 0.15f, 1f));
        ImGui.BeginDisabled(state.Mode == PlayMode.Edit);
        if (ImGui.Button(Icons.Pause + " Pause", new Num.Vector2(width, 0))) state.Pause();
        ImGui.EndDisabled();
        if (paused) ImGui.PopStyleColor();

        ImGui.BeginDisabled(state.Mode == PlayMode.Edit);
        if (ImGui.Button(Icons.Stop + " Stop", new Num.Vector2(width, 0))) state.Stop();
        ImGui.EndDisabled();
    }

    private void HandleShortcuts()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput || !io.KeyCtrl) return;

        if (ImGui.IsKeyPressed(ImGuiKey.Z))
        {
            if (io.KeyShift) state.Redo();
            else state.Undo();
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Y)) state.Redo();
        if (ImGui.IsKeyPressed(ImGuiKey.S, false)) SaveOrAsk(null);
        if (ImGui.IsKeyPressed(ImGuiKey.O, false)) Guarded(ShowOpenDialog);
        if (ImGui.IsKeyPressed(ImGuiKey.N, false)) Guarded(state.NewScene);
        if (ImGui.IsKeyPressed(ImGuiKey.D, false) && state.Selected is { } selected) state.Duplicate(selected);
        if (ImGui.IsKeyPressed(ImGuiKey.B, false)) project.Rebuild();
    }

    // ---- file actions ---------------------------------------------------------------------------

    /// <summary>Runs an action that would discard the open scene, asking to save first if there are changes.</summary>
    public void Guarded(Action action)
    {
        if (!state.Dirty)
        {
            action();
            return;
        }
        _afterDiscardCheck = action;
        _openUnsavedPopup = true;
    }

    /// <summary>Opens a scene file after the unsaved-changes check.</summary>
    public void OpenScene(string path) => Guarded(() => state.OpenScene(path));

    private void ShowOpenDialog() => dialog.ShowOpen(ScenesDirectory(), path => state.OpenScene(path));

    private void ShowSaveDialog(Action? afterSave)
    {
        var name = state.ScenePath != null ? Path.GetFileName(state.ScenePath) : "NewScene" + Engine.Serialization.SceneSerializer.FileExtension;
        dialog.ShowSave(ScenesDirectory(), name, path =>
        {
            if (state.SaveAs(path)) afterSave?.Invoke();
        });
    }

    private void SaveOrAsk(Action? afterSave)
    {
        if (state.Save()) afterSave?.Invoke();
        else if (state.ScenePath == null) ShowSaveDialog(afterSave);
    }

    private string ScenesDirectory()
    {
        if (state.ScenePath != null) return Path.GetDirectoryName(state.ScenePath)!;
        var scenes = Path.Combine(state.Assets.RootDirectory, "Scenes");
        return Directory.Exists(scenes) ? scenes : state.Assets.RootDirectory;
    }

    private void DrawUnsavedPopup()
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved changes");
            _openUnsavedPopup = false;
        }
        if (!ImGui.BeginPopupModal("Unsaved changes", ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.TextUnformatted("The scene has unsaved changes.");
        ImGui.Spacing();
        if (ImGui.Button("Save", new Num.Vector2(100, 0)))
        {
            var action = _afterDiscardCheck;
            _afterDiscardCheck = null;
            ImGui.CloseCurrentPopup();
            SaveOrAsk(action);
        }
        ImGui.SameLine();
        if (ImGui.Button("Don't Save", new Num.Vector2(100, 0)))
        {
            var action = _afterDiscardCheck;
            _afterDiscardCheck = null;
            ImGui.CloseCurrentPopup();
            action?.Invoke();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Num.Vector2(100, 0)))
        {
            _afterDiscardCheck = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}

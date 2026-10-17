using ImGuiNET;
using Microsoft.Xna.Framework;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Editor.Ui;
using MonoGameEngine.Engine;
using MonoGameEngine.Tooling.Projects;

namespace MonoGameEngine.Editor;

/// <summary>
/// Editor host window: owns the ImGui layer and, for the open project, the shared <see cref="EditorState"/> and the
/// panels. Opening a project builds it in the background (<see cref="ProjectSession"/>), then replaces the state
/// and panels; with no project open only the start screen is shown.
/// </summary>
public sealed class EditorGame : Game
{
    private static readonly string[] ProjectExtensions = [ProjectFile.Extension, ".sln", ".slnx", ".csproj"];

    private readonly GraphicsDeviceManager _graphics;
    private readonly EditorSettings _settings = EditorSettings.Load();
    private readonly string? _startPath;
    private ConsoleLog? _console;

    private ImGuiRenderer _imgui = null!;
    private ImGuiInput _input = null!;
    private FileDialog _fileDialog = null!;
    private ProjectDialogs _dialogs = null!;
    private ProjectActions _projectActions = null!;

    private ProjectSession? _project;
    private ProjectSession? _pending;
    private SceneSnapshot? _pendingRestore;

    private AssetManager _assets = null!;
    private EditorState? _state;
    private EditTracker _tracker = null!;
    private SceneViewRenderer? _sceneRenderer;
    private MainMenu _menu = null!;
    private HierarchyPanel _hierarchy = null!;
    private InspectorPanel _inspector = null!;
    private SceneViewPanel _sceneView = null!;
    private AssetsPanel _assetsPanel = null!;
    private ConsolePanel _consolePanel = null!;
    private bool _exitRequested;

    /// <summary>Creates the editor window.</summary>
    /// <param name="projectPath">Project to open at startup (<c>.mgeproj</c>, solution or project file), or null for the last used one.</param>
    public EditorGame(string? projectPath = null)
    {
        _startPath = projectPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Math.Max(_settings.WindowWidth, 800),
            PreferredBackBufferHeight = Math.Max(_settings.WindowHeight, 600),
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    private static string EngineVersion
    {
        get
        {
            var version = typeof(Component).Assembly.GetName().Version;
            return version == null ? "0.0" : $"{version.Major}.{version.Minor}";
        }
    }

    /// <summary>Keeps the back buffer the same size as the window, including on maximize and restore.</summary>
    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return; // minimized
        if (_graphics.PreferredBackBufferWidth == bounds.Width && _graphics.PreferredBackBufferHeight == bounds.Height) return;

        _graphics.PreferredBackBufferWidth = bounds.Width;
        _graphics.PreferredBackBufferHeight = bounds.Height;
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        _console = new ConsoleLog();

        _imgui = new ImGuiRenderer(this);
        var io = ImGui.GetIO();
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        Directory.CreateDirectory(EditorSettings.Directory);
        _imgui.SetIniFilename(EditorSettings.LayoutPath);

        PanelLayout.Load(_settings.PanelLayout);
        _input = new ImGuiInput(Window);
        _fileDialog = new FileDialog();
        _dialogs = new ProjectDialogs(_fileDialog);
        _projectActions = new ProjectActions(
            ShowNewProjectDialog,
            ShowOpenProjectDialog,
            OpenProjectPath,
            ShowProjectSettings,
            RebuildProject,
            () => _settings.RecentProjects);
        _consolePanel = new ConsolePanel(_console);

        var start = _startPath ?? _settings.LastProjectPath ?? FindNearbyProject();
        if (start != null && File.Exists(start)) OpenProjectPath(start);

        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_exitRequested) Exit();
        _input.Update(IsActive);
        if (_pending != null && _pending.Poll()) FinishSession();
        _state?.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _imgui.BeginFrame(gameTime);
        PanelLayout.BeginFrame();

        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
        if (_state != null)
        {
            _menu.Draw();
            _hierarchy.Draw();
            _sceneView.Draw();
            _inspector.Draw();
            _assetsPanel.Draw();
            _consolePanel.Draw();
        }
        else if (_pending == null)
        {
            _dialogs.DrawStartScreen(_settings.RecentProjects, ShowNewProjectDialog, ShowOpenProjectDialog, OpenProjectPath);
            _consolePanel.Draw();
        }
        _fileDialog.Draw();
        _dialogs.Draw();
        if (_settings.ShowDemoWindow)
        {
            bool open = true;
            ImGui.ShowDemoWindow(ref open);
            _settings.ShowDemoWindow = open;
        }
        _tracker?.Update();

        UpdateWindowTitle();

        // The scene render target is drawn after the panels decided its size, before ImGui samples it.
        if (_state != null) _sceneRenderer?.Render(_state, _settings.ShowGrid);
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(new Color(27, 27, 27));
        _imgui.EndFrame();

        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _settings.PanelLayout = PanelLayout.Export();
        _settings.WindowWidth = Window.ClientBounds.Width;
        _settings.WindowHeight = Window.ClientBounds.Height;
        _settings.Save();
        base.OnExiting(sender, args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _console?.Dispose();
            _sceneRenderer?.Dispose();
            _pending?.Dispose();
            _project?.Dispose();
            _imgui?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---- projects -------------------------------------------------------------------------------

    private void ShowNewProjectDialog()
    {
        var folder = _settings.NewProjectFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoGameEngine");
        var defaults = new TemplateOptions
        {
            Name = "MyGame",
            ParentDirectory = folder,
            EnginePackagePath = ProjectTemplate.FindEnginePackage(Path.Combine(AppContext.BaseDirectory, "Packages")) ?? "",
        };
        _dialogs.ShowNew(defaults, CreateProject);
    }

    private void CreateProject(TemplateOptions options)
    {
        try
        {
            var path = ProjectTemplate.Create(options);
            _settings.NewProjectFolder = options.ParentDirectory;
            Log.Info($"Created {Path.GetFileName(path)} in {Path.GetDirectoryName(path)}.");
            OpenProjectPath(path);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not create the project: {ex.Message}");
            _dialogs.ShowError($"Could not create the project: {ex.Message}");
        }
    }

    private void ShowOpenProjectDialog()
    {
        var start = _project?.Project.Directory ?? _settings.RecentProjects.Select(Path.GetDirectoryName).FirstOrDefault() ?? Environment.CurrentDirectory;
        _fileDialog.ShowOpenFile("Open Project", start!, ProjectExtensions, OpenProjectPath);
    }

    /// <summary>Opens a <c>.mgeproj</c> directly, or sets one up from a solution or project file.</summary>
    private void OpenProjectPath(string path)
    {
        try
        {
            if (path.EndsWith(ProjectFile.Extension, StringComparison.OrdinalIgnoreCase))
            {
                BeginSession(ProjectFile.Load(path), null);
                return;
            }

            var beside = Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(path))!, "*" + ProjectFile.Extension).FirstOrDefault();
            if (beside != null)
            {
                BeginSession(ProjectFile.Load(beside), null);
                return;
            }
            _dialogs.ShowSetup(ProjectSetup.FromPath(path, EngineVersion), setup => SaveAndOpen(setup, null));
        }
        catch (Exception ex)
        {
            Log.Error($"Could not open '{path}': {ex.Message}");
            _dialogs.ShowError($"Could not open '{Path.GetFileName(path)}': {ex.Message}");
        }
    }

    private void ShowProjectSettings()
    {
        if (_project == null) return;
        _dialogs.ShowSetup(ProjectSetup.FromProjectFile(_project.Project), setup => SaveAndOpen(setup, _state?.TakeSnapshot()));
    }

    /// <summary>Rebuilds the game and reloads its components, keeping the open scene and its unsaved changes.</summary>
    private void RebuildProject()
    {
        if (_project == null || _pending != null) return;
        try
        {
            BeginSession(ProjectFile.Load(_project.Project.FilePath), _state?.TakeSnapshot());
        }
        catch (Exception ex)
        {
            Log.Error($"Could not reload the project: {ex.Message}");
        }
    }

    private void SaveAndOpen(ProjectSetup setup, SceneSnapshot? restore)
    {
        try
        {
            var file = setup.ToProjectFile();
            file.Save();
            BeginSession(file, restore);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not save the project file: {ex.Message}");
            _dialogs.ShowError($"Could not save the project file: {ex.Message}");
        }
    }

    private void BeginSession(ProjectFile file, SceneSnapshot? restore)
    {
        if (_pending != null) return;
        _pendingRestore = restore;
        _pending = new ProjectSession(file, Services);
        _pending.Start();
        _dialogs.Building = true;
        _dialogs.BuildingName = file.Name;
        Log.Info($"Opening project {file.Name}...");
    }

    private void FinishSession()
    {
        var session = _pending!;
        _pending = null;
        _dialogs.Building = false;

        foreach (var message in session.Messages)
        {
            if (message.Contains(" error ", StringComparison.OrdinalIgnoreCase)) Log.Error(message);
            else Log.Info(message);
        }
        if (session.Phase == SessionPhase.Failed)
        {
            _pendingRestore = null;
            Log.Error(session.Error ?? "The project could not be opened.");
            _dialogs.ShowError(session.Error ?? "The project could not be opened.");
            return;
        }

        var declared = session.Project.EngineVersion;
        if (declared != null && declared != EngineVersion)
            Log.Warning($"The project was made for Engine {declared}; this editor has Engine {EngineVersion}.");

        SwitchTo(session);
    }

    /// <summary>Makes a ready session the open project and rebuilds the editor state and panels around it.</summary>
    private void SwitchTo(ProjectSession session)
    {
        var old = _project;
        _project = session;
        CreateEditor(session.Assets!);
        old?.Dispose();

        _settings.RememberProject(session.Project.FilePath);
        _settings.Save();

        var restore = _pendingRestore;
        _pendingRestore = null;
        if (restore == null || !_state!.RestoreSnapshot(restore)) OpenStartupScene();
        Log.Info($"Project {session.Project.Name} ready.");
    }

    private void CreateEditor(AssetManager assets)
    {
        _sceneRenderer?.Dispose();
        _assets = assets;
        _state = new EditorState(_assets);
        _state.SceneFileChanged += OnSceneFileChanged;
        _tracker = new EditTracker(_state);
        _sceneRenderer = new SceneViewRenderer(GraphicsDevice, _imgui);

        var browser = new AssetBrowser(_assets);
        _menu = new MainMenu(_state, _settings, _fileDialog, _projectActions, () => _exitRequested = true);
        _hierarchy = new HierarchyPanel(_state);
        _inspector = new InspectorPanel(_state, _tracker, browser);
        _sceneView = new SceneViewPanel(_state, _settings, _sceneRenderer);
        _assetsPanel = new AssetsPanel(_state, browser, _imgui, _menu.OpenScene);
    }

    /// <summary>Looks for a project file next to the editor or in a folder above it, so running from a checkout just works.</summary>
    private static string? FindNearbyProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var found = dir.EnumerateFiles("*" + ProjectFile.Extension).FirstOrDefault();
            if (found != null) return found.FullName;
        }
        return null;
    }

    private void OpenStartupScene()
    {
        var key = _project!.Project.FilePath;
        if (_settings.LastSceneByProject.TryGetValue(key, out var last) && File.Exists(last) && _state!.OpenScene(last)) return;

        var main = Path.Combine(_assets.RootDirectory, _project.Project.StartupScene);
        if (File.Exists(main)) _state!.OpenScene(main);
    }

    private void OnSceneFileChanged()
    {
        if (_project != null && _state?.ScenePath != null) _settings.LastSceneByProject[_project.Project.FilePath] = _state.ScenePath;
        _settings.Save();
    }

    private void UpdateWindowTitle()
    {
        string title;
        if (_state == null)
        {
            title = "MonoGameEngine Editor";
        }
        else
        {
            string name = _state.ScenePath != null ? Path.GetFileName(_state.ScenePath) : "Untitled";
            string mode = _state.Mode == PlayMode.Edit ? "" : $" [{_state.Mode}]";
            title = $"{name}{(_state.Dirty ? "*" : "")}{mode} - {_project?.Project.Name} - MonoGameEngine Editor";
        }
        if (Window.Title != title) Window.Title = title;
    }
}

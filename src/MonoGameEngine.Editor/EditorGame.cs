using ImGuiNET;
using Microsoft.Xna.Framework;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Editor.Ui;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Reflection;

namespace MonoGameEngine.Editor;

/// <summary>Editor host window: owns the ImGui layer, the shared <see cref="EditorState"/> and the panels.</summary>
public sealed class EditorGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly EditorSettings _settings = EditorSettings.Load();
    private ConsoleLog? _console;

    private ImGuiRenderer _imgui = null!;
    private ImGuiInput _input = null!;
    private AssetManager _assets = null!;
    private EditorState _state = null!;
    private EditTracker _tracker = null!;
    private SceneViewRenderer _sceneRenderer = null!;
    private MainMenu _menu = null!;
    private FileDialog _fileDialog = null!;
    private HierarchyPanel _hierarchy = null!;
    private InspectorPanel _inspector = null!;
    private SceneViewPanel _sceneView = null!;
    private AssetsPanel _assetsPanel = null!;
    private ConsolePanel _consolePanel = null!;
    private bool _exitRequested;

    public EditorGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Math.Max(_settings.WindowWidth, 800),
            PreferredBackBufferHeight = Math.Max(_settings.WindowHeight, 600),
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        _console = new ConsoleLog();
        ComponentRegistry.RegisterAssembly(typeof(GameApp).Assembly);

        var contentRoot = AssetManager.FindContentRoot(preferSource: true);
        if (contentRoot == null)
        {
            contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
            Directory.CreateDirectory(contentRoot);
            Log.Warning($"Content folder not found; using '{contentRoot}'.");
        }

        _imgui = new ImGuiRenderer(this);
        var io = ImGui.GetIO();
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        Directory.CreateDirectory(EditorSettings.Directory);
        _imgui.SetIniFilename(EditorSettings.LayoutPath);

        _input = new ImGuiInput(Window);
        _assets = new AssetManager(Content, contentRoot);
        _state = new EditorState(_assets);
        _state.SceneFileChanged += OnSceneFileChanged;
        _tracker = new EditTracker(_state);
        _sceneRenderer = new SceneViewRenderer(GraphicsDevice, _imgui);

        var browser = new AssetBrowser(_assets);
        _fileDialog = new FileDialog();
        _menu = new MainMenu(_state, _settings, _fileDialog, () => _exitRequested = true);
        _hierarchy = new HierarchyPanel(_state);
        _inspector = new InspectorPanel(_state, _tracker, browser);
        _sceneView = new SceneViewPanel(_state, _settings, _sceneRenderer);
        _assetsPanel = new AssetsPanel(_state, browser, _imgui, _menu.OpenScene);
        _consolePanel = new ConsolePanel(_console);

        OpenStartupScene();
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_exitRequested) Exit();
        _input.Update(IsActive);
        _state.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _imgui.BeginFrame(gameTime);

        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
        _menu.Draw();
        _hierarchy.Draw();
        _sceneView.Draw();
        _inspector.Draw();
        _assetsPanel.Draw();
        _consolePanel.Draw();
        _fileDialog.Draw();
        if (_settings.ShowDemoWindow)
        {
            bool open = true;
            ImGui.ShowDemoWindow(ref open);
            _settings.ShowDemoWindow = open;
        }
        _tracker.Update();

        UpdateWindowTitle();

        // The scene render target is drawn after the panels decided its size, before ImGui samples it.
        _sceneRenderer.Render(_state, _settings.ShowGrid);
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(new Color(38, 38, 42));
        _imgui.EndFrame();

        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
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
            _assets?.Dispose();
            _imgui?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OpenStartupScene()
    {
        var last = _settings.LastScenePath;
        if (last != null && File.Exists(last) && _state.OpenScene(last)) return;

        var main = Path.Combine(_assets.RootDirectory, GameApp.StartScene);
        if (File.Exists(main)) _state.OpenScene(main);
    }

    private void OnSceneFileChanged()
    {
        _settings.LastScenePath = _state.ScenePath;
        _settings.Save();
    }

    private void UpdateWindowTitle()
    {
        string name = _state.ScenePath != null ? Path.GetFileName(_state.ScenePath) : "Untitled";
        string mode = _state.Mode == PlayMode.Edit ? "" : $" [{_state.Mode}]";
        string title = $"{name}{(_state.Dirty ? "*" : "")}{mode} - MonoGameEngine Editor";
        if (Window.Title != title) Window.Title = title;
    }
}

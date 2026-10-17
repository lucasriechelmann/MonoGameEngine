using ImGuiNET;
using MonoGameEngine.Tooling.Projects;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// The dialogs around opening a project: the start screen shown when no project is open, the Project Setup
/// dialog (project dropdowns for a solution), the "Building" modal and the error message.
/// </summary>
public sealed class ProjectDialogs(FileDialog fileDialog)
{
    private const string NewTitle = "New Project";
    private const string SetupTitle = "Project Setup";
    private const string BuildTitle = "Opening Project";
    private const string ErrorTitle = "Could not open project";

    private ProjectSetup? _setup;
    private Action<ProjectSetup>? _accept;
    private bool _openSetup;
    private string _name = "";
    private string _contentRoot = "";
    private string _startupScene = "";

    private TemplateOptions? _newOptions;
    private Action<TemplateOptions>? _create;
    private bool _openNew;
    private string _newName = "";
    private string _newFolder = "";
    private bool _desktopGl = true;
    private bool _windowsDx;

    private bool _buildShown;
    private string? _error;
    private bool _openError;

    /// <summary>True while a project is being built; shows the modal.</summary>
    public bool Building { get; set; }

    /// <summary>Name shown in the building modal.</summary>
    public string BuildingName { get; set; } = "";

    /// <summary>Asks for a name, a folder and the platforms of a new project, starting from <paramref name="defaults"/>.</summary>
    public void ShowNew(TemplateOptions defaults, Action<TemplateOptions> create)
    {
        _newOptions = defaults;
        _create = create;
        _newName = defaults.Name;
        _newFolder = defaults.ParentDirectory;
        _desktopGl = defaults.Platforms.Contains(TemplatePlatform.DesktopGL);
        _windowsDx = defaults.Platforms.Contains(TemplatePlatform.WindowsDX);
        _openNew = true;
    }

    /// <summary>Asks the user to confirm or adjust the project choices before the project file is written.</summary>
    public void ShowSetup(ProjectSetup setup, Action<ProjectSetup> accept)
    {
        _setup = setup;
        _accept = accept;
        _name = setup.Name;
        _contentRoot = setup.ContentRoot;
        _startupScene = setup.StartupScene;
        _openSetup = true;
    }

    /// <summary>Shows an error message in a modal.</summary>
    public void ShowError(string message)
    {
        _error = message;
        _openError = true;
    }

    /// <summary>Draws the welcome window used while no project is open.</summary>
    public void DrawStartScreen(IReadOnlyList<string> recent, Action newProject, Action open, Action<string> openRecent)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + viewport.Size * 0.5f, ImGuiCond.Always, new Num.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Num.Vector2(460, 0));
        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;
        if (!ImGui.Begin("Welcome", flags))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("MonoGameEngine Editor");
        ImGui.TextDisabled("Create a game project, or open a .mgeproj (or a .sln / .csproj to set one up).");
        ImGui.Spacing();
        if (ImGui.Button(Icons.Add + "  New Project...", new Num.Vector2(-1, 0))) newProject();
        if (ImGui.Button(Icons.FolderOpen + "  Open Project...", new Num.Vector2(-1, 0))) open();

        if (recent.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText("Recent");
            foreach (var path in recent)
            {
                if (ImGui.Selectable(Path.GetFileNameWithoutExtension(path) + "##" + path)) openRecent(path);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(path);
            }
        }
        ImGui.End();
    }

    /// <summary>Draws whichever modal is open. Call every frame.</summary>
    public void Draw()
    {
        DrawNew();
        DrawSetup();
        DrawBuilding();
        DrawError();
    }

    private void DrawNew()
    {
        if (_openNew)
        {
            ImGui.OpenPopup(NewTitle);
            _openNew = false;
        }
        bool open = true;
        if (_newOptions == null || !ImGui.BeginPopupModal(NewTitle, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        var options = _newOptions;
        const float width = 380f;
        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Name", ref _newName, 64);

        ImGui.SetNextItemWidth(width - 90);
        ImGui.InputText("##folder", ref _newFolder, 260);
        ImGui.SameLine();
        if (ImGui.Button("Browse...", new Num.Vector2(80, 0)))
        {
            // The folder dialog is a separate popup, so this one steps aside and comes back with the choice.
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            fileDialog.ShowFolder("Choose Project Folder", _newFolder,
                path => { _newFolder = path; _openNew = true; }, () => _openNew = true);
            return;
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("Folder");

        ImGui.Spacing();
        ImGui.TextUnformatted("Platforms");
        ImGui.Checkbox("DesktopGL (cross-platform desktop)", ref _desktopGl);
        ImGui.BeginDisabled(!OperatingSystem.IsWindows());
        ImGui.Checkbox("WindowsDX (Windows, DirectX)", ref _windowsDx);
        ImGui.EndDisabled();

        options.Name = _newName;
        options.ParentDirectory = _newFolder;
        options.Platforms = [];
        if (_desktopGl) options.Platforms.Add(TemplatePlatform.DesktopGL);
        if (_windowsDx && OperatingSystem.IsWindows()) options.Platforms.Add(TemplatePlatform.WindowsDX);

        var problems = ProjectTemplate.Problems(options);
        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width + 80);
        foreach (var problem in problems) ImGui.TextColored(new Num.Vector4(1f, 0.45f, 0.4f, 1f), problem);
        if (problems.Count == 0)
        {
            ImGui.TextDisabled("Creates " + ProjectTemplate.ProjectDirectory(options));
            ImGui.TextDisabled($"{options.Name}.Core holds your components; one launcher project per platform.");
        }
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        ImGui.BeginDisabled(problems.Count > 0);
        if (ImGui.Button("Create", new Num.Vector2(120, 0)))
        {
            var create = _create;
            ImGui.CloseCurrentPopup();
            _newOptions = null;
            create?.Invoke(options);
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Num.Vector2(120, 0)) || !open)
        {
            _newOptions = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawSetup()
    {
        if (_openSetup)
        {
            ImGui.OpenPopup(SetupTitle);
            _openSetup = false;
        }
        bool open = true;
        if (_setup == null || !ImGui.BeginPopupModal(SetupTitle, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        var setup = _setup;
        const float width = 420f;
        ImGui.TextDisabled(setup.SolutionPath ?? setup.Projects.FirstOrDefault()?.Path ?? "");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Name", ref _name, 128);

        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo("Code project", setup.Projects.Count == 0 ? "" : Label(setup, setup.CodeIndex)))
        {
            for (int i = 0; i < setup.Projects.Count; i++)
            {
                ImGui.BeginDisabled(!setup.Projects[i].IsEditorLoadable);
                if (ImGui.Selectable(Label(setup, i) + "##code" + i, i == setup.CodeIndex)) setup.CodeIndex = i;
                ImGui.EndDisabled();
            }
            ImGui.EndCombo();
        }
        Hint("Holds your components. The editor builds it and loads its assembly.");

        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo("Launch project", setup.LaunchIndex < 0 ? "(same as code project)" : Label(setup, setup.LaunchIndex)))
        {
            if (ImGui.Selectable("(same as code project)##launch", setup.LaunchIndex < 0)) setup.LaunchIndex = -1;
            for (int i = 0; i < setup.Projects.Count; i++)
            {
                if (ImGui.Selectable(Label(setup, i) + "##launch" + i, i == setup.LaunchIndex)) setup.LaunchIndex = i;
            }
            ImGui.EndCombo();
        }
        Hint("The platform head that is built and run as the game; it builds the content.");

        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Content folder", ref _contentRoot, 260);
        ImGui.SetNextItemWidth(width);
        ImGui.InputText("Startup scene", ref _startupScene, 260);

        setup.Name = _name;
        setup.ContentRoot = _contentRoot;
        setup.StartupScene = _startupScene;

        var problems = setup.Problems();
        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width + 120);
        foreach (var problem in problems) ImGui.TextColored(new Num.Vector4(1f, 0.45f, 0.4f, 1f), problem);
        if (problems.Count == 0) ImGui.TextDisabled("Saved as " + Path.GetFileName(setup.ProjectFilePath));
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        ImGui.BeginDisabled(problems.Count > 0);
        if (ImGui.Button("Open Project", new Num.Vector2(120, 0)))
        {
            var accept = _accept;
            ImGui.CloseCurrentPopup();
            _setup = null;
            accept?.Invoke(setup);
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Num.Vector2(120, 0)) || !open)
        {
            _setup = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawBuilding()
    {
        if (Building && !_buildShown)
        {
            ImGui.OpenPopup(BuildTitle);
            _buildShown = true;
        }
        if (!_buildShown || !ImGui.BeginPopupModal(BuildTitle, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove)) return;

        ImGui.TextUnformatted($"Building {BuildingName}" + new string('.', (int)(ImGui.GetTime() * 2) % 4));
        ImGui.TextDisabled("Builds the game, then loads its components and content.");
        if (!Building)
        {
            _buildShown = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawError()
    {
        if (_openError)
        {
            ImGui.OpenPopup(ErrorTitle);
            _openError = false;
        }
        if (!ImGui.BeginPopupModal(ErrorTitle, ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.PushTextWrapPos(520);
        ImGui.TextUnformatted(_error ?? "");
        ImGui.PopTextWrapPos();
        ImGui.TextDisabled("The Console panel lists the build errors.");
        ImGui.Spacing();
        if (ImGui.Button("OK", new Num.Vector2(100, 0))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static string Label(ProjectSetup setup, int index)
    {
        var project = setup.Projects[index];
        return $"{project.Name}  [{project.FrameworkLabel}]" + (project.IsEditorLoadable ? "" : "  (launch only)");
    }

    private static void Hint(string text)
    {
        ImGui.Indent();
        ImGui.TextDisabled(text);
        ImGui.Unindent();
    }
}

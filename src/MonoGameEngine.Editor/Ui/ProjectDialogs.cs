using ImGuiNET;
using MonoGameEngine.Tooling.Projects;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// The dialogs around opening a project: the start screen shown when no project is open, the Project Setup
/// dialog (project dropdowns for a solution), the "Building" modal and the error message.
/// </summary>
public sealed class ProjectDialogs
{
    private const string SetupTitle = "Project Setup";
    private const string BuildTitle = "Opening Project";
    private const string ErrorTitle = "Could not open project";

    private ProjectSetup? _setup;
    private Action<ProjectSetup>? _accept;
    private bool _openSetup;
    private string _name = "";
    private string _contentRoot = "";
    private string _startupScene = "";

    private bool _buildShown;
    private string? _error;
    private bool _openError;

    /// <summary>True while a project is being built; shows the modal.</summary>
    public bool Building { get; set; }

    /// <summary>Name shown in the building modal.</summary>
    public string BuildingName { get; set; } = "";

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
    public void DrawStartScreen(IReadOnlyList<string> recent, Action open, Action<string> openRecent)
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
        ImGui.TextDisabled("Open a game project to start. Choose a .mgeproj, or a .sln / .csproj to set one up.");
        ImGui.Spacing();
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
        DrawSetup();
        DrawBuilding();
        DrawError();
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

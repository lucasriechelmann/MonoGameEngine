using System.Text.Json;
using MonoGameEngine.Engine;

namespace MonoGameEngine.Editor.State;

/// <summary>Editor preferences kept between sessions. Window layout is stored by ImGui in <c>imgui.ini</c> next to this file.</summary>
public sealed class EditorSettings
{
    private const int MaxRecentProjects = 8;

    /// <summary>Folder holding <c>editor-settings.json</c> and <c>imgui.ini</c>.</summary>
    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonoGameEngine");

    private static string FilePath => Path.Combine(Directory, "editor-settings.json");

    /// <summary>Full path of the layout file ImGui should use.</summary>
    public static string LayoutPath => Path.Combine(Directory, "imgui.ini");

    /// <summary>Project (<c>.mgeproj</c>) opened at the next start.</summary>
    public string? LastProjectPath { get; set; }

    /// <summary>Folder the last new project was created in.</summary>
    public string? NewProjectFolder { get; set; }

    /// <summary>Recently opened projects, newest first.</summary>
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>Scene last open in each project, keyed by project file path.</summary>
    public Dictionary<string, string> LastSceneByProject { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Show the grid in the Scene view.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Draw collider outlines in the Scene view.</summary>
    public bool ShowColliders { get; set; } = true;

    /// <summary>Show the ImGui demo window.</summary>
    public bool ShowDemoWindow { get; set; }

    /// <summary>Window width in pixels.</summary>
    public int WindowWidth { get; set; } = 1600;

    /// <summary>Window height in pixels.</summary>
    public int WindowHeight { get; set; } = 900;

    /// <summary>Panel rectangles as fractions of the window work area (x, y, width, height), keyed by window name.</summary>
    public Dictionary<string, float[]>? PanelLayout { get; set; }

    /// <summary>Makes <paramref name="projectPath"/> the project to reopen and moves it to the top of the recent list.</summary>
    public void RememberProject(string projectPath)
    {
        LastProjectPath = projectPath;
        RecentProjects.RemoveAll(p => string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase));
        RecentProjects.Insert(0, projectPath);
        if (RecentProjects.Count > MaxRecentProjects) RecentProjects.RemoveRange(MaxRecentProjects, RecentProjects.Count - MaxRecentProjects);
    }

    /// <summary>Loads settings, returning defaults if the file is missing or unreadable.</summary>
    public static EditorSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(FilePath)) ?? new EditorSettings();
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not read editor settings: {ex.Message}");
        }
        return new EditorSettings();
    }

    /// <summary>Writes settings to disk.</summary>
    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not save editor settings: {ex.Message}");
        }
    }
}

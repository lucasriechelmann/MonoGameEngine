using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>
/// The editor's project file (<c>*.mgeproj</c>): says which solution, which project holds the game code,
/// which project is launched, and where the content and the startup scene are. All paths are relative to
/// the folder containing the file, with forward slashes.
/// </summary>
public sealed class ProjectFile
{
    /// <summary>File extension of a project file.</summary>
    public const string Extension = ".mgeproj";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Schema version of this file.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Game";

    /// <summary>Solution (<c>.sln</c> or <c>.slnx</c>) the projects belong to, or null when a single project is used.</summary>
    public string? Solution { get; set; }

    /// <summary>
    /// Project that defines the game's components. The editor builds it and loads its assembly, so it must
    /// target a framework the editor can load (not Android or iOS).
    /// </summary>
    public string CodeProject { get; set; } = "";

    /// <summary>Project built and run as the game, usually a platform head. Empty means the code project.</summary>
    public string? LaunchProject { get; set; }

    /// <summary>Folder with the source assets, <c>Content.mgcb</c> and scenes.</summary>
    public string ContentRoot { get; set; } = "content";

    /// <summary>Scene opened first, relative to <see cref="ContentRoot"/>.</summary>
    public string StartupScene { get; set; } = "Scenes/main.scene.json";

    /// <summary>Engine version (major.minor) the game was made for. Used to warn about a mismatch.</summary>
    public string? EngineVersion { get; set; }

    /// <summary>Absolute path of this file. Set by <see cref="Load"/>; not saved.</summary>
    [JsonIgnore]
    public string FilePath { get; set; } = "";

    /// <summary>Folder containing this file.</summary>
    [JsonIgnore]
    public string Directory => Path.GetDirectoryName(FilePath) ?? "";

    /// <summary>The project to build and launch: <see cref="LaunchProject"/>, or the code project when unset.</summary>
    [JsonIgnore]
    public string EffectiveLaunchProject => string.IsNullOrEmpty(LaunchProject) ? CodeProject : LaunchProject;

    /// <summary>Resolves a path stored in this file to an absolute path.</summary>
    public string Resolve(string relative) => Path.GetFullPath(Path.Combine(Directory, relative));

    /// <summary>Stores an absolute path as relative to this file, with forward slashes.</summary>
    public string MakeRelative(string absolute) =>
        Path.GetRelativePath(Directory, absolute).Replace('\\', '/');

    /// <summary>Reads a project file. Throws if it is missing or malformed.</summary>
    public static ProjectFile Load(string path)
    {
        var full = Path.GetFullPath(path);
        var file = JsonSerializer.Deserialize<ProjectFile>(File.ReadAllText(full), Options)
                   ?? throw new InvalidDataException("Empty project file.");
        file.FilePath = full;
        return file;
    }

    /// <summary>Writes this project file to <see cref="FilePath"/>.</summary>
    public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
}

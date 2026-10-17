namespace MonoGameEngine.Tooling.Projects;

/// <summary>
/// The choices behind the editor's Project Setup dialog: which project holds the game code (loaded into the
/// editor), which one is launched (a platform head, or the same project), and where content and the startup
/// scene live. Starts from a solution, a single project or an existing <see cref="ProjectFile"/> and guesses
/// sensible defaults; <see cref="ToProjectFile"/> produces the file to save.
/// </summary>
public sealed class ProjectSetup
{
    private static readonly string[] SkippedFolders = ["bin", "obj", "node_modules", ".git", ".vs"];

    private ProjectSetup(string directory, string projectFilePath, string? solutionPath, IReadOnlyList<CsprojInfo> projects)
    {
        Directory = directory;
        ProjectFilePath = projectFilePath;
        SolutionPath = solutionPath;
        Projects = projects;
    }

    /// <summary>Folder the project file will live in; stored paths are relative to it.</summary>
    public string Directory { get; }

    /// <summary>Where the <c>.mgeproj</c> is (or will be) written.</summary>
    public string ProjectFilePath { get; }

    /// <summary>The solution the projects come from, or null for a single-project setup.</summary>
    public string? SolutionPath { get; }

    /// <summary>The C# projects to choose from.</summary>
    public IReadOnlyList<CsprojInfo> Projects { get; }

    /// <summary>Display name of the game.</summary>
    public string Name { get; set; } = "Game";

    /// <summary>Index into <see cref="Projects"/> of the project whose assembly the editor loads.</summary>
    public int CodeIndex { get; set; }

    /// <summary>Index into <see cref="Projects"/> of the project to launch, or -1 to launch the code project.</summary>
    public int LaunchIndex { get; set; } = -1;

    /// <summary>Content folder, relative to <see cref="Directory"/>.</summary>
    public string ContentRoot { get; set; } = "content";

    /// <summary>Startup scene, relative to the content folder.</summary>
    public string StartupScene { get; set; } = "Scenes/main.scene.json";

    /// <summary>Engine version (major.minor) to record in the project file.</summary>
    public string? EngineVersion { get; set; }

    /// <summary>Starts a setup from a solution or a single project file, with guessed defaults.</summary>
    public static ProjectSetup FromPath(string path, string? engineVersion = null)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)!;
        var isSolution = SolutionReader.IsSolution(full);
        var name = Path.GetFileNameWithoutExtension(full);

        var projects = isSolution
            ? SolutionReader.ReadProjects(full).Select(p => CsprojInfo.Read(p.AbsolutePath)).ToList()
            : [CsprojInfo.Read(full)];

        var setup = new ProjectSetup(directory, Path.Combine(directory, name + ProjectFile.Extension), isSolution ? full : null, projects)
        {
            Name = name,
            EngineVersion = engineVersion,
        };
        setup.GuessDefaults();
        return setup;
    }

    /// <summary>Starts a setup from an existing project file, to change its choices.</summary>
    public static ProjectSetup FromProjectFile(ProjectFile file)
    {
        var solution = string.IsNullOrEmpty(file.Solution) ? null : file.Resolve(file.Solution);
        var projects = solution != null && File.Exists(solution)
            ? SolutionReader.ReadProjects(solution).Select(p => CsprojInfo.Read(p.AbsolutePath)).ToList()
            : new[] { file.CodeProject, file.LaunchProject }
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().Select(p => CsprojInfo.Read(file.Resolve(p!))).ToList();

        var setup = new ProjectSetup(file.Directory, file.FilePath, solution, projects)
        {
            Name = file.Name,
            ContentRoot = file.ContentRoot,
            StartupScene = file.StartupScene,
            EngineVersion = file.EngineVersion,
        };
        setup.CodeIndex = Math.Max(0, setup.IndexOf(file.CodeProject));
        setup.LaunchIndex = string.IsNullOrEmpty(file.LaunchProject) || file.LaunchProject == file.CodeProject ? -1 : setup.IndexOf(file.LaunchProject);
        return setup;
    }

    /// <summary>Problems that stop the project from being opened; empty when it is valid.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) problems.Add("Enter a project name.");
        if (Projects.Count == 0) problems.Add("No C# projects were found.");
        else if (CodeIndex < 0 || CodeIndex >= Projects.Count) problems.Add("Choose the project that holds the game code.");
        else if (!Projects[CodeIndex].IsEditorLoadable)
            problems.Add($"{Projects[CodeIndex].Name} targets {Projects[CodeIndex].FrameworkLabel}, which the editor cannot load. Put the components in a shared desktop-compatible project.");
        if (!System.IO.Directory.Exists(Path.GetFullPath(Path.Combine(Directory, ContentRoot))))
            problems.Add($"Content folder '{ContentRoot}' does not exist.");
        return problems;
    }

    /// <summary>Builds the project file for the current choices (not yet saved).</summary>
    public ProjectFile ToProjectFile()
    {
        var file = new ProjectFile
        {
            FilePath = ProjectFilePath,
            Name = Name.Trim(),
            ContentRoot = ContentRoot.Replace('\\', '/'),
            StartupScene = StartupScene.Replace('\\', '/'),
            EngineVersion = EngineVersion,
        };
        file.Solution = SolutionPath == null ? null : file.MakeRelative(SolutionPath);
        file.CodeProject = file.MakeRelative(Projects[CodeIndex].Path);
        file.LaunchProject = LaunchIndex < 0 || LaunchIndex == CodeIndex ? null : file.MakeRelative(Projects[LaunchIndex].Path);
        return file;
    }

    private int IndexOf(string? relative)
    {
        if (string.IsNullOrEmpty(relative)) return -1;
        var full = Path.GetFullPath(Path.Combine(Directory, relative));
        for (int i = 0; i < Projects.Count; i++)
        {
            if (string.Equals(Projects[i].Path, full, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private void GuessDefaults()
    {
        // Code: prefer a loadable library that uses the Engine (a shared core), then any loadable Engine user.
        var loadable = Enumerable.Range(0, Projects.Count).Where(i => Projects[i].IsEditorLoadable).ToList();
        bool IsExe(int i) => Projects[i].OutputType.EndsWith("Exe", StringComparison.OrdinalIgnoreCase);
        CodeIndex = loadable.Where(i => Projects[i].ReferencesEngine && !IsExe(i)).Cast<int?>().FirstOrDefault()
                    ?? loadable.Where(i => Projects[i].ReferencesEngine).Cast<int?>().FirstOrDefault()
                    ?? loadable.Cast<int?>().FirstOrDefault()
                    ?? 0;

        // Launch: the first project that builds content and is not the code project.
        LaunchIndex = Enumerable.Range(0, Projects.Count).Where(i => i != CodeIndex && Projects[i].HasContent && Projects[i].IsEditorLoadable).Cast<int?>().FirstOrDefault() ?? -1;

        var contentSource = Projects.Select(p => p.ContentReference).FirstOrDefault(c => c != null);
        var contentDir = contentSource != null ? Path.GetDirectoryName(contentSource) : FindContentFolder(Directory);
        if (contentDir != null) ContentRoot = Path.GetRelativePath(Directory, contentDir).Replace('\\', '/');

        var contentFull = Path.GetFullPath(Path.Combine(Directory, ContentRoot));
        if (System.IO.Directory.Exists(contentFull) && !File.Exists(Path.Combine(contentFull, StartupScene)))
        {
            var scene = System.IO.Directory.EnumerateFiles(contentFull, "*.scene.json", SearchOption.AllDirectories).Order().FirstOrDefault();
            if (scene != null) StartupScene = Path.GetRelativePath(contentFull, scene).Replace('\\', '/');
        }
    }

    /// <summary>Finds the shallowest folder (up to three levels down) containing <c>Content.mgcb</c>.</summary>
    private static string? FindContentFolder(string root)
    {
        var level = new List<string> { root };
        for (int depth = 0; depth <= 3 && level.Count > 0; depth++)
        {
            foreach (var dir in level)
            {
                if (File.Exists(Path.Combine(dir, "Content.mgcb"))) return dir;
            }
            level = level.SelectMany(SafeSubdirectories).ToList();
        }
        return null;
    }

    private static IEnumerable<string> SafeSubdirectories(string dir)
    {
        try
        {
            return System.IO.Directory.EnumerateDirectories(dir)
                .Where(d => !SkippedFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}

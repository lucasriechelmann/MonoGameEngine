using System.Reflection;
using Microsoft.Xna.Framework.Content;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Reflection;
using MonoGameEngine.Tooling.Projects;

namespace MonoGameEngine.Editor.State;

/// <summary>Where a <see cref="ProjectSession"/> is in opening a project.</summary>
public enum SessionPhase
{
    /// <summary>The game project is being built on a background thread.</summary>
    Building,

    /// <summary>Built and loaded: <see cref="ProjectSession.Assets"/> and the game components are available.</summary>
    Ready,

    /// <summary>The build or the load failed; see <see cref="ProjectSession.Error"/>.</summary>
    Failed,
}

/// <summary>
/// An open game project: builds it, loads the game's code assembly into a collectible context and registers
/// its components, and provides the <see cref="AssetManager"/> reading the project's built content. Opening
/// a project, changing the launch project and rebuilding all create a new session; disposing the old one
/// unregisters and unloads its code. Poll it every frame; all work after the build runs on the calling thread.
/// </summary>
public sealed class ProjectSession : IDisposable
{
    private readonly IServiceProvider _services;
    private Task<BuildOutcome>? _build;
    private GameAssemblyContext? _context;
    private ContentManager? _content;

    /// <summary>Creates a session. Call <see cref="Start"/> to begin building.</summary>
    /// <param name="file">The project to open.</param>
    /// <param name="services">Service provider of the editor window, used by the content manager to find the graphics device.</param>
    public ProjectSession(ProjectFile file, IServiceProvider services)
    {
        Project = file;
        _services = services;
    }

    /// <summary>The project being opened.</summary>
    public ProjectFile Project { get; }

    /// <summary>Current phase.</summary>
    public SessionPhase Phase { get; private set; } = SessionPhase.Building;

    /// <summary>Why the session failed, when <see cref="Phase"/> is <see cref="SessionPhase.Failed"/>.</summary>
    public string? Error { get; private set; }

    /// <summary>Build output lines worth showing in the Console.</summary>
    public IReadOnlyList<string> Messages { get; private set; } = [];

    /// <summary>The game's code assembly, once ready.</summary>
    public Assembly? GameAssembly { get; private set; }

    /// <summary>Loads textures of the project; available once ready.</summary>
    public AssetManager? Assets { get; private set; }

    /// <summary>Starts building on a background thread.</summary>
    public void Start() => _build = Task.Run(BuildAsync);

    /// <summary>
    /// Checks whether the build finished and, if so, loads the result. Returns true exactly when the session
    /// just became <see cref="SessionPhase.Ready"/> or <see cref="SessionPhase.Failed"/>.
    /// </summary>
    public bool Poll()
    {
        if (Phase != SessionPhase.Building || _build is not { IsCompleted: true } build) return false;

        try
        {
            var outcome = build.GetAwaiter().GetResult();
            Messages = outcome.Messages;
            if (outcome.Error != null) return Fail(outcome.Error);
            Load(outcome);
            Phase = SessionPhase.Ready;
        }
        catch (Exception ex)
        {
            return Fail($"Could not open the project: {ex.Message}");
        }
        return true;
    }

    /// <summary>Unregisters the game's components, unloads its code and releases the loaded content.</summary>
    public void Dispose()
    {
        Assets?.Dispose();
        _content?.Dispose();
        Assets = null;
        _content = null;
        if (GameAssembly != null) ComponentRegistry.UnregisterAssembly(GameAssembly);
        GameAssembly = null;
        _context?.Unload();
        _context = null;
    }

    private bool Fail(string error)
    {
        Error = error;
        Phase = SessionPhase.Failed;
        Dispose();
        return true;
    }

    private void Load(BuildOutcome outcome)
    {
        var sourceRoot = Project.Resolve(Project.ContentRoot);
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException($"Content folder '{Project.ContentRoot}' not found.");

        _context = new GameAssemblyContext(outcome.CodeAssemblyPath!);
        GameAssembly = _context.LoadMain();
        ComponentRegistry.RegisterAssembly(GameAssembly);

        _content = new ContentManager(_services, outcome.BuiltContentDirectory ?? Path.Combine(sourceRoot, "Content"));
        Assets = new AssetManager(_content, sourceRoot);
    }

    /// <summary>Runs on a background thread: builds and locates the outputs. Touches no editor state.</summary>
    private async Task<BuildOutcome> BuildAsync()
    {
        var messages = new List<string>();
        var codePath = Project.Resolve(Project.CodeProject);
        var launchPath = Project.Resolve(Project.EffectiveLaunchProject);

        // The launch project builds the content, but a mobile head cannot be built for the desktop: use the code project then.
        var buildPath = CsprojInfo.Read(launchPath).IsEditorLoadable ? launchPath : codePath;
        messages.Add($"Building {Path.GetFileName(buildPath)}...");

        var build = await ProjectBuilder.BuildAsync(buildPath);
        if (!build.Success)
        {
            messages.AddRange(ErrorLines(build.Output));
            return new BuildOutcome(null, null, messages, $"Build failed for {Path.GetFileName(buildPath)}.");
        }

        var codeDll = build.TargetPath;
        var targetDir = build.TargetDir;
        if (!string.Equals(buildPath, codePath, StringComparison.OrdinalIgnoreCase))
        {
            var code = await ProjectBuilder.LocateOutputAsync(codePath);
            codeDll = code?.TargetPath;
        }
        if (codeDll == null || !File.Exists(codeDll))
            return new BuildOutcome(null, null, messages, $"Could not find the built assembly of {Path.GetFileName(codePath)}.");

        var contentDir = targetDir != null ? Path.Combine(targetDir, "Content") : null;
        if (contentDir == null || !Directory.Exists(contentDir))
        {
            messages.Add($"No built Content folder next to {Path.GetFileName(buildPath)}; textures will not load until Content.mgcb is built.");
            contentDir = null;
        }
        messages.Add("Build succeeded.");
        return new BuildOutcome(codeDll, contentDir, messages, null);
    }

    private static IEnumerable<string> ErrorLines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Contains(" error ", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(10);

    private sealed record BuildOutcome(string? CodeAssemblyPath, string? BuiltContentDirectory, IReadOnlyList<string> Messages, string? Error);
}

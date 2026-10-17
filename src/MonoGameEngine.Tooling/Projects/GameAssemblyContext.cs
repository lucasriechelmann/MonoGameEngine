using System.Reflection;
using System.Runtime.Loader;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>
/// A collectible load context for the game's code assembly. Assemblies the editor already has (the Engine,
/// MonoGame, Aether) are taken from the editor so a game <c>Component</c> derives from the very same
/// <c>Component</c> type the editor uses; the game's own assemblies are read into memory so rebuilding does
/// not hit a locked file.
/// </summary>
public sealed class GameAssemblyContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _shared;

    /// <summary>Creates a context for the assembly at <paramref name="assemblyPath"/>.</summary>
    /// <param name="assemblyPath">Full path of the game assembly; its dependencies are looked up next to it.</param>
    /// <param name="sharedAssemblies">Simple names always taken from the default context, in addition to everything it has loaded.</param>
    public GameAssemblyContext(string assemblyPath, IEnumerable<string>? sharedAssemblies = null)
        : base("Game:" + Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: true)
    {
        AssemblyPath = assemblyPath;
        _resolver = new AssemblyDependencyResolver(assemblyPath);
        _shared = new HashSet<string>(sharedAssemblies ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Full path of the main assembly.</summary>
    public string AssemblyPath { get; }

    /// <summary>Loads the main assembly (from memory, so the file stays unlocked).</summary>
    public Assembly LoadMain() => LoadFromFile(AssemblyPath);

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name == null || IsShared(name)) return null; // the default context supplies it

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null && File.Exists(path) ? LoadFromFile(path) : null;
    }

    private bool IsShared(string name) =>
        _shared.Contains(name) || Default.Assemblies.Any(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

    private Assembly LoadFromFile(string path)
    {
        using var dll = new MemoryStream(File.ReadAllBytes(path));
        var pdbPath = Path.ChangeExtension(path, ".pdb");
        if (!File.Exists(pdbPath)) return LoadFromStream(dll);
        using var pdb = new MemoryStream(File.ReadAllBytes(pdbPath));
        return LoadFromStream(dll, pdb);
    }
}

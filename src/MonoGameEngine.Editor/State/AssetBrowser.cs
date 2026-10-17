using MonoGameEngine.Engine;

namespace MonoGameEngine.Editor.State;

/// <summary>An entry in the Assets panel.</summary>
public readonly record struct AssetEntry(string Name, string RelativePath, string FullPath, bool IsDirectory);

/// <summary>Lists the content folder. Listings are cached and refreshed on request.</summary>
public sealed class AssetBrowser(AssetManager assets)
{
    private static readonly HashSet<string> HiddenFolders = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj" };

    private readonly Dictionary<string, List<AssetEntry>> _folderCache = new(StringComparer.OrdinalIgnoreCase);
    private List<string>? _allTextures;
    private string? _listedDirectory;
    private List<AssetEntry> _entries = new();

    /// <summary>Absolute path of the content folder.</summary>
    public string Root => assets.RootDirectory;

    /// <summary>The folder currently shown (absolute).</summary>
    public string CurrentDirectory { get; private set; } = assets.RootDirectory;

    /// <summary>Folders then files of <see cref="CurrentDirectory"/>.</summary>
    public IReadOnlyList<AssetEntry> Entries
    {
        get
        {
            if (_listedDirectory != CurrentDirectory) Refresh();
            return _entries;
        }
    }

    /// <summary>True if the current folder is the content root.</summary>
    public bool IsAtRoot => string.Equals(Path.GetFullPath(CurrentDirectory), Path.GetFullPath(Root),
        StringComparison.OrdinalIgnoreCase);

    /// <summary>The sub folders of <paramref name="directory"/> (cached until <see cref="Refresh"/>), for the folder tree.</summary>
    public IReadOnlyList<AssetEntry> GetSubfolders(string directory)
    {
        if (_folderCache.TryGetValue(directory, out var cached)) return cached;

        var folders = new List<AssetEntry>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(directory).Order())
            {
                var name = Path.GetFileName(dir);
                if (!HiddenFolders.Contains(name)) folders.Add(new AssetEntry(name, assets.ToRelative(dir) ?? name, dir, true));
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not list '{directory}': {ex.Message}");
        }
        return _folderCache[directory] = folders;
    }

    /// <summary>True if <paramref name="path"/> is <paramref name="ancestor"/> or inside it.</summary>
    public static bool IsSameOrInside(string ancestor, string path)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ancestor));
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return p.Equals(a, StringComparison.OrdinalIgnoreCase)
               || p.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Opens a sub folder.</summary>
    public void Enter(string fullPath) => CurrentDirectory = fullPath;

    /// <summary>Goes up one folder, staying inside the content root.</summary>
    public void Up()
    {
        if (IsAtRoot) return;
        var parent = Directory.GetParent(CurrentDirectory)?.FullName;
        CurrentDirectory = parent != null && assets.ToRelative(parent) != null ? parent : Root;
    }

    /// <summary>The current folder relative to the content root, for display.</summary>
    public string CurrentRelative => assets.ToRelative(CurrentDirectory) is { Length: > 0 } rel && rel != "." ? rel : "";

    /// <summary>Re-reads the folder and the texture list.</summary>
    public void Refresh()
    {
        _allTextures = null;
        _folderCache.Clear();
        _listedDirectory = CurrentDirectory;
        _entries = new List<AssetEntry>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(CurrentDirectory).Order())
            {
                var name = Path.GetFileName(dir);
                if (HiddenFolders.Contains(name)) continue;
                _entries.Add(new AssetEntry(name, assets.ToRelative(dir) ?? name, dir, true));
            }
            foreach (var file in Directory.EnumerateFiles(CurrentDirectory).Order())
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".mgcb", StringComparison.OrdinalIgnoreCase)) continue;
                _entries.Add(new AssetEntry(name, assets.ToRelative(file) ?? name, file, false));
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not list '{CurrentDirectory}': {ex.Message}");
            CurrentDirectory = Root;
        }
    }

    /// <summary>Relative paths of every texture under the content root.</summary>
    public IReadOnlyList<string> GetAllTextures()
    {
        if (_allTextures != null) return _allTextures;
        _allTextures = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = assets.ToRelative(file);
                if (relative == null || !AssetManager.IsTexture(file)) continue;
                var first = relative.Split('/')[0];
                if (HiddenFolders.Contains(first)) continue;
                _allTextures.Add(relative);
            }
            _allTextures.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not list textures: {ex.Message}");
        }
        return _allTextures;
    }
}

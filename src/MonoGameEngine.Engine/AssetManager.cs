using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEngine.Engine;

/// <summary>
/// Loads assets by the path of their source file relative to the content folder, for example
/// <c>Textures/crate.png</c>. The texture is loaded from the MonoGame content pipeline output
/// (<c>Textures/crate.xnb</c>), so it must be listed in <c>content/Content.mgcb</c>.
/// </summary>
public sealed class AssetManager : IDisposable
{
    private static readonly string[] TextureExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private readonly ContentManager _content;
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a manager. <paramref name="content"/> loads the built assets; <paramref name="sourceDirectory"/>
    /// is the folder with the source files and scene files (used to browse and to make paths relative).
    /// </summary>
    public AssetManager(ContentManager content, string sourceDirectory)
    {
        _content = content;
        RootDirectory = Path.GetFullPath(sourceDirectory);
    }

    /// <summary>Absolute path of the folder with source assets and scene files.</summary>
    public string RootDirectory { get; }

    /// <summary>True if the path has an image extension this manager can load.</summary>
    public static bool IsTexture(string path) =>
        TextureExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Converts a path to the stored form: forward slashes, no leading slash.</summary>
    public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>Returns the path of <paramref name="absolutePath"/> relative to the content folder, or <c>null</c> if outside it.</summary>
    public string? ToRelative(string absolutePath)
    {
        var relative = Path.GetRelativePath(RootDirectory, absolutePath);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? null
            : Normalize(relative);
    }

    /// <summary>Loads (and caches) a texture. Returns <c>null</c> and logs once if it was not built or cannot be loaded.</summary>
    public Texture2D? GetTexture(string relativePath)
    {
        if (_textures.TryGetValue(relativePath, out var cached)) return cached;

        Texture2D? texture = null;
        var assetName = Path.ChangeExtension(relativePath, null);
        try
        {
            texture = _content.Load<Texture2D>(assetName);
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not load texture '{relativePath}' (is it listed in content/Content.mgcb and built?): {ex.Message}");
        }
        _textures[relativePath] = texture;
        return texture;
    }

    /// <summary>Releases the loaded assets.</summary>
    public void Dispose()
    {
        _textures.Clear();
        _content.Unload();
    }

    /// <summary>
    /// Finds the content folder: an adjacent <c>Content</c> folder next to the executable, or the
    /// repository's <c>content</c> folder (the one holding <c>Content.mgcb</c>) found by walking up.
    /// </summary>
    /// <param name="preferSource">Look for the source folder first, so the editor edits the real files.</param>
    public static string? FindContentRoot(bool preferSource = false)
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "Content");
        if (!preferSource && Directory.Exists(beside)) return beside;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(candidate, "Content.mgcb"))) return candidate;
        }
        return Directory.Exists(beside) ? beside : null;
    }
}

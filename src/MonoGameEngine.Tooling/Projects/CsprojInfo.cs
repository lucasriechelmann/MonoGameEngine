using System.Xml.Linq;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>
/// What the editor needs to know about a project file, read from the XML without evaluating MSBuild.
/// <c>TargetFramework</c> is also looked up in <c>Directory.Build.props</c> files above the project.
/// </summary>
public sealed class CsprojInfo
{
    private static readonly string[] UnloadableSuffixes = ["-android", "-ios", "-tvos", "-maccatalyst", "-browser"];

    private CsprojInfo(string path) => Path = path;

    /// <summary>Full path of the project file.</summary>
    public string Path { get; }

    /// <summary>Project name (file name without extension).</summary>
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>Target frameworks, e.g. <c>net10.0</c>. Empty if none could be found.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; private set; } = [];

    /// <summary>The <c>OutputType</c> (<c>Exe</c>, <c>WinExe</c>, <c>Library</c>); empty means a library.</summary>
    public string OutputType { get; private set; } = "";

    /// <summary>True if the project references the Engine as a package or a project.</summary>
    public bool ReferencesEngine { get; private set; }

    /// <summary>Full path of the <c>Content.mgcb</c> the project builds, or null if it has none.</summary>
    public string? ContentReference { get; private set; }

    /// <summary>True if the project builds <c>Content.mgcb</c>.</summary>
    public bool HasContent => ContentReference != null;

    /// <summary>
    /// True if a desktop process can load the project's assembly: it targets a plain or Windows/Linux/macOS
    /// framework. Mobile heads (Android, iOS) cannot be loaded and can only be launched.
    /// </summary>
    public bool IsEditorLoadable =>
        TargetFrameworks.Any(tf => !UnloadableSuffixes.Any(s => tf.Contains(s, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Short text for the project dropdown, e.g. <c>net10.0</c>.</summary>
    public string FrameworkLabel => TargetFrameworks.Count == 0 ? "?" : string.Join(", ", TargetFrameworks);

    /// <summary>Reads a project file. Never throws; an unreadable file gives empty information.</summary>
    public static CsprojInfo Read(string csprojPath)
    {
        var info = new CsprojInfo(System.IO.Path.GetFullPath(csprojPath));
        try
        {
            var doc = XDocument.Load(info.Path);
            var frameworks = ReadFrameworks(doc);
            for (var dir = System.IO.Path.GetDirectoryName(info.Path); frameworks.Count == 0 && !string.IsNullOrEmpty(dir); dir = System.IO.Path.GetDirectoryName(dir))
            {
                var props = System.IO.Path.Combine(dir, "Directory.Build.props");
                if (File.Exists(props)) frameworks = ReadFrameworks(XDocument.Load(props));
            }
            info.TargetFrameworks = frameworks;
            info.OutputType = doc.Descendants("OutputType").Select(e => e.Value.Trim()).FirstOrDefault() ?? "";
            info.ReferencesEngine = doc.Descendants()
                .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference")
                .Any(e => ((string?)e.Attribute("Include") ?? "").Contains("MonoGameEngine.Engine", StringComparison.OrdinalIgnoreCase));
            var content = doc.Descendants("MonoGameContentReference").Select(e => (string?)e.Attribute("Include")).FirstOrDefault(i => i != null);
            if (content != null)
            {
                info.ContentReference = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(info.Path)!, content.Replace('\\', System.IO.Path.DirectorySeparatorChar)));
            }
        }
        catch (Exception)
        {
            // Unreadable project: leave the information empty so the editor can still list it.
        }
        return info;
    }

    private static List<string> ReadFrameworks(XDocument doc)
    {
        var list = new List<string>();
        foreach (var e in doc.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks"))
            list.AddRange(e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return list;
    }
}

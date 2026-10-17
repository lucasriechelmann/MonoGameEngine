using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>A C# project listed in a solution.</summary>
/// <param name="Name">Project name (file name without extension).</param>
/// <param name="AbsolutePath">Full path of the <c>.csproj</c>.</param>
public sealed record SolutionProject(string Name, string AbsolutePath);

/// <summary>Lists the C# projects of a <c>.sln</c> or <c>.slnx</c> file without needing MSBuild.</summary>
public static partial class SolutionReader
{
    [GeneratedRegex("^\\s*Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"([^\"]+\\.csproj)\"\\s*,", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SlnProject();

    /// <summary>True for a solution file extension this reader understands.</summary>
    public static bool IsSolution(string path) =>
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the <c>.csproj</c> projects of a solution, in file order. Missing project files are skipped.</summary>
    public static IReadOnlyList<SolutionProject> ReadProjects(string solutionPath)
    {
        var full = Path.GetFullPath(solutionPath);
        var dir = Path.GetDirectoryName(full)!;
        var relatives = full.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? ReadSlnx(full)
            : SlnProject().Matches(File.ReadAllText(full)).Select(m => m.Groups[1].Value);

        var result = new List<SolutionProject>();
        foreach (var relative in relatives)
        {
            var path = Path.GetFullPath(Path.Combine(dir, relative.Replace('\\', Path.DirectorySeparatorChar)));
            if (File.Exists(path)) result.Add(new SolutionProject(Path.GetFileNameWithoutExtension(path), path));
        }
        return result;
    }

    private static IEnumerable<string> ReadSlnx(string path) =>
        XDocument.Load(path).Descendants("Project")
            .Select(e => (string?)e.Attribute("Path"))
            .Where(p => p != null && p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))!;
}

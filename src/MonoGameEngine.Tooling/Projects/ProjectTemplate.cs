using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>A platform head a new project can include. Each is a thin launcher around the shared core project.</summary>
public enum TemplatePlatform
{
    /// <summary>Cross-platform desktop (OpenGL) head, <c>net10.0</c>.</summary>
    DesktopGL,

    /// <summary>Windows (DirectX) head, <c>net10.0-windows</c>.</summary>
    WindowsDX,
}

/// <summary>What the New Project dialog collects.</summary>
public sealed class TemplateOptions
{
    /// <summary>Project and solution name; also names the folder, the projects and (made identifier-safe) the namespace.</summary>
    public string Name { get; set; } = "MyGame";

    /// <summary>Folder the project folder is created in.</summary>
    public string ParentDirectory { get; set; } = "";

    /// <summary>Platform heads to create. The first one is the launch project.</summary>
    public List<TemplatePlatform> Platforms { get; set; } = [TemplatePlatform.DesktopGL];

    /// <summary>The Engine package (<c>MonoGameEngine.Engine.&lt;version&gt;.nupkg</c>) to copy into the project's local feed.</summary>
    public string EnginePackagePath { get; set; } = "";

    /// <summary>MonoGame version the generated projects reference.</summary>
    public string MonoGameVersion { get; set; } = "3.8.6";
}

/// <summary>
/// Creates a new game project from templates embedded in this assembly: a solution, a platform-neutral core project
/// that holds the components and the game class, one thin head per platform, a content folder with a sample scene,
/// a local NuGet feed holding the Engine package, and the <c>.mgeproj</c> that opens it in the editor.
/// </summary>
public static partial class ProjectTemplate
{
    private const string Prefix = "Templates/";
    private static readonly Regex PackageName = PackageNameRegex();
    private static readonly string[] BinaryExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    [GeneratedRegex(@"^MonoGameEngine\.Engine\.(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)\.nupkg$", RegexOptions.IgnoreCase)]
    private static partial Regex PackageNameRegex();

    /// <summary>Problems that stop the project from being created; empty when the options are valid.</summary>
    public static IReadOnlyList<string> Problems(TemplateOptions options)
    {
        var problems = new List<string>();
        if (!Regex.IsMatch(options.Name, "^[A-Za-z][A-Za-z0-9_.-]*$"))
            problems.Add("The name must start with a letter and use only letters, digits, '_', '-' and '.'.");
        if (options.Platforms.Count == 0) problems.Add("Choose at least one platform.");
        if (string.IsNullOrWhiteSpace(options.ParentDirectory)) problems.Add("Choose a folder.");
        else if (Directory.Exists(ProjectDirectory(options)) && Directory.EnumerateFileSystemEntries(ProjectDirectory(options)).Any())
            problems.Add($"The folder '{ProjectDirectory(options)}' already exists and is not empty.");
        if (EngineVersionOf(options.EnginePackagePath) == null || !File.Exists(options.EnginePackagePath))
            problems.Add("The Engine package (MonoGameEngine.Engine.<version>.nupkg) was not found next to the editor.");
        return problems;
    }

    /// <summary>Full path of the folder the project is created in.</summary>
    public static string ProjectDirectory(TemplateOptions options) => Path.GetFullPath(Path.Combine(options.ParentDirectory, options.Name));

    /// <summary>Reads the version out of an Engine package file name, or null if it is not one.</summary>
    public static string? EngineVersionOf(string packagePath)
    {
        var match = PackageName.Match(Path.GetFileName(packagePath));
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Finds the newest Engine package in a folder, or null.</summary>
    public static string? FindEnginePackage(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "MonoGameEngine.Engine.*.nupkg").Where(f => EngineVersionOf(f) != null).OrderDescending().FirstOrDefault()
            : null;

    /// <summary>Turns a project name into a valid C# namespace segment.</summary>
    public static string ToIdentifier(string name) => Regex.Replace(name, "[^A-Za-z0-9_]", "_");

    /// <summary>Creates the project and returns the path of its <c>.mgeproj</c>. Throws if the options have problems.</summary>
    public static string Create(TemplateOptions options)
    {
        var problems = Problems(options);
        if (problems.Count > 0) throw new InvalidOperationException(problems[0]);

        var root = ProjectDirectory(options);
        var version = EngineVersionOf(options.EnginePackagePath)!;
        var core = options.Name + ".Core";
        var heads = options.Platforms.Distinct().Select(p => options.Name + "." + p).ToList();

        var tokens = new Dictionary<string, string>
        {
            ["__NAME__"] = options.Name,
            ["__ID__"] = ToIdentifier(options.Name),
            ["__ENGINE_VERSION__"] = version,
            ["__MONOGAME_VERSION__"] = options.MonoGameVersion,
            ["__GUID_CAMERA__"] = Guid.NewGuid().ToString(),
            ["__GUID_CRATE__"] = Guid.NewGuid().ToString(),
        };

        Directory.CreateDirectory(root);
        WriteTemplates("Shared/", root, tokens);
        WriteTemplates("Core/", Path.Combine(root, core), tokens);
        foreach (var platform in options.Platforms.Distinct())
            WriteTemplates($"Head.{platform}/", Path.Combine(root, options.Name + "." + platform), tokens);

        var packages = Path.Combine(root, "packages");
        Directory.CreateDirectory(packages);
        File.Copy(options.EnginePackagePath, Path.Combine(packages, Path.GetFileName(options.EnginePackagePath)), overwrite: true);

        var projectPaths = new[] { $"{core}/{core}.csproj" }.Concat(heads.Select(h => $"{h}/{h}.csproj")).ToList();
        File.WriteAllText(Path.Combine(root, options.Name + ".sln"), BuildSolution(projectPaths));

        var file = new ProjectFile
        {
            FilePath = Path.Combine(root, options.Name + ProjectFile.Extension),
            Name = options.Name,
            Solution = options.Name + ".sln",
            CodeProject = projectPaths[0],
            LaunchProject = projectPaths[1],
            ContentRoot = "content",
            StartupScene = "Scenes/main.scene.json",
            EngineVersion = string.Join('.', version.Split('.').Take(2)),
        };
        file.Save();
        return file.FilePath;
    }

    private static void WriteTemplates(string folder, string targetRoot, Dictionary<string, string> tokens)
    {
        var assembly = typeof(ProjectTemplate).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Order())
        {
            var logical = resource.Replace('\\', '/');
            if (!logical.StartsWith(Prefix + folder, StringComparison.Ordinal)) continue;

            var relative = logical[(Prefix + folder).Length..];
            if (relative.EndsWith(".tmpl", StringComparison.Ordinal)) relative = relative[..^".tmpl".Length];
            relative = ReplaceTokens(relative, tokens);
            var target = Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var stream = assembly.GetManifestResourceStream(resource)!;
            if (BinaryExtensions.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase))
            {
                using var output = File.Create(target);
                stream.CopyTo(output);
            }
            else
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                File.WriteAllText(target, ReplaceTokens(reader.ReadToEnd(), tokens));
            }
        }
    }

    private static string ReplaceTokens(string text, Dictionary<string, string> tokens)
    {
        foreach (var (token, value) in tokens) text = text.Replace(token, value);
        return text;
    }

    /// <summary>Writes a classic <c>.sln</c> (readable by Visual Studio, Rider and the dotnet CLI) listing the projects.</summary>
    private static string BuildSolution(IReadOnlyList<string> projectPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        builder.AppendLine("# Visual Studio Version 17");
        builder.AppendLine("VisualStudioVersion = 17.0.31903.59");
        builder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        var guids = projectPaths.Select(_ => Guid.NewGuid().ToString().ToUpperInvariant()).ToList();
        for (int i = 0; i < projectPaths.Count; i++)
        {
            var name = Path.GetFileNameWithoutExtension(projectPaths[i]);
            builder.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{name}\", \"{projectPaths[i].Replace('/', '\\')}\", \"{{{guids[i]}}}\"");
            builder.AppendLine("EndProject");
        }

        builder.AppendLine("Global");
        builder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        builder.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        builder.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var guid in guids)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                builder.AppendLine($"\t\t{{{guid}}}.{configuration}|Any CPU.ActiveCfg = {configuration}|Any CPU");
                builder.AppendLine($"\t\t{{{guid}}}.{configuration}|Any CPU.Build.0 = {configuration}|Any CPU");
            }
        }
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("EndGlobal");
        return builder.ToString();
    }
}

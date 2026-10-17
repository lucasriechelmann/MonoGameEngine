using MonoGameEngine.Tooling.Projects;

namespace MonoGameEngine.Engine.Tests;

/// <summary>The New Project generator: files, token replacement, and that the result opens as a project again.</summary>
public sealed class TemplateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mge-template-" + Guid.NewGuid().ToString("N"));
    private readonly string _package;

    public TemplateTests()
    {
        Directory.CreateDirectory(_dir);
        _package = Path.Combine(_dir, "MonoGameEngine.Engine.0.1.0.nupkg");
        File.WriteAllText(_package, "fake package");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private TemplateOptions Options(string name = "My-Game", params TemplatePlatform[] platforms) => new()
    {
        Name = name,
        ParentDirectory = Path.Combine(_dir, "out"),
        EnginePackagePath = _package,
        Platforms = platforms.Length == 0 ? [TemplatePlatform.DesktopGL, TemplatePlatform.WindowsDX] : [.. platforms],
    };

    [Fact]
    public void Create_WritesSolutionCoreHeadsContentAndLocalFeed()
    {
        var mgeproj = ProjectTemplate.Create(Options());
        var root = Path.GetDirectoryName(mgeproj)!;

        foreach (var expected in new[]
                 {
                     "My-Game.sln", "My-Game.mgeproj", "nuget.config", ".gitignore", ".config/dotnet-tools.json",
                     "My-Game.Core/My-Game.Core.csproj", "My-Game.Core/GameApp.cs", "My-Game.Core/Spinner.cs",
                     "My-Game.DesktopGL/My-Game.DesktopGL.csproj", "My-Game.DesktopGL/Program.cs",
                     "My-Game.WindowsDX/My-Game.WindowsDX.csproj", "My-Game.WindowsDX/Program.cs",
                     "content/Content.mgcb", "content/Scenes/main.scene.json", "content/Textures/crate.png",
                     "packages/MonoGameEngine.Engine.0.1.0.nupkg",
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, expected)), expected + " is missing");
        }
    }

    [Fact]
    public void Create_ReplacesTokensAndUsesAnIdentifierSafeNamespace()
    {
        var root = Path.GetDirectoryName(ProjectTemplate.Create(Options()))!;

        var app = File.ReadAllText(Path.Combine(root, "My-Game.Core", "GameApp.cs"));
        Assert.Contains("namespace My_Game;", app);
        Assert.Contains("Window.Title = \"My-Game\";", app);

        var core = File.ReadAllText(Path.Combine(root, "My-Game.Core", "My-Game.Core.csproj"));
        Assert.Contains("Include=\"MonoGameEngine.Engine\" Version=\"0.1.0\"", core);

        var head = File.ReadAllText(Path.Combine(root, "My-Game.DesktopGL", "My-Game.DesktopGL.csproj"));
        Assert.Contains("..\\My-Game.Core\\My-Game.Core.csproj", head);
        Assert.Contains("MonoGame.Framework.DesktopGL\" Version=\"3.8.6\"", head);

        var scene = File.ReadAllText(Path.Combine(root, "content", "Scenes", "main.scene.json"));
        Assert.Contains("\"My_Game.Spinner\"", scene);
        Assert.DoesNotContain("__", scene);
        Assert.DoesNotContain("__", head);
    }

    [Fact]
    public void Create_CopiesTheSampleTextureByteForByte()
    {
        var root = Path.GetDirectoryName(ProjectTemplate.Create(Options()))!;

        var png = File.ReadAllBytes(Path.Combine(root, "content", "Textures", "crate.png"));
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]); // PNG signature survived
    }

    [Fact]
    public void Create_ProducesAProjectThatReopensWithCoreAsCodeAndFirstHeadAsLaunch()
    {
        var mgeproj = ProjectTemplate.Create(Options());

        var file = ProjectFile.Load(mgeproj);
        Assert.Equal("My-Game.Core/My-Game.Core.csproj", file.CodeProject);
        Assert.Equal("My-Game.DesktopGL/My-Game.DesktopGL.csproj", file.LaunchProject);
        Assert.Equal("0.1", file.EngineVersion);

        var setup = ProjectSetup.FromProjectFile(file);
        Assert.Equal(["My-Game.Core", "My-Game.DesktopGL", "My-Game.WindowsDX"], setup.Projects.Select(p => p.Name));
        Assert.Empty(setup.Problems());

        // The solution alone also guesses the same split, so deleting the .mgeproj loses nothing.
        var guessed = ProjectSetup.FromPath(Path.Combine(file.Directory, "My-Game.sln"));
        Assert.Equal("My-Game.Core", guessed.Projects[guessed.CodeIndex].Name);
        Assert.Equal("My-Game.DesktopGL", guessed.Projects[guessed.LaunchIndex].Name);
        Assert.Equal("content", guessed.ContentRoot);
    }

    [Fact]
    public void Create_OnlyWritesTheChosenPlatforms()
    {
        var root = Path.GetDirectoryName(ProjectTemplate.Create(Options("Solo", TemplatePlatform.WindowsDX)))!;

        Assert.True(Directory.Exists(Path.Combine(root, "Solo.WindowsDX")));
        Assert.False(Directory.Exists(Path.Combine(root, "Solo.DesktopGL")));
        Assert.Equal(["Solo.Core", "Solo.WindowsDX"], SolutionReader.ReadProjects(Path.Combine(root, "Solo.sln")).Select(p => p.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1Game")]
    [InlineData("My Game")]
    [InlineData("a/b")]
    public void Problems_RejectsBadNames(string name)
    {
        Assert.NotEmpty(ProjectTemplate.Problems(Options(name)));
    }

    [Fact]
    public void Problems_RejectsANonEmptyTargetFolderAndAMissingPackage()
    {
        var options = Options();
        Directory.CreateDirectory(ProjectTemplate.ProjectDirectory(options));
        File.WriteAllText(Path.Combine(ProjectTemplate.ProjectDirectory(options), "x.txt"), "");
        Assert.Contains(ProjectTemplate.Problems(options), p => p.Contains("not empty"));

        options.Name = "Other";
        options.EnginePackagePath = Path.Combine(_dir, "missing.nupkg");
        Assert.Contains(ProjectTemplate.Problems(options), p => p.Contains("Engine package"));
    }

    [Fact]
    public void FindEnginePackage_PicksTheNewestAndParsesVersions()
    {
        File.WriteAllText(Path.Combine(_dir, "MonoGameEngine.Engine.0.2.0.nupkg"), "");
        File.WriteAllText(Path.Combine(_dir, "Other.1.0.0.nupkg"), "");

        Assert.Equal("MonoGameEngine.Engine.0.2.0.nupkg", Path.GetFileName(ProjectTemplate.FindEnginePackage(_dir)));
        Assert.Equal("0.2.0", ProjectTemplate.EngineVersionOf("MonoGameEngine.Engine.0.2.0.nupkg"));
        Assert.Null(ProjectTemplate.EngineVersionOf("Other.1.0.0.nupkg"));
    }
}

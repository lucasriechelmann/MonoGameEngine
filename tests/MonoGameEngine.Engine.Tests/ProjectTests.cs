using MonoGameEngine.Tooling.Projects;

namespace MonoGameEngine.Engine.Tests;

/// <summary>Project file, solution and project-file reading, build output parsing and the game load context.</summary>
public sealed class ProjectTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mge-tests-" + Guid.NewGuid().ToString("N"));

    public ProjectTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ProjectFile_RoundTripsAndResolvesRelativePaths()
    {
        var path = Path.Combine(_dir, "game.mgeproj");
        new ProjectFile
        {
            FilePath = path,
            Name = "Demo",
            Solution = "Demo.sln",
            CodeProject = "src/Core/Core.csproj",
            LaunchProject = "src/Desktop/Desktop.csproj",
            ContentRoot = "content",
        }.Save();

        var loaded = ProjectFile.Load(path);

        Assert.Equal("Demo", loaded.Name);
        Assert.Equal("src/Desktop/Desktop.csproj", loaded.EffectiveLaunchProject);
        Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "src", "Core", "Core.csproj")), loaded.Resolve(loaded.CodeProject));
        Assert.Equal("src/Core/Core.csproj", loaded.MakeRelative(loaded.Resolve("src/Core/Core.csproj")));
    }

    [Fact]
    public void ProjectFile_LaunchProjectDefaultsToCodeProject()
    {
        var file = new ProjectFile { FilePath = Path.Combine(_dir, "a.mgeproj"), CodeProject = "Game/Game.csproj" };
        Assert.Equal("Game/Game.csproj", file.EffectiveLaunchProject);
    }

    [Fact]
    public void SolutionReader_ReadsSlnAndSkipsFoldersAndMissingFiles()
    {
        Write("A/A.csproj", "<Project />");
        Write("B/B.csproj", "<Project />");
        var sln = Write("Demo.sln", """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{827E0CD3-B72D-47B6-A68D-7590B98EB39B}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "A", "A\A.csproj", "{4D14B28E-D149-4317-822A-3EE03E072019}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "B", "B\B.csproj", "{881CF440-C87C-4840-91AF-913A7CCDF72D}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Gone", "Gone\Gone.csproj", "{04D961AD-EC3F-4BFE-8DAD-099D0C3FEF5F}"
            EndProject
            """);

        var projects = SolutionReader.ReadProjects(sln);

        Assert.Equal(["A", "B"], projects.Select(p => p.Name));
    }

    [Fact]
    public void SolutionReader_ReadsSlnx()
    {
        Write("src/Core/Core.csproj", "<Project />");
        var slnx = Write("Demo.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/Core/Core.csproj" />
              </Folder>
              <Project Path="docs/readme.md" />
            </Solution>
            """);

        var projects = SolutionReader.ReadProjects(slnx);

        var project = Assert.Single(projects);
        Assert.Equal("Core", project.Name);
    }

    [Fact]
    public void CsprojInfo_ReadsFrameworkEngineReferenceAndContent()
    {
        var path = Write("Game/Game.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>WinExe</OutputType></PropertyGroup>
              <ItemGroup>
                <MonoGameContentReference Include="..\content\Content.mgcb" />
                <PackageReference Include="MonoGameEngine.Engine" Version="0.1.0" />
              </ItemGroup>
            </Project>
            """);

        var info = CsprojInfo.Read(path);

        Assert.Equal(["net10.0"], info.TargetFrameworks);
        Assert.Equal("WinExe", info.OutputType);
        Assert.True(info.ReferencesEngine);
        Assert.True(info.HasContent);
        Assert.True(info.IsEditorLoadable);
    }

    [Fact]
    public void CsprojInfo_TakesFrameworkFromDirectoryBuildProps()
    {
        Write("Directory.Build.props", "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        var path = Write("Core/Core.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        Assert.Equal(["net9.0"], CsprojInfo.Read(path).TargetFrameworks);
    }

    [Theory]
    [InlineData("net10.0-android", false)]
    [InlineData("net10.0-ios", false)]
    [InlineData("net10.0-windows", true)]
    [InlineData("net10.0", true)]
    public void CsprojInfo_MobileHeadsAreNotEditorLoadable(string framework, bool loadable)
    {
        var path = Write("P/P.csproj", $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>{framework}</TargetFramework></PropertyGroup></Project>");

        Assert.Equal(loadable, CsprojInfo.Read(path).IsEditorLoadable);
    }

    private string MakeMultiPlatformSolution()
    {
        Write("content/Content.mgcb", "");
        Write("content/Scenes/level1.scene.json", "{}");
        Write("Game.Core/Game.Core.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="MonoGameEngine.Engine" Version="0.1.0" /></ItemGroup>
            </Project>
            """);
        Write("Game.Desktop/Game.Desktop.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>WinExe</OutputType></PropertyGroup>
              <ItemGroup>
                <MonoGameContentReference Include="..\content\Content.mgcb" />
                <ProjectReference Include="..\Game.Core\Game.Core.csproj" />
              </ItemGroup>
            </Project>
            """);
        Write("Game.Android/Game.Android.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0-android</TargetFramework></PropertyGroup>
              <ItemGroup><MonoGameContentReference Include="..\content\Content.mgcb" /></ItemGroup>
            </Project>
            """);
        return Write("Game.sln", """
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Core", "Game.Core\Game.Core.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Desktop", "Game.Desktop\Game.Desktop.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Android", "Game.Android\Game.Android.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            """);
    }

    [Fact]
    public void ProjectSetup_GuessesSharedCoreAsCodeAndADesktopHeadAsLaunch()
    {
        var setup = ProjectSetup.FromPath(MakeMultiPlatformSolution(), "0.1");

        Assert.Equal("Game.Core", setup.Projects[setup.CodeIndex].Name);
        Assert.Equal("Game.Desktop", setup.Projects[setup.LaunchIndex].Name);
        Assert.Equal("content", setup.ContentRoot);
        Assert.Equal("Scenes/level1.scene.json", setup.StartupScene);
        Assert.Empty(setup.Problems());

        var file = setup.ToProjectFile();
        Assert.Equal("Game.sln", file.Solution);
        Assert.Equal("Game.Core/Game.Core.csproj", file.CodeProject);
        Assert.Equal("Game.Desktop/Game.Desktop.csproj", file.LaunchProject);
        Assert.Equal("0.1", file.EngineVersion);
        Assert.Equal(Path.Combine(_dir, "Game.mgeproj"), file.FilePath);
    }

    [Fact]
    public void ProjectSetup_RefusesAMobileCodeProject()
    {
        var setup = ProjectSetup.FromPath(MakeMultiPlatformSolution());
        setup.CodeIndex = setup.Projects.ToList().FindIndex(p => p.Name == "Game.Android");

        Assert.Contains(setup.Problems(), p => p.Contains("cannot load"));
    }

    [Fact]
    public void ProjectSetup_SingleProjectUsesItAsCodeAndLaunch()
    {
        Write("content/Content.mgcb", "");
        var csproj = Write("Solo/Solo.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>WinExe</OutputType></PropertyGroup>
              <ItemGroup>
                <MonoGameContentReference Include="..\content\Content.mgcb" />
                <PackageReference Include="MonoGameEngine.Engine" Version="0.1.0" />
              </ItemGroup>
            </Project>
            """);

        var file = ProjectSetup.FromPath(csproj).ToProjectFile();

        Assert.Null(file.Solution);
        Assert.Equal("Solo.csproj", file.CodeProject);
        Assert.Null(file.LaunchProject);
        Assert.Equal("../content", file.ContentRoot);
    }

    [Fact]
    public void ProjectSetup_FromProjectFileKeepsTheSavedChoices()
    {
        var sln = MakeMultiPlatformSolution();
        var saved = ProjectSetup.FromPath(sln);
        saved.LaunchIndex = saved.Projects.ToList().FindIndex(p => p.Name == "Game.Android");
        saved.ToProjectFile().Save();

        var reopened = ProjectSetup.FromProjectFile(ProjectFile.Load(saved.ProjectFilePath));

        Assert.Equal("Game.Core", reopened.Projects[reopened.CodeIndex].Name);
        Assert.Equal("Game.Android", reopened.Projects[reopened.LaunchIndex].Name);
    }

    [Fact]
    public void ProjectBuilder_ParsesGetPropertyOutputAfterBuildNoise()
    {
        var output = "Build succeeded.\n{\n  \"Properties\": {\n    \"TargetPath\": \"C:\\\\x\\\\bin\\\\Game.dll\",\n    \"TargetDir\": \"C:\\\\x\\\\bin\\\\\"\n  }\n}\n";

        var (path, dir) = ProjectBuilder.ParseProperties(output);

        Assert.Equal(@"C:\x\bin\Game.dll", path);
        Assert.Equal(@"C:\x\bin\", dir);
    }

    [Fact]
    public void ProjectBuilder_ParsePropertiesToleratesGarbage()
    {
        Assert.Equal((null, null), ProjectBuilder.ParseProperties("error MSB1003"));
    }

    [Fact]
    public void GameAssemblyContext_LoadsACopyAndSharesAssembliesTheEditorAlreadyHas()
    {
        // A copy of this very assembly stands in for a game dll: the copy is a second, separate load, while
        // the Engine (already loaded by the host) must be the shared one, so Component stays the same type.
        var copy = Path.Combine(_dir, "GameCopy.dll");
        File.Copy(typeof(ProjectTests).Assembly.Location, copy);

        var context = new GameAssemblyContext(copy);
        var assembly = context.LoadMain();
        var copiedTestType = assembly.GetType(typeof(ProjectTests).FullName!)!;

        Assert.NotSame(typeof(ProjectTests).Assembly, assembly);
        Assert.NotSame(typeof(ProjectTests), copiedTestType);
        Assert.Same(typeof(Component), copiedTestType.Assembly.GetReferencedAssemblies()
            .Select(context.LoadFromAssemblyName)
            .First(a => a.GetName().Name == "MonoGameEngine.Engine").GetType(typeof(Component).FullName!));
        Assert.True(File.Exists(copy)); // loaded from memory: the file is not locked
        File.Delete(copy);
    }
}

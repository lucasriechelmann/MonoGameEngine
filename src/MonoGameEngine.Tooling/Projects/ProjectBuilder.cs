using System.Diagnostics;
using System.Text.Json;

namespace MonoGameEngine.Tooling.Projects;

/// <summary>Result of building a project with <c>dotnet build</c>.</summary>
/// <param name="Success">True if the build exited with code 0.</param>
/// <param name="TargetPath">Full path of the built assembly, or null if unknown.</param>
/// <param name="TargetDir">Folder holding the build output (the built <c>Content</c> folder sits inside it).</param>
/// <param name="Output">Combined build output, for the console.</param>
public sealed record BuildResult(bool Success, string? TargetPath, string? TargetDir, string Output);

/// <summary>Builds a project through the <c>dotnet</c> CLI and reports where the output went.</summary>
public static class ProjectBuilder
{
    /// <summary>Builds <paramref name="projectPath"/> (restoring packages) and returns its output location.</summary>
    public static async Task<BuildResult> BuildAsync(string projectPath, string configuration = "Debug", CancellationToken cancel = default)
    {
        // Not "-getProperty" here: with it MSBuild only evaluates the project, and neither restores nor builds.
        var (code, output) = await RunAsync(["build", projectPath, "-c", configuration, "--nologo", "-v", "q"], cancel);
        if (code != 0) return new BuildResult(false, null, null, output);

        var located = await LocateOutputAsync(projectPath, configuration, cancel);
        return new BuildResult(true, located?.TargetPath, located?.TargetDir, output);
    }

    /// <summary>Asks MSBuild where the project's output goes without building it. Returns null if it cannot be evaluated.</summary>
    public static async Task<BuildResult?> LocateOutputAsync(string projectPath, string configuration = "Debug", CancellationToken cancel = default)
    {
        var (code, output) = await RunAsync(
            ["msbuild", projectPath, $"-property:Configuration={configuration}", "-getProperty:TargetPath", "-getProperty:TargetDir"], cancel);
        var (path, dir) = ParseProperties(output);
        return code == 0 && path != null ? new BuildResult(true, path, dir, output) : null;
    }

    /// <summary>Extracts <c>TargetPath</c> and <c>TargetDir</c> from the JSON that <c>-getProperty</c> prints.</summary>
    public static (string? TargetPath, string? TargetDir) ParseProperties(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end < start) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(output[start..(end + 1)]);
            var props = doc.RootElement.GetProperty("Properties");
            string? Get(string name) => props.TryGetProperty(name, out var v) && v.GetString() is { Length: > 0 } s ? s : null;
            return (Get("TargetPath"), Get("TargetDir"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static async Task<(int Code, string Output)> RunAsync(string[] args, CancellationToken cancel)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancel);
        var stderr = process.StandardError.ReadToEndAsync(cancel);
        await process.WaitForExitAsync(cancel);
        return (process.ExitCode, await stdout + await stderr);
    }
}

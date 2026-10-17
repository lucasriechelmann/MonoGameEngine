using ImGuiNET;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>The "Console" window: engine log messages with level filtering.</summary>
public sealed class ConsolePanel(ConsoleLog log)
{
    private readonly bool[] _show = [true, true, true];
    private int _lastVersion;
    private bool _autoScroll = true;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        PanelLayout.Place("Console", 0.5f, 0.62f, 0.5f, 0.38f);
        bool visible = ImGui.Begin("Console");
        PanelLayout.Remember("Console");
        if (!visible)
        {
            ImGui.End();
            return;
        }

        if (ImGui.Button(Icons.Delete + " Clear")) log.Clear();
        ImGui.SameLine();
        FilterToggle("Info", LogLevel.Info);
        ImGui.SameLine();
        FilterToggle("Warnings", LogLevel.Warning);
        ImGui.SameLine();
        FilterToggle("Errors", LogLevel.Error);
        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);
        ImGui.Separator();

        ImGui.BeginChild("console_lines", Num.Vector2.Zero);
        foreach (var entry in log.Entries)
        {
            if (!_show[(int)entry.Level]) continue;
            ImGui.PushStyleColor(ImGuiCol.Text, LevelColor(entry.Level));
            ImGui.TextUnformatted($"[{entry.Time:HH:mm:ss}] {entry.Text}");
            ImGui.PopStyleColor();
        }
        if (_autoScroll && log.Version != _lastVersion) ImGui.SetScrollHereY(1f);
        _lastVersion = log.Version;
        ImGui.EndChild();
        ImGui.End();
    }

    private void FilterToggle(string label, LogLevel level)
    {
        ImGui.Checkbox($"{label} ({log.Count(level)})", ref _show[(int)level]);
    }

    private static Num.Vector4 LevelColor(LogLevel level) => level switch
    {
        LogLevel.Warning => new Num.Vector4(1f, 0.85f, 0.35f, 1f),
        LogLevel.Error => new Num.Vector4(1f, 0.45f, 0.4f, 1f),
        _ => new Num.Vector4(0.85f, 0.85f, 0.85f, 1f),
    };
}

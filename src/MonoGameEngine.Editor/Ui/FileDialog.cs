using ImGuiNET;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Serialization;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>A small ImGui dialog for choosing a scene file to open or a file name to save (no native dialogs, no dependencies).</summary>
public sealed class FileDialog
{
    private string _title = "";
    private bool _saving;
    private bool _openRequested;
    private string _directory = "";
    private string _fileName = "";
    private Action<string>? _accept;
    private string[] _extensions = [SceneSerializer.FileExtension];

    /// <summary>Asks the user to pick an existing scene file.</summary>
    public void ShowOpen(string startDirectory, Action<string> accept) =>
        Show("Open Scene", false, startDirectory, "", accept, [SceneSerializer.FileExtension]);

    /// <summary>Asks the user to pick an existing file with one of the given extensions (for example <c>.sln</c>).</summary>
    public void ShowOpenFile(string title, string startDirectory, string[] extensions, Action<string> accept) =>
        Show(title, false, startDirectory, "", accept, extensions);

    /// <summary>Asks the user for a file name to save a scene as.</summary>
    public void ShowSave(string startDirectory, string defaultName, Action<string> accept) =>
        Show("Save Scene As", true, startDirectory, defaultName, accept, [SceneSerializer.FileExtension]);

    /// <summary>Draws the dialog if it is open. Call every frame.</summary>
    public void Draw()
    {
        if (_openRequested)
        {
            ImGui.OpenPopup(_title);
            _openRequested = false;
        }

        ImGui.SetNextWindowSize(new Num.Vector2(520, 380), ImGuiCond.Appearing);
        bool open = true;
        if (!ImGui.BeginPopupModal(_title, ref open)) return;

        ImGui.TextUnformatted(_directory);
        if (ImGui.Button(Icons.ArrowUpward + " Up")) _directory = Directory.GetParent(_directory)?.FullName ?? _directory;
        ImGui.Separator();

        float footer = ImGui.GetFrameHeightWithSpacing() * (_saving ? 2 : 1) + 6;
        ImGui.BeginChild("file_list", new Num.Vector2(0, -footer));
        string? enter = null;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(_directory).Order())
            {
                if (ImGui.Selectable(Icons.Folder + "  " + Path.GetFileName(dir), false, ImGuiSelectableFlags.AllowDoubleClick)
                    && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) enter = dir;
            }
            foreach (var file in Directory.EnumerateFiles(_directory)
                         .Where(f => _extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))).Order())
            {
                var name = Path.GetFileName(file);
                if (!ImGui.Selectable(Icons.Layers + "  " + name + "##" + name, name == _fileName, ImGuiSelectableFlags.AllowDoubleClick)) continue;
                _fileName = name;
                if (!_saving && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) Accept();
            }
        }
        catch (Exception ex)
        {
            ImGui.TextDisabled(ex.Message);
        }
        ImGui.EndChild();
        if (enter != null) _directory = enter;

        if (_saving)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##filename", ref _fileName, 128);
        }
        if (ImGui.Button(_saving ? "Save" : "Open", new Num.Vector2(100, 0))) Accept();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Num.Vector2(100, 0)) || !open) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void Show(string title, bool saving, string startDirectory, string defaultName, Action<string> accept, string[] extensions)
    {
        _extensions = extensions;
        _title = title;
        _saving = saving;
        _directory = Directory.Exists(startDirectory) ? startDirectory : AppContext.BaseDirectory;
        _fileName = defaultName;
        _accept = accept;
        _openRequested = true;
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(_fileName)) return;
        var name = _fileName.Trim();
        if (_saving && !name.EndsWith(SceneSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
            name += SceneSerializer.FileExtension;
        var path = Path.Combine(_directory, name);
        if (!_saving && !File.Exists(path))
        {
            Log.Warning($"'{name}' does not exist.");
            return;
        }
        ImGui.CloseCurrentPopup();
        _accept?.Invoke(path);
    }
}

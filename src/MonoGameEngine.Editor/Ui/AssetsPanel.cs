using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>The "Assets" window: browses the content folder. Textures can be dragged into the Scene view or an asset field.</summary>
public sealed class AssetsPanel(EditorState state, AssetBrowser browser, ImGuiRenderer imgui, Action<string> openScene)
{
    private readonly Dictionary<string, IntPtr> _thumbnails = new();
    private float _thumbSize = 64f;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        Ui.PlaceFirstUse(0f, 0.62f, 0.5f, 0.38f);
        if (!ImGui.Begin("Assets"))
        {
            ImGui.End();
            return;
        }

        ImGui.BeginDisabled(browser.IsAtRoot);
        if (ImGui.Button("Up")) browser.Up();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) browser.Refresh();
        ImGui.SameLine();
        ImGui.TextDisabled("content/" + browser.CurrentRelative);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 110f + ImGui.GetCursorPosX());
        ImGui.SetNextItemWidth(110f);
        ImGui.SliderFloat("##thumb", ref _thumbSize, 32f, 128f, "%.0f px");
        ImGui.Separator();

        ImGui.BeginChild("assets_list", Num.Vector2.Zero);
        float cell = _thumbSize + ImGui.GetStyle().ItemSpacing.X + 8f;
        int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cell));
        int column = 0;

        string? enterDirectory = null;
        foreach (var entry in browser.Entries)
        {
            if (column > 0) ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.PushID(entry.FullPath);
            DrawEntry(entry, ref enterDirectory);
            ImGui.PopID();
            ImGui.EndGroup();
            column = (column + 1) % columns;
        }
        ImGui.EndChild();

        if (enterDirectory != null) browser.Enter(enterDirectory);
        ImGui.End();
    }

    private void DrawEntry(AssetEntry entry, ref string? enterDirectory)
    {
        var size = new Num.Vector2(_thumbSize, _thumbSize);
        bool doubleClicked = false;

        if (!entry.IsDirectory && AssetManager.IsTexture(entry.Name) && GetThumbnail(entry.RelativePath) is var id && id != IntPtr.Zero)
        {
            ImGui.ImageButton("##thumb", id, size);
            doubleClicked = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);

            if (ImGui.BeginDragDropSource())
            {
                state.DraggedAsset = entry.RelativePath;
                Ui.SetPayload(Ui.AssetPayload);
                ImGui.Image(id, new Num.Vector2(48, 48));
                ImGui.Text(entry.Name);
                ImGui.EndDragDropSource();
            }
        }
        else
        {
            string icon = entry.IsDirectory ? "[dir]" : entry.Name.EndsWith(SceneFileExtension, StringComparison.OrdinalIgnoreCase) ? "[scene]" : "[file]";
            ImGui.Button($"{icon}##icon", size);
            doubleClicked = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
        }

        if (doubleClicked)
        {
            if (entry.IsDirectory) enterDirectory = entry.FullPath;
            else if (entry.Name.EndsWith(SceneFileExtension, StringComparison.OrdinalIgnoreCase)) openScene(entry.FullPath);
        }

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + _thumbSize);
        ImGui.TextWrapped(entry.Name);
        ImGui.PopTextWrapPos();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.RelativePath);
    }

    private const string SceneFileExtension = Engine.Serialization.SceneSerializer.FileExtension;

    private IntPtr GetThumbnail(string relativePath)
    {
        if (_thumbnails.TryGetValue(relativePath, out var id)) return id;
        Texture2D? texture = state.Assets.GetTexture(relativePath);
        id = texture == null ? IntPtr.Zero : imgui.BindTexture(texture);
        _thumbnails[relativePath] = id;
        return id;
    }
}

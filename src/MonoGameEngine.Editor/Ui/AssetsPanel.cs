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
    private float _treeWidth = 190f;
    private string? _lastDirectory;
    private bool _revealCurrent;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        PanelLayout.Place("Assets", 0f, 0.62f, 0.5f, 0.38f);
        bool visible = ImGui.Begin("Assets");
        PanelLayout.Remember("Assets");
        if (!visible)
        {
            ImGui.End();
            return;
        }

        ImGui.BeginDisabled(browser.IsAtRoot);
        if (ImGui.Button(Icons.ArrowUpward + " Up")) browser.Up();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(Icons.Refresh + " Refresh")) browser.Refresh();
        ImGui.SameLine();
        ImGui.TextDisabled("content/" + browser.CurrentRelative);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 110f + ImGui.GetCursorPosX());
        ImGui.SetNextItemWidth(110f);
        ImGui.SliderFloat("##thumb", ref _thumbSize, 32f, 128f, "%.0f px");
        ImGui.Separator();

        // Folder tree on the left, with a draggable splitter, then the files of the selected folder.
        if (_lastDirectory != browser.CurrentDirectory)
        {
            _lastDirectory = browser.CurrentDirectory;
            _revealCurrent = true; // open the tree down to the folder (it may have changed from the grid or Up)
        }
        _treeWidth = Math.Clamp(_treeWidth, 120f, MathF.Max(120f, ImGui.GetContentRegionAvail().X - 160f));

        ImGui.BeginChild("assets_tree", new Num.Vector2(_treeWidth, 0));
        DrawFolderNode(browser.Root, "content", isRoot: true);
        ImGui.EndChild();
        _revealCurrent = false;

        ImGui.SameLine(0f, 2f);
        ImGui.InvisibleButton("##assets_splitter", new Num.Vector2(5f, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive()) _treeWidth += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin() + new Num.Vector2(2f, 0f),
                ImGui.GetItemRectMax() - new Num.Vector2(1f, 0f), Ui.Rgba(0.38f, 0.8f, 1f, 0.8f));
        ImGui.SameLine(0f, 2f);

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

    /// <summary>One folder in the tree. Clicking selects it; the arrow expands it.</summary>
    private void DrawFolderNode(string fullPath, string name, bool isRoot = false)
    {
        var subfolders = browser.GetSubfolders(fullPath);
        bool isCurrent = AssetBrowser.IsSameOrInside(fullPath, browser.CurrentDirectory)
                         && AssetBrowser.IsSameOrInside(browser.CurrentDirectory, fullPath);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (subfolders.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (isCurrent) flags |= ImGuiTreeNodeFlags.Selected;
        if (isRoot) ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        else if (_revealCurrent && AssetBrowser.IsSameOrInside(fullPath, browser.CurrentDirectory))
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);

        bool open = ImGui.TreeNodeEx($"{(isCurrent ? Icons.FolderOpen : Icons.Folder)}  {name}###tree_{fullPath}", flags);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !ImGui.IsItemToggledOpen()) browser.Enter(fullPath);

        if (open)
        {
            foreach (var folder in subfolders) DrawFolderNode(folder.FullPath, folder.Name);
            ImGui.TreePop();
        }
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
            bool isScene = entry.Name.EndsWith(SceneFileExtension, StringComparison.OrdinalIgnoreCase);
            string icon = entry.IsDirectory ? Icons.Folder : isScene ? Icons.Layers : Icons.File;
            var tint = entry.IsDirectory ? new Num.Vector4(1f, 0.78f, 0.28f, 1f)
                : isScene ? new Num.Vector4(0.38f, 0.8f, 1f, 1f)
                : new Num.Vector4(0.78f, 0.78f, 0.78f, 1f);

            if (ImGuiRenderer.LargeIcons is { } large) ImGui.PushFont(large);
            ImGui.PushStyleColor(ImGuiCol.Text, tint);
            ImGui.Button($"{icon}##icon", size);
            ImGui.PopStyleColor();
            if (ImGuiRenderer.LargeIcons != null) ImGui.PopFont();
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

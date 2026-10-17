using ImGuiNET;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Commands;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>The "Hierarchy" window: entity tree with select, rename, drag to reparent or reorder, and a context menu.</summary>
public sealed class HierarchyPanel(EditorState state)
{
    private Guid? _dragId;
    private Guid? _renameId;
    private string _renameText = "";
    private bool _openRenamePopup;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        PanelLayout.Place("Hierarchy", 0f, 0f, 0.18f, 0.62f);
        bool visible = ImGui.Begin("Hierarchy");
        PanelLayout.Remember("Hierarchy");
        if (!visible)
        {
            ImGui.End();
            return;
        }

        if (ImGui.Button(Icons.Add + " Create")) ImGui.OpenPopup("create_menu");
        if (ImGui.BeginPopup("create_menu"))
        {
            DrawCreateItems(null);
            ImGui.EndPopup();
        }
        ImGui.Separator();

        var roots = state.ActiveScene.Roots;
        for (int i = 0; i < roots.Count; i++)
        {
            DrawGap(null, i);
            DrawNode(roots[i]);
        }
        DrawGap(null, roots.Count);

        // Empty space: drop here to move to the root, click to deselect, right-click to create.
        var rest = ImGui.GetContentRegionAvail();
        if (rest.Y > 4)
        {
            ImGui.InvisibleButton("##rest", new Num.Vector2(MathF.Max(rest.X, 1f), rest.Y));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) state.Select(null);
            AcceptEntityDrop(null, roots.Count);
            if (ImGui.BeginPopupContextItem("rest_ctx"))
            {
                DrawCreateItems(null);
                ImGui.EndPopup();
            }
        }

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.GetIO().WantTextInput)
        {
            var selected = state.Selected;
            if (selected != null && ImGui.IsKeyPressed(ImGuiKey.Delete, false)) state.Delete(selected);
            if (selected != null && ImGui.IsKeyPressed(ImGuiKey.F2, false)) BeginRename(selected);
        }

        DrawRenamePopup();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _dragId = null;
        ImGui.End();
    }

    private void DrawNode(Entity entity)
    {
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen;
        if (entity.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (state.SelectedId == entity.Id) flags |= ImGuiTreeNodeFlags.Selected;

        bool dimmed = !entity.ActiveInHierarchy;
        if (dimmed) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        bool open = ImGui.TreeNodeEx($"{IconFor(entity)}  {entity.Name}###{entity.Id}", flags);
        if (dimmed) ImGui.PopStyleColor();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !ImGui.IsItemToggledOpen()) state.Select(entity);
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) BeginRename(entity);

        if (ImGui.BeginPopupContextItem())
        {
            state.Select(entity);
            DrawEntityMenu(entity);
            ImGui.EndPopup();
        }

        if (ImGui.BeginDragDropSource())
        {
            _dragId = entity.Id;
            Ui.SetPayload(Ui.EntityPayload);
            ImGui.Text(entity.Name);
            ImGui.EndDragDropSource();
        }
        AcceptEntityDrop(entity, entity.Children.Count);

        if (open)
        {
            var children = entity.Children;
            for (int i = 0; i < children.Count; i++)
            {
                DrawGap(entity, i);
                DrawNode(children[i]);
            }
            DrawGap(entity, children.Count);
            ImGui.TreePop();
        }
    }

    /// <summary>A thin drop target between rows, for reordering.</summary>
    private void DrawGap(Entity? parent, int index)
    {
        ImGui.PushID($"gap{parent?.Id}_{index}");
        ImGui.InvisibleButton("##gap", new Num.Vector2(MathF.Max(ImGui.GetContentRegionAvail().X, 1f), 3f));
        if (_dragId != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new Num.Vector2(min.X, (min.Y + max.Y) * 0.5f),
                new Num.Vector2(max.X, (min.Y + max.Y) * 0.5f), Ui.Rgba(0.35f, 0.65f, 1f), 2f);
        }
        AcceptEntityDrop(parent, index);
        ImGui.PopID();
    }

    private void AcceptEntityDrop(Entity? parent, int index)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        if (Ui.AcceptPayload(Ui.EntityPayload) && _dragId is { } id && state.ActiveScene.Find(id) is { } dragged)
        {
            int final = index;
            if (dragged.Parent == parent && dragged.SiblingIndex is var old and >= 0 && old < index) final--;
            state.Reparent(dragged, parent, final);
            _dragId = null;
        }
        ImGui.EndDragDropTarget();
    }

    private static string IconFor(Entity entity)
    {
        if (entity.GetComponent<Camera2D>() != null) return Icons.PhotoCamera;
        if (entity.GetComponent<SpriteRenderer>() != null) return Icons.Image;
        return entity.Children.Count > 0 ? Icons.Layers : Icons.CropFree;
    }

    private void DrawEntityMenu(Entity entity)
    {
        if (ImGui.MenuItem(Icons.Edit + "  Rename", "F2")) BeginRename(entity);
        if (ImGui.MenuItem(Icons.ContentCopy + "  Duplicate", "Ctrl+D")) state.Duplicate(entity);
        if (ImGui.MenuItem(Icons.Delete + "  Delete", "Del")) state.Delete(entity);
        ImGui.Separator();
        DrawCreateItems(entity);
    }

    private void DrawCreateItems(Entity? parent)
    {
        string suffix = parent == null ? "" : " (child)";
        if (ImGui.MenuItem("Create Empty" + suffix)) state.CreateEmpty(parent);
        if (ImGui.MenuItem("Create Sprite" + suffix)) state.CreateSprite("", parent);
        if (ImGui.MenuItem("Create Camera" + suffix)) state.CreateCamera(parent);
    }

    private void BeginRename(Entity entity)
    {
        _renameId = entity.Id;
        _renameText = entity.Name;
        _openRenamePopup = true;
    }

    private void DrawRenamePopup()
    {
        if (_openRenamePopup)
        {
            ImGui.OpenPopup("Rename Entity");
            _openRenamePopup = false;
        }
        if (!ImGui.BeginPopup("Rename Entity")) return;

        ImGui.SetKeyboardFocusHere();
        bool enter = ImGui.InputText("##rename", ref _renameText, 128, ImGuiInputTextFlags.EnterReturnsTrue);
        if (enter && _renameId is { } id && state.ActiveScene.Find(id) is { } entity && _renameText != entity.Name)
            state.Execute(SetPropertyCommand.For(entity, nameof(Entity.Name), entity.Name, _renameText));
        if (enter || ImGui.IsKeyPressed(ImGuiKey.Escape)) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
}

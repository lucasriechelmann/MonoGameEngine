using ImGuiNET;
using Microsoft.Xna.Framework;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Commands;
using MonoGameEngine.Engine.Reflection;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// The "Inspector" window. Widgets are chosen by reflection from each component's public properties and
/// the Engine inspector attributes. Edits apply live and become one undo command per edit (see <see cref="EditTracker"/>).
/// </summary>
public sealed class InspectorPanel(EditorState state, EditTracker tracker, AssetBrowser assets)
{
    private static readonly System.Reflection.PropertyInfo NameProperty = typeof(Entity).GetProperty(nameof(Entity.Name))!;
    private static readonly System.Reflection.PropertyInfo ActiveProperty = typeof(Entity).GetProperty(nameof(Entity.Active))!;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        Ui.PlaceFirstUse(0.74f, 0f, 0.26f, 0.62f);
        if (!ImGui.Begin("Inspector"))
        {
            ImGui.End();
            return;
        }

        var entity = state.Selected;
        if (entity == null)
        {
            ImGui.TextDisabled("Nothing selected.");
            ImGui.End();
            return;
        }

        DrawEntityHeader(entity);
        ImGui.Separator();

        // Copy: a component may be removed while drawing.
        var components = entity.Components.ToArray();
        foreach (var component in components) DrawComponent(entity, component);

        ImGui.Spacing();
        DrawAddComponent(entity);
        ImGui.End();
    }

    private void DrawEntityHeader(Entity entity)
    {
        bool active = entity.Active;
        if (ImGui.Checkbox("##active", ref active))
        {
            tracker.Changed(entity, ActiveProperty, entity.Active);
            entity.Active = active;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active");
        ImGui.SameLine();

        string name = entity.Name;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##name", ref name, 128))
        {
            tracker.Changed(entity, NameProperty, entity.Name);
            entity.Name = name;
        }
        ImGui.TextDisabled($"Id {entity.Id}");
    }

    private void DrawComponent(Entity entity, Component component)
    {
        var type = component.GetType();
        ImGui.PushID(component.GetHashCode());

        bool open = ImGui.CollapsingHeader(type.Name, ImGuiTreeNodeFlags.DefaultOpen);
        if (component is not Transform && ImGui.BeginPopupContextItem("component_menu"))
        {
            if (ImGui.MenuItem("Remove Component")) state.Execute(new RemoveComponentCommand(component));
            ImGui.EndPopup();
        }

        if (open)
        {
            DrawMember(component, MemberCache.GetMembers(typeof(Component)).Single(m => m.Name == nameof(Component.Enabled)));
            foreach (var member in MemberCache.GetMembers(type))
            {
                if (member.IsHidden) continue;
                DrawMember(component, member);
            }
        }
        ImGui.PopID();
    }

    private void DrawMember(object target, MemberMetadata member)
    {
        if (member.Header != null) ImGui.SeparatorText(member.Header);

        object? before = member.GetValue(target);
        object? value = before;
        ImGui.PushID(member.Name);
        ImGui.BeginDisabled(member.IsReadOnly);
        bool changed = DrawEditor(member, ref value);
        ImGui.EndDisabled();
        if (member.Tooltip != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(member.Tooltip);
        ImGui.PopID();

        if (changed && !member.IsReadOnly)
        {
            tracker.Changed(target, member.Property, before);
            member.SetValue(target, value);
        }
    }

    private bool DrawEditor(MemberMetadata member, ref object? value)
    {
        string label = member.Name;
        var type = member.Type;

        if (type == typeof(float))
        {
            float v = (float)value!;
            bool changed = member.Range is { } r
                ? ImGui.SliderFloat(label, ref v, (float)r.Min, (float)r.Max)
                : ImGui.DragFloat(label, ref v, 0.05f);
            value = v;
            return changed;
        }
        if (type == typeof(int))
        {
            int v = (int)value!;
            bool changed = member.Range is { } r
                ? ImGui.SliderInt(label, ref v, (int)r.Min, (int)r.Max)
                : ImGui.DragInt(label, ref v, 0.2f);
            value = v;
            return changed;
        }
        if (type == typeof(bool))
        {
            bool v = (bool)value!;
            bool changed = ImGui.Checkbox(label, ref v);
            value = v;
            return changed;
        }
        if (type == typeof(string))
        {
            string v = (string?)value ?? "";
            bool changed = member.AssetPath != null ? DrawAssetField(label, ref v) : ImGui.InputText(label, ref v, 512);
            value = v;
            return changed;
        }
        if (type == typeof(Vector2))
        {
            var v = ((Vector2)value!).ToNum();
            bool changed = member.Range is { } r
                ? ImGui.SliderFloat2(label, ref v, (float)r.Min, (float)r.Max)
                : ImGui.DragFloat2(label, ref v, 0.05f);
            value = v.ToXna();
            return changed;
        }
        if (type == typeof(Color))
        {
            var v = ((Color)value!).ToVector4();
            var num = new Num.Vector4(v.X, v.Y, v.Z, v.W);
            bool changed = ImGui.ColorEdit4(label, ref num);
            value = new Color(num.X, num.Y, num.Z, num.W);
            return changed;
        }
        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);
            int index = Array.IndexOf(values, value);
            bool changed = ImGui.Combo(label, ref index, names, names.Length);
            if (changed && index >= 0) value = values.GetValue(index);
            return changed;
        }

        ImGui.TextDisabled($"{label}: unsupported type {type.Name}");
        return false;
    }

    /// <summary>A text box for an asset path that also accepts a drop from the Assets panel and offers a picker.</summary>
    private bool DrawAssetField(string label, ref string path)
    {
        bool changed = false;
        float button = ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(ImGui.CalcItemWidth() - button - ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.InputText("##path", ref path, 512)) changed = true;

        if (ImGui.BeginDragDropTarget())
        {
            if (Ui.AcceptPayload(Ui.AssetPayload) && state.DraggedAsset is { } dropped && AssetManager.IsTexture(dropped))
            {
                path = dropped;
                changed = true;
            }
            ImGui.EndDragDropTarget();
        }

        ImGui.SameLine();
        if (ImGui.Button("...", new Num.Vector2(button, button))) ImGui.OpenPopup("pick_asset");
        if (ImGui.BeginPopup("pick_asset"))
        {
            ImGui.TextDisabled("Textures");
            ImGui.Separator();
            foreach (var texture in assets.GetAllTextures())
            {
                if (!ImGui.Selectable(texture, texture == path)) continue;
                path = texture;
                changed = true;
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        if (!string.IsNullOrEmpty(path) && state.Assets.GetTexture(path) == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(1f, 0.5f, 0.4f, 1f));
            ImGui.TextUnformatted("File not found");
            ImGui.PopStyleColor();
        }
        return changed;
    }

    private void DrawAddComponent(Entity entity)
    {
        if (ImGui.Button("Add Component", new Num.Vector2(-1, 0))) ImGui.OpenPopup("add_component");
        if (!ImGui.BeginPopup("add_component")) return;

        foreach (var type in ComponentRegistry.GetTypes())
        {
            if (type == typeof(Transform) || !ImGui.Selectable(type.Name)) continue;
            var component = entity.AddComponent(type);
            state.Record(new AddComponentCommand(component));
        }
        ImGui.EndPopup();
    }
}

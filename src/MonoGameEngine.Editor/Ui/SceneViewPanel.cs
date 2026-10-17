using ImGuiNET;
using Microsoft.Xna.Framework;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// The "Scene" window: shows the render target, handles pan (middle mouse), zoom (wheel), click to select,
/// the translate gizmo, and dropping textures from the Assets panel.
/// </summary>
public sealed class SceneViewPanel(EditorState state, EditorSettings settings, SceneViewRenderer renderer)
{
    private const float PickRadius = 14f;
    private const float PreviewCameraWidth = 1280f;
    private const float PreviewCameraHeight = 720f;

    private readonly TranslateGizmo _gizmo = new();
    private bool _panning;

    /// <summary>Draws the window.</summary>
    public void Draw()
    {
        PanelLayout.Place("Scene", 0.18f, 0f, 0.56f, 0.62f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
        bool visible = ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        PanelLayout.Remember("Scene");
        if (!visible)
        {
            ImGui.End();
            return;
        }

        DrawToolbar();

        var avail = ImGui.GetContentRegionAvail();
        var size = new Vector2(MathF.Max(avail.X, 16f), MathF.Max(avail.Y, 16f));
        var origin = ImGui.GetCursorScreenPos().ToXna();
        var texture = renderer.EnsureTarget((int)size.X, (int)size.Y);

        ImGui.Image(texture, size.ToNum());
        ImGui.SetCursorScreenPos(origin.ToNum());
        ImGui.InvisibleButton("##sceneview", size.ToNum(),
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        bool hovered = ImGui.IsItemHovered();

        HandleAssetDrop(origin, size);
        HandleInput(origin, size, hovered);
        DrawOverlay(origin, size);

        ImGui.End();
    }

    private void DrawToolbar()
    {
        ImGui.SetCursorPos(new Num.Vector2(6, ImGui.GetCursorPosY() + 4));
        bool grid = settings.ShowGrid;
        if (ImGui.Checkbox("Grid", ref grid)) settings.ShowGrid = grid;
        ImGui.SameLine();
        bool colliders = settings.ShowColliders;
        if (ImGui.Checkbox("Colliders", ref colliders)) settings.ShowColliders = colliders;
        ImGui.SameLine();
        if (ImGui.Button(Icons.Refresh + " Reset View")) state.ViewCamera.Reset();
        ImGui.SameLine();
        ImGui.TextDisabled($"{state.ViewCamera.Zoom * 100f:0}%  |  middle mouse: pan  |  wheel: zoom");
        ImGui.SetCursorPosX(0);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
    }

    private void HandleAssetDrop(Vector2 origin, Vector2 size)
    {
        if (!ImGui.BeginDragDropTarget()) return;
        if (Ui.AcceptPayload(Ui.AssetPayload) && state.DraggedAsset is { } path && AssetManager.IsTexture(path))
        {
            var world = state.ViewCamera.ScreenToWorld(ImGui.GetMousePos().ToXna() - origin, size);
            state.CreateSprite(path, null, world);
            state.DraggedAsset = null;
        }
        ImGui.EndDragDropTarget();
    }

    private void HandleInput(Vector2 origin, Vector2 size, bool hovered)
    {
        var camera = state.ViewCamera;
        var io = ImGui.GetIO();
        var mouse = ImGui.GetMousePos().ToXna();

        // Pan with the middle mouse button.
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) _panning = true;
        if (_panning)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
                camera.Center -= io.MouseDelta.ToXna() / camera.Zoom;
            else
                _panning = false;
        }

        // Zoom around the cursor with the wheel.
        if (hovered && io.MouseWheel != 0f)
            camera.ZoomAt(mouse - origin, size, MathF.Pow(1.1f, io.MouseWheel));

        // The gizmo gets first claim on the mouse, then clicking picks an entity.
        var selected = state.Selected;
        if (selected != null) _gizmo.Update(state, selected, origin, size, mouse, hovered && !_panning);

        if (hovered && !_panning && !_gizmo.IsHovering && !_gizmo.IsDragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var world = camera.ScreenToWorld(mouse - origin, size);
            state.Select(ScenePicker.Pick(state.ActiveScene, world, PickRadius / camera.Zoom));
        }

        if (hovered && selected != null && ImGui.IsKeyPressed(ImGuiKey.F, false) && !io.WantTextInput)
            camera.Center = selected.Transform.WorldPosition;

        if (hovered && selected != null && ImGui.IsKeyPressed(ImGuiKey.Delete, false) && !io.WantTextInput)
            state.Delete(selected);
    }

    private void DrawOverlay(Vector2 origin, Vector2 size)
    {
        var camera = state.ViewCamera;
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin.ToNum(), (origin + size).ToNum(), true);

        var selected = state.Selected;
        var entities = state.ActiveScene.Entities;
        if (settings.ShowColliders)
        {
            for (int i = 0; i < entities.Count; i++) DrawColliders(draw, camera, entities[i], origin, size);
        }
        for (int i = 0; i < entities.Count; i++)
        {
            var camComponent = entities[i].GetComponent<Camera2D>();
            if (camComponent != null && entities[i].ActiveInHierarchy)
                DrawCameraFrame(draw, camera, camComponent, entities[i] == selected, origin, size);
        }

        if (selected != null)
        {
            DrawSelectionOutline(draw, camera, selected, origin, size);
            _gizmo.Draw(draw, camera, selected, origin, size);
        }

        if (state.Mode != PlayMode.Edit)
        {
            string label = state.Mode == PlayMode.Playing ? "PLAYING" : "PAUSED";
            draw.AddText(new Num.Vector2(origin.X + 10, origin.Y + 8), Ui.Rgba(1f, 0.75f, 0.2f), label);
        }
        draw.PopClipRect();
    }

    /// <summary>Outlines the box and circle colliders of an entity (green, or orange for triggers).</summary>
    private static void DrawColliders(ImDrawListPtr draw, SceneViewCamera view, Entity entity, Vector2 origin, Vector2 size)
    {
        if (!entity.ActiveInHierarchy) return;
        var components = entity.Components;
        for (int i = 0; i < components.Count; i++)
        {
            if (components[i] is not Collider2D collider) continue;
            uint color = collider.IsTrigger ? Ui.Rgba(1f, 0.65f, 0.2f, 0.9f) : Ui.Rgba(0.3f, 0.9f, 0.4f, 0.9f);

            if (collider is BoxCollider2D box)
            {
                var corners = box.GetWorldCorners();
                for (int c = 0; c < 4; c++)
                {
                    var a = origin + view.WorldToScreen(corners[c], size);
                    var b = origin + view.WorldToScreen(corners[(c + 1) % 4], size);
                    draw.AddLine(a.ToNum(), b.ToNum(), color, 1.5f);
                }
            }
            else if (collider is CircleCollider2D circle)
            {
                var centre = origin + view.WorldToScreen(circle.GetWorldCenter(), size);
                draw.AddCircle(centre.ToNum(), circle.GetWorldRadius() * view.Zoom, color, 40, 1.5f);
            }
        }
    }

    private static void DrawSelectionOutline(ImDrawListPtr draw, SceneViewCamera camera, Entity entity, Vector2 origin, Vector2 size)
    {
        uint color = Ui.Rgba(0.35f, 0.65f, 1f);
        var corners = ScenePicker.GetSpriteCorners(entity);
        if (corners == null)
        {
            var c = origin + camera.WorldToScreen(entity.Transform.WorldPosition, size);
            draw.AddCircle(c.ToNum(), 9f, color, 24, 1.5f);
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var a = origin + camera.WorldToScreen(corners[i], size);
            var b = origin + camera.WorldToScreen(corners[(i + 1) % 4], size);
            draw.AddLine(a.ToNum(), b.ToNum(), color, 1.5f);
        }
    }

    private static void DrawCameraFrame(ImDrawListPtr draw, SceneViewCamera view, Camera2D cam, bool selected, Vector2 origin, Vector2 size)
    {
        var half = new Vector2(PreviewCameraWidth, PreviewCameraHeight) * 0.5f / cam.Zoom;
        var min = origin + view.WorldToScreen(cam.Position - half, size);
        var max = origin + view.WorldToScreen(cam.Position + half, size);
        draw.AddRect(min.ToNum(), max.ToNum(), Ui.Rgba(1f, 0.9f, 0.3f, selected ? 0.9f : 0.35f), 0f, ImDrawFlags.None, 1.5f);
    }
}

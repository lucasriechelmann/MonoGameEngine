using ImGuiNET;
using Microsoft.Xna.Framework;
using MonoGameEngine.Editor.State;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Commands;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// A 2D translate gizmo: drag the red arrow for X, the green arrow for Y or the square for both.
/// One drag becomes one undo command. Positions here are screen pixels unless noted.
/// </summary>
public sealed class TranslateGizmo
{
    private const float AxisLength = 72f;
    private const float HitWidth = 7f;
    private const float CenterHalf = 7f;

    private enum Handle { None, X, Y, Both }

    private Handle _hover;
    private Handle _active;
    private Vector2 _startMouseWorld;
    private Vector2 _startWorld;
    private Vector2 _startLocal;
    private Transform? _dragged;

    /// <summary>True while a handle is being dragged.</summary>
    public bool IsDragging => _active != Handle.None;

    /// <summary>True if the mouse is over a handle (so clicking must not pick another entity).</summary>
    public bool IsHovering => _hover != Handle.None;

    /// <summary>
    /// Handles hover, drag start, dragging and release for the selected entity.
    /// <paramref name="mouse"/> and <paramref name="origin"/> are screen pixels; <paramref name="size"/> is the view size.
    /// </summary>
    public void Update(EditorState state, Entity entity, Vector2 origin, Vector2 size, Vector2 mouse, bool viewHovered)
    {
        var camera = state.ViewCamera;
        var transform = entity.Transform;

        if (_active != Handle.None)
        {
            if (_dragged != transform)
            {
                _active = Handle.None; // selection changed mid drag
                return;
            }
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var delta = camera.ScreenToWorld(mouse - origin, size) - _startMouseWorld;
                if (_active == Handle.X) delta.Y = 0;
                else if (_active == Handle.Y) delta.X = 0;
                transform.WorldPosition = _startWorld + delta;
            }
            else
            {
                EndDrag(state, transform);
            }
            return;
        }

        _hover = viewHovered ? HitTest(camera, transform, origin, size, mouse) : Handle.None;
        if (_hover != Handle.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _active = _hover;
            _dragged = transform;
            _startMouseWorld = camera.ScreenToWorld(mouse - origin, size);
            _startWorld = transform.WorldPosition;
            _startLocal = transform.Position;
        }
    }

    /// <summary>Draws the gizmo for the entity into the current window's draw list.</summary>
    public void Draw(ImDrawListPtr draw, SceneViewCamera camera, Entity entity, Vector2 origin, Vector2 size)
    {
        var center = origin + camera.WorldToScreen(entity.Transform.WorldPosition, size);
        var active = _active != Handle.None ? _active : _hover;

        uint x = Ui.Rgba(0.95f, 0.25f, 0.25f, active is Handle.X ? 1f : 0.85f);
        uint y = Ui.Rgba(0.30f, 0.85f, 0.35f, active is Handle.Y ? 1f : 0.85f);
        uint both = Ui.Rgba(0.95f, 0.85f, 0.25f, active is Handle.Both ? 1f : 0.6f);

        var xEnd = center + new Vector2(AxisLength, 0);
        var yEnd = center + new Vector2(0, AxisLength);
        draw.AddLine(center.ToNum(), xEnd.ToNum(), x, 2.5f);
        draw.AddTriangleFilled((xEnd + new Vector2(10, 0)).ToNum(), (xEnd + new Vector2(0, -5)).ToNum(),
            (xEnd + new Vector2(0, 5)).ToNum(), x);
        draw.AddLine(center.ToNum(), yEnd.ToNum(), y, 2.5f);
        draw.AddTriangleFilled((yEnd + new Vector2(0, 10)).ToNum(), (yEnd + new Vector2(-5, 0)).ToNum(),
            (yEnd + new Vector2(5, 0)).ToNum(), y);
        draw.AddRectFilled((center - new Vector2(CenterHalf)).ToNum(), (center + new Vector2(CenterHalf)).ToNum(), both);
    }

    private static Handle HitTest(SceneViewCamera camera, Transform transform, Vector2 origin, Vector2 size, Vector2 mouse)
    {
        var center = origin + camera.WorldToScreen(transform.WorldPosition, size);
        var d = mouse - center;

        if (MathF.Abs(d.X) <= CenterHalf && MathF.Abs(d.Y) <= CenterHalf) return Handle.Both;
        if (d.X >= 0 && d.X <= AxisLength + 10 && MathF.Abs(d.Y) <= HitWidth) return Handle.X;
        if (d.Y >= 0 && d.Y <= AxisLength + 10 && MathF.Abs(d.X) <= HitWidth) return Handle.Y;
        return Handle.None;
    }

    private void EndDrag(EditorState state, Transform transform)
    {
        var end = transform.Position;
        if (end != _startLocal)
            state.Record(SetPropertyCommand.For(transform, nameof(Transform.Position), _startLocal, end));
        _active = Handle.None;
        _dragged = null;
    }
}

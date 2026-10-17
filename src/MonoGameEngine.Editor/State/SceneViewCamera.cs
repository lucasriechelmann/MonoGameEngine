using Microsoft.Xna.Framework;

namespace MonoGameEngine.Editor.State;

/// <summary>The editor's own camera for the Scene view: a world point at the centre and a zoom in pixels per unit.</summary>
public sealed class SceneViewCamera
{
    /// <summary>Smallest allowed zoom.</summary>
    public const float MinZoom = 0.05f;

    /// <summary>Largest allowed zoom.</summary>
    public const float MaxZoom = 50f;

    /// <summary>World point at the centre of the view.</summary>
    public Vector2 Center { get; set; }

    /// <summary>Pixels per world unit.</summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>View matrix for a render target of the given size.</summary>
    public Matrix GetViewMatrix(Point size) =>
        Matrix.CreateTranslation(-Center.X, -Center.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(size.X * 0.5f, size.Y * 0.5f, 0f);

    /// <summary>Converts a position inside the view (pixels from its top-left) to world space.</summary>
    public Vector2 ScreenToWorld(Vector2 local, Vector2 size) => Center + (local - size * 0.5f) / Zoom;

    /// <summary>Converts a world position to pixels from the top-left of the view.</summary>
    public Vector2 WorldToScreen(Vector2 world, Vector2 size) => (world - Center) * Zoom + size * 0.5f;

    /// <summary>Multiplies the zoom, keeping the world point under <paramref name="local"/> fixed.</summary>
    public void ZoomAt(Vector2 local, Vector2 size, float factor)
    {
        var before = ScreenToWorld(local, size);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Center += before - ScreenToWorld(local, size);
    }

    /// <summary>Back to the origin at 100%.</summary>
    public void Reset()
    {
        Center = Vector2.Zero;
        Zoom = 1f;
    }
}

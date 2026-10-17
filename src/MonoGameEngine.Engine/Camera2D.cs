using Microsoft.Xna.Framework;

namespace MonoGameEngine.Engine;

/// <summary>A 2D camera. <see cref="SceneRunner"/> draws through the first enabled camera it finds.</summary>
public sealed class Camera2D : Component
{
    /// <summary>The world point shown at the centre of the screen.</summary>
    [Tooltip("The world point shown at the centre of the screen.")]
    public Vector2 Position { get; set; }

    /// <summary>Pixels per world unit.</summary>
    [Range(0.05, 20), Tooltip("Pixels per world unit.")]
    public float Zoom { get; set; } = 1f;

    /// <summary>Builds the view matrix for a viewport of the given size.</summary>
    public Matrix GetViewMatrix(Point viewportSize) =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(viewportSize.X * 0.5f, viewportSize.Y * 0.5f, 0f);
}

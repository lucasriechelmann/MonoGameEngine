using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEngine.Engine;

/// <summary>Draws a texture at the entity's world transform.</summary>
public sealed class SpriteRenderer : Component
{
    /// <summary>Texture path relative to the content folder, with forward slashes.</summary>
    [AssetPath("texture"), Tooltip("Texture path relative to the content folder.")]
    public string TexturePath { get; set; } = "";

    /// <summary>Multiplied with the texture colour.</summary>
    [Tooltip("Colour multiplied with the texture.")]
    public Color Tint { get; set; } = Color.White;

    /// <summary>Pivot as a fraction of the texture size: (0,0) top-left, (0.5,0.5) centre.</summary>
    [Tooltip("Pivot as a fraction of the texture size. (0.5, 0.5) is the centre.")]
    public Vector2 Origin { get; set; } = new(0.5f, 0.5f);

    /// <summary>0 draws in front, 1 draws behind.</summary>
    [Range(0, 1), Tooltip("0 draws in front, 1 draws behind.")]
    public float LayerDepth { get; set; }

    /// <summary>The loaded texture, or <c>null</c> if the path is empty or the file is missing.</summary>
    public Texture2D? GetTexture() =>
        string.IsNullOrEmpty(TexturePath) ? null : Scene?.Assets?.GetTexture(TexturePath);

    /// <inheritdoc />
    protected internal override void OnDraw(SpriteBatch spriteBatch)
    {
        var texture = GetTexture();
        if (texture == null) return;

        var t = Entity.Transform;
        var origin = new Vector2(texture.Width * Origin.X, texture.Height * Origin.Y);
        spriteBatch.Draw(texture, t.WorldPosition, null, Tint, t.WorldRotation, origin, t.WorldScale,
            SpriteEffects.None, LayerDepth);
    }
}

using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;

namespace MonoGameEngine.Editor.Ui;

/// <summary>Finds the entity under a point in world space.</summary>
public static class ScenePicker
{
    /// <summary>
    /// Returns the front-most sprite containing <paramref name="world"/>; entities without a sprite are
    /// picked when within <paramref name="radius"/> of their position. Returns null if nothing is hit.
    /// </summary>
    public static Entity? Pick(Scene scene, Vector2 world, float radius)
    {
        Entity? best = null;
        float bestDepth = float.MaxValue;

        var entities = scene.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            var entity = entities[i];
            if (!entity.ActiveInHierarchy) continue;

            float depth;
            var sprite = entity.GetComponent<SpriteRenderer>();
            var texture = sprite?.GetTexture();
            if (sprite != null && texture != null)
            {
                var local = entity.Transform.InverseTransformPoint(world)
                            + new Vector2(texture.Width * sprite.Origin.X, texture.Height * sprite.Origin.Y);
                if (local.X < 0 || local.Y < 0 || local.X > texture.Width || local.Y > texture.Height) continue;
                depth = sprite.LayerDepth;
            }
            else if (Vector2.DistanceSquared(entity.Transform.WorldPosition, world) <= radius * radius)
            {
                depth = -1f; // markers are easy to lose under big sprites, so they win
            }
            else
            {
                continue;
            }

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = entity;
            }
        }
        return best;
    }

    /// <summary>The four world-space corners of a sprite entity, or null if it has no loaded texture.</summary>
    public static Vector2[]? GetSpriteCorners(Entity entity)
    {
        var sprite = entity.GetComponent<SpriteRenderer>();
        var texture = sprite?.GetTexture();
        if (sprite == null || texture == null) return null;

        var size = new Vector2(texture.Width, texture.Height);
        var origin = size * sprite.Origin;
        var t = entity.Transform;
        return
        [
            t.TransformPoint(-origin),
            t.TransformPoint(new Vector2(size.X, 0) - origin),
            t.TransformPoint(size - origin),
            t.TransformPoint(new Vector2(0, size.Y) - origin),
        ];
    }
}

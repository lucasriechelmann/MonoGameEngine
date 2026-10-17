using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace MonoGameEngine.Engine;

/// <summary>
/// Position, rotation and scale of an entity, relative to its parent (local) or the scene (world).
/// The world transform composes parent translation, rotation (radians, clockwise on screen) and scale
/// without shear. Currently 2D; the rotation and position can be widened to 3D later.
/// </summary>
public sealed class Transform : Component
{
    private Vector2 _position;
    private float _rotation;
    private Vector2 _scale = Vector2.One;

    private bool _dirty = true;
    private Vector2 _worldPosition;
    private float _worldRotation;
    private Vector2 _worldScale = Vector2.One;

    /// <summary>Local position relative to the parent.</summary>
    [Tooltip("Local position relative to the parent.")]
    public Vector2 Position
    {
        get => _position;
        set { if (_position != value) { _position = value; MarkDirty(); } }
    }

    /// <summary>Local rotation in radians.</summary>
    [Tooltip("Local rotation in radians.")]
    public float Rotation
    {
        get => _rotation;
        set { if (_rotation != value) { _rotation = value; MarkDirty(); } }
    }

    /// <summary>Local scale.</summary>
    [Tooltip("Local scale.")]
    public Vector2 Scale
    {
        get => _scale;
        set { if (_scale != value) { _scale = value; MarkDirty(); } }
    }

    /// <summary>World position. Setting it adjusts the local position.</summary>
    [JsonIgnore]
    public Vector2 WorldPosition
    {
        get { Refresh(); return _worldPosition; }
        set
        {
            var parent = ParentTransform;
            if (parent == null) { Position = value; return; }
            parent.Refresh();
            var offset = Rotate(value - parent._worldPosition, -parent._worldRotation);
            Position = SafeDivide(offset, parent._worldScale);
        }
    }

    /// <summary>World rotation in radians. Setting it adjusts the local rotation.</summary>
    [JsonIgnore]
    public float WorldRotation
    {
        get { Refresh(); return _worldRotation; }
        set
        {
            var parent = ParentTransform;
            if (parent == null) { Rotation = value; return; }
            parent.Refresh();
            Rotation = value - parent._worldRotation;
        }
    }

    /// <summary>World scale. Setting it adjusts the local scale.</summary>
    [JsonIgnore]
    public Vector2 WorldScale
    {
        get { Refresh(); return _worldScale; }
        set
        {
            var parent = ParentTransform;
            if (parent == null) { Scale = value; return; }
            parent.Refresh();
            Scale = SafeDivide(value, parent._worldScale);
        }
    }

    /// <summary>The matrix that maps local space to world space.</summary>
    [JsonIgnore]
    public Matrix WorldMatrix
    {
        get
        {
            Refresh();
            return Matrix.CreateScale(_worldScale.X, _worldScale.Y, 1f)
                 * Matrix.CreateRotationZ(_worldRotation)
                 * Matrix.CreateTranslation(_worldPosition.X, _worldPosition.Y, 0f);
        }
    }

    /// <summary>Converts a point from this transform's local space to world space.</summary>
    public Vector2 TransformPoint(Vector2 local)
    {
        Refresh();
        return _worldPosition + Rotate(local * _worldScale, _worldRotation);
    }

    /// <summary>Converts a world-space point into this transform's local space.</summary>
    public Vector2 InverseTransformPoint(Vector2 world)
    {
        Refresh();
        return SafeDivide(Rotate(world - _worldPosition, -_worldRotation), _worldScale);
    }

    private Transform? ParentTransform => Entity?.Parent?.Transform;

    internal void MarkDirty()
    {
        if (_dirty) return; // children are already dirty whenever a parent is dirty
        _dirty = true;
        var children = Entity?.Children;
        if (children == null) return;
        for (int i = 0; i < children.Count; i++) children[i].Transform.MarkDirty();
    }

    private void Refresh()
    {
        if (!_dirty) return;
        var parent = ParentTransform;
        if (parent == null)
        {
            _worldPosition = _position;
            _worldRotation = _rotation;
            _worldScale = _scale;
        }
        else
        {
            parent.Refresh();
            _worldScale = parent._worldScale * _scale;
            _worldRotation = parent._worldRotation + _rotation;
            _worldPosition = parent._worldPosition + Rotate(_position * parent._worldScale, parent._worldRotation);
        }
        _dirty = false;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private static Vector2 SafeDivide(Vector2 v, Vector2 d) =>
        new(d.X == 0f ? 0f : v.X / d.X, d.Y == 0f ? 0f : v.Y / d.Y);
}

using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;

namespace MonoGameEngine.Engine.Tests;

public class SceneAndTransformTests
{
    [Fact]
    public void CreateEntity_AddsToRootsAndLookup()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("A");

        Assert.Contains(e, scene.Roots);
        Assert.Same(e, scene.Find(e.Id));
        Assert.Same(scene, e.Scene);
        Assert.NotNull(e.Transform);
    }

    [Fact]
    public void SetParent_MovesEntityAndRejectsCycles()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B");
        var c = scene.CreateEntity("C", b);

        Assert.True(a.SetParent(b));
        Assert.Equal([c, a], b.Children);
        Assert.DoesNotContain(a, scene.Roots);

        Assert.False(b.SetParent(c));
        Assert.False(b.SetParent(b));
        Assert.Null(b.Parent);
    }

    [Fact]
    public void SetParent_IndexIsFinalPosition()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B");
        var c = scene.CreateEntity("C");

        c.SetParent(null, 0);

        Assert.Equal([c, a, b], scene.Roots);
    }

    [Fact]
    public void WorldTransform_ComposesParentTranslationRotationScale()
    {
        var scene = new Scene();
        var parent = scene.CreateEntity("P");
        parent.Transform.Position = new Vector2(10, 0);
        parent.Transform.Rotation = MathHelper.PiOver2;
        parent.Transform.Scale = new Vector2(2, 2);
        var child = scene.CreateEntity("C", parent);
        child.Transform.Position = new Vector2(1, 0);

        // Child offset (1,0) is scaled to (2,0) and rotated 90 degrees to (0,2).
        var world = child.Transform.WorldPosition;
        Assert.Equal(10f, world.X, 3);
        Assert.Equal(2f, world.Y, 3);
        Assert.Equal(MathHelper.PiOver2, child.Transform.WorldRotation, 3);
        Assert.Equal(2f, child.Transform.WorldScale.X, 3);
    }

    [Fact]
    public void ChangingParent_InvalidatesChildWorldCache()
    {
        var scene = new Scene();
        var parent = scene.CreateEntity("P");
        var child = scene.CreateEntity("C", parent);
        child.Transform.Position = new Vector2(1, 1);
        _ = child.Transform.WorldPosition; // fill the cache

        parent.Transform.Position = new Vector2(5, 5);

        Assert.Equal(new Vector2(6, 6), child.Transform.WorldPosition);
    }

    [Fact]
    public void SettingWorldPosition_RoundTripsThroughLocal()
    {
        var scene = new Scene();
        var parent = scene.CreateEntity("P");
        parent.Transform.Position = new Vector2(3, 4);
        parent.Transform.Rotation = 0.7f;
        parent.Transform.Scale = new Vector2(2, 3);
        var child = scene.CreateEntity("C", parent);

        child.Transform.WorldPosition = new Vector2(-8, 9);

        Assert.Equal(-8f, child.Transform.WorldPosition.X, 3);
        Assert.Equal(9f, child.Transform.WorldPosition.Y, 3);
    }

    [Fact]
    public void InverseTransformPoint_IsInverseOfTransformPoint()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");
        e.Transform.Position = new Vector2(7, -2);
        e.Transform.Rotation = 1.1f;
        e.Transform.Scale = new Vector2(2, 0.5f);

        var world = e.Transform.TransformPoint(new Vector2(3, 4));
        var local = e.Transform.InverseTransformPoint(world);

        Assert.Equal(3f, local.X, 3);
        Assert.Equal(4f, local.Y, 3);
    }

    [Fact]
    public void RemoveAndAdd_PreservesSubtreeAndIds()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B", a);

        Assert.True(scene.Remove(a));
        Assert.Null(scene.Find(b.Id));
        Assert.Null(b.Scene);

        Assert.True(scene.Add(a));
        Assert.Same(b, scene.Find(b.Id));
        Assert.Same(scene, b.Scene);
    }

    [Fact]
    public void TransformCannotBeRemovedOrDuplicated()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");

        Assert.False(e.RemoveComponent(e.Transform));
        Assert.Same(e.Transform, e.AddComponent<Transform>());
        Assert.Single(e.Components);
    }
}

using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Reflection;
using MonoGameEngine.Engine.Serialization;

namespace MonoGameEngine.Engine.Tests;

public class SerializationTests
{
    public enum Mode { Slow, Fast }

    public sealed class Mover : Component
    {
        [Range(0, 10), Tooltip("Speed.")]
        public float Speed { get; set; } = 3f;

        public int Count { get; set; }
        public bool Loop { get; set; }
        public string Label { get; set; } = "";
        public Mode Mode { get; set; }
        public Vector2 Target { get; set; }
        public Color Colour { get; set; } = Color.White;

        [HideInInspector]
        public int Secret { get; set; }

        public int ComputedOnly => Count * 2;
    }

    public SerializationTests() => ComponentRegistry.RegisterAssembly(typeof(SerializationTests).Assembly);

    private static Scene BuildSample()
    {
        var scene = new Scene { Name = "Sample" };
        var root = scene.CreateEntity("Root");
        root.Transform.Position = new Vector2(10.5f, -3f);
        root.Transform.Rotation = 0.25f;
        root.Transform.Scale = new Vector2(2, 3);

        var child = scene.CreateEntity("Child", root);
        child.Active = false;
        var sprite = child.AddComponent<SpriteRenderer>();
        sprite.TexturePath = "Textures/player.png";
        sprite.Tint = new Color(10, 20, 30, 40);
        sprite.Origin = new Vector2(0.25f, 0.75f);
        sprite.LayerDepth = 0.4f;

        var grandChild = scene.CreateEntity("Grandchild", child);
        var camera = grandChild.AddComponent<Camera2D>();
        camera.Position = new Vector2(5, 6);
        camera.Zoom = 2.5f;
        camera.Enabled = false;

        var mover = scene.CreateEntity("Mover").AddComponent<Mover>();
        mover.Speed = 7.5f;
        mover.Count = 4;
        mover.Loop = true;
        mover.Label = "hi \"there\"";
        mover.Mode = Mode.Fast;
        mover.Target = new Vector2(1, 2);
        mover.Colour = new Color(255, 0, 128, 255);
        mover.Secret = 9;
        return scene;
    }

    [Fact]
    public void RoundTrip_PreservesHierarchyIdsAndAllBuiltInComponents()
    {
        var original = BuildSample();

        var copy = SceneSerializer.Deserialize(SceneSerializer.Serialize(original));

        Assert.Equal("Sample", copy.Name);
        Assert.Equal(original.Entities.Count, copy.Entities.Count);
        foreach (var e in original.Entities)
        {
            var c = copy.Find(e.Id);
            Assert.NotNull(c);
            Assert.Equal(e.Name, c.Name);
            Assert.Equal(e.Active, c.Active);
            Assert.Equal(e.Parent?.Id, c.Parent?.Id);
            Assert.Equal(e.Children.Select(x => x.Id), c.Children.Select(x => x.Id));
            Assert.Equal(e.Components.Select(x => x.GetType()), c.Components.Select(x => x.GetType()));
        }

        var root = copy.Roots[0];
        Assert.Equal(new Vector2(10.5f, -3f), root.Transform.Position);
        Assert.Equal(0.25f, root.Transform.Rotation);
        Assert.Equal(new Vector2(2, 3), root.Transform.Scale);

        var sprite = root.Children[0].GetComponent<SpriteRenderer>()!;
        Assert.Equal("Textures/player.png", sprite.TexturePath);
        Assert.Equal(new Color(10, 20, 30, 40), sprite.Tint);
        Assert.Equal(new Vector2(0.25f, 0.75f), sprite.Origin);
        Assert.Equal(0.4f, sprite.LayerDepth);

        var camera = root.Children[0].Children[0].GetComponent<Camera2D>()!;
        Assert.Equal(new Vector2(5, 6), camera.Position);
        Assert.Equal(2.5f, camera.Zoom);
        Assert.False(camera.Enabled);
    }

    [Fact]
    public void RoundTrip_PreservesCustomComponentProperties()
    {
        var copy = SceneSerializer.Deserialize(SceneSerializer.Serialize(BuildSample()));

        var mover = copy.Roots.Single(e => e.Name == "Mover").GetComponent<Mover>()!;
        Assert.Equal(7.5f, mover.Speed);
        Assert.Equal(4, mover.Count);
        Assert.True(mover.Loop);
        Assert.Equal("hi \"there\"", mover.Label);
        Assert.Equal(Mode.Fast, mover.Mode);
        Assert.Equal(new Vector2(1, 2), mover.Target);
        Assert.Equal(new Color(255, 0, 128, 255), mover.Colour);
        Assert.Equal(9, mover.Secret); // hidden from the inspector, but still saved
    }

    [Fact]
    public void Serialize_IsStable()
    {
        var scene = BuildSample();
        var json = SceneSerializer.Serialize(scene);

        Assert.Equal(json, SceneSerializer.Serialize(SceneSerializer.Deserialize(json)));
    }

    [Fact]
    public void Serialize_WritesDiscriminatorAndSkipsComputedProperties()
    {
        var json = SceneSerializer.Serialize(BuildSample());

        Assert.Contains("\"$type\": \"MonoGameEngine.Engine.SpriteRenderer\"", json);
        Assert.DoesNotContain("ComputedOnly", json);
        Assert.DoesNotContain("WorldPosition", json);
        Assert.Contains("\"#0A141E28\"", json);
    }

    [Fact]
    public void Deserialize_SkipsUnknownComponentTypesAndBadValues()
    {
        const string json = """
        {
          "version": 1, "name": "X",
          "entities": [ {
            "id": "11111111-1111-1111-1111-111111111111", "name": "E", "active": true,
            "components": [
              { "$type": "Nope.DoesNotExist", "Foo": 1 },
              { "$type": "MonoGameEngine.Engine.Camera2D", "Zoom": "not a number", "Position": { "x": 1, "y": 2 } }
            ],
            "children": []
          } ]
        }
        """;

        var scene = SceneSerializer.Deserialize(json);

        var entity = scene.Roots.Single();
        Assert.Equal(2, entity.Components.Count); // Transform + Camera2D
        var camera = entity.GetComponent<Camera2D>()!;
        Assert.Equal(1f, camera.Zoom); // default kept
        Assert.Equal(new Vector2(1, 2), camera.Position);
    }

    [Fact]
    public void Clone_CopiesSubtreeWithNewIdsRightAfterOriginal()
    {
        var scene = BuildSample();
        var root = scene.Roots[0];
        int before = scene.Entities.Count;

        var copy = SceneSerializer.Clone(root);

        Assert.Equal(before + 3, scene.Entities.Count);
        Assert.NotEqual(root.Id, copy.Id);
        Assert.Equal(1, copy.SiblingIndex);
        Assert.Equal("Root (Copy)", copy.Name);
        Assert.Equal(root.Children.Count, copy.Children.Count);
        Assert.NotEqual(root.Children[0].Id, copy.Children[0].Id);
        Assert.Equal(root.Transform.Position, copy.Transform.Position);
    }

    [Fact]
    public void FileSaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scene-{Guid.NewGuid()}{SceneSerializer.FileExtension}");
        try
        {
            SceneSerializer.Save(BuildSample(), path);
            var loaded = SceneSerializer.Load(path);
            Assert.Equal(4, loaded.Entities.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MemberCache_ExposesInspectorAttributes()
    {
        var members = MemberCache.GetMembers(typeof(Mover));

        var speed = members.Single(m => m.Name == "Speed");
        Assert.Equal(10, speed.Range!.Max);
        Assert.Equal("Speed.", speed.Tooltip);
        Assert.True(members.Single(m => m.Name == "Secret").IsHidden);
        Assert.DoesNotContain(members, m => m.Name == "ComputedOnly");
        Assert.DoesNotContain(members, m => m.Name == "Entity");
        Assert.Equal("TexturePath",
            MemberCache.GetMembers(typeof(SpriteRenderer)).Single(m => m.AssetPath != null).Name);
    }

    [Fact]
    public void ComponentRegistry_FindsBuiltInAndRegisteredTypes()
    {
        var names = ComponentRegistry.GetTypes().Select(t => t.Name).ToList();

        Assert.Contains("SpriteRenderer", names);
        Assert.Contains("Camera2D", names);
        Assert.Contains("Mover", names);
        Assert.Same(typeof(Camera2D), ComponentRegistry.Find("MonoGameEngine.Engine.Camera2D"));
        Assert.Null(ComponentRegistry.Find("Nope"));
        Assert.IsType<JsonObject>(SceneSerializer.SerializeEntity(BuildSample().Roots[0]));
    }
}

public class SampleContentTests
{
    [Fact]
    public void MainScene_LoadsAndReferencesExistingTextures()
    {
        var root = AssetManager.FindContentRoot(preferSource: true);
        Assert.NotNull(root);

        var scene = SceneSerializer.Load(Path.Combine(root, "Scenes", "main" + SceneSerializer.FileExtension));

        Assert.NotEmpty(scene.Entities); // the scene is edited in the editor, so do not assert an exact count
        Assert.NotNull(scene.Entities.Single(e => e.GetComponent<Camera2D>() != null));
        foreach (var sprite in scene.Entities.Select(e => e.GetComponent<SpriteRenderer>()).Where(s => s != null))
            Assert.True(File.Exists(Path.Combine(root, sprite!.TexturePath)), sprite.TexturePath);
        Assert.Equal("Ball", scene.Entities.Single(e => e.Name == "Crate").Children.Single().Name);
    }
}

public class PhysicsDemoContentTests
{
    [Fact]
    public void PhysicsDemoScene_LoadsWithPhysicsAndAnimation()
    {
        var root = AssetManager.FindContentRoot(preferSource: true);
        Assert.NotNull(root);

        var scene = SceneSerializer.Load(Path.Combine(root, "Scenes", "physics_demo" + SceneSerializer.FileExtension));

        Assert.Equal(new Microsoft.Xna.Framework.Vector2(0, 980), scene.Gravity);
        Assert.Contains(scene.Entities, e => e.GetComponent<Rigidbody2D>() != null);
        Assert.Contains(scene.Entities, e => e.GetComponent<CircleCollider2D>() is { IsTrigger: true });
        var coin = scene.Entities.Single(e => e.GetComponent<SpriteAnimator>() != null);
        Assert.Equal(8, coin.GetComponent<SpriteAnimator>()!.FrameCount);
        Assert.True(File.Exists(Path.Combine(root, coin.GetComponent<SpriteRenderer>()!.TexturePath)));
    }
}

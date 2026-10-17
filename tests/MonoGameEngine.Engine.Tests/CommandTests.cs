using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Commands;
using MonoGameEngine.Engine.Serialization;

namespace MonoGameEngine.Engine.Tests;

public class CommandTests
{
    [Fact]
    public void SetProperty_UndoRedo()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");
        var history = new CommandHistory();

        history.Execute(SetPropertyCommand.For(e.Transform, nameof(Transform.Position), Vector2.Zero, new Vector2(4, 5)));
        Assert.Equal(new Vector2(4, 5), e.Transform.Position);

        history.Undo();
        Assert.Equal(Vector2.Zero, e.Transform.Position);

        history.Redo();
        Assert.Equal(new Vector2(4, 5), e.Transform.Position);
    }

    [Fact]
    public void Record_DoesNotApplyAgain_ButSupportsUndo()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");
        var history = new CommandHistory();
        e.Transform.Position = new Vector2(9, 9); // a gizmo drag already moved it

        history.Record(SetPropertyCommand.For(e.Transform, nameof(Transform.Position), Vector2.Zero, new Vector2(9, 9)));
        history.Undo();

        Assert.Equal(Vector2.Zero, e.Transform.Position);
    }

    [Fact]
    public void NewCommand_ClearsRedoStack()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");
        var history = new CommandHistory();
        history.Execute(SetPropertyCommand.For(e, nameof(Entity.Name), "E", "One"));
        history.Undo();
        Assert.True(history.CanRedo);

        history.Execute(SetPropertyCommand.For(e, nameof(Entity.Name), "E", "Two"));

        Assert.False(history.CanRedo);
        Assert.Equal("Two", e.Name);
    }

    [Fact]
    public void History_RespectsCapacity()
    {
        var e = new Scene().CreateEntity("E");
        var history = new CommandHistory(capacity: 2);
        for (int i = 0; i < 5; i++)
            history.Execute(SetPropertyCommand.For(e, nameof(Entity.Name), "a", $"n{i}"));

        history.Undo();
        history.Undo();

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void CreateAndDelete_UndoRedo()
    {
        var scene = new Scene();
        var history = new CommandHistory();
        var e = scene.CreateEntity("New");
        history.Record(new CreateEntityCommand(e));

        history.Undo();
        Assert.Null(scene.Find(e.Id));

        history.Redo();
        Assert.Same(e, scene.Find(e.Id));

        history.Execute(new DeleteEntityCommand(e));
        Assert.Empty(scene.Roots);

        history.Undo();
        Assert.Same(e, scene.Find(e.Id));
    }

    [Fact]
    public void Delete_UndoRestoresSubtreeAndPosition()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B");
        var c = scene.CreateEntity("C");
        var child = scene.CreateEntity("Child", b);
        var history = new CommandHistory();

        history.Execute(new DeleteEntityCommand(b));
        Assert.Equal([a, c], scene.Roots);
        Assert.Null(scene.Find(child.Id));

        history.Undo();

        Assert.Equal([a, b, c], scene.Roots);
        Assert.Same(child, scene.Find(child.Id));
        Assert.Same(b, child.Parent);
    }

    [Fact]
    public void Reparent_KeepsWorldPositionAndUndoRestoresLocal()
    {
        var scene = new Scene();
        var parent = scene.CreateEntity("P");
        parent.Transform.Position = new Vector2(100, 50);
        parent.Transform.Scale = new Vector2(2, 2);
        var e = scene.CreateEntity("E");
        e.Transform.Position = new Vector2(30, 40);
        var history = new CommandHistory();

        history.Execute(new ReparentEntityCommand(e, parent));

        Assert.Same(parent, e.Parent);
        Assert.Equal(30f, e.Transform.WorldPosition.X, 3);
        Assert.Equal(40f, e.Transform.WorldPosition.Y, 3);
        Assert.Equal(new Vector2(-35, -5), e.Transform.Position);

        history.Undo();
        Assert.Null(e.Parent);
        Assert.Equal(new Vector2(30, 40), e.Transform.Position);

        history.Redo();
        Assert.Same(parent, e.Parent);
        Assert.Equal(new Vector2(-35, -5), e.Transform.Position);
    }

    [Fact]
    public void Reparent_IntoOwnDescendant_IsIgnored()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B", a);
        var cmd = new ReparentEntityCommand(a, b);

        Assert.False(cmd.IsValid);
        cmd.Do();

        Assert.Null(a.Parent);
    }

    [Fact]
    public void ReorderWithinSameParent_UndoRestoresOrder()
    {
        var scene = new Scene();
        var a = scene.CreateEntity("A");
        var b = scene.CreateEntity("B");
        var c = scene.CreateEntity("C");
        var history = new CommandHistory();

        history.Execute(new ReparentEntityCommand(c, null, 0));
        Assert.Equal([c, a, b], scene.Roots);

        history.Undo();
        Assert.Equal([a, b, c], scene.Roots);
    }

    [Fact]
    public void AddAndRemoveComponent_UndoRedoKeepsValues()
    {
        var scene = new Scene();
        var e = scene.CreateEntity("E");
        var history = new CommandHistory();
        var sprite = e.AddComponent<SpriteRenderer>();
        sprite.TexturePath = "a.png";
        history.Record(new AddComponentCommand(sprite));

        history.Undo();
        Assert.Null(e.GetComponent<SpriteRenderer>());

        history.Redo();
        Assert.Same(sprite, e.GetComponent<SpriteRenderer>());

        history.Execute(new RemoveComponentCommand(sprite));
        Assert.Null(e.GetComponent<SpriteRenderer>());
        history.Undo();
        Assert.Equal("a.png", e.GetComponent<SpriteRenderer>()!.TexturePath);
    }

    [Fact]
    public void Snapshot_RestoresSceneExactly()
    {
        var scene = new Scene { Name = "S" };
        var e = scene.CreateEntity("E");
        e.Transform.Position = new Vector2(1, 2);
        e.AddComponent<SpriteRenderer>().TexturePath = "x.png";
        var snapshot = SceneSerializer.Serialize(scene);

        e.Transform.Position = new Vector2(99, 99);
        scene.CreateEntity("Spawned");
        var restored = SceneSerializer.Deserialize(snapshot);

        Assert.Equal(snapshot, SceneSerializer.Serialize(restored));
        Assert.Equal(new Vector2(1, 2), restored.Find(e.Id)!.Transform.Position);
    }

    [Fact]
    public void Changed_FiresOnEveryStackChange()
    {
        var e = new Scene().CreateEntity("E");
        var history = new CommandHistory();
        int count = 0;
        history.Changed += () => count++;

        history.Execute(SetPropertyCommand.For(e, nameof(Entity.Name), "E", "F"));
        history.Undo();
        history.Redo();
        history.Clear();

        Assert.Equal(4, count);
    }
}

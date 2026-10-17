using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;

namespace MonoGameEngine.Engine.Tests;

public class LifecycleTests
{
    private sealed class Recorder : Component
    {
        public List<string> Calls { get; } = [];

        protected override void OnAwake() => Calls.Add("awake");
        protected override void OnStart() => Calls.Add("start");
        protected override void OnUpdate(GameTime gameTime) => Calls.Add("update");
        protected override void OnDestroy() => Calls.Add("destroy");
    }

    [Fact]
    public void Lifecycle_RunsInOrder()
    {
        var scene = new Scene();
        var rec = scene.CreateEntity("E").AddComponent<Recorder>();
        var runner = new SceneRunner(scene);

        runner.Update(new GameTime());
        runner.Update(new GameTime());
        runner.Stop();

        Assert.Equal(["awake", "start", "update", "update", "destroy"], rec.Calls);
    }

    [Fact]
    public void AllAwakesRunBeforeAnyStart()
    {
        var scene = new Scene();
        var order = new List<string>();
        var a = scene.CreateEntity("A").AddComponent<Recorder>();
        var b = scene.CreateEntity("B").AddComponent<Recorder>();
        var runner = new SceneRunner(scene);

        runner.Start();

        Assert.Equal(["awake", "start"], a.Calls);
        Assert.Equal(["awake", "start"], b.Calls);
        Assert.Empty(order);
    }

    [Fact]
    public void InactiveEntity_DoesNotRunUntilActivated()
    {
        var scene = new Scene();
        var entity = scene.CreateEntity("E");
        entity.Active = false;
        var rec = entity.AddComponent<Recorder>();
        var runner = new SceneRunner(scene);

        runner.Update(new GameTime());
        Assert.Empty(rec.Calls);

        entity.Active = true;
        runner.Update(new GameTime());
        Assert.Equal(["awake", "start", "update"], rec.Calls);
    }

    [Fact]
    public void DisabledComponent_SkipsStartAndUpdate()
    {
        var scene = new Scene();
        var rec = scene.CreateEntity("E").AddComponent<Recorder>();
        rec.Enabled = false;
        var runner = new SceneRunner(scene);

        runner.Update(new GameTime());

        Assert.Equal(["awake"], rec.Calls);
    }

    [Fact]
    public void Destroy_RunsOnDestroyAndRemovesAtEndOfUpdate()
    {
        var scene = new Scene();
        var entity = scene.CreateEntity("E");
        var rec = entity.AddComponent<Recorder>();
        var runner = new SceneRunner(scene);
        runner.Update(new GameTime());

        entity.Destroy();
        Assert.Same(scene, entity.Scene); // still there until the update ends
        runner.Update(new GameTime());

        Assert.Null(entity.Scene);
        Assert.Contains("destroy", rec.Calls);
    }

    [Fact]
    public void EntityAddedWhileRunning_IsStartedOnNextUpdate()
    {
        var scene = new Scene();
        var runner = new SceneRunner(scene);
        runner.Update(new GameTime());

        var rec = scene.CreateEntity("Late").AddComponent<Recorder>();
        runner.Update(new GameTime());

        Assert.Equal(["awake", "start", "update"], rec.Calls);
    }

    [Fact]
    public void Stop_ThenStart_RunsLifecycleAgain()
    {
        var scene = new Scene();
        var rec = scene.CreateEntity("E").AddComponent<Recorder>();
        var runner = new SceneRunner(scene);

        runner.Start();
        runner.Stop();
        runner.Start();

        Assert.Equal(["awake", "start", "destroy", "awake", "start"], rec.Calls);
    }
}

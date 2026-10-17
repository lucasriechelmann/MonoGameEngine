using Microsoft.Xna.Framework;
using MonoGameEngine.Engine;
using MonoGameEngine.Engine.Serialization;

namespace MonoGameEngine.Engine.Tests;

public class AnimationTests
{
    private static SpriteAnimator NewAnimator(int frames = 4, bool loop = true, float fps = 10f, int start = 0)
    {
        var entity = new Scene().CreateEntity("Sprite");
        var animator = entity.AddComponent<SpriteAnimator>();
        animator.FrameWidth = 64;
        animator.FrameHeight = 64;
        animator.FrameCount = frames;
        animator.FramesPerSecond = fps;
        animator.Loop = loop;
        animator.StartFrame = start;
        return animator;
    }

    [Fact]
    public void EditorShowsStartFrame_BeforeTheSceneRuns()
    {
        var animator = NewAnimator(start: 2);

        Assert.Equal(new Rectangle(128, 0, 64, 64), animator.GetSourceRectangle(256, 64));
    }

    [Fact]
    public void Advance_StepsThroughFramesAndLoops()
    {
        var animator = NewAnimator(frames: 4, fps: 10f);
        animator.Restart();

        animator.Advance(0.05f);
        Assert.Equal(0, animator.CurrentFrame);
        animator.Advance(0.1f); // 0.15 s
        Assert.Equal(1, animator.CurrentFrame);
        animator.Advance(0.3f); // 0.45 s
        Assert.Equal(0, animator.CurrentFrame); // 4 frames at 10 fps: wraps at 0.4 s
    }

    [Fact]
    public void Advance_WithoutLoop_StopsOnLastFrame()
    {
        var animator = NewAnimator(frames: 3, loop: false, fps: 10f);
        animator.Restart();

        animator.Advance(5f);

        Assert.Equal(2, animator.CurrentFrame);
        Assert.False(animator.IsPlaying);
    }

    [Fact]
    public void StartFrame_OffsetsTheAnimation()
    {
        var animator = NewAnimator(frames: 2, fps: 10f, start: 4);
        animator.Restart();

        animator.Advance(0.15f);

        Assert.Equal(5, animator.CurrentFrame);
    }

    [Fact]
    public void SourceRectangle_WrapsToNextRowOfTheSheet()
    {
        var scene = new Scene();
        var animator = scene.CreateEntity("S").AddComponent<SpriteAnimator>();
        animator.FrameWidth = 64;
        animator.FrameHeight = 64;
        animator.FrameCount = 4;
        animator.StartFrame = 3;

        // A 128x128 sheet has two columns, so frame 3 is the bottom-right cell.
        Assert.Equal(new Rectangle(64, 64, 64, 64), animator.GetSourceRectangle(128, 128));
    }

    [Fact]
    public void SourceRectangle_OutsideTheTexture_FallsBackToWholeTexture()
    {
        var animator = NewAnimator(start: 50);

        Assert.Equal(new Rectangle(0, 0, 128, 64), animator.GetSourceRectangle(128, 64));
    }

    [Fact]
    public void PlayOnStart_False_KeepsTheFirstFrame()
    {
        var scene = new Scene();
        var animator = scene.CreateEntity("S").AddComponent<SpriteAnimator>();
        animator.PlayOnStart = false;
        animator.FrameCount = 4;
        var runner = new SceneRunner(scene);

        for (int i = 0; i < 30; i++) runner.Update(PhysicsTests.Step);

        Assert.Equal(0, animator.CurrentFrame);
        Assert.False(animator.IsPlaying);
    }

    [Fact]
    public void RunningScene_PlaysTheAnimation()
    {
        var scene = new Scene();
        var animator = scene.CreateEntity("S").AddComponent<SpriteAnimator>();
        animator.FrameCount = 4;
        animator.FramesPerSecond = 60f;
        var runner = new SceneRunner(scene);

        for (int i = 0; i < 2; i++) runner.Update(PhysicsTests.Step);

        Assert.True(animator.IsPlaying);
        Assert.True(animator.CurrentFrame > 0);
    }
}

public class PhysicsTests
{
    /// <summary>One 60 Hz frame.</summary>
    public static GameTime Step { get; } = new(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));

    private sealed class ContactRecorder : Component
    {
        public List<string> Events { get; } = [];

        protected override void OnCollisionEnter(Entity other) => Events.Add("collision:" + other.Name);
        protected override void OnTriggerEnter(Entity other) => Events.Add("trigger:" + other.Name);
    }

    public PhysicsTests() => Engine.Reflection.ComponentRegistry.RegisterAssembly(typeof(PhysicsTests).Assembly);

    private static Entity AddGround(Scene scene, float y = 300f)
    {
        var ground = scene.CreateEntity("Ground");
        ground.Transform.Position = new Vector2(0, y);
        ground.AddComponent<BoxCollider2D>().Size = new Vector2(2000, 50);
        return ground;
    }

    private static Entity AddBox(Scene scene, string name = "Box", float y = 0f, float size = 50f)
    {
        var box = scene.CreateEntity(name);
        box.Transform.Position = new Vector2(0, y);
        box.AddComponent<Rigidbody2D>();
        box.AddComponent<BoxCollider2D>().Size = new Vector2(size, size);
        return box;
    }

    private static void Run(SceneRunner runner, int frames)
    {
        for (int i = 0; i < frames; i++) runner.Update(Step);
    }

    [Fact]
    public void DynamicBox_FallsAndRestsOnStaticGround()
    {
        var scene = new Scene();
        AddGround(scene); // top surface at y = 275
        var box = AddBox(scene);
        var runner = new SceneRunner(scene);

        Run(runner, 240);

        // The box is 50 tall, so its centre rests 25 above the ground top.
        Assert.InRange(box.Transform.WorldPosition.Y, 245f, 252f);
        Assert.InRange(box.Transform.WorldPosition.X, -2f, 2f);
    }

    [Fact]
    public void WithoutGround_TheBoxKeepsFalling()
    {
        var scene = new Scene();
        var box = AddBox(scene);
        var runner = new SceneRunner(scene);

        Run(runner, 60);

        // About half of g*t^2 after one second.
        Assert.InRange(box.Transform.WorldPosition.Y, 400f, 500f);
    }

    [Fact]
    public void SceneGravity_IsUsed()
    {
        var scene = new Scene { Gravity = new Vector2(500f, 0f) };
        var box = AddBox(scene);
        var runner = new SceneRunner(scene);

        Run(runner, 60);

        Assert.True(box.Transform.WorldPosition.X > 150f);
        Assert.InRange(box.Transform.WorldPosition.Y, -1f, 1f);
    }

    [Fact]
    public void GravityScaleZero_FloatsInPlace()
    {
        var scene = new Scene();
        var box = AddBox(scene);
        box.GetComponent<Rigidbody2D>()!.GravityScale = 0f;
        var runner = new SceneRunner(scene);

        Run(runner, 60);

        Assert.InRange(box.Transform.WorldPosition.Y, -1f, 1f);
    }

    [Fact]
    public void BouncyBall_ReversesDirection()
    {
        var scene = new Scene();
        AddGround(scene);
        var ball = scene.CreateEntity("Ball");
        ball.Transform.Position = new Vector2(0, 100);
        var body = ball.AddComponent<Rigidbody2D>();
        var circle = ball.AddComponent<CircleCollider2D>();
        circle.Radius = 20f;
        circle.Bounciness = 0.9f;
        var runner = new SceneRunner(scene);

        float fastestUp = 0f;
        for (int i = 0; i < 200; i++)
        {
            runner.Update(Step);
            fastestUp = MathF.Min(fastestUp, body.LinearVelocity.Y); // up is negative
        }

        Assert.True(fastestUp < -200f, $"expected the ball to bounce upward, fastest upward velocity was {fastestUp}");
    }

    [Fact]
    public void KinematicBody_MovesByVelocity()
    {
        var scene = new Scene();
        var entity = scene.CreateEntity("Mover");
        var body = entity.AddComponent<Rigidbody2D>();
        body.BodyType = RigidbodyType2D.Kinematic;
        entity.AddComponent<BoxCollider2D>().Size = new Vector2(20, 20);
        var runner = new SceneRunner(scene);
        runner.Start();

        body.LinearVelocity = new Vector2(100f, 0f);
        Run(runner, 60);

        Assert.InRange(entity.Transform.WorldPosition.X, 95f, 105f);
        Assert.InRange(entity.Transform.WorldPosition.Y, -1f, 1f);
    }

    [Fact]
    public void ColliderWithoutRigidbody_IsStaticAndBlocks()
    {
        var scene = new Scene();
        var wall = scene.CreateEntity("Wall");
        wall.Transform.Position = new Vector2(0, 200);
        wall.AddComponent<BoxCollider2D>().Size = new Vector2(400, 20);
        var box = AddBox(scene);
        var runner = new SceneRunner(scene);

        Run(runner, 240);

        Assert.InRange(wall.Transform.WorldPosition.Y, 199.9f, 200.1f);
        Assert.InRange(box.Transform.WorldPosition.Y, 160f, 167f); // stops on top of the wall
    }

    [Fact]
    public void CollisionAndTriggerEvents_AreDelivered()
    {
        var scene = new Scene();
        var ground = AddGround(scene);
        var groundEvents = ground.AddComponent<ContactRecorder>();

        var zone = scene.CreateEntity("Zone");
        zone.Transform.Position = new Vector2(0, 150);
        var zoneCollider = zone.AddComponent<BoxCollider2D>();
        zoneCollider.Size = new Vector2(200, 40);
        zoneCollider.IsTrigger = true;
        var zoneEvents = zone.AddComponent<ContactRecorder>();

        var box = AddBox(scene);
        var boxEvents = box.AddComponent<ContactRecorder>();
        var runner = new SceneRunner(scene);

        Run(runner, 240);

        Assert.Contains("trigger:Zone", boxEvents.Events);
        Assert.Contains("collision:Ground", boxEvents.Events);
        Assert.Contains("trigger:Box", zoneEvents.Events);
        Assert.Contains("collision:Box", groundEvents.Events);
        Assert.InRange(box.Transform.WorldPosition.Y, 245f, 252f); // the trigger did not block the box
    }

    [Fact]
    public void ScaleChangesTheShape()
    {
        var scene = new Scene();
        AddGround(scene);
        var box = AddBox(scene, size: 50f);
        box.Transform.Scale = new Vector2(2f, 2f); // now 100 tall
        var runner = new SceneRunner(scene);

        Run(runner, 240);

        Assert.InRange(box.Transform.WorldPosition.Y, 220f, 227f); // 275 - 50
    }

    [Fact]
    public void Stop_ReleasesBodies_AndTheSceneCanRunAgain()
    {
        var scene = new Scene();
        AddGround(scene);
        var box = AddBox(scene);
        var body = box.GetComponent<Rigidbody2D>()!;
        var runner = new SceneRunner(scene);
        Run(runner, 30);

        runner.Stop();
        Assert.Equal(Vector2.Zero, body.LinearVelocity);

        box.Transform.Position = new Vector2(0, 0);
        Run(runner, 240);
        Assert.InRange(box.Transform.WorldPosition.Y, 245f, 252f);
    }

    [Fact]
    public void DestroyedEntity_LeavesTheSimulation()
    {
        var scene = new Scene();
        AddGround(scene);
        var box = AddBox(scene);
        var runner = new SceneRunner(scene);
        Run(runner, 10);

        box.Destroy();
        Run(runner, 5);

        Assert.Null(box.Scene);
    }

    [Fact]
    public void PhysicsComponentsAndGravity_RoundTripThroughJson()
    {
        var scene = new Scene { Gravity = new Vector2(10f, 20f) };
        var e = scene.CreateEntity("Body");
        var rb = e.AddComponent<Rigidbody2D>();
        rb.BodyType = RigidbodyType2D.Kinematic;
        rb.GravityScale = 0.5f;
        rb.LinearDamping = 1.5f;
        rb.FixedRotation = true;
        var box = e.AddComponent<BoxCollider2D>();
        box.Size = new Vector2(30, 40);
        box.Offset = new Vector2(1, 2);
        box.IsTrigger = true;
        box.Bounciness = 0.25f;
        var circle = e.AddComponent<CircleCollider2D>();
        circle.Radius = 12f;
        var anim = e.AddComponent<SpriteAnimator>();
        anim.FrameWidth = 32;
        anim.FrameCount = 6;
        anim.FramesPerSecond = 12f;
        anim.Loop = false;
        anim.PlayOnStart = false;

        var copy = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene));

        Assert.Equal(new Vector2(10f, 20f), copy.Gravity);
        var copied = copy.Roots.Single();
        var rb2 = copied.GetComponent<Rigidbody2D>()!;
        Assert.Equal(RigidbodyType2D.Kinematic, rb2.BodyType);
        Assert.Equal(0.5f, rb2.GravityScale);
        Assert.Equal(1.5f, rb2.LinearDamping);
        Assert.True(rb2.FixedRotation);
        var box2 = copied.GetComponent<BoxCollider2D>()!;
        Assert.Equal(new Vector2(30, 40), box2.Size);
        Assert.Equal(new Vector2(1, 2), box2.Offset);
        Assert.True(box2.IsTrigger);
        Assert.Equal(0.25f, box2.Bounciness);
        Assert.Equal(12f, copied.GetComponent<CircleCollider2D>()!.Radius);
        var anim2 = copied.GetComponent<SpriteAnimator>()!;
        Assert.Equal(32, anim2.FrameWidth);
        Assert.Equal(6, anim2.FrameCount);
        Assert.False(anim2.Loop);
        Assert.False(anim2.PlayOnStart);
        Assert.DoesNotContain("LinearVelocity", SceneSerializer.Serialize(scene));
    }
}

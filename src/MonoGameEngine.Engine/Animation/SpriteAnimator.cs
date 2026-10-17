using Microsoft.Xna.Framework;

namespace MonoGameEngine.Engine;

/// <summary>
/// Plays a sprite sheet: the texture of the entity's <see cref="SpriteRenderer"/> is cut into a grid of
/// <see cref="FrameWidth"/> x <see cref="FrameHeight"/> frames, read left to right, top to bottom. In the editor
/// the first frame is shown; the animation plays while the scene runs.
/// </summary>
public sealed class SpriteAnimator : Component
{
    private float _time;

    /// <summary>Width of one frame in texture pixels.</summary>
    [Tooltip("Width of one frame in texture pixels.")]
    public int FrameWidth { get; set; } = 64;

    /// <summary>Height of one frame in texture pixels.</summary>
    [Tooltip("Height of one frame in texture pixels.")]
    public int FrameHeight { get; set; } = 64;

    /// <summary>Index of the first frame of the animation.</summary>
    [Tooltip("Index of the first frame (frames are numbered left to right, top to bottom).")]
    public int StartFrame { get; set; }

    /// <summary>Number of frames in the animation, starting at <see cref="StartFrame"/>.</summary>
    [Tooltip("Number of frames in the animation.")]
    public int FrameCount { get; set; } = 1;

    /// <summary>Playback speed.</summary>
    [Range(0.1, 60), Tooltip("Frames per second.")]
    public float FramesPerSecond { get; set; } = 10f;

    /// <summary>Start over after the last frame; otherwise stop on it.</summary>
    [Tooltip("Start over after the last frame. Otherwise the animation stops on the last frame.")]
    public bool Loop { get; set; } = true;

    /// <summary>Start playing as soon as the scene starts.</summary>
    [Tooltip("Start playing as soon as the scene starts.")]
    public bool PlayOnStart { get; set; } = true;

    /// <summary>The frame index being shown while the scene runs.</summary>
    public int CurrentFrame { get; private set; }

    /// <summary>True while the animation is advancing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Starts or resumes playback.</summary>
    public void Play() => IsPlaying = true;

    /// <summary>Pauses on the current frame.</summary>
    public void Stop() => IsPlaying = false;

    /// <summary>Goes back to the first frame and plays.</summary>
    public void Restart()
    {
        _time = 0f;
        CurrentFrame = StartFrame;
        IsPlaying = true;
    }

    /// <summary>Moves the animation forward by <paramref name="seconds"/>.</summary>
    public void Advance(float seconds)
    {
        if (!IsPlaying) return;

        int frames = Math.Max(1, FrameCount);
        _time += seconds;
        int step = (int)(_time * Math.Max(FramesPerSecond, 0.01f));

        if (Loop)
        {
            step %= frames;
        }
        else if (step >= frames)
        {
            step = frames - 1;
            IsPlaying = false;
        }
        CurrentFrame = StartFrame + step;
    }

    /// <summary>The part of a texture of the given size that shows the current frame (the first frame outside play).</summary>
    public Rectangle GetSourceRectangle(int textureWidth, int textureHeight)
    {
        int frameWidth = Math.Max(1, FrameWidth);
        int frameHeight = Math.Max(1, FrameHeight);
        int columns = Math.Max(1, textureWidth / frameWidth);
        int frame = Math.Max(0, Started ? CurrentFrame : StartFrame);

        var rect = new Rectangle(frame % columns * frameWidth, frame / columns * frameHeight, frameWidth, frameHeight);
        rect = Rectangle.Intersect(rect, new Rectangle(0, 0, textureWidth, textureHeight));
        return rect.IsEmpty ? new Rectangle(0, 0, textureWidth, textureHeight) : rect;
    }

    /// <inheritdoc />
    protected internal override void OnStart()
    {
        _time = 0f;
        CurrentFrame = StartFrame;
        IsPlaying = PlayOnStart;
    }

    /// <inheritdoc />
    protected internal override void OnUpdate(GameTime gameTime) => Advance((float)gameTime.ElapsedGameTime.TotalSeconds);
}

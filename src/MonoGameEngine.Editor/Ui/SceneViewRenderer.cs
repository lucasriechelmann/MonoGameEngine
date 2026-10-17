using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEngine.Editor.State;

namespace MonoGameEngine.Editor.Ui;

/// <summary>Owns the render target the Scene view shows, and draws the grid and scene into it.</summary>
public sealed class SceneViewRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly ImGuiRenderer _imgui;
    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private RenderTarget2D? _target;
    private IntPtr _textureId;

    private static readonly Color Background = new(30, 31, 36);
    private static readonly Color GridMinor = new(255, 255, 255, 18);
    private static readonly Color GridMajor = new(255, 255, 255, 40);

    public SceneViewRenderer(GraphicsDevice device, ImGuiRenderer imgui)
    {
        _device = device;
        _imgui = imgui;
        _batch = new SpriteBatch(device);
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);
    }

    /// <summary>Makes the render target match the panel size (recreated only when the size changes) and returns its ImGui id.</summary>
    public IntPtr EnsureTarget(int width, int height)
    {
        width = Math.Max(width, 16);
        height = Math.Max(height, 16);
        if (_target == null || _target.Width != width || _target.Height != height)
        {
            if (_textureId != IntPtr.Zero) _imgui.UnbindTexture(_textureId);
            _target?.Dispose();
            _target = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            _textureId = _imgui.BindTexture(_target);
        }
        return _textureId;
    }

    /// <summary>Draws the grid and the active scene into the render target.</summary>
    public void Render(EditorState state, bool showGrid)
    {
        if (_target == null) return;
        var size = new Point(_target.Width, _target.Height);
        var camera = state.ViewCamera;
        var view = camera.GetViewMatrix(size);

        _device.SetRenderTarget(_target);
        _device.Clear(Background);

        if (showGrid) DrawGrid(camera, size, view);
        state.ViewRunner.Draw(_batch, size, view);

        _device.SetRenderTarget(null);
    }

    private void DrawGrid(SceneViewCamera camera, Point size, Matrix view)
    {
        float step = 64f;
        while (step * camera.Zoom < 20f) step *= 2f;
        while (step * camera.Zoom > 160f) step /= 2f;

        var screenSize = new Vector2(size.X, size.Y);
        var topLeft = camera.ScreenToWorld(Vector2.Zero, screenSize);
        var bottomRight = camera.ScreenToWorld(screenSize, screenSize);
        float thickness = 1f / camera.Zoom;

        _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, null, view);
        for (float x = MathF.Floor(topLeft.X / step) * step; x <= bottomRight.X; x += step)
        {
            bool axis = MathF.Abs(x) < 0.001f;
            var color = axis ? new Color(230, 80, 80, 150) : IsMajor(x, step) ? GridMajor : GridMinor;
            _batch.Draw(_pixel, new Vector2(x - thickness * 0.5f, topLeft.Y), null, color, 0f, Vector2.Zero,
                new Vector2(thickness, bottomRight.Y - topLeft.Y), SpriteEffects.None, 0f);
        }
        for (float y = MathF.Floor(topLeft.Y / step) * step; y <= bottomRight.Y; y += step)
        {
            bool axis = MathF.Abs(y) < 0.001f;
            var color = axis ? new Color(80, 220, 100, 150) : IsMajor(y, step) ? GridMajor : GridMinor;
            _batch.Draw(_pixel, new Vector2(topLeft.X, y - thickness * 0.5f), null, color, 0f, Vector2.Zero,
                new Vector2(bottomRight.X - topLeft.X, thickness), SpriteEffects.None, 0f);
        }
        _batch.End();
    }

    private static bool IsMajor(float value, float step) => MathF.Abs(MathF.IEEERemainder(value, step * 4f)) < 0.001f;

    /// <inheritdoc />
    public void Dispose()
    {
        _target?.Dispose();
        _pixel.Dispose();
        _batch.Dispose();
    }
}

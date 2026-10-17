using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEngine.Editor;

/// <summary>
/// Renders ImGui draw data with MonoGame. Owns the ImGui context, the font atlas texture and
/// reusable vertex/index buffers (grown on demand, never reallocated per frame).
/// </summary>
public sealed unsafe class ImGuiRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizer;
    private readonly Dictionary<IntPtr, Texture2D> _textures = new();
    private readonly IntPtr _context;

    private VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[5000];
    private ushort[] _indices = new ushort[10000];
    private DynamicVertexBuffer? _vertexBuffer;
    private DynamicIndexBuffer? _indexBuffer;
    private IntPtr _nextTextureId = 1;
    private IntPtr _fontTextureId;

    public ImGuiRenderer(Game game)
    {
        _device = game.GraphicsDevice;
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        _rasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            DepthBias = 0,
            FillMode = FillMode.Solid,
            MultiSampleAntiAlias = false,
            ScissorTestEnable = true,
            SlopeScaleDepthBias = 0,
        };
        _effect = new BasicEffect(_device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            World = Matrix.Identity,
            View = Matrix.Identity,
        };

        LoadFonts();
        Ui.Win11Theme.Apply();
        RebuildFontAtlas();
    }

    /// <summary>A large Material Icons font for icon tiles (for example in the Assets panel). Null if the font file is missing.</summary>
    public static ImFontPtr? LargeIcons { get; private set; }

    private const float TextSize = 17f;
    private const float IconSize = 19f;
    private const float LargeIconSize = 46f;

    /// <summary>
    /// Segoe UI (the Windows 11 UI font, read from the system and not redistributed) merged with Material Icons.
    /// Falls back to ImGui's built-in font when Segoe UI is not installed.
    /// </summary>
    private void LoadFonts()
    {
        var io = ImGui.GetIO();
        var segoe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(segoe)) io.Fonts.AddFontFromFileTTF(segoe, TextSize);
        else io.Fonts.AddFontDefault();

        var iconFont = Path.Combine(AppContext.BaseDirectory, "Fonts", "MaterialIcons-Regular.ttf");
        if (!File.Exists(iconFont))
        {
            Engine.Log.Warning($"Icon font not found at '{iconFont}'.");
            return;
        }

        var ranges = Ui.Icons.BuildGlyphRanges();
        var rangePtr = Marshal.AllocHGlobal(ranges.Length * sizeof(ushort)); // must outlive the atlas build
        Marshal.Copy((short[])(Array)ranges, 0, rangePtr, ranges.Length);

        var merge = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
        merge.MergeMode = true;
        merge.PixelSnapH = true;
        merge.GlyphMinAdvanceX = IconSize + 2f;
        merge.GlyphOffset = new System.Numerics.Vector2(0f, 3f);
        io.Fonts.AddFontFromFileTTF(iconFont, IconSize, merge, rangePtr);

        var large = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
        LargeIcons = io.Fonts.AddFontFromFileTTF(iconFont, LargeIconSize, large, rangePtr);
    }

    /// <summary>Sets where ImGui saves and loads the window layout. The string lives as long as the process.</summary>
    public void SetIniFilename(string path)
    {
        ImGui.GetIO().NativePtr->IniFilename = (byte*)Marshal.StringToCoTaskMemUTF8(path);
    }

    /// <summary>Registers a texture so it can be shown with <c>ImGui.Image</c>.</summary>
    public IntPtr BindTexture(Texture2D texture)
    {
        var id = _nextTextureId++;
        _textures[id] = texture;
        return id;
    }

    /// <summary>Releases a texture previously returned by <see cref="BindTexture"/>.</summary>
    public void UnbindTexture(IntPtr id) => _textures.Remove(id);

    /// <summary>Starts a new ImGui frame. Call the input update first.</summary>
    public void BeginFrame(GameTime gameTime)
    {
        var io = ImGui.GetIO();
        io.DeltaTime = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 1e-5f);
        var pp = _device.PresentationParameters;
        io.DisplaySize = new System.Numerics.Vector2(pp.BackBufferWidth, pp.BackBufferHeight);
        io.DisplayFramebufferScale = System.Numerics.Vector2.One;
        ImGui.NewFrame();
    }

    /// <summary>Finishes the frame and draws it to the current render target.</summary>
    public void EndFrame()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void RebuildFontAtlas()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

        var data = new byte[width * height * bytesPerPixel];
        Marshal.Copy((IntPtr)pixels, data, 0, data.Length);

        var texture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
        texture.SetData(data);

        if (_fontTextureId != IntPtr.Zero) _textures.Remove(_fontTextureId);
        _fontTextureId = BindTexture(texture);
        io.Fonts.SetTexID(_fontTextureId);
        io.Fonts.ClearTexData();
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0 || drawData.TotalVtxCount == 0) return;

        var target = _device.Viewport.Bounds;
        var lastScissor = _device.ScissorRectangle;
        var lastBlend = _device.BlendState;
        var lastDepth = _device.DepthStencilState;
        var lastRaster = _device.RasterizerState;
        var lastSampler = _device.SamplerStates[0];

        _device.BlendFactor = Color.White;
        _device.BlendState = BlendState.NonPremultiplied;
        _device.RasterizerState = _rasterizer;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.SamplerStates[0] = SamplerState.PointClamp;

        UploadBuffers(drawData);

        var displaySize = ImGui.GetIO().DisplaySize;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, displaySize.X, displaySize.Y, 0f, -1f, 1f);

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        int vtxOffset = 0;
        int idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var cmd = cmdList.CmdBuffer[i];
                if (cmd.ElemCount == 0) continue;
                if (!_textures.TryGetValue(cmd.TextureId, out var texture)) continue;

                var clip = cmd.ClipRect;
                int x0 = Math.Clamp((int)clip.X, 0, target.Width);
                int y0 = Math.Clamp((int)clip.Y, 0, target.Height);
                int x1 = Math.Clamp((int)clip.Z, 0, target.Width);
                int y1 = Math.Clamp((int)clip.W, 0, target.Height);
                if (x1 <= x0 || y1 <= y0) continue;
                _device.ScissorRectangle = new Rectangle(x0, y0, x1 - x0, y1 - y0);

                _effect.Texture = texture;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        vtxOffset + (int)cmd.VtxOffset,
                        idxOffset + (int)cmd.IdxOffset,
                        (int)cmd.ElemCount / 3);
                }
            }
            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }

        _device.ScissorRectangle = lastScissor;
        _device.BlendState = lastBlend;
        _device.DepthStencilState = lastDepth;
        _device.RasterizerState = lastRaster;
        _device.SamplerStates[0] = lastSampler;
    }

    private void UploadBuffers(ImDrawDataPtr drawData)
    {
        int totalVtx = drawData.TotalVtxCount;
        int totalIdx = drawData.TotalIdxCount;

        if (_vertexBuffer == null || _vertexBuffer.VertexCount < totalVtx)
        {
            _vertexBuffer?.Dispose();
            int size = (int)(totalVtx * 1.5f);
            _vertexBuffer = new DynamicVertexBuffer(
                _device, VertexPositionColorTexture.VertexDeclaration, size, BufferUsage.WriteOnly);
            _vertices = new VertexPositionColorTexture[size];
        }
        if (_indexBuffer == null || _indexBuffer.IndexCount < totalIdx)
        {
            _indexBuffer?.Dispose();
            int size = (int)(totalIdx * 1.5f);
            _indexBuffer = new DynamicIndexBuffer(
                _device, IndexElementSize.SixteenBits, size, BufferUsage.WriteOnly);
            _indices = new ushort[size];
        }

        int vtxOffset = 0;
        int idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            var src = (ImDrawVert*)cmdList.VtxBuffer.Data;
            for (int i = 0; i < cmdList.VtxBuffer.Size; i++)
            {
                _vertices[vtxOffset + i] = new VertexPositionColorTexture(
                    new Vector3(src[i].pos.X, src[i].pos.Y, 0f),
                    new Color { PackedValue = src[i].col },
                    new Vector2(src[i].uv.X, src[i].uv.Y));
            }

            new ReadOnlySpan<ushort>((void*)cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size)
                .CopyTo(_indices.AsSpan(idxOffset));

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }

        _vertexBuffer.SetData(_vertices, 0, totalVtx, SetDataOptions.Discard);
        _indexBuffer.SetData(_indices, 0, totalIdx, SetDataOptions.Discard);
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _effect.Dispose();
        _rasterizer.Dispose();
        foreach (var t in _textures.Values) t.Dispose();
        _textures.Clear();
        ImGui.DestroyContext(_context);
    }
}

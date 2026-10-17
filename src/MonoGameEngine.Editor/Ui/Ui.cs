using ImGuiNET;
using Microsoft.Xna.Framework;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>Small conversions and helpers shared by the panels.</summary>
public static class Ui
{
    /// <summary>Payload type used when dragging an entity in the Hierarchy.</summary>
    public const string EntityPayload = "ENTITY";

    /// <summary>Payload type used when dragging an asset from the Assets panel.</summary>
    public const string AssetPayload = "ASSET_PATH";

    /// <summary>MonoGame vector to System.Numerics vector.</summary>
    public static Num.Vector2 ToNum(this Vector2 v) => new(v.X, v.Y);

    /// <summary>System.Numerics vector to MonoGame vector.</summary>
    public static Vector2 ToXna(this Num.Vector2 v) => new(v.X, v.Y);

    /// <summary>Packs an RGBA colour for ImGui draw lists.</summary>
    public static uint Rgba(float r, float g, float b, float a = 1f) =>
        ImGui.ColorConvertFloat4ToU32(new Num.Vector4(r, g, b, a));

    /// <summary>Places a window on first use only; a saved layout wins afterwards.</summary>
    public static void PlaceFirstUse(float x, float y, float w, float h)
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new Num.Vector2(vp.WorkPos.X + vp.WorkSize.X * x, vp.WorkPos.Y + vp.WorkSize.Y * y),
            ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Num.Vector2(vp.WorkSize.X * w, vp.WorkSize.Y * h), ImGuiCond.FirstUseEver);
    }

    /// <summary>Sends a one-byte payload; the real data lives in the panel that started the drag.</summary>
    public static unsafe void SetPayload(string type)
    {
        byte marker = 1;
        ImGui.SetDragDropPayload(type, (IntPtr)(&marker), 1);
    }

    /// <summary>True if a payload of the given type was dropped on the last item.</summary>
    public static unsafe bool AcceptPayload(string type)
    {
        var payload = ImGui.AcceptDragDropPayload(type);
        return payload.NativePtr != null;
    }
}

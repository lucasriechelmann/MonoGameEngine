using ImGuiNET;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>A dark Windows 11 (Fluent) look: rounded controls, neutral grays and the system accent blue.</summary>
public static class Win11Theme
{
    private static readonly Num.Vector4 Accent = Rgb(0x60CDFF);
    private static readonly Num.Vector4 AccentStrong = Rgb(0x0078D4);

    /// <summary>Applies colours and metrics to the current ImGui context.</summary>
    public static void Apply()
    {
        var style = ImGui.GetStyle();

        style.WindowRounding = 6f;
        style.ChildRounding = 6f;
        style.PopupRounding = 8f;
        style.FrameRounding = 5f;
        style.GrabRounding = 5f;
        style.ScrollbarRounding = 10f;
        style.TabRounding = 5f;

        style.WindowPadding = new Num.Vector2(10f, 10f);
        style.FramePadding = new Num.Vector2(10f, 6f);
        style.ItemSpacing = new Num.Vector2(8f, 7f);
        style.ItemInnerSpacing = new Num.Vector2(6f, 6f);
        style.IndentSpacing = 18f;
        style.ScrollbarSize = 14f;
        style.GrabMinSize = 12f;
        style.WindowBorderSize = 1f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.WindowTitleAlign = new Num.Vector2(0f, 0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.None;
        style.SeparatorTextBorderSize = 1f;

        var c = style.Colors;
        c[(int)ImGuiCol.Text] = Rgb(0xFFFFFF);
        c[(int)ImGuiCol.TextDisabled] = Rgb(0x9D9D9D);
        c[(int)ImGuiCol.WindowBg] = Rgb(0x202020);
        c[(int)ImGuiCol.ChildBg] = Rgb(0x1C1C1C);
        c[(int)ImGuiCol.PopupBg] = Rgb(0x2C2C2C);
        c[(int)ImGuiCol.Border] = Rgb(0x3A3A3A);
        c[(int)ImGuiCol.BorderShadow] = new Num.Vector4(0f, 0f, 0f, 0f);
        c[(int)ImGuiCol.FrameBg] = Rgb(0x2D2D2D);
        c[(int)ImGuiCol.FrameBgHovered] = Rgb(0x353535);
        c[(int)ImGuiCol.FrameBgActive] = Rgb(0x3D3D3D);
        c[(int)ImGuiCol.TitleBg] = Rgb(0x1B1B1B);
        c[(int)ImGuiCol.TitleBgActive] = Rgb(0x262626);
        c[(int)ImGuiCol.TitleBgCollapsed] = Rgb(0x1B1B1B);
        c[(int)ImGuiCol.MenuBarBg] = Rgb(0x1B1B1B);
        c[(int)ImGuiCol.ScrollbarBg] = new Num.Vector4(0f, 0f, 0f, 0f);
        c[(int)ImGuiCol.ScrollbarGrab] = Rgb(0x5A5A5A);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = Rgb(0x7A7A7A);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Rgb(0x9A9A9A);
        c[(int)ImGuiCol.CheckMark] = Accent;
        c[(int)ImGuiCol.SliderGrab] = Accent;
        c[(int)ImGuiCol.SliderGrabActive] = Rgb(0x8FDBFF);
        c[(int)ImGuiCol.Button] = Rgb(0x333333);
        c[(int)ImGuiCol.ButtonHovered] = Rgb(0x3D3D3D);
        c[(int)ImGuiCol.ButtonActive] = Rgb(0x474747);
        c[(int)ImGuiCol.Header] = WithAlpha(Accent, 0.22f);
        c[(int)ImGuiCol.HeaderHovered] = new Num.Vector4(1f, 1f, 1f, 0.07f);
        c[(int)ImGuiCol.HeaderActive] = WithAlpha(Accent, 0.32f);
        c[(int)ImGuiCol.Separator] = Rgb(0x3A3A3A);
        c[(int)ImGuiCol.SeparatorHovered] = Accent;
        c[(int)ImGuiCol.SeparatorActive] = Accent;
        c[(int)ImGuiCol.ResizeGrip] = new Num.Vector4(1f, 1f, 1f, 0.08f);
        c[(int)ImGuiCol.ResizeGripHovered] = WithAlpha(Accent, 0.6f);
        c[(int)ImGuiCol.ResizeGripActive] = Accent;
        c[(int)ImGuiCol.Tab] = Rgb(0x262626);
        c[(int)ImGuiCol.TabHovered] = Rgb(0x3D3D3D);
        c[(int)ImGuiCol.DockingPreview] = WithAlpha(Accent, 0.45f);
        c[(int)ImGuiCol.DockingEmptyBg] = Rgb(0x1B1B1B);
        c[(int)ImGuiCol.TextSelectedBg] = WithAlpha(AccentStrong, 0.6f);
        c[(int)ImGuiCol.DragDropTarget] = Accent;
        c[(int)ImGuiCol.ModalWindowDimBg] = new Num.Vector4(0f, 0f, 0f, 0.5f);
    }

    private static Num.Vector4 Rgb(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    private static Num.Vector4 WithAlpha(Num.Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);
}

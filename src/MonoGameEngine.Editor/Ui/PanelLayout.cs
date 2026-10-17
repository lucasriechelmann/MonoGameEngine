using ImGuiNET;
using Num = System.Numerics;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// Keeps floating panels proportional to the main window. Each panel's rectangle is remembered as a fraction of
/// the work area; when the window is resized or maximized the panels are scaled to match. Docked windows are left
/// to ImGui's docking. Saved fractions take over from the old absolute layout in <c>imgui.ini</c>. The fractions are saved in <see cref="State.EditorSettings"/>.
/// </summary>
public static class PanelLayout
{
    private const int SettleFrames = 3; // the menu bar needs a few frames before the work area is stable

    private static readonly Dictionary<string, float[]> Fractions = new();
    private static readonly HashSet<string> Applied = new();
    private static Num.Vector2 _lastSize;
    private static int _frame;

    /// <summary>True during the frame in which the window size changed.</summary>
    public static bool Resized { get; private set; }

    /// <summary>Replaces the remembered fractions with saved ones.</summary>
    public static void Load(Dictionary<string, float[]>? saved)
    {
        if (saved == null) return;
        foreach (var (name, f) in saved)
        {
            if (f.Length == 4) Fractions[name] = f;
        }
    }

    /// <summary>The fractions to save.</summary>
    public static Dictionary<string, float[]> Export() => new(Fractions);

    /// <summary>Call once per frame before drawing panels.</summary>
    public static void BeginFrame()
    {
        var size = ImGui.GetMainViewport().WorkSize;
        Resized = _frame >= SettleFrames && _lastSize != Num.Vector2.Zero && size != _lastSize;
        _lastSize = size;
        _frame++;
    }

    /// <summary>
    /// Call before <c>ImGui.Begin</c>. Applies the remembered fractions (or the given defaults when there are none)
    /// once at startup and again whenever the window is resized.
    /// </summary>
    public static void Place(string name, float x, float y, float w, float h)
    {
        if (_frame < SettleFrames || (Applied.Contains(name) && !Resized)) return;
        Applied.Add(name);

        var f = Fractions.TryGetValue(name, out var saved) ? saved : new[] { x, y, w, h };
        var vp = ImGui.GetMainViewport();
        Set(vp, f[0], f[1], f[2], f[3], ImGuiCond.Always);
    }

    /// <summary>Call right after <c>ImGui.Begin</c> (whatever it returned) to remember where the user put the panel.</summary>
    public static void Remember(string name)
    {
        if (Resized || !Applied.Contains(name) || ImGui.IsWindowDocked()) return;

        var vp = ImGui.GetMainViewport();
        if (vp.WorkSize.X <= 0 || vp.WorkSize.Y <= 0) return;
        var pos = ImGui.GetWindowPos() - vp.WorkPos;
        var size = ImGui.GetWindowSize();
        Fractions[name] = [pos.X / vp.WorkSize.X, pos.Y / vp.WorkSize.Y, size.X / vp.WorkSize.X, size.Y / vp.WorkSize.Y];
    }

    private static void Set(ImGuiViewportPtr vp, float x, float y, float w, float h, ImGuiCond cond)
    {
        ImGui.SetNextWindowPos(new Num.Vector2(vp.WorkPos.X + vp.WorkSize.X * x, vp.WorkPos.Y + vp.WorkSize.Y * y), cond);
        ImGui.SetNextWindowSize(new Num.Vector2(vp.WorkSize.X * w, vp.WorkSize.Y * h), cond);
    }
}

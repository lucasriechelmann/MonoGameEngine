using System.Reflection;

namespace MonoGameEngine.Editor.Ui;

/// <summary>
/// Material Icons code points (Apache 2.0, see <c>Fonts/LICENSE-MaterialIcons.txt</c>). Concatenate an icon with
/// text, for example <c>Icons.Save + " Save"</c>. Every constant added here is loaded into the font atlas automatically.
/// </summary>
public static class Icons
{
    public const string Folder = "";
    public const string FolderOpen = "";
    public const string File = "";
    public const string Description = "";
    public const string Image = "";
    public const string Layers = "";
    public const string Category = "";
    public const string CropFree = "";
    public const string PhotoCamera = "";
    public const string PlayArrow = "";
    public const string Pause = "";
    public const string Stop = "";
    public const string Add = "";
    public const string Delete = "";
    public const string ContentCopy = "";
    public const string Edit = "";
    public const string Undo = "";
    public const string Redo = "";
    public const string Save = "";
    public const string Refresh = "";
    public const string ArrowUpward = "";
    public const string GridOn = "";
    public const string Close = "";

    /// <summary>Pairs of (first, last) code points covering every icon above, ending with 0, as ImGui expects.</summary>
    public static ushort[] BuildGlyphRanges()
    {
        var codes = typeof(Icons)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (ushort)((string)f.GetRawConstantValue()!)[0])
            .Distinct()
            .Order()
            .ToList();

        var ranges = new ushort[codes.Count * 2 + 1];
        for (int i = 0; i < codes.Count; i++)
        {
            ranges[i * 2] = codes[i];
            ranges[i * 2 + 1] = codes[i];
        }
        return ranges;
    }
}

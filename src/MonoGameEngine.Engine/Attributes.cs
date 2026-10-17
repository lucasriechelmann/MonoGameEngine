namespace MonoGameEngine.Engine;

/// <summary>Limits a numeric component property to a range and shows it as a slider in the Inspector.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class RangeAttribute(double min, double max) : Attribute
{
    /// <summary>Smallest allowed value.</summary>
    public double Min { get; } = min;

    /// <summary>Largest allowed value.</summary>
    public double Max { get; } = max;
}

/// <summary>Text shown when hovering the property in the Inspector.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class TooltipAttribute(string text) : Attribute
{
    /// <summary>The tooltip text.</summary>
    public string Text { get; } = text;
}

/// <summary>The Inspector shows the property but does not allow editing it.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ReadOnlyAttribute : Attribute;

/// <summary>The Inspector does not show the property. It is still serialized.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class HideInInspectorAttribute : Attribute;

/// <summary>Draws a bold header line above the property in the Inspector.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class HeaderAttribute(string text) : Attribute
{
    /// <summary>The header text.</summary>
    public string Text { get; } = text;
}

/// <summary>
/// Marks a string property as a path to an asset, relative to the content folder.
/// The Inspector draws it as an asset field that accepts drag and drop from the Assets panel.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AssetPathAttribute(string kind = "texture") : Attribute
{
    /// <summary>The asset kind, for example <c>texture</c>.</summary>
    public string Kind { get; } = kind;
}

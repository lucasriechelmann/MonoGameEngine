using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MonoGameEngine.Engine.Reflection;

/// <summary>
/// A serialized public read/write property of a component or entity, with its inspector attributes.
/// The serializer and the editor Inspector both work from this list.
/// </summary>
public sealed class MemberMetadata
{
    internal MemberMetadata(PropertyInfo property)
    {
        Property = property;
        Range = property.GetCustomAttribute<RangeAttribute>();
        Tooltip = property.GetCustomAttribute<TooltipAttribute>()?.Text;
        Header = property.GetCustomAttribute<HeaderAttribute>()?.Text;
        AssetPath = property.GetCustomAttribute<AssetPathAttribute>();
        IsReadOnly = property.IsDefined(typeof(ReadOnlyAttribute));
        IsHidden = property.IsDefined(typeof(HideInInspectorAttribute));
    }

    /// <summary>The underlying property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Property name, also the JSON key.</summary>
    public string Name => Property.Name;

    /// <summary>Property type.</summary>
    public Type Type => Property.PropertyType;

    /// <summary>The <see cref="RangeAttribute"/>, if any.</summary>
    public RangeAttribute? Range { get; }

    /// <summary>Tooltip text, if any.</summary>
    public string? Tooltip { get; }

    /// <summary>Header text, if any.</summary>
    public string? Header { get; }

    /// <summary>The <see cref="AssetPathAttribute"/>, if any.</summary>
    public AssetPathAttribute? AssetPath { get; }

    /// <summary>The Inspector must not allow editing.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The Inspector must not show this member.</summary>
    public bool IsHidden { get; }

    /// <summary>Reads the value from <paramref name="target"/>.</summary>
    public object? GetValue(object target) => Property.GetValue(target);

    /// <summary>Writes the value to <paramref name="target"/>.</summary>
    public void SetValue(object target, object? value) => Property.SetValue(target, value);
}

/// <summary>Reflection cache for serializable members.</summary>
public static class MemberCache
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<MemberMetadata>> Cache = new();

    /// <summary>
    /// Public instance properties with a public getter and setter, excluding indexers and anything
    /// marked <see cref="JsonIgnoreAttribute"/>. Base class members come first.
    /// </summary>
    public static IReadOnlyList<MemberMetadata> GetMembers(Type type) => Cache.GetOrAdd(type, Build);

    /// <summary>Forgets the cached members of every type in <paramref name="assembly"/> so its load context can be unloaded.</summary>
    public static void Forget(Assembly assembly)
    {
        foreach (var type in Cache.Keys)
        {
            if (type.Assembly == assembly) Cache.TryRemove(type, out _);
        }
    }

    private static IReadOnlyList<MemberMetadata> Build(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0
                        && p.GetMethod is { IsPublic: true }
                        && p.SetMethod is { IsPublic: true }
                        && !p.IsDefined(typeof(JsonIgnoreAttribute)))
            .OrderBy(p => Depth(p.DeclaringType))
            .Select(p => new MemberMetadata(p))
            .ToList();

    private static int Depth(Type? type)
    {
        int depth = 0;
        for (var t = type; t?.BaseType != null; t = t.BaseType) depth++;
        return depth;
    }
}

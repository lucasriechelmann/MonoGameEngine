using System.Reflection;

namespace MonoGameEngine.Engine.Reflection;

/// <summary>
/// Finds every concrete <see cref="Component"/> type. Used by the serializer (type discriminator lookup)
/// and the editor's Add Component menu. Engine types are always known; call
/// <see cref="RegisterAssembly"/> for an assembly that defines game components.
/// </summary>
public static class ComponentRegistry
{
    private static readonly object Gate = new();
    private static readonly List<Assembly> Assemblies = [typeof(Component).Assembly];
    private static List<Type>? _types;

    /// <summary>Includes the component types of <paramref name="assembly"/> in the registry.</summary>
    public static void RegisterAssembly(Assembly assembly)
    {
        lock (Gate)
        {
            if (Assemblies.Contains(assembly)) return;
            Assemblies.Add(assembly);
            _types = null;
        }
    }

    /// <summary>Removes an assembly added with <see cref="RegisterAssembly"/> (for example before its load context is unloaded).</summary>
    public static void UnregisterAssembly(Assembly assembly)
    {
        lock (Gate)
        {
            if (assembly == typeof(Component).Assembly || !Assemblies.Remove(assembly)) return;
            _types = null;
        }
        MemberCache.Forget(assembly);
    }

    /// <summary>All concrete component types with a public parameterless constructor, sorted by name.</summary>
    public static IReadOnlyList<Type> GetTypes()
    {
        lock (Gate)
        {
            return _types ??= Assemblies
                .SelectMany(SafeGetTypes)
                .Where(t => t is { IsAbstract: false, IsClass: true }
                            && typeof(Component).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();
        }
    }

    /// <summary>Finds a component type by its full name (the JSON type discriminator).</summary>
    public static Type? Find(string fullName)
    {
        var types = GetTypes();
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i].FullName == fullName) return types[i];
        }
        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}

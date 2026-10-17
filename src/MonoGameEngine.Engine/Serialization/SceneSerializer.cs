using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MonoGameEngine.Engine.Reflection;

namespace MonoGameEngine.Engine.Serialization;

/// <summary>
/// Saves and loads scenes as JSON. Entities nest their children; components are written with a
/// <c>$type</c> discriminator followed by their serialized properties. Entity ids are kept.
/// </summary>
public static class SceneSerializer
{
    /// <summary>The scene file format version written by this build.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Conventional file extension for scene files.</summary>
    public const string FileExtension = ".scene.json";

    private const string TypeKey = "$type";

    /// <summary>The options used for all scene JSON.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new Vector2JsonConverter(), new ColorJsonConverter(), new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a scene to a JSON string.</summary>
    public static string Serialize(Scene scene)
    {
        var entities = new JsonArray();
        foreach (var root in scene.Roots) entities.Add(SerializeEntity(root));

        var document = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["name"] = scene.Name,
            ["entities"] = entities,
        };
        return document.ToJsonString(Options);
    }

    /// <summary>Creates a scene from JSON. Unknown component types and bad values are skipped with a warning.</summary>
    public static Scene Deserialize(string json, AssetManager? assets = null)
    {
        var document = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("The scene file is empty.");
        int version = document["version"]?.GetValue<int>() ?? 1;
        if (version > CurrentVersion)
            Log.Warning($"Scene file version {version} is newer than supported version {CurrentVersion}.");

        var scene = new Scene { Name = document["name"]?.GetValue<string>() ?? "Untitled", Assets = assets };
        if (document["entities"] is JsonArray entities)
        {
            foreach (var node in entities)
            {
                if (node is JsonObject obj) DeserializeEntity(obj, scene, null, newIds: false);
            }
        }
        return scene;
    }

    /// <summary>Writes a scene to a file.</summary>
    public static void Save(Scene scene, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(scene));
    }

    /// <summary>Reads a scene from a file.</summary>
    public static Scene Load(string path, AssetManager? assets = null) =>
        Deserialize(File.ReadAllText(path), assets);

    /// <summary>Serializes one entity with its components and children.</summary>
    public static JsonObject SerializeEntity(Entity entity)
    {
        var components = new JsonArray();
        foreach (var component in entity.Components) components.Add(SerializeComponent(component));

        var children = new JsonArray();
        foreach (var child in entity.Children) children.Add(SerializeEntity(child));

        return new JsonObject
        {
            ["id"] = entity.Id.ToString(),
            ["name"] = entity.Name,
            ["active"] = entity.Active,
            ["components"] = components,
            ["children"] = children,
        };
    }

    /// <summary>
    /// Creates an entity (and its children) from JSON under <paramref name="parent"/>.
    /// With <paramref name="newIds"/> every entity gets a fresh id, which duplicating needs.
    /// </summary>
    public static Entity DeserializeEntity(JsonObject node, Scene scene, Entity? parent, bool newIds, int index = -1)
    {
        var id = !newIds && Guid.TryParse(node["id"]?.GetValue<string>(), out var parsed) && scene.Find(parsed) == null
            ? parsed
            : Guid.NewGuid();

        var entity = new Entity(id, node["name"]?.GetValue<string>() ?? "Entity")
        {
            Active = node["active"]?.GetValue<bool>() ?? true,
        };

        if (node["components"] is JsonArray components)
        {
            foreach (var componentNode in components)
            {
                if (componentNode is JsonObject obj) DeserializeComponent(obj, entity);
            }
        }

        scene.Add(entity, parent, index);
        if (node["children"] is JsonArray children)
        {
            foreach (var childNode in children)
            {
                if (childNode is JsonObject obj) DeserializeEntity(obj, scene, entity, newIds);
            }
        }
        return entity;
    }

    /// <summary>Copies an entity and its subtree with new ids, placing the copy right after the original.</summary>
    public static Entity Clone(Entity entity)
    {
        var scene = entity.Scene ?? throw new InvalidOperationException("The entity is not in a scene.");
        var copy = DeserializeEntity(SerializeEntity(entity), scene, entity.Parent, newIds: true,
            entity.SiblingIndex + 1);
        copy.Name = entity.Name + " (Copy)";
        return copy;
    }

    private static JsonObject SerializeComponent(Component component)
    {
        var type = component.GetType();
        var node = new JsonObject { [TypeKey] = type.FullName };
        foreach (var member in MemberCache.GetMembers(type))
        {
            node[member.Name] = JsonSerializer.SerializeToNode(member.GetValue(component), member.Type, Options);
        }
        return node;
    }

    private static void DeserializeComponent(JsonObject node, Entity entity)
    {
        var typeName = node[TypeKey]?.GetValue<string>();
        var type = typeName == null ? null : ComponentRegistry.Find(typeName);
        if (type == null)
        {
            Log.Warning($"Skipped unknown component type '{typeName}' on entity '{entity.Name}'.");
            return;
        }

        var component = entity.AddComponent(type);
        foreach (var member in MemberCache.GetMembers(type))
        {
            if (!node.TryGetPropertyValue(member.Name, out var value) || value == null) continue;
            try
            {
                member.SetValue(component, value.Deserialize(member.Type, Options));
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not read {type.Name}.{member.Name} on '{entity.Name}': {ex.Message}");
            }
        }
    }
}

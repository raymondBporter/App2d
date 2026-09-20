using System.Collections.ObjectModel;

namespace App2d.Core.Characters;

/// <summary>A session's validated type snapshots and shared packed motion libraries.</summary>
public sealed class EntityCatalog
{
    // The game uses 32-unit tiles. Authoring uses approximately two-unit tall people.
    public const float WorldUnits = 40f;
    public string Root { get; }
    public IReadOnlyDictionary<string, EntityTypeDefinition> Types { get; }
    public IReadOnlyDictionary<string, PointLibrary> Libraries { get; }

    public EntityCatalog(string root)
    {
        Root = Path.GetFullPath(root);
        var types = new Dictionary<string, EntityTypeDefinition>(StringComparer.Ordinal);
        var libraries = new Dictionary<string, PointLibrary>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "entities"), "*.json").Order(StringComparer.Ordinal))
        {
            var type = EntityTypeDefinition.Load(file);
            if (string.IsNullOrEmpty(type.Library) || type.Library != Path.GetFileName(type.Library) || type.Library.Contains(".."))
                throw new InvalidDataException("Invalid entity library path.");
            if (!libraries.TryGetValue(type.Library, out var library))
                libraries.Add(type.Library, library = PointLibrary.Load(Path.Combine(Root, type.Library, "library.json")));
            type.Validate(library);
            if (!types.TryAdd(type.Id, type)) throw new InvalidDataException("Duplicate entity type: " + type.Id);
        }
        Types = new ReadOnlyDictionary<string, EntityTypeDefinition>(types);
        Libraries = new ReadOnlyDictionary<string, PointLibrary>(libraries);
    }
}

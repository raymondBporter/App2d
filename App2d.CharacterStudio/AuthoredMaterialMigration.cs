using App2d.Core.Characters.Authored;
using App2d.Core.IO;

namespace App2d.CharacterStudio;

/// <summary>Rewrites authored models and props through their typed geometry and material writers.</summary>
internal static class AuthoredMaterialMigration
{
    public static void Run(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var changes = new List<(string Path, string Json)>();
        foreach (var path in Files("models"))
        {
            var original = File.ReadAllText(path);
            var migrated = CharacterModel.FromJson(original).ToJson();
            if (original != migrated) changes.Add((path, migrated));
        }
        foreach (var path in Files("props"))
        {
            var original = File.ReadAllText(path);
            var migrated = PropAsset.FromJson(original).ToJson();
            if (original != migrated) changes.Add((path, migrated));
        }
        foreach (var (path, json) in changes) AtomicFile.WriteAllText(path, json);
        Console.WriteLine($"Migrated {changes.Count} authored model and prop file(s) to typed materials.");

        IEnumerable<string> Files(string folder) => Directory.Exists(Path.Combine(root, folder))
            ? Directory.EnumerateFiles(Path.Combine(root, folder), "*.json").Order(StringComparer.Ordinal)
            : [];
    }
}

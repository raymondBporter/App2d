using App2d.Core.Characters.Authored;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>
/// How explicit generation writes into an authored tree (character-system migration, P11b): a file that does not exist is
/// written, one that already holds the generated content is reported unchanged, and one that differs is editor-owned and kept
/// unless the run was forced. Every writer appends to the report, so a regeneration says exactly what it touched.
/// </summary>
public sealed class SeedReport(bool force = false)
{
    public const string Written = "written", Unchanged = "unchanged", Kept = "kept (differs from the generated content; edited after an earlier run or authored by hand; use --force to replace)", Replaced = "replaced (forced)";

    public bool Force { get; } = force;
    public List<(string Path, string Status)> Entries { get; } = [];

    public IEnumerable<string> Lines => Entries.Select(e => $"{e.Status}: {e.Path}");
    public int Count(string status) => Entries.Count(e => e.Status == status);

    /// <summary>Writes <paramref name="json"/> to <paramref name="path"/> under the policy above and records what happened.</summary>
    public string Write(string path, string json)
    {
        string status;
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AuthoredAsset.Write(path, json);
            status = Written;
        }
        else if (File.ReadAllText(path) == json)
        {
            status = Unchanged;
        }
        else if (Force)
        {
            AuthoredAsset.Write(path, json);
            status = Replaced;
        }
        else
        {
            status = Kept;
        }

        Entries.Add((path, status));
        return status;
    }

    public override string ToString() => $"{Count(Written)} written, {Count(Unchanged)} unchanged, {Count(Kept)} kept, {Count(Replaced)} replaced";
}

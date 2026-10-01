namespace App2d.Core.Characters.Authored;

/// <summary>Editable appearance assets use the existing prop file format, attachments, undo, and runtime meshes.</summary>
public static class AppearanceAuthoring
{
    public static readonly string[] Templates = ["hair", "beard", "skirt", "blank"];

    public static PropAsset New(string id, string name, string template, string ink, float lineWidth, Func<string, PropAsset?>? findTemplate = null)
    {
        var source = template switch { "hair" => PersonWardrobe.ShortHair, "beard" => "brute-beard", "skirt" => "cinder-hide-wrap", "blank" => null, _ => throw new InvalidDataException("Unknown appearance template.") };
        var art = source is null ? new PropAsset { Usage = "clothing", Attachment = PersonWardrobe.BodySocket, Tip = default } :
            PropAsset.FromJson((findTemplate?.Invoke(source) ?? PersonWardrobe.Props().Single(p => p.Id == source)).ToJson());
        art.Id = id; art.Name = name; art.Ink = ink; art.LineWidth = lineWidth; art.BackView = null;
        if (source is null) art.Solids.Add(PropGeometry.Extrude([new(-.15f, 0), new(.15f, 0), new(.15f, .3f), new(-.15f, .3f)], .02f, "#b97549"));
        art.Validate(); return art;
    }

    public static PropAsset Duplicate(PropAsset source, string id, string name)
    {
        var copy = PropAsset.FromJson(source.ToJson()); copy.Id = id; copy.Name = name; copy.Validate(); return copy;
    }

    /// <summary>Build and validate before replacing anything, so an invalid outline leaves the last good drawing intact.</summary>
    public static void Cutout(PropAsset art, int index, IEnumerable<PuppetPoint> points, float thickness)
    {
        var old = art.Solids[index]; var mesh = PropGeometry.Extrude(points, thickness, old.Fill);
        mesh.Outlined = old.Outlined;
        new PropAsset { Id = "check", Name = "Check", Solids = [mesh] }.Validate();
        art.Solids[index] = mesh;
    }
}

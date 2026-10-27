namespace App2d.Core.Characters.Authored;

/// <summary>Library part templates and cutout editing, using the shared geometry and document path.</summary>
public static class AppearanceAuthoring
{
    public static readonly string[] Templates = ["hair", "beard", "skirt", "sandal", "boot", "blank"];

    /// <param name="hair">The hair prop the "hair" template starts from: the game profile's hair gear when the caller knows the profile, else the built-in default.</param>
    public static Part New(string id, string name, string template, string ink, float lineWidth, Func<string, Part?>? findTemplate = null, string? hair = null)
    {
        var source = template switch
        {
            "hair" => hair ?? PersonLoadout.ShortHair,
            "beard" => "brute-beard",
            "skirt" => "cinder-hide-wrap",
            "sandal" => "sandal-left",
            "boot" => "boot-left",
            "blank" => null,
            _ => throw new InvalidDataException("Unknown appearance template.")
        };
        var art = source is null
            ? new Part
            {
                Usage = "clothing",
                Attachment = Humanoid.BodySocket,
            }
            : Part.FromJson((findTemplate?.Invoke(source) ?? throw new InvalidDataException($"Appearance template '{source}' was not found.")).ToJson());

        art.Id = id;
        art.Name = name;
        foreach (var clip in art.Clips) clip.Model = id;
        art.Ink = ink;
        art.LineWidth = lineWidth;
        art.BackView = null;
        if (source is null)
        {
            art.Geometry.Add(PartGeometryBuilder.Extrude([new(-.15f, 0), new(.15f, 0), new(.15f, .3f), new(-.15f, .3f)], .02f, "#b97549"));
        }

        art.Validate();
        return art;
    }

    public static Part Duplicate(Part source, string id, string name)
    {
        var copy = Part.FromJson(source.ToJson());
        copy.Id = id;
        copy.Name = name;
        foreach (var clip in copy.Clips) clip.Model = id;
        copy.Validate();
        return copy;
    }

    /// <summary>Build and validate before replacing anything, so an invalid outline leaves the last good drawing intact.</summary>
    public static void Cutout(Part art, int index, IEnumerable<PuppetPoint> points, float thickness)
    {
        var old = art.Geometry[index];
        var mesh = PartGeometryBuilder.Extrude(points, thickness, old.RenderMaterial.Fill!);
        mesh.Id = old.Id;
        mesh.A = old.A;
        mesh.Frame = old.Frame;
        mesh.Material = old.Material;
        new Part
        {
            Id = "check",
            Name = "Check",
            Geometry = [mesh]
        }.Validate();
        art.Geometry[index] = mesh;
    }
}

using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

public sealed record CharacterDrawOptions(bool Joints = false, bool Guides = true, bool Trails = true, string Mode = "filled", bool Rest = false, FacePose? Face = null);

/// <summary>Reusable anatomy workspace. Build and draw actors sequentially with one workspace per library, rather than allocating meshes per actor.</summary>
public sealed class CharacterGeometry
{
    private readonly PointLibrary _library;
    private readonly Vector3[] _pose;
    private readonly ICharacterAnatomy _anatomy;
    public CharacterMesh Body { get; } = new();
    public CharacterMesh Face { get; } = new(1024);
    public CharacterMesh Backdrop { get; } = new(2048);
    public IReadOnlyList<Vector3> Pose => _pose;

    public CharacterGeometry(PointLibrary library)
    {
        _library = library; _pose = new Vector3[library.PointNames.Count];
        _anatomy = library.Anatomy switch
        {
            "person" => new PersonAnatomy(library), "hound" => new HoundAnatomy(library),
            "monster" => new MonsterAnatomy(library), "inventory" => new InventoryAnatomy(library),
            _ => throw new InvalidDataException("Unknown character anatomy: " + library.Anatomy)
        };
    }
    public void Build(PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options, bool holdEnd = false)
    {
        if (!_library.Clips.TryGetValue(clip.Id, out var owned) || !ReferenceEquals(owned, clip)) throw new ArgumentException("Clip belongs to another library.");
        clip.Sample(time, _pose, holdEnd);
        Body.Clear(); Face.Clear(); Backdrop.Clear();
        _anatomy.Build(_pose, clip, time, look, options, Body, Face, Backdrop);
        if (options.Guides)
            Backdrop.Line(new(-100, 0, 8), new(100, 0, 8), .008f, new Color(115, 133, 143));
    }
}

internal interface ICharacterAnatomy
{
    void Build(Vector3[] raw, PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options,
        CharacterMesh mesh, CharacterMesh face, CharacterMesh backdrop);
}

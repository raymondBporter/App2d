using App2d.Core.Characters.Geometry;
using App2d.Core.Json;
using App2d.Core.Mathematics.Transforms;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>A typed rig operation. The discriminator selects code; references and parameters belong to the resource.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ModelChain), "two-bone-ik")]
public abstract record RigConstraint
{
    public string Id { get; set; } = "";
}

/// <summary>
/// A two-bone IK constraint over rigid bone frames (<see cref="BoneFrameIk"/>): the root and joint are bone nodes with lengths,
/// the joint and end bound to their parents' tips; the solve turns the root and joint frames and carries their subtrees.
/// </summary>
public sealed record ModelChain : RigConstraint
{
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
    public string Frame { get; set; } = CharacterModel.Locomotion;
    public string Scale { get; set; } = CharacterModel.Unit;
}

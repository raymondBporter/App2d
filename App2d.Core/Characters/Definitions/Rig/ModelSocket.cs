using App2d.Core.Characters.Geometry;
using App2d.Core.Json;
using App2d.Core.Mathematics.Transforms;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// A named attachment frame for faces, equipment and collision. Its rigid local transform is carried by the chosen
/// control frame, with a separate drawing-layer offset. Orient tracks add local 3D rotation. Not structural.
/// </summary>
public sealed record ModelSocket
{
    public string Id { get; set; } = "";
    public string Control { get; set; } = "";
    /// <summary>The control whose rotation orients the socket. Null uses Control's own; locomotion keeps the screen axes.</summary>
    public string? Frame { get; set; }
    /// <summary>When set, local +Y points from Control to this control in XY. Useful for clothing following a torso segment.</summary>
    public string? Toward { get; set; }
    /// <summary>Local XY translation and rest rotation. The authored JSON keeps offsetX, offsetY and angle.</summary>
    public RigidTransform2D Transform { get; set; }
    public float Layer { get; set; }
    [JsonIgnore] public LayeredFrame2D LocalPlacement => new(Transform.ToFrame(), Layer);
    /// <summary>The slot contract (P10b): the <see cref="Part.Usage"/> of the parts this socket takes; null takes any.</summary>
    public string? Usage { get; set; }
}

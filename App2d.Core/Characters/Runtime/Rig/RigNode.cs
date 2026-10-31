using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One compiled rig node: the parent-local similarity setup every control resolves to, its dimension, link and joint limits,
/// and its derived rest frame in the character frame. The evaluator composes <c>nodeInCharacter = nodeLocal * parentInCharacter</c>
/// from these; positions, directions and angles are read from the result.
/// </summary>
public sealed record RigNode(
    string Id,
    string? Parent,
    string Kind,
    RigSetup Setup,
    float Length,
    RigLink? Link,
    Matrix3x2 RestFrame,
    float RestAngle,
    float RestLayer,
    int Handedness,
    RigLimit? Limit = null)
{
    public Vector2 ScaleVector => Setup.ScaleVector;
    public float Rotation => Setup.Rotation;
    /// <summary>The parent-local setup layer; RestLayer is the accumulated layer in the character frame.</summary>
    public float Layer => Setup.Layer;
    public bool CharacterOriented => Link?.Orientation == RigLinkOrientations.Character;
    /// <summary>Compatibility view packing the rest XY origin and drawing layer. No physical Z is stored here.</summary>
    public Vector3 Rest => new(RestFrame.M31, RestFrame.M32, RestLayer);
    /// <summary>The setup origin in the parent's local axes, before animation: the link's bound position or the setup translation.</summary>
    public Vector2 Origin(float parentLength) => Link is { } link ? link.Origin(parentLength) : Setup.Translation;
}

using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One compiled rig node: the parent-local similarity setup every control resolves to, its dimension, link and joint limits,
/// and its derived rest frame in the character frame. The evaluator composes <c>nodeLocal.Then(parentInCharacter)</c>
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
    public Vector2f ScaleVector => Setup.ScaleVector;
    public float Rotation => Setup.Rotation;
    /// <summary>The parent-local setup layer; RestLayer is the accumulated layer in the character frame.</summary>
    public float Layer => Setup.Layer;
    public bool CharacterOriented => Link?.Orientation == RigLinkOrientations.Character;
    /// <summary>Compatibility view packing the rest XY origin and drawing layer. No physical Z is stored here.</summary>
    public Vector3f Rest => new(RestFrame.M31, RestFrame.M32, RestLayer);
    /// <summary>The setup origin in the parent's local axes, before animation: the link's bound position or the setup translation.</summary>
    public Vector2f Origin(float parentLength) => Link is { } link ? link.Origin(parentLength) : Setup.Translation;
}

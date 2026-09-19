using System.Numerics;

namespace App2d.Noodle.Joints;

/// <summary>Constrains a requested child anchor relative to a parent anchor.</summary>
internal interface IJoint2D
{
    string Name { get; }
    string Constraint { get; }
    Vector2 Constrain(Vector2 parentAnchor, Vector2 requestedChildAnchor);
}

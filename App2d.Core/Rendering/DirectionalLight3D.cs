using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>
/// White light from one direction plus a constant ambient term, shaded per normal with Lambert's cosine law:
/// <c>Ambient + Diffuse * max(0, normal · TowardLight)</c>. When the two terms sum to 1, a face turned straight at the
/// light keeps its own color and every other face is darker, down to <see cref="Ambient"/>.
/// </summary>
public readonly record struct DirectionalLight3D
{
    public DirectionalLight3D(Vector3 towardLight, float ambient, float diffuse)
    {
        TowardLight = Vector3.Normalize(towardLight);
        Ambient = ambient;
        Diffuse = diffuse;
    }

    /// <summary>Unit vector from a surface toward the light.</summary>
    public Vector3 TowardLight { get; }
    public float Ambient { get; }
    public float Diffuse { get; }

    /// <summary>The flat facet shading for character props: soft, so ink and silhouette carry the form.</summary>
    public static DirectionalLight3D Props { get; } = new(new(-.3f, .5f, -1), .72f, .28f);

    /// <summary>Brightness for a unit normal; multiply a color's RGB by it.</summary>
    public float Shade(Vector3 normal) => Ambient + Diffuse * MathF.Max(0, Vector3.Dot(normal, TowardLight));
}

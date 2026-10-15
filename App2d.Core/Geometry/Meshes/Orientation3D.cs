using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Meshes;

/// <summary>Orientation of three 3D basis vectors, including reflections.</summary>
public static class Orientation3D
{
    /// <summary>Returns +1 for a right-handed basis, -1 for a reflected basis, or false for a flat/invalid basis.</summary>
    public static bool TryGetHandedness(Vector3 along, Vector3 across, Vector3 normal, out int handedness)
    {
        if (!NumericValidation.IsFinite(along) || !NumericValidation.IsFinite(across) ||
            !NumericValidation.IsFinite(normal))
        {
            handedness = 0;
            return false;
        }
        var volume = along.ToVector3d().Cross(across.ToVector3d()).Dot(normal.ToVector3d());
        handedness = Math.Sign(volume);
        return handedness != 0;
    }
}

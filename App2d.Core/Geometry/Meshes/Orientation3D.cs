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
        var x = (double)along.Y * across.Z - (double)along.Z * across.Y;
        var y = (double)along.Z * across.X - (double)along.X * across.Z;
        var z = (double)along.X * across.Y - (double)along.Y * across.X;
        var volume = x * normal.X + y * normal.Y + z * normal.Z;
        handedness = Math.Sign(volume);
        return handedness != 0;
    }
}

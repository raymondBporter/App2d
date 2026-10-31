namespace App2d.Core.Mathematics.Transforms;

public static class Matrix4x4Extensions
{
    public static bool IsFinite(this System.Numerics.Matrix4x4 matrix)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                if (!float.IsFinite(matrix[row, column])) return false;
            }
        }

        return true;
    }
}

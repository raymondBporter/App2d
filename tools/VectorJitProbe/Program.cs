using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VectorJitProbe;

// Keep each entry point visible to the JIT disassembler; everything inside it should inline completely.
// F* probes are float vectors (System.Numerics wrappers); D* probes are double vectors (inherited formulas).
public static class Probes
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float F2Length(Vector2f v) => v.Length();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector2f F2Normalized(Vector2f v) => v.Normalized();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float F2Distance(Vector2f a, Vector2f b) => a.Distance(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector2f F2Clamp(Vector2f v, Vector2f minimum, Vector2f maximum) => v.Clamp(minimum, maximum);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float F2Cross(Vector2f a, Vector2f b) => a.Cross(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector2f F2Reject(Vector2f a, Vector2f unitNormal) => a.Reject(unitNormal);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float F2GenericLength(Vector2f v) => GenericLength<Vector2f, float>(v);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector2f F2GenericLerp(Vector2f a, Vector2f b, float t) => GenericLerp<Vector2f, float>(a, b, t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector3f F3Cross(Vector3f a, Vector3f b) => a.Cross(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector3f F3Normalized(Vector3f v) => v.Normalized();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector4f F4Lerp(Vector4f a, Vector4f b, float t) => a.Lerp(b, t);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float F4Dot(Vector4f a, Vector4f b) => a.Dot(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double D2Length(Vector2d v) => v.Length();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector2d D2Divide(Vector2d v, double scale) => v / scale;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double D2Cross(Vector2d a, Vector2d b) => a.Cross(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double D2GenericLength(Vector2d v) => GenericLength<Vector2d, double>(v);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector3d D3Cross(Vector3d a, Vector3d b) => a.Cross(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double D3Distance(Vector3d a, Vector3d b) => a.Distance(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double D4Dot(Vector4d a, Vector4d b) => Vector4d.Dot(a, b);

    private static TReal GenericLength<TVector, TReal>(TVector value)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => TVector.Length(value);

    private static TVector GenericLerp<TVector, TReal>(TVector a, TVector b, TReal t)
        where TVector : unmanaged, IVector<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal> => TVector.Lerp(a, b, t);
}

internal static class Program
{
    public static void Main()
    {
        var a = new Vector2f(3, 4);
        var b = new Vector2f(1, 0);
        Check(Probes.F2Length(a) == 5f, nameof(Probes.F2Length));
        Check(MathF.Abs(Probes.F2Normalized(a).X - 0.6f) < 1e-6f, nameof(Probes.F2Normalized));
        Check(MathF.Abs(Probes.F2Distance(a, b) - MathF.Sqrt(20)) < 1e-6f, nameof(Probes.F2Distance));
        Check(Probes.F2Clamp(new(9, -9), b, a) == new Vector2f(3, 0), nameof(Probes.F2Clamp));
        Check(Probes.F2Cross(a, b) == -4f, nameof(Probes.F2Cross));
        Check(Probes.F2Reject(a, b) == new Vector2f(0, 4), nameof(Probes.F2Reject));
        Check(Probes.F2GenericLength(a) == 5f, nameof(Probes.F2GenericLength));
        Check(Probes.F2GenericLerp(a, b, 0.5f) == new Vector2f(2, 2), nameof(Probes.F2GenericLerp));
        Check(Probes.F3Cross(Vector3f.UnitX, Vector3f.UnitY) == Vector3f.UnitZ, nameof(Probes.F3Cross));
        Check(MathF.Abs(Probes.F3Normalized(new(0, 3, 4)).Z - 0.8f) < 1e-6f, nameof(Probes.F3Normalized));
        Check(Probes.F4Lerp(new(1, 2, 3, 4), new(5, 6, 7, 8), 0.5f) == new Vector4f(3, 4, 5, 6), nameof(Probes.F4Lerp));
        Check(Probes.F4Dot(new(1, 2, 3, 4), Vector4f.One) == 10f, nameof(Probes.F4Dot));
        Check(Probes.D2Length(new(3, 4)) == 5d, nameof(Probes.D2Length));
        Check(Probes.D2Divide(new(3, 4), 2) == new Vector2d(1.5, 2), nameof(Probes.D2Divide));
        Check(Probes.D2Cross(new(3, 4), new(1, 0)) == -4d, nameof(Probes.D2Cross));
        Check(Probes.D2GenericLength(new(3, 4)) == 5d, nameof(Probes.D2GenericLength));
        Check(Probes.D3Cross(Vector3d.UnitX, Vector3d.UnitY) == Vector3d.UnitZ, nameof(Probes.D3Cross));
        Check(Probes.D3Distance(new(0, 3, 4), default) == 5d, nameof(Probes.D3Distance));
        Check(Probes.D4Dot(new(1, 2, 3, 4), Vector4d.One) == 10d, nameof(Probes.D4Dot));
        Console.WriteLine($"Vector JIT probe passed: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}.");
    }

    private static void Check(bool condition, string operation)
    {
        if (!condition) throw new InvalidOperationException($"Unexpected result for {operation}.");
    }
}

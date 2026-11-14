using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Matrix2x3fTests
{
    [Fact]
    public void TranslationTimesRotationRotatesFirst()
    {
        var rotation = Matrix2x3f.CreateRotation(MathF.PI / 2);
        var translation = Matrix2x3f.CreateTranslation(new(10, 0));

        Assert.Equal(new Vector2f(10, 1), translation * rotation * Vector2f.UnitX);
        Assert.Equal(translation * rotation, rotation.Then(translation));
        Assert.Equal(Vector2f.UnitY, (translation * rotation).TransformDirection(Vector2f.UnitX));
    }

    [Fact]
    public void CompositionAndInteropMatchNumericsForShearReflectionAndTranslation()
    {
        var local = new Matrix3x2(1.5f, -.25f, .75f, 2, 3, 5);
        var parent = new Matrix3x2(-2, .5f, .125f, 4, 10, -7);
        var point = new Vector2f(2, -3);
        var localColumns = Matrix2x3f.FromNumerics(local);
        var parentColumns = Matrix2x3f.FromNumerics(parent);
        var composed = parentColumns * localColumns;

        Assert.Equal(local, localColumns.ToNumerics());
        Assert.Equal(parent, parentColumns.ToNumerics());
        Assert.Equal(local * parent, composed.ToNumerics());
        Assert.Equal(local * parent, local.Then(parent));
        Assert.Equal(Vector2f.Transform(point, local * parent), composed * point);
        Assert.Equal(Vector2f.TransformNormal(point, local * parent), composed.TransformDirection(point));
    }
}

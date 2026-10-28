using App2d.Core.Json;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Mathematics;

public sealed class Affine2DTests
{

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonfiniteAuthoredChannelsFailValidation(float bad)
    {
        foreach (var transform in new Affine2D[]
        {
            new() { X = bad }, new() { Y = bad }, new() { Rotation = bad },
            new() { ScaleX = bad }, new() { ScaleY = bad },
            new() { ShearX = bad }, new() { ShearY = bad }
        })
        {
            Assert.False(transform.IsFinite);
            var error = Assert.Throws<InvalidDataException>(() => transform.Validate("bone"));
            Assert.Equal("bone: transform values must be finite.", error.Message);
        }
    }

    [Fact]
    public void FiniteChannelsAllowCollapseAndAreDistinctFromAnEvaluatedMatrixOverflow()
    {
        var collapsed = new Affine2D { Scale = Vector2.Zero };
        Assert.True(collapsed.IsFinite);
        collapsed.Validate("collapsed");
        Assert.False(collapsed.TryInverse(out _));

        var overflow = new Affine2D { Rotation = float.MaxValue, ShearX = float.MaxValue };
        Assert.True(overflow.IsFinite);
        overflow.Validate("overflow");
        Assert.False(overflow.Matrix.IsFinite());
        Assert.False(overflow.TryInverse(out _));
    }

    [Fact]
    public void ComposingRotationWithNonuniformScaleRetainsTheIntroducedShear()
    {
        var rotation = new Affine2D { Rotation = MathF.PI / 4 };
        var scale = new Affine2D { Scale = new(2, 1) };
        var composed = rotation.Then(scale);
        Assert.NotEqual(0, composed.ShearY);
        var point = new Vector2(3, -2);
        Near(scale.TransformPoint(rotation.TransformPoint(point)), composed.TransformPoint(point));
        Assert.True(composed.TryInverse(out var inverse));
        Near(point, inverse.TransformPoint(composed.TransformPoint(point)));
    }

    [Fact]
    public void TrsMatchesExistingSceneMatricesAndShearComposesInRowVectorOrder()
    {
        var transform = new Affine2D { Position = new(3, -4), Rotation = .7f, Scale = new(-2, 3) };
        var matrix = Matrix3x2.CreateScale(-2, 3) * Matrix3x2.CreateRotation(.7f) * Matrix3x2.CreateTranslation(3, -4);
        Assert.Equal(matrix, transform.Matrix);
        transform.ShearX = -.2f;
        transform.ShearY = .3f;
        var parent = new Affine2D { Position = new(7, 2), Rotation = -1.1f, Scale = new(.8f, 1.4f), ShearY = -.4f };
        var composed = transform.Then(parent);
        var point = new Vector2(1.2f, -.8f);
        Near(parent.TransformPoint(transform.TransformPoint(point)), composed.TransformPoint(point));
        Near(Vector2.TransformNormal(point, transform.Matrix), transform.TransformDirection(point));
        Assert.True(composed.TryInverse(out var inverse));
        Near(point, inverse.TransformPoint(composed.TransformPoint(point)));
        Near(Vector2.Zero, Affine2D.Identity.TransformPoint(Vector2.Zero));
    }

    public static TheoryData<float, float, float, float, float, float> Matrices => new()
    {
        { 1, 0, 0, 1, 0, 0 },
        { -2, .3f, 1.1f, 3, 7, 2 },
        { 0, 0, 2, 3, 4, 5 },
        { 2, 3, 4, 6, 7, 8 },
        { 0, 0, 0, 0, 2, 4 }
    };

    [Theory, MemberData(nameof(Matrices))]
    public void MatrixConversionAndSnapshotsRetainReflectionsAndSingularMaps(float m11, float m12, float m21, float m22, float m31, float m32)
    {
        var matrix = new Matrix3x2(m11, m12, m21, m22, m31, m32);
        var affine = Affine2D.FromMatrix(matrix);
        Assert.Equal(matrix, affine.Matrix);
        Assert.Equal(matrix, affine.EvaluateMatrix(affine.Position, affine.Rotation, affine.Scale, new(affine.ShearX, affine.ShearY)));
        Assert.Equal(matrix, (affine with
        {
        }).Matrix);
        var restored = new Affine2D();
        restored.CopyFrom(affine);
        Assert.Equal(matrix, restored.Matrix);
        Near(Vector2.Transform(new(2, -3), matrix), affine.TransformPoint(new(2, -3)));
        Assert.Equal(matrix.GetDeterminant() != 0, affine.TryInverse(out _));
        var fromChannels = new Affine2D
        {
            Position = affine.Position,
            Rotation = affine.Rotation,
            Scale = affine.Scale,
            ShearX = affine.ShearX,
            ShearY = affine.ShearY
        };
        Near(affine.TransformPoint(new(2, -3)), fromChannels.TransformPoint(new(2, -3)));
    }

    [Fact]
    public void DirectChannelEvaluationAppliesScaleRotationThenTranslationAndAllowsCollapse()
    {
        var matrix = Affine2D.CreateMatrix(new(10, 20), MathF.PI / 2, new(2, 3));
        Near(new(4, 22), Vector2.Transform(new(1, 2), matrix));
        Near(new(-6, 2), Vector2.TransformNormal(new(1, 2), matrix));
        var collapsed = Affine2D.CreateMatrix(new(10, 20), 0, new(0, 3));
        Near(new(10, 26), Vector2.Transform(new(100, 2), collapsed));
        Assert.Equal(0, collapsed.GetDeterminant());
    }

    [Fact]
    public void ProposedChannelsMatchAnEditedCopyWithoutEditingOrNotifyingTheOwner()
    {
        var transform = Affine2D.FromMatrix(new(-2, .3f, 1.1f, 3, 7, 2));
        var before = JsonSerializer.Serialize(transform, JsonProfiles.CamelCase);
        var version = transform.Version;
        var notifications = 0;
        transform.Changed += () => notifications++;
        var expected = transform with { X = -3, Rotation = 2 * MathF.Tau, ScaleY = 0, ShearX = -.2f };
        var actual = transform.EvaluateMatrix(new(-3, transform.Y), 2 * MathF.Tau, new(transform.ScaleX, 0), new(-.2f, transform.ShearY));
        Assert.Equal(expected.Matrix, actual);
        Assert.Equal(before, JsonSerializer.Serialize(transform, JsonProfiles.CamelCase));
        Assert.Equal(version, transform.Version);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void EqualChannelProposalsKeepSignedZeroWhenAnotherChannelChanges()
    {
        var negativeZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var transform = new Affine2D { X = 1, ShearY = .1f };
        transform.X = negativeZero;
        var expected = transform with { X = 0, Y = 2, ShearY = .2f };
        var actual = transform.EvaluateMatrix(new(0, 2), 0, Vector2.One, new(0, .2f));
        Assert.Equal(expected.Matrix, actual);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(actual.M31));
    }

    [Fact]
    public void DirectAndProposedMatrixEvaluationAllocateNoManagedMemory()
    {
        var transform = new Affine2D { X = 2, Rotation = .4f, Scale = new(-2, 3), ShearY = .2f };
        float Sample() => transform.EvaluateMatrix(new(3, 4), .7f, new(-3, 2), new(.1f, -.2f)).M11
            + Affine2D.CreateMatrix(new(3, 4), .7f, new(-3, 2), new(.1f, -.2f)).M22;
        for (var i = 0; i < 100; i++) Sample();
        var total = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) total += Sample();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(float.IsFinite(total));
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void CopiesDoNotShareSubscribersAndCachesDoNotAffectValueEquality()
    {
        var transform = new Affine2D { Position = new(2, 3), Scale = new(2, 1), ShearY = .3f };
        var notifications = 0;
        transform.Changed += () => notifications++;
        var saved = transform with
        {
        };
        var hash = transform.GetHashCode();
        var version = transform.Version;
        _ = transform.Matrix;
        Assert.Equal(saved, transform);
        Assert.Equal(hash, transform.GetHashCode());
        Assert.Equal(version, transform.Version);
        var other = transform with
        {
            X = 9,
            ShearX = .4f
        };
        Assert.Equal(0, notifications);
        Assert.Equal(new Vector2(2, 3), transform.Position);
        transform.CopyFrom(other);
        Assert.Equal(1, notifications);
        Assert.Equal(other, transform);
        transform.CopyFrom(other);
        transform.Position = transform.Position;
        transform.Scale = transform.Scale;
        Assert.Equal(1, notifications);
        transform.CopyFrom(saved);
        Assert.Equal(2, notifications);
        Assert.Equal(saved, transform);
    }

    [Fact]
    public void ChannelEditsInvalidateSceneBoundsAndCollisionPose()
    {
        var placed = new SpatialObject2D(Rectangle2D.Builder.CreateFromSize(2, 2));
        var bounds = placed.WorldBounds;
        _ = placed.CollisionPose;
        placed.Transform.X = 3;
        Assert.NotEqual(bounds, placed.WorldBounds);
        Assert.Equal(new(3, 0), placed.CollisionPose.Translation);
        bounds = placed.WorldBounds;
        placed.Transform.ShearY = .4f;
        Assert.NotEqual(bounds, placed.WorldBounds);
        Assert.Throws<InvalidOperationException>(() => placed.CollisionPose);
        placed.Transform.ShearY = 0;
        placed.Transform.ScaleX = 0;
        Assert.False(placed.Transform.TryInverse(out _));
        Assert.False(placed.ContainsWorldPoint(new(3, 0)));
        Assert.Throws<InvalidOperationException>(() => placed.CollisionPose);
    }

    [Fact]
    public void JsonContainsOnlyTheExistingSevenAuthoredChannels()
    {
        var transform = new Affine2D { Position = new(2, 3), Rotation = .4f, Scale = new(-2, 1.5f), ShearX = .1f, ShearY = -.3f };
        _ = transform.Matrix;
        var json = JsonSerializer.Serialize(transform, JsonProfiles.CamelCase);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["rotation", "scaleX", "scaleY", "shearX", "shearY", "x", "y"], document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        var restored = JsonSerializer.Deserialize<Affine2D>(json, JsonProfiles.CamelCase)!;
        Assert.Equal(transform, restored);
        Near(transform.TransformPoint(new(1, 2)), restored.TransformPoint(new(1, 2)));
        Assert.False(Affine2D.TryFromMatrix(new(float.NaN, 0, 0, 1, 0, 0), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => Affine2D.FromMatrix(new(1, 0, 0, 1, float.PositiveInfinity, 0)));
    }

    [Theory]
    [InlineData(1e20f)]
    [InlineData(1e-20f)]
    public void InversionWorksWhenFloatDeterminantsWouldOverflowOrUnderflow(float scale)
    {
        var transform = new Affine2D { Scale = new(scale) };
        Assert.True(transform.TryInverse(out var inverse));
        var point = new Vector2(1, -2);
        Near(point, inverse.TransformPoint(transform.TransformPoint(point)));
    }

    private static void Near(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 2e-5f, $"Expected {expected}, found {actual}.");
}

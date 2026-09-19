using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

/// <summary>
/// A volume-preserving 2D skin. Cross-section centers use two-bone linear blend skinning;
/// their width is reconstructed from the deformed tangent so a bent elbow stays round.
/// </summary>
internal sealed class NoodleArm2D
{
    private static readonly XnaColor DefaultSkinColor = new(241, 166, 105);
    private static readonly XnaColor UpperWeightColor = new(255, 102, 116);
    private static readonly XnaColor LowerWeightColor = new(70, 190, 255);
    private static readonly XnaColor OutlineColor = new(255, 226, 183, 235);
    private static readonly XnaColor BoneColor = new(248, 244, 221, 220);
    private static readonly XnaColor JointColor = new(21, 29, 43, 255);

    private readonly int _sectionCount;
    private readonly float _radius;
    private readonly Vector2[] _centers;
    private readonly Vector2[] _upperEdge;
    private readonly Vector2[] _lowerEdge;
    private readonly float[] _lowerWeights;
    private readonly Vector2 _defaultTarget;
    private readonly int _defaultBendDirection;
    private readonly XnaColor _skinColor;
    private readonly WorldObject2D _shoulderCap;
    private readonly WorldObject2D _wristCap;
    private readonly SolidColorShader _skinShader;
    private readonly SolidColorShader _upperWeightShader = new(UpperWeightColor);
    private readonly SolidColorShader _lowerWeightShader = new(LowerWeightColor);

    public NoodleArm2D(
        Vector2 shoulder,
        float upperLength,
        float lowerLength,
        float radius,
        Vector2? defaultTarget = null,
        int bendDirection = 1,
        XnaColor? skinColor = null,
        int sectionCount = 36)
    {
        Shoulder = shoulder;
        UpperLength = upperLength;
        LowerLength = lowerLength;
        _radius = radius;
        _sectionCount = sectionCount;
        _defaultTarget = defaultTarget ?? shoulder + new Vector2(235f, 105f);
        _defaultBendDirection = Math.Sign(bendDirection);
        BendDirection = _defaultBendDirection;
        _skinColor = skinColor ?? DefaultSkinColor;
        _skinShader = new SolidColorShader(_skinColor);
        _centers = new Vector2[sectionCount + 1];
        _upperEdge = new Vector2[sectionCount + 1];
        _lowerEdge = new Vector2[sectionCount + 1];
        _lowerWeights = new float[sectionCount + 1];
        _shoulderCap = new WorldObject2D(new Circle2D(radius), _skinShader);
        _wristCap = new WorldObject2D(new Circle2D(radius * 0.72f), _skinShader);
        Target = _defaultTarget;
        RebuildSkin();
    }

    public Vector2 Shoulder { get; }
    public float UpperLength { get; }
    public float LowerLength { get; }
    public Vector2 Target { get; private set; }
    public int BendDirection { get; private set; }
    public TwoBoneIkPose Pose { get; private set; }
    public Vector2 DefaultTarget => _defaultTarget;

    public void SetTarget(Vector2 target)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y))
            return;

        Target = target;
        RebuildSkin();
    }

    public void Reset()
    {
        BendDirection = _defaultBendDirection;
        Target = _defaultTarget;
        RebuildSkin();
    }

    public void FlipBend()
    {
        BendDirection = -BendDirection;
        RebuildSkin();
    }

    public void Render(Renderer2D renderer, bool showWeights)
    {
        _shoulderCap.Shader = showWeights ? _upperWeightShader : _skinShader;
        _wristCap.Shader = showWeights ? _lowerWeightShader : _skinShader;
        renderer.Draw(_shoulderCap);

        Span<Vector2> quad = stackalloc Vector2[4];
        for (var section = 0; section < _sectionCount; section++)
        {
            quad[0] = _lowerEdge[section];
            quad[1] = _lowerEdge[section + 1];
            quad[2] = _upperEdge[section + 1];
            quad[3] = _upperEdge[section];
            var weight = (_lowerWeights[section] + _lowerWeights[section + 1]) * 0.5f;
            if (showWeights)
                renderer.DrawWorldConvexPolygon(quad, XnaColor.Lerp(UpperWeightColor, LowerWeightColor, weight));
            else
                renderer.DrawWorldConvexPolygon(quad, _skinColor);
        }

        renderer.Draw(_wristCap);
        renderer.DrawWorldPolyline(_upperEdge, OutlineColor, 2f);
        renderer.DrawWorldPolyline(_lowerEdge, OutlineColor, 2f);

        Span<Vector2> upperBone = [Pose.Shoulder, Pose.Elbow];
        Span<Vector2> lowerBone = [Pose.Elbow, Pose.Wrist];
        renderer.DrawWorldPolyline(upperBone, BoneColor, 5f);
        renderer.DrawWorldPolyline(lowerBone, BoneColor, 5f);
        renderer.DrawWorldCircle(Pose.Shoulder, 8f, JointColor, 5f);
        renderer.DrawWorldCircle(Pose.Elbow, 9f, JointColor, 5f);
        renderer.DrawWorldCircle(Pose.Wrist, 8f, JointColor, 5f);
    }

    private void RebuildSkin()
    {
        Pose = TwoBoneIk2D.Solve(Shoulder, Target, UpperLength, LowerLength, BendDirection);

        var bindUpper = Matrix3x2.CreateTranslation(Shoulder);
        var bindLower = Matrix3x2.CreateTranslation(Shoulder + new Vector2(UpperLength, 0f));
        Matrix3x2.Invert(bindUpper, out var inverseBindUpper);
        Matrix3x2.Invert(bindLower, out var inverseBindLower);
        var currentUpper = Matrix3x2.CreateRotation(Pose.UpperAngle) * Matrix3x2.CreateTranslation(Shoulder);
        var currentLower = Matrix3x2.CreateRotation(Pose.LowerAngle) * Matrix3x2.CreateTranslation(Pose.Elbow);
        var upperSkin = inverseBindUpper * currentUpper;
        var lowerSkin = inverseBindLower * currentLower;

        var totalLength = UpperLength + LowerLength;
        var blendRadius = MathF.Min(UpperLength, LowerLength) * 0.42f;
        for (var section = 0; section <= _sectionCount; section++)
        {
            var distance = totalLength * section / _sectionCount;
            var restCenter = Shoulder + new Vector2(distance, 0f);
            var lowerWeight = Interpolation.SmoothStep(
                (distance - (UpperLength - blendRadius)) / (blendRadius * 2f));
            var upperPoint = Vector2.Transform(restCenter, upperSkin);
            var lowerPoint = Vector2.Transform(restCenter, lowerSkin);
            _centers[section] = Vector2.Lerp(upperPoint, lowerPoint, lowerWeight);
            _lowerWeights[section] = lowerWeight;
        }

        for (var section = 0; section <= _sectionCount; section++)
        {
            var tangent = section switch
            {
                0 => _centers[1] - _centers[0],
                _ when section == _sectionCount => _centers[^1] - _centers[^2],
                _ => _centers[section + 1] - _centers[section - 1]
            };
            if (tangent.LengthSquared() <= float.Epsilon)
                tangent = Vector2.UnitX;
            else
                tangent = Vector2.Normalize(tangent);

            var normal = new Vector2(-tangent.Y, tangent.X);
            var progress = section / (float)_sectionCount;
            var sectionRadius = _radius * (1f - 0.28f * progress);
            _upperEdge[section] = _centers[section] + normal * sectionRadius;
            _lowerEdge[section] = _centers[section] - normal * sectionRadius;
        }

        _shoulderCap.Transform.Position = _centers[0];
        _wristCap.Transform.Position = _centers[^1];
    }

}

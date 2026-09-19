using App2d.Core.Geometry;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Noodle;

internal sealed class NoodlePerson2D
{
    private static readonly XnaColor BodyColor = new(151, 111, 255);
    private static readonly XnaColor FaceColor = new(27, 25, 48);
    private static readonly XnaColor SelectedControlColor = new(117, 255, 178);
    private static readonly XnaColor AvailableControlColor = new(197, 187, 232, 210);

    private readonly SolidColorShader _bodyShader = new(BodyColor);
    private readonly NoodleArm2D _leftArm;
    private readonly NoodleArm2D _rightArm;
    private readonly NoodleArm2D _leftLeg;
    private readonly NoodleArm2D _rightLeg;
    private readonly WorldObject2D _torso;
    private readonly WorldObject2D _pelvis;
    private readonly WorldObject2D _neck;
    private readonly WorldObject2D _head;
    private readonly WorldObject2D _leftEye;
    private readonly WorldObject2D _rightEye;
    private readonly LimbEntry[] _limbs;
    private int _selectedLimbIndex = 1;

    public NoodlePerson2D()
    {
        _leftArm = new NoodleArm2D(
            new Vector2(-43f, 74f), 112f, 103f, 23f,
            new Vector2(-128f, -62f), bendDirection: -1, skinColor: BodyColor);
        _rightArm = new NoodleArm2D(
            new Vector2(43f, 74f), 120f, 112f, 25f,
            new Vector2(225f, 112f), bendDirection: 1, skinColor: BodyColor);
        _leftLeg = new NoodleArm2D(
            new Vector2(-27f, -63f), 126f, 116f, 29f,
            new Vector2(-43f, -292f), bendDirection: -1, skinColor: BodyColor);
        _rightLeg = new NoodleArm2D(
            new Vector2(27f, -63f), 126f, 116f, 29f,
            new Vector2(53f, -292f), bendDirection: 1, skinColor: BodyColor);
        _limbs =
        [
            new("left hand", _leftArm),
            new("right hand", _rightArm),
            new("left foot", _leftLeg),
            new("right foot", _rightLeg)
        ];

        _torso = Create(new Capsule2D(new Vector2(0f, -49f), new Vector2(0f, 47f), 45f), new(0f, 8f));
        _pelvis = Create(new Capsule2D(new Vector2(-18f, 0f), new Vector2(18f, 0f), 34f), new(0f, -57f));
        _neck = Create(new Capsule2D(new Vector2(0f, -11f), new Vector2(0f, 15f), 18f), new(0f, 93f));
        _head = Create(new Circle2D(47f), new(0f, 151f));
        var faceShader = new SolidColorShader(FaceColor);
        _leftEye = Create(new Circle2D(5f), new(-15f, 159f), faceShader);
        _rightEye = Create(new Circle2D(5f), new(15f, 159f), faceShader);
    }

    public NoodleArm2D TargetLimb => _limbs[_selectedLimbIndex].Limb;
    public string SelectedControlName => _limbs[_selectedLimbIndex].Name;

    public void SelectNearestControl(Vector2 position)
    {
        var bestDistanceSquared = float.PositiveInfinity;
        var bestLimbIndex = 0;
        for (var limbIndex = 0; limbIndex < _limbs.Length; limbIndex++)
        {
            var limb = _limbs[limbIndex].Limb;
            var distanceSquared = Vector2.DistanceSquared(position, limb.Target);
            if (distanceSquared > bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestLimbIndex = limbIndex;
        }

        _selectedLimbIndex = bestLimbIndex;
    }

    public void Reset()
    {
        _leftArm.Reset();
        _rightArm.Reset();
        _leftLeg.Reset();
        _rightLeg.Reset();
        _selectedLimbIndex = 1;
    }

    public void Render(Renderer2D renderer, bool showWeights)
    {
        _leftArm.Render(renderer, showWeights);
        _leftLeg.Render(renderer, showWeights);
        _rightLeg.Render(renderer, showWeights);
        renderer.Draw(_neck);
        renderer.Draw(_torso);
        renderer.Draw(_pelvis);
        _rightArm.Render(renderer, showWeights);
        renderer.Draw(_head);
        renderer.Draw(_leftEye);
        renderer.Draw(_rightEye);

        Span<Vector2> mouth = [new(-14f, 137f), new(0f, 132f), new(14f, 137f)];
        renderer.DrawWorldPolyline(mouth, FaceColor, 3f);
    }

    public void RenderControls(Renderer2D renderer)
    {
        for (var limbIndex = 0; limbIndex < _limbs.Length; limbIndex++)
        {
            var limb = _limbs[limbIndex].Limb;
            var isSelected = limbIndex == _selectedLimbIndex;
            var color = isSelected ? SelectedControlColor : AvailableControlColor;
            renderer.DrawWorldCircle(limb.Target, isSelected ? 15f : 10f, color, isSelected ? 3f : 2f);
            if (!isSelected)
                continue;

            Span<Vector2> horizontal = [limb.Target - new Vector2(22f, 0f), limb.Target + new Vector2(22f, 0f)];
            Span<Vector2> vertical = [limb.Target - new Vector2(0f, 22f), limb.Target + new Vector2(0f, 22f)];
            renderer.DrawWorldPolyline(horizontal, color, 2f);
            renderer.DrawWorldPolyline(vertical, color, 2f);
        }
    }

    private WorldObject2D Create(IShape2D shape, Vector2 position, IShader2D? shader = null)
    {
        var item = new WorldObject2D(shape, shader ?? _bodyShader);
        item.Transform.Position = position;
        return item;
    }

    private readonly record struct LimbEntry(string Name, NoodleArm2D Limb);
}

using System.Numerics;
using App2d.Engine;

namespace App2d.Gameplay;

public sealed class SwordAttack2D(WorldObject2D worldObject)
{
    private const float AttackDuration = 0.24f;
    private const float CooldownDuration = 0.32f;
    private float _elapsed;
    private float _cooldown;

    public WorldObject2D WorldObject { get; } = worldObject;
    public int AttackId { get; private set; }
    public bool IsActive { get; private set; }

    public bool TryStart()
    {
        if (_cooldown > 0f)
            return false;

        AttackId++;
        _elapsed = 0f;
        _cooldown = CooldownDuration;
        IsActive = true;
        WorldObject.IsVisible = true;
        return true;
    }

    public void Update(float deltaSeconds, Vector2 ownerPosition, float facing)
    {
        _cooldown = Math.Max(0f, _cooldown - deltaSeconds);
        WorldObject.Transform.Position = ownerPosition + new Vector2(facing * 10f, 2f);

        if (!IsActive)
            return;

        _elapsed += deltaSeconds;
        var progress = Math.Clamp(_elapsed / AttackDuration, 0f, 1f);
        var rightFacingAngle = Lerp(1.18f, -1.08f, progress);
        WorldObject.Transform.Rotation = facing >= 0f
            ? rightFacingAngle
            : MathF.PI - rightFacingAngle;

        if (_elapsed < AttackDuration)
            return;

        IsActive = false;
        WorldObject.IsVisible = false;
    }

    public void Cancel()
    {
        IsActive = false;
        WorldObject.IsVisible = false;
    }

    private static float Lerp(float start, float end, float amount) =>
        start + (end - start) * amount;
}

using App2d.Gameplay.Persons.Actions;
using System.Numerics;

namespace App2d.Gameplay.Persons;

/// <summary>
/// Small deterministic command producer for the first hostile person. It uses
/// ordinary person commands and intentionally leaves advanced traversal alone.
/// </summary>
internal sealed class RivalBrain2D(float minimumX, float maximumX)
{
    private const float SwordRange = 105f;
    private const float GunRange = 520f;
    private float _attackDelaySeconds = 0.45f;
    private float _dashDelaySeconds = 1.2f;
    private float _jumpDelaySeconds;
    private float _jumpHoldSeconds;

    public PersonCommand2D Decide(
        Person2D person,
        PersonArsenal2D arsenal,
        Vector2 targetPosition,
        float deltaSeconds)
    {
        _attackDelaySeconds = Math.Max(0f, _attackDelaySeconds - deltaSeconds);
        _dashDelaySeconds = Math.Max(0f, _dashDelaySeconds - deltaSeconds);
        _jumpDelaySeconds = Math.Max(0f, _jumpDelaySeconds - deltaSeconds);
        _jumpHoldSeconds = Math.Max(0f, _jumpHoldSeconds - deltaSeconds);

        var offset = targetPosition - person.Position;
        var distanceX = MathF.Abs(offset.X);
        var wantsGun = distanceX > 175f;
        var switchWeapon = wantsGun
            ? !string.Equals(arsenal.EquipmentId, "gun", StringComparison.Ordinal)
            : !string.Equals(arsenal.EquipmentId, "sword", StringComparison.Ordinal);

        var moveX = ChooseMovement(person.Position.X, offset.X, wantsGun);
        var dashPressed =
            _dashDelaySeconds <= 0f &&
            person.IsGrounded &&
            distanceX is >= 250f and <= 430f &&
            MathF.Abs(offset.Y) <= 80f &&
            CanMove(person.Position.X, MathF.Sign(offset.X));
        if (dashPressed)
            _dashDelaySeconds = 2.1f;

        var jumpPressed =
            _jumpDelaySeconds <= 0f &&
            person.IsGrounded &&
            offset.Y >= 72f;
        if (jumpPressed)
        {
            _jumpDelaySeconds = 0.9f;
            _jumpHoldSeconds = 0.16f;
        }

        var useWeapon = false;
        if (_attackDelaySeconds <= 0f && MathF.Abs(offset.Y) <= 95f)
        {
            useWeapon = wantsGun
                ? distanceX <= GunRange
                : distanceX <= SwordRange;
            if (useWeapon)
                _attackDelaySeconds = wantsGun ? 0.72f : 0.48f;
        }

        return new PersonCommand2D(
            new PersonMovementIntent2D(
                moveX,
                jumpPressed,
                jumpPressed || _jumpHoldSeconds > 0f,
                JumpReleased: false,
                DropThroughPressed: false,
                dashPressed),
            useWeapon,
            targetPosition,
            switchWeapon);
    }

    private float ChooseMovement(float positionX, float targetOffsetX, bool usingGun)
    {
        if (positionX <= minimumX + 24f)
            return 1f;
        if (positionX >= maximumX - 24f)
            return -1f;

        var distance = MathF.Abs(targetOffsetX);
        var direction = MathF.Sign(targetOffsetX);
        if (usingGun)
        {
            if (distance < 155f)
                return -direction;
            if (distance > 300f)
                return direction;
            return 0f;
        }

        if (distance > 72f)
            return direction;
        if (distance < 38f)
            return -direction;
        return 0f;
    }

    private bool CanMove(float positionX, float direction) =>
        direction < 0f
            ? positionX > minimumX + 120f
            : positionX < maximumX - 120f;
}

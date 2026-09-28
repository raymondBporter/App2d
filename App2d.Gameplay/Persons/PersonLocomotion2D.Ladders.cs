using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.Persons;

public sealed partial class PersonLocomotion2D
{
    private readonly IChunkedTileMap2D? _tileMap;
    private float _ladderRelatchTime;

    public bool IsClimbingLadder { get; private set; }

    public void DetachFromLadder()
    {
        if (!IsClimbingLadder)
            return;
        IsClimbingLadder = false;
        _ladderRelatchTime = Metrics.LadderRelatchDelay;
        _body.GravityScale = 1f;
    }

    private bool TryUpdateLadder(float deltaSeconds)
    {
        // Down on a ladder still lands on the floor. Once supported, use normal
        // grounded movement (including an explicit down+jump drop-through).
        if (IsGrounded && _intent.ClimbY <= 0f)
        {
            DetachFromLadder();
            return false;
        }

        // A jump press while climbing always leaves the ladder; a jump press beside a
        // ladder jumps instead of grabbing it. Climbing itself comes only from ClimbY.
        var jumpOff = _intent.JumpPressed;
        if (IsClimbingLadder && jumpOff)
        {
            DetachFromLadder();
            _body.LinearVelocity = new Vector2(_intent.MoveX * Metrics.RunSpeed, Metrics.JumpSpeed);
            _jumpInitialSpeed = Metrics.JumpSpeed;
            _jumpBufferTime = _wallJumpBufferTime = _coyoteTime = 0f;
            IsGrounded = false;
            JumpStarted?.Invoke();
            return true;
        }

        if (MathF.Abs(_intent.MoveX) > 0.1f ||
            _ladderRelatchTime > 0f || jumpOff ||
            !TryFindLadder(out var ladder))
        {
            DetachFromLadder();
            return false;
        }
        if (!IsClimbingLadder && MathF.Abs(_intent.ClimbY) < 0.01f)
            return false;

        // Ease the collider (including its facing-dependent offset) toward the
        // ladder, only if this step is clear. Climbing retains solid collision.
        var bounds = _body.WorldObject.WorldBounds;
        var position = _body.WorldObject.Transform.Position;
        position.X += MoveTowards(bounds.Center.X, ladder.Center.X,
            Metrics.LadderAlignSpeed * deltaSeconds) - bounds.Center.X;
        if (!CanOccupy(position))
        {
            DetachFromLadder();
            return false;
        }
        _body.WorldObject.Transform.Position = position;

        IsClimbingLadder = true;
        IsGrounded = IsWallGripping = false;
        _wallDirection = 0f;
        _jumpInitialSpeed = _jumpBufferTime = _wallJumpBufferTime = _coyoteTime = 0f;
        RestoreAirJumps();
        _airDashAvailable = true;
        var speed = Math.Clamp(_intent.ClimbY, -1f, 1f) * Metrics.LadderClimbSpeed;
        // Keep the body's center at the ladder top so the climbing pose still
        // overlaps the last rung. Using the feet here lifts the whole body above it.
        if (speed > 0f && deltaSeconds > 0f)
            speed = Math.Min(speed, Math.Max(0f, (ladder.Top - bounds.Center.Y) / deltaSeconds));
        _body.LinearVelocity = new Vector2(0f, speed);
        _body.GravityScale = 0f;
        return true;
    }

    private bool TryFindLadder(out Bounds2D ladder)
    {
        ladder = default;
        if (_tileMap is null)
            return false;

        var bounds = _body.WorldObject.WorldBounds;
        var size = _tileMap.TileSize;
        var origin = _tileMap.Origin;
        // Give the center a little reach beyond either edge of a ladder tile.
        var firstX = Math.Max(0, (int)MathF.Floor((bounds.Center.X - Metrics.LadderGrabGrace - origin.X) / size));
        var lastX = Math.Min(_tileMap.Width - 1, (int)MathF.Floor((bounds.Center.X + Metrics.LadderGrabGrace - origin.X) / size));
        // Reach slightly below the feet to permit descending from a ledge.
        var firstY = Math.Max(0, (int)MathF.Floor((bounds.Bottom - Metrics.GroundProbeDistance - origin.Y) / size));
        var lastY = Math.Min(_tileMap.Height - 1, (int)MathF.Floor((bounds.Center.Y - origin.Y) / size));
        var nearestDistance = float.PositiveInfinity;
        for (var x = firstX; x <= lastX; x++)
        {
            var distance = MathF.Abs(origin.X + (x + 0.5f) * size - bounds.Center.X);
            if (distance >= nearestDistance)
                continue;
            for (var y = firstY; y <= lastY; y++)
            {
                if (!_tileMap.GetTileKind(x, y).IsLadder())
                    continue;
                var bottom = y;
                var top = y;
                while (bottom > 0 && _tileMap.GetTileKind(x, bottom - 1).IsLadder())
                    bottom--;
                while (top + 1 < _tileMap.Height && _tileMap.GetTileKind(x, top + 1).IsLadder())
                    top++;
                ladder = new Bounds2D(
                    origin + new Vector2(x, bottom) * size,
                    origin + new Vector2(x + 1, top + 1) * size);
                nearestDistance = distance;
                break;
            }
        }
        return float.IsFinite(nearestDistance);
    }
}

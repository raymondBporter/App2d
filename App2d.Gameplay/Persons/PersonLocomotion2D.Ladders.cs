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
        // W/Up doubles as climb and jump. A fresh press at the final rung
        // must jump away rather than be consumed by the upward climb clamp.
        var jumpAtTop = IsClimbingLadder && _intent.JumpPressed &&
            TryFindLadder(out var currentLadder) &&
            _body.WorldObject.WorldBounds.Center.Y >= currentLadder.Top - 0.01f;
        var jumpOff = _intent.LadderJumpPressed ||
            (_intent.JumpPressed && MathF.Abs(_intent.ClimbY) < 0.01f) || jumpAtTop;
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

        // Align the collider (including its facing-dependent offset), only if
        // the destination is clear. Climbing retains normal solid collision.
        var bounds = _body.WorldObject.WorldBounds;
        var position = _body.WorldObject.Transform.Position;
        position.X += ladder.Center.X - bounds.Center.X;
        if (!CanOccupy(position))
        {
            DetachFromLadder();
            return false;
        }
        _body.WorldObject.Transform.Position = position;
        if (_intent.ClimbY < 0f)
            TryBeginDropThrough();

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
        var x = (int)MathF.Floor((bounds.Center.X - origin.X) / size);
        if (x < 0 || x >= _tileMap.Width)
            return false;
        // Reach slightly below the feet to permit descending from a ledge.
        var firstY = Math.Max(0, (int)MathF.Floor((bounds.Bottom - Metrics.GroundProbeDistance - origin.Y) / size));
        var lastY = Math.Min(_tileMap.Height - 1, (int)MathF.Floor((bounds.Center.Y - origin.Y) / size));
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
            return true;
        }
        return false;
    }
}

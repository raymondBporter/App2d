using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons;

public sealed partial class PersonLocomotion2D
{
    internal sealed record SimulationState(
        PersonMovementIntent2D Intent,
        Vector2 PositionBeforePhysics,
        float VerticalSpeedBeforePhysics,
        float GravityScaleBeforePhysics,
        float CoyoteTime,
        float JumpBufferTime,
        float JumpInitialSpeed,
        float WallJumpBufferTime,
        float WallRelatchTime,
        float WallDirection,
        float DashTimeRemaining,
        float DashCooldownRemaining,
        float DashDirection,
        float LadderRelatchTime,
        bool WasGroundedBeforePhysics,
        bool AirDashAvailable,
        int AirJumpsRemaining,
        bool IsGrounded,
        bool IsWallGripping,
        bool IsDashing,
        bool IsClimbingLadder) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _intent, _positionBeforePhysics, _verticalSpeedBeforePhysics, _gravityScaleBeforePhysics, _coyoteTime, _jumpBufferTime, _jumpInitialSpeed, _wallJumpBufferTime, _wallRelatchTime, _wallDirection, _dashTimeRemaining, _dashCooldownRemaining, _dashDirection, _ladderRelatchTime, _wasGroundedBeforePhysics, _airDashAvailable, _airJumpsRemaining, IsGrounded, IsWallGripping, IsDashing, IsClimbingLadder);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _intent = state.Intent;
        _positionBeforePhysics = state.PositionBeforePhysics;
        _verticalSpeedBeforePhysics = state.VerticalSpeedBeforePhysics;
        _gravityScaleBeforePhysics = state.GravityScaleBeforePhysics;
        _coyoteTime = state.CoyoteTime;
        _jumpBufferTime = state.JumpBufferTime;
        _jumpInitialSpeed = state.JumpInitialSpeed;
        _wallJumpBufferTime = state.WallJumpBufferTime;
        _wallRelatchTime = state.WallRelatchTime;
        _wallDirection = state.WallDirection;
        _dashTimeRemaining = state.DashTimeRemaining;
        _dashCooldownRemaining = state.DashCooldownRemaining;
        _dashDirection = state.DashDirection;
        _ladderRelatchTime = state.LadderRelatchTime;
        _wasGroundedBeforePhysics = state.WasGroundedBeforePhysics;
        _airDashAvailable = state.AirDashAvailable;
        _airJumpsRemaining = state.AirJumpsRemaining;
        IsGrounded = state.IsGrounded;
        IsWallGripping = state.IsWallGripping;
        IsDashing = state.IsDashing;
        IsClimbingLadder = state.IsClimbingLadder;
    }
}

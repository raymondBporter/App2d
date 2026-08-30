using App2d.Core;
using App2d.Rendering;
using SkiaSharp;
using System.Numerics;

namespace App2d.Gameplay.Player;

public sealed class TraversalDebugRenderer2D(TraversalMetrics2D traversal)
{
    private readonly TraversalMetrics2D _traversal = ArgGuard.RequireNotNull(traversal);
    private readonly Vector2[] _runningJumpArc = traversal.BuildJumpArc(traversal.RunSpeed);
    private readonly Vector2[] _standingJumpArc = traversal.BuildJumpArc(0f);
    private readonly JumpProfile2D _runningJumpProfile = traversal.MeasureJump(traversal.RunSpeed);
    private readonly JumpProfile2D _standingJumpProfile = traversal.MeasureJump(0f);

    public void Draw(Renderer2D renderer, Vector2 playerPosition, float facing)
    {
        ArgGuard.ThrowIfNull(renderer);
        Span<Vector2> runningArc = stackalloc Vector2[_runningJumpArc.Length];
        Span<Vector2> standingArc = stackalloc Vector2[_standingJumpArc.Length];
        for (var i = 0; i < runningArc.Length; i++)
        {
            runningArc[i] = playerPosition + new Vector2(_runningJumpArc[i].X * facing, _runningJumpArc[i].Y);
        }
        for (var i = 0; i < standingArc.Length; i++)
        {
            standingArc[i] = playerPosition + new Vector2(_standingJumpArc[i].X * facing, _standingJumpArc[i].Y);
        }

        renderer.DrawWorldPolyline(runningArc, new SKColor(255, 92, 137, 220), 3f);
        renderer.DrawWorldPolyline(standingArc, new SKColor(110, 235, 255, 220), 2f);
        renderer.DrawScreenLabel(
            $"GRID {TraversalMetrics2D.DesignUnit:0}u  |  BODY {_traversal.PlayerColliderSize.X / _traversal.TileSize:0.00}t x {_traversal.PlayerColliderSize.Y / _traversal.TileSize:0.00}t  |  " +
            $"PASSAGE {_traversal.StandingPassageTiles}t + {_traversal.StandingClearance:0}u  |  " +
            $"RUN JUMP {_runningJumpProfile.HorizontalDistance / _traversal.TileSize:0.00}t x {_runningJumpProfile.ApexHeight / _traversal.TileSize:0.00}t  |  " +
            $"STAND {_standingJumpProfile.HorizontalDistance / _traversal.TileSize:0.00}t  |  " +
            $"AIR {_runningJumpProfile.Airtime:0.000}s  |  COYOTE {_traversal.RunSpeed * _traversal.CoyoteDuration / _traversal.TileSize:0.00}t  |  " +
            $"AIR JUMP {_traversal.AirJumpSpeedMultiplier:P0} power",
            new Vector2(24f, 88f));
    }
}

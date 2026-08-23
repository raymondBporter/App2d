using System.Numerics;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class TraversalDebugRenderer2D
{
    private readonly TraversalMetrics2D _traversal;
    private readonly Vector2[] _runningJumpArc;
    private readonly Vector2[] _standingJumpArc;
    private readonly JumpProfile2D _runningJumpProfile;
    private readonly JumpProfile2D _standingJumpProfile;

    public TraversalDebugRenderer2D(TraversalMetrics2D traversal)
    {
        _traversal = traversal ?? throw new ArgumentNullException(nameof(traversal));
        _runningJumpArc = traversal.BuildJumpArc(traversal.RunSpeed);
        _standingJumpArc = traversal.BuildJumpArc(0f);
        _runningJumpProfile = traversal.MeasureJump(traversal.RunSpeed);
        _standingJumpProfile = traversal.MeasureJump(0f);
    }

    public void Draw(Renderer2D renderer, Vector2 playerPosition, float facing)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawWorldCircle(
            playerPosition,
            _traversal.GrappleReach,
            new SKColor(255, 220, 92, 150),
            2f);

        Span<Vector2> runningArc = stackalloc Vector2[_runningJumpArc.Length];
        Span<Vector2> standingArc = stackalloc Vector2[_standingJumpArc.Length];
        for (var i = 0; i < runningArc.Length; i++)
        {
            runningArc[i] = playerPosition + new Vector2(
                _runningJumpArc[i].X * facing,
                _runningJumpArc[i].Y);
        }
        for (var i = 0; i < standingArc.Length; i++)
        {
            standingArc[i] = playerPosition + new Vector2(
                _standingJumpArc[i].X * facing,
                _standingJumpArc[i].Y);
        }

        renderer.DrawWorldPolyline(runningArc, new SKColor(255, 92, 137, 220), 3f);
        renderer.DrawWorldPolyline(standingArc, new SKColor(110, 235, 255, 220), 2f);
        renderer.DrawScreenLabel(
            $"RUN JUMP {_runningJumpProfile.HorizontalDistance / _traversal.TileSize:0.00}t x {_runningJumpProfile.ApexHeight / _traversal.TileSize:0.00}t  |  " +
            $"STAND {_standingJumpProfile.HorizontalDistance / _traversal.TileSize:0.00}t  |  " +
            $"AIR {_runningJumpProfile.Airtime:0.000}s  |  COYOTE {_traversal.RunSpeed * _traversal.CoyoteDuration / _traversal.TileSize:0.00}t  |  " +
            $"HOOK {_traversal.GrappleReach / _traversal.TileSize:0.00}t + {_traversal.GrappleRangeGrace:0}px grace",
            new Vector2(24f, 88f));
    }
}

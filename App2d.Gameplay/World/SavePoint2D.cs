using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Checkpoint entry and activation state for the current campaign player.</summary>
internal sealed partial class SavePoint2D
{
    private readonly Vector2 _basePosition;
    private bool _playerWasInside;
    public SavePoint2D(WorldThingSpec2D spec, float respawnGroundOffset)
    {
        ArgGuard.ThrowIfNull(spec);
        ArgGuard.ThrowIfNotPositive(respawnGroundOffset);
        StateGuard.ThrowIf(spec.Kind != WorldThingKind2D.SavePoint, "A checkpoint requires a save-point thing.");
        Spec = spec;
        _basePosition = spec.Position - new Vector2(0f, respawnGroundOffset);
    }
    public WorldThingSpec2D Spec { get; }
    public bool IsActive { get; private set; }
    public CheckpointState2D CaptureState() => new(Spec.ThingId, _basePosition, IsActive);
    public bool Update(float deltaSeconds, Bounds2D playerBounds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        var isInside = TriggerBounds.Intersects(playerBounds);
        var entered = isInside && !_playerWasInside;
        _playerWasInside = isInside;
        return entered;
    }
    public void SetActive(bool active) => IsActive = active;
    private Bounds2D TriggerBounds => new(
        _basePosition + new Vector2(-64f, -8f), _basePosition + new Vector2(64f, 132f));
}

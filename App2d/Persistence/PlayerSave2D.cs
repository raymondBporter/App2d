using App2d.Core;

namespace App2d.Gameplay.Persistence;

public sealed record PlayerSave2D
{
    public PlayerSave2D(long savePointId, int hitPoints)
    {
        StateGuard.ThrowIf(savePointId <= 0, "A saved save-point ID must be positive.");
        ArgGuard.ThrowIfNotPositive(hitPoints);

        SavePointId = savePointId;
        HitPoints = hitPoints;
    }

    public long SavePointId { get; }
    public int HitPoints { get; }
}

using App2d.Core.Validation;

namespace App2d.Persistence;

public sealed record PlayerSave2D
{
    public PlayerSave2D(long savePointId, int hitPoints)
    {
        Ensure.Positive(savePointId);
        Ensure.Positive(hitPoints);

        SavePointId = savePointId;
        HitPoints = hitPoints;
    }

    public long SavePointId { get; }
    public int HitPoints { get; }
}

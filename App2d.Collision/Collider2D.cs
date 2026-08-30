namespace App2d.Engine.Collision;

public sealed class Collider2D
{
    private ColliderMobility2D _mobility;

    internal Collider2D(
        CollisionSystem2D system,
        int id,
        SpatialObject2D worldObject,
        ColliderMobility2D mobility)
    {
        System = system;
        Id = id;
        WorldObject = ArgGuard.RequireNotNull(worldObject);
        _mobility = mobility;
        WorldObject.Transform.Changed += HandleTransformChanged;
    }

    internal CollisionSystem2D System { get; }
    internal int QueryStamp { get; set; }
    public int Id { get; }
    public SpatialObject2D WorldObject { get; }
    public ColliderMobility2D Mobility
    {
        get => _mobility;
        set
        {
            if (_mobility == value)
                return;
            var previous = _mobility;
            _mobility = value;
            System.OnMobilityChanged(previous, value);
        }
    }
    public bool IsEnabled { get; set; } = true;
    public bool IsSensor { get; set; }
    public uint CollisionLayer { get; set; } = 1u;
    public uint CollisionMask { get; set; } = uint.MaxValue;
    public object? UserData { get; set; }

    public bool CanCollideWith(Collider2D other) =>
        IsEnabled && other.IsEnabled &&
        (CollisionMask & other.CollisionLayer) != 0u &&
        (other.CollisionMask & CollisionLayer) != 0u;

    internal void Detach() =>
        WorldObject.Transform.Changed -= HandleTransformChanged;

    private void HandleTransformChanged() =>
        System.OnTransformChanged(this);
}

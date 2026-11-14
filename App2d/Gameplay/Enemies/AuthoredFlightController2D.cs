using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

/// <summary>Flight movement only. Editable action events define the pose phases and mandatory low pass.</summary>
internal sealed class AuthoredFlightController2D(FlightDef config)
{
    private const float Scale = 40;
    private Vector2f _home, _dive;
    private float _lowY;
    private bool _committed;
    public bool Ready { get; private set; }
    public void Begin(Vector2f position)
    {
        _home = position;
        _committed = false;
    }

    public Vector2f Update(float dt, Vector2f position, Vector2f target, ResolvedAction? action, float at,
        ControllerConfig controller, ref int facing, bool blocked, Func<Vector2f, float> recoveryAltitude)
    {
        if (action is null)
        {
            if (Vector2f.DistanceSquared(target, position) > 560 * 560)
            {
                Ready = false;
                return Vector2f.Zero;
            }
            if (MathF.Abs(target.X - position.X) > 1)
            {
                facing = Math.Sign(target.X - position.X);
            }

            var desired = target + new Vector2f(-facing * controller.Range * Scale * .75f, config.HoverHeight * Scale);
            Ready = Vector2f.Distance(position, desired) < 20;
            return Seek(desired - position, config.Speed * Scale, dt);
        }
        Ready = false;
        float Phase(string id) => action.Events.First(e => e.Event.Id == id).Seconds;
        if (!_committed && at >= Phase("commit"))
        {
            // Sample once at commitment, including facing: late dodges never bend the lane.
            var delta = target - position;
            if (MathF.Abs(delta.X) > 1)
            {
                facing = Math.Sign(delta.X);
            }

            _lowY = recoveryAltitude(target);
            var flightSeconds = (Phase("brake") - Phase("rush")) * .77f;
            _dive = Seek(delta, config.DiveSpeed * Scale, flightSeconds);
            _committed = true;
        }
        if (at < Phase("rush"))
        {
            return Vector2f.Zero;
        }

        if (at < Phase("brake"))
        {
            if (blocked)
            {
                return Vector2f.Zero;
            }

            return new(_dive.X, MathF.Max(_dive.Y, (_lowY - position.Y) / MathF.Max(dt, .0001f)));
        }
        if (at < Phase("low"))
        {
            var fraction = 1 - (at - Phase("brake")) / (Phase("low") - Phase("brake"));
            return new(blocked ? 0 : _dive.X * fraction, Seek(new(0, _lowY - position.Y), config.Speed * Scale, dt).Y);
        }
        if (at < Phase("reset"))
        {
            return new(facing * config.LowSpeed * Scale, Seek(new(0, _lowY - position.Y), config.DiveSpeed * Scale, dt).Y);
        }

        return Seek(_home - position, config.Speed * Scale, dt);
    }

    private static Vector2f Seek(Vector2f delta, float speed, float dt)
    {
        var distance = delta.Length();
        return distance < .001f ? Vector2f.Zero : delta / distance * MathF.Min(speed, distance / MathF.Max(dt, .0001f));
    }
}

using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Presentation.Audio;
using App2d.Rendering;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Presentation.Persons.Presentation;

/// <summary>Deliberately simple cast art. All completion cues come from confirmed simulation events.</summary>
public sealed class SpellPresentation2D(ISoundEffectSink2D sounds) : IDisposable
{
    private PersonState2D _state;
    private SoundEffectVoice2D _healVoice;
    private bool _healing;
    private float _pulse, _cancel, _clock;
    private Vector2 _pulsePosition;

    public void Apply(PersonState2D state, IEnumerable<WeaponEvent2D> events)
    {
        _state = state;
        foreach (var e in events)
        {
            switch (e)
            {
                case HealStarted2D:
                    StopHeal();
                    _healing = true;
                    _healVoice = sounds.BeginAt(SoundEffect2D.HealCharge, Center);
                    break;
                case HealCompleted2D completed:
                    StopHeal();
                    _pulse = .4f; _pulsePosition = completed.Position + new Vector2(0, 8);
                    sounds.PlayAt(SoundEffect2D.HealComplete, completed.Position);
                    break;
                case HealCancelled2D cancelled:
                    StopHeal(); _cancel = .18f;
                    sounds.PlayAt(SoundEffect2D.HealCancel, cancelled.Position);
                    break;
            }
        }
        if (!state.Spells.IsHealing)
        {
            StopHeal();
        }
        else if (!_healing) { _healing = true; _healVoice = sounds.BeginAt(SoundEffect2D.HealCharge, Center); }
        _healVoice.SetPosition(Center);
    }

    private Vector2 Center => _state.Position + new Vector2(_state.Facing * 7, 8);

    public void Advance(float dt)
    {
        _clock += dt;
        _pulse = Math.Max(0, _pulse - dt); _cancel = Math.Max(0, _cancel - dt);
        if (_healing)
        {
            if (!_healVoice.IsPlaying) _healVoice = sounds.BeginAt(SoundEffect2D.HealCharge, Center);
            _healVoice.SetVolumeScale(.35f + .65f * _state.Spells.HealProgress, .02f);
        }
    }

    public void Draw(Renderer2D renderer)
    {
        if (_state.Spells.IsHealing)
        {
            var p = Math.Clamp(_state.Spells.HealProgress, 0, 1);
            var radius = float.Lerp(26, 10, p);
            renderer.DrawWorldCircle(Center, radius, new Color(93, 219, 255, 150), 1.5f);
            renderer.DrawWorldCircle(Center, 3 + p * 5, new Color(211, 250, 255, 230), 3f);
            for (var i = 0; i < 6; i++)
            {
                var angle = _clock * 4 + i * MathF.Tau / 6;
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                renderer.DrawWorldCircle(Center + offset, 1.5f, new Color(179, 245, 255), 2f);
            }
        }
        if (_pulse > 0)
        {
            var p = 1 - _pulse / .4f;
            renderer.DrawWorldCircle(_pulsePosition, float.Lerp(10, 44, p), new Color(175, 250, 233, (int)(230 * (1 - p))), 3f);
        }
        if (_cancel > 0)
        {
            renderer.DrawWorldCircle(Center, 10 + (1 - _cancel / .18f) * 10,
                new Color(124, 177, 200, (int)(140 * _cancel / .18f)), 1f);
        }
    }

    private void StopHeal() { _healVoice.Stop(.025f); _healVoice = default; _healing = false; }
    public void Reset() { StopHeal(); _state = default; _pulse = _cancel = 0; }
    public void Dispose() => Reset();
}

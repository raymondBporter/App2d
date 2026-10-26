using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One-way, explicit conversion of a prototype motion (absolute per-key positions) into channels for a model built from the
/// same rest pose. Keys keep their times; joints solved by IK get no channel. The prototype stays a reference fixture.
/// </summary>
public static class PuppetMotionConverter
{
    public static MotionClip Convert(PuppetDefinition puppet, PuppetMotion motion, CharacterModel model, string id, string name, string travelScale)
    {
        var resolved = ResolvedModel.Rig(model);
        // A chain's joint and end are solved from the target; a bone constraint also turns its root, whose origin still
        // follows its parent. A control the prototype never had (a solved bone inserted at a pivot, P11a) gets no channel
        // and follows its parent; every other unsolved control keys its prototype position as before.
        var solved = model.IkChains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        foreach (var chain in model.IkChains.Where(c => c.Solver == ModelChain.BoneSolver))
        {
            // The solved bone beneath a pivot (P11a) sits at its parent's origin and takes its position from it: no channel.
            var bone = resolved.Controls[chain.Root];
            if (bone.Parent is { } parent && Vector2.Distance(new(resolved.Rest[chain.Root].X, resolved.Rest[chain.Root].Y), new(resolved.Rest[parent].X, resolved.Rest[parent].Y)) < 1e-6f)
            {
                solved.Add(chain.Root);
            }
        }
        foreach (var control in model.Controls.Where(c => !solved.Contains(c.Id)))
        {
            for (var parent = control.Parent; parent is not null; parent = resolved.Controls[parent].Parent)
            {
                if (solved.Contains(parent))
                {
                    throw new InvalidDataException($"Control '{control.Id}' hangs from IK-solved '{parent}'; conversion does not support that yet.");
                }
            }
        }

        Vector3 Rest(string control) => resolved.Rest[control];
        var clip = new MotionClip
        {
            Id = id,
            Name = name,
            Model = model.Id,
            StructureRevision = model.StructureRevision,
            Duration = motion.Duration,
            Loop = motion.Loop,
            Reference = resolved.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            Travel = new() { Scale = travelScale },
        };
        var tracks = new Dictionary<(string, string), ClipTrack>();
        void Add(string kind, string target, float time, Vector3 delta)
        {
            if (!tracks.TryGetValue((kind, target), out var track))
            {
                tracks[(kind, target)] = track = new()
                {
                    Kind = kind,
                    Target = target
                };
            }

            track.Keys.Add(new()
            {
                Time = time,
                Value = new(delta.X, delta.Y, delta.Z)
            });
        }
        foreach (var key in motion.Keys)
        {
            var position = key.Position.XYZ;
            var locomotion = new Vector3(position.X, 0, 0);
            Vector3 World(string control) => (key.Points.TryGetValue(control, out var p) ? p.XYZ : Rest(control)) + position;
            clip.Travel.Keys.Add(new()
            {
                Time = key.Time,
                Value = new(position.X, 0, 0)
            });
            foreach (var control in model.Controls.Where(c => !solved.Contains(c.Id)))
            {
                var parentWorld = control.Parent is null ? locomotion : World(control.Parent);
                var parentRest = control.Parent is null ? Vector3.Zero : Rest(control.Parent);
                Add(MotionClip.TranslateKind, control.Id, key.Time, World(control.Id) - parentWorld - (Rest(control.Id) - parentRest));
            }
            foreach (var chain in model.IkChains)
            {
                var inLocomotion = chain.Frame == CharacterModel.Locomotion;
                var frameWorld = inLocomotion ? locomotion : World(chain.Frame);
                var frameRest = inLocomotion ? Vector3.Zero : Rest(chain.Frame);
                Add(MotionClip.TargetKind, chain.Id, key.Time, World(chain.End) - frameWorld - (Rest(chain.End) - frameRest));
            }
        }
        const float epsilon = 1e-5f; // below this a track is float noise from subtracting absolute positions
        clip.Tracks = [.. tracks.Values.Where(t => t.Keys.Any(k => MathF.Abs(k.Value.X) > epsilon || MathF.Abs(k.Value.Y) > epsilon || MathF.Abs(k.Value.Z) > epsilon))];
        var start = clip.Travel.Keys.Count > 0 ? clip.Travel.Keys[0].Value.X : 0;
        foreach (var contact in motion.Contacts)
        {
            var chain = model.IkChains.Single(c => c.End == contact.End);
            clip.Contacts.Add(new()
            {
                Chain = chain.Id,
                Start = contact.Start,
                Finish = contact.Finish,
                Target = PuppetPoint.From(contact.Target.XYZ - new Vector3(start, 0, 0) - Rest(chain.End))
            });
        }
        clip.Validate(resolved);
        return clip;
    }
}

using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterSeeds;

/// <summary>
/// The phase 3 starter content, written as ordinary authored assets: Person idle/thrust/jump clips, a spear, a stalker
/// creature with its own clips, and three entities (a spear guard that cannot jump, a player that can, and the stalker).
/// Generated from code so the clips stay reviewable; afterwards they are plain files the editor opens like any other.
/// </summary>
public static class StarterContent
{
    public const string SpearGuard = "spear-guard", Player = "player", Stalker = "stalker", StalkerPest = "stalker-pest", Spear = "spear";
    public const string HeavyWalk = "person-heavy-walk";

    /// <summary>Sockets, motion sets, hurt layout, control groups and looks the Person template carries. None of them change structure.</summary>
    public static void AddPersonExtras(CharacterModel model)
    {
        // back, back-view, sword-hand and gun-hand belong to the player move set (App2d.CharacterSeeds/PlayerMoves); keep them in step.
        model.Sockets =
        [
            new() { Id = Humanoid.RightGripSocket, Control = Humanoid.RightHand, Frame = Humanoid.Chest, Angle = .12f }, new() { Id = Humanoid.LeftGripSocket, Control = Humanoid.LeftHand, Frame = Humanoid.Chest, Angle = .12f },
            new() { Id = Humanoid.BackSocket, Control = Humanoid.Chest, Frame = Humanoid.Chest, OffsetX = -.1f, OffsetY = -.08f, Angle = -MathF.PI / 2 - .12f },
            new() { Id = Humanoid.BackViewSocket, Control = Humanoid.Chest, Frame = Humanoid.Chest, OffsetX = -.13f, OffsetY = .12f, Angle = -MathF.PI / 2 + .55f },
            new() { Id = Humanoid.SwordSocket, Control = Humanoid.RightHand, Frame = Humanoid.RightShoulder }, new() { Id = Humanoid.GunSocket, Control = Humanoid.RightHand, Frame = Humanoid.RightShoulder },
            .. PersonWardrobe.Sockets(),
        ];
        model.MotionSets =
        [
            new() { Id = "standard", Name = "Standard", Roles = Reactions(new() { ["idle"] = "person-idle", ["walk"] = "person-walk", ["run"] = "person-run", ["jump"] = "person-jump" }) },
            new() { Id = "deliberate", Name = "Deliberate", Roles = Reactions(new() { ["idle"] = "person-idle", ["walk"] = "person-walk" }) },
            new() { Id = "heavy", Name = "Heavy", Roles = Reactions(new() { ["idle"] = "person-idle", ["walk"] = HeavyWalk, ["run"] = "person-run" }) },
        ];
        // The upper group is what an upper-body action owns over locomotion; the legs and pelvis stay with the gait.
        model.Groups =
        [
            new() { Id = Humanoid.UpperGroup, Targets = [.. PersonLoadout.UpperBody.Order(StringComparer.Ordinal)] },
            new() { Id = Humanoid.LowerGroup, Targets = [Humanoid.Hips, Humanoid.LeftHip, Humanoid.LeftLeg, Humanoid.RightHip, Humanoid.RightLeg] },
        ];
        model.Looks =
        [
            new() { Id = "sage", Name = "Sage", Parts = new() { [Humanoid.Body] = new() { Material = new() { Fill = "#d8e9db" } } } },
            // The looks' face words (slate focused, ember determined, night smug) join as instance expressions when the face is attached (PersonTemplate.AttachFace).
            new() { Id = "slate", Name = "Slate", Parts = new() { [Humanoid.Body] = new() { Material = new() { Fill = "#b8c4d6" } }, [Humanoid.Head] = new() { Material = new() { Fill = "#efe6da" } } } },
            new() { Id = "ember", Name = "Ember", Parts = new() { [Humanoid.Body] = new() { Material = new() { Fill = "#eab596" } } } },
            new() { Id = "night", Name = "Night", Parts = new() { [Humanoid.Body] = new() { Material = new() { Fill = "#7d879a" } }, [Humanoid.Head] = new() { Material = new() { Fill = "#e2d6c6" } } } },
        ];
    }

    /// <summary>
    /// The Person's hurt colliders (P8c, declared by the seeds since P11b): a body box over the hips, chest and shoulders, a head
    /// box and a legs box, each holding the rest-pose bounds of its controls on the entity's own build, following its anchor control.
    /// </summary>
    public static IEnumerable<PartGeometry> PersonHurt(ResolvedModel model, float legsPad = .07f) =>
    [
        ColliderAuthoring.HurtBox(model, Humanoid.Body, [Humanoid.Hips, Humanoid.Chest, Humanoid.LeftShoulder, Humanoid.RightShoulder], .14f),
        ColliderAuthoring.HurtBox(model, Humanoid.Head, [Humanoid.Head], .3f),
        ColliderAuthoring.HurtBox(model, "legs", [Humanoid.LeftHip, Humanoid.RightHip, Humanoid.LeftKnee, Humanoid.RightKnee, Humanoid.LeftFoot, Humanoid.RightFoot], legsPad),
    ];

    /// <summary>The resolved tall-thin build the spear guard stands on, from the seeds alone.</summary>
    private static ResolvedModel TallThin()
    {
        var person = PersonTemplate.Model(face: true);
        return ResolvedModel.From(person, PersonBuild.TallThin.Apply(person, "tall-thin", "Tall and thin", "#d9e2ef"), PersonTemplate.PartOf);
    }

    public const string PersonHitClip = "person-hit", PersonDeathClip = "person-death";

    /// <summary>Every Person set reacts with the same hit and death until a set needs its own.</summary>
    private static Dictionary<string, string> Reactions(Dictionary<string, string> roles)
    {
        roles[EntityControllers.Hit] = PersonHitClip;
        roles[EntityControllers.Death] = PersonDeathClip;
        return roles;
    }

    public static void Write(string authoredRoot, CharacterModel person, SeedReport? report = null)
    {
        report ??= new();
        void Save(string folder, string id, string json) => report.Write(Path.Combine(authoredRoot, folder, id + ".json"), json);
        var resolved = ResolvedModel.From(person, null, PersonTemplate.PartOf);
        var clips = new[] { PersonIdle(resolved), PersonThrust(resolved), PersonJump(resolved), PersonHit(resolved), PersonDeath(resolved) };
        foreach (var clip in clips)
        {
            clip.Validate(resolved);
            Save("animations", clip.Id, clip.ToJson());
        }
        var spear = SpearProp();
        Save("parts", spear.Id, spear.ToJson());

        var stalker = StalkerModel();
        var stalkerResolved = ResolvedModel.From(stalker);
        Save("models", stalker.Id, stalker.ToJson());
        foreach (var clip in new[] { StalkerIdle(stalkerResolved), StalkerWalk(stalkerResolved), StalkerLunge(stalkerResolved), StalkerHit(stalkerResolved), StalkerDeath(stalkerResolved) })
        {
            clip.Validate(stalkerResolved);
            Save("animations", clip.Id, clip.ToJson());
        }

        foreach (var entity in new[] { SpearGuardEntity(), PlayerEntity(), StalkerEntity() })
        {
            entity.Validate();
            Save("parts", entity.Id, entity.ToJson());
        }
    }

    // ---- Person clips ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A heavier walk derived from the reviewed walk rather than keyed from scratch: a slower cycle over the same stride, the
    /// pelvis lower with a deeper bob, the chest pitched forward and a shorter arm swing. An artistic choice any build can use.
    /// </summary>
    public static MotionClip PersonHeavyWalk(MotionClip walk)
    {
        const float Slow = 1.3f, Sink = -.05f, Bob = 1.6f, Lean = .05f, Swing = .7f;
        var clip = ClipAuthoring.Duplicate(walk, HeavyWalk, "Heavy walk");
        clip.Duration *= Slow;
        foreach (var key in clip.Tracks.SelectMany(t => t.Keys).Concat(clip.Travel.Keys))
        {
            key.Time *= Slow;
        }

        foreach (var contact in clip.Contacts)
        {
            contact.Start *= Slow;
            contact.Finish *= Slow;
        }
        foreach (var marker in clip.Markers)
        {
            marker.Time *= Slow;
        }

        foreach (var key in clip.Plays.SelectMany(p => p.Keys))
        {
            key.Time *= Slow;
        }

        ClipTrack Channel(string kind, string target)
        {
            var track = clip.Tracks.FirstOrDefault(t => t.Kind == kind && t.Target == target);
            if (track is null)
            {
                track = new()
                {
                    Kind = kind,
                    Target = target,
                    Keys = [new() { Time = 0 }, new() { Time = clip.Duration }]
                };
                clip.Tracks.Add(track);
            }
            return track;
        }
        var hips = Channel(MotionClip.TranslateKind, Humanoid.Hips);
        var mean = hips.Keys.Average(k => k.Value.Y);
        foreach (var key in hips.Keys)
        {
            key.Value = key.Value with { Y = mean + (key.Value.Y - mean) * Bob + Sink };
        }

        foreach (var key in Channel(MotionClip.TranslateKind, Humanoid.Chest).Keys)
        {
            key.Value = key.Value with { X = key.Value.X + Lean };
        }

        foreach (var arm in new[] { Humanoid.LeftArm, Humanoid.RightArm })
        {
            if (clip.Tracks.FirstOrDefault(t => t.Kind == MotionClip.TargetKind && t.Target == arm) is { } track)
            {
                var center = track.Keys.Average(k => k.Value.X);
                foreach (var key in track.Keys)
                {
                    key.Value = key.Value with { X = center + (key.Value.X - center) * Swing };
                }
            }
        }

        return clip;
    }

    private static ClipKey K(float time, float x = 0, float y = 0, string ease = ClipEase.Smooth) => new() { Time = time, Value = new(x, y, 0), Ease = ease };
    private static ClipKey R(float time, float angle, string ease = ClipEase.Smooth) => new() { Time = time, Angle = angle, Ease = ease };
    private static ClipTrack Track(string kind, string target, params ClipKey[] keys) => new() { Kind = kind, Target = target, Keys = [.. keys] };
    private static ClipContact Plant(string chain, float start, float finish, float x = 0) => new() { Chain = chain, Start = start, Finish = finish, Target = new(x) };

    private static MotionClip Clip(ResolvedModel model, string id, string name, float duration, bool loop) => new()
    {
        Id = id,
        Name = name,
        Model = model.Base.Id,
        StructureRevision = model.Base.StructureRevision,
        Duration = duration,
        Loop = loop,
        Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        Travel = new()
        {
            Scale = Humanoid.LegMeasure
        },
    };

    /// <summary>A quiet two-second breath with both feet planted for the whole cycle.</summary>
    public static MotionClip PersonIdle(ResolvedModel model)
    {
        var clip = Clip(model, "person-idle", "Idle", 2, true);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, Humanoid.Hips, K(0), K(1, 0, -.012f), K(2)),
            Track(MotionClip.RotateKind, Humanoid.Chest, R(0, 0), R(1, .018f), R(2, 0)),
            Track(MotionClip.TargetKind, Humanoid.LeftArm, K(0), K(1, -.01f, .02f), K(2)),
            Track(MotionClip.TargetKind, Humanoid.RightArm, K(0), K(1, .01f, .02f), K(2)),
        ];
        clip.Contacts = [Plant(Humanoid.LeftLeg, 0, 2), Plant(Humanoid.RightLeg, 0, 2)];
        return clip;
    }

    /// <summary>Hit reaction: the chest snaps back and the hips dip on planted feet, then settle.</summary>
    public static MotionClip PersonHit(ResolvedModel model)
    {
        var clip = Clip(model, PersonHitClip, "Hit recoil", .3f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, Humanoid.Hips,
                K(0, -0.045f, -0.025f, ClipEase.Linear), K(1 / 60f, -0.1f, -0.055f, ClipEase.Linear), K(0.05f, -0.1f, -0.055f, ClipEase.Smooth), K(0.17f, -0.03f, -0.015f, ClipEase.Smooth), K(0.3f, 0, 0, ClipEase.Linear)),
            Track(MotionClip.RotateKind, Humanoid.Hips,
                R(0, 0.16f, ClipEase.Linear), R(1 / 60f, 0.32f, ClipEase.Linear), R(0.05f, 0.32f, ClipEase.Smooth), R(0.17f, 0.08f, ClipEase.Smooth), R(0.3f, 0, ClipEase.Linear)),
            Track(MotionClip.RotateKind, Humanoid.Chest,
                R(0, 0.08f, ClipEase.Linear), R(1 / 60f, 0.16f, ClipEase.Linear), R(0.05f, 0.16f, ClipEase.Smooth), R(0.17f, 0.04f, ClipEase.Smooth), R(0.3f, 0, ClipEase.Linear)),
            Track(MotionClip.TargetKind, Humanoid.LeftArm,
                K(0, 0.1f, 0.1f, ClipEase.Linear), K(1 / 60f, 0.22f, 0.16f, ClipEase.Linear), K(0.05f, 0.22f, 0.16f, ClipEase.Smooth), K(0.3f, 0, 0, ClipEase.Linear)),
            Track(MotionClip.TargetKind, Humanoid.RightArm,
                K(0, -0.05f, 0.06f, ClipEase.Linear), K(1 / 60f, -0.12f, 0.16f, ClipEase.Linear), K(0.05f, -0.12f, 0.16f, ClipEase.Smooth), K(0.3f, 0, 0, ClipEase.Linear)),
        ];
        clip.Contacts = [Plant(Humanoid.LeftLeg, 0, .3f), Plant(Humanoid.RightLeg, 0, .3f)];
        clip.Plays = [new() { Instance = PersonTemplate.FaceInstance, Keys = [new() { Time = 0, Expression = "hurt" }] }];
        return clip;
    }

    /// <summary>Death: the knees give, the body folds back onto the ground and stays down.</summary>
    public static MotionClip PersonDeath(ResolvedModel model)
    {
        var clip = Clip(model, PersonDeathClip, "Recoil and collapse", 1.1f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, Humanoid.Hips,
                K(0, -0.045f, -0.025f, ClipEase.Linear), K(1 / 60f, -0.13f, -0.06f, ClipEase.Linear), K(0.075f, -0.16f, -0.08f, ClipEase.Smooth), K(0.25f, -0.2f, -0.2f, ClipEase.Smooth), K(0.75f, -0.28f, -0.81f, ClipEase.Smooth), K(1.1f, -0.3f, -0.79f, ClipEase.Smooth)),
            Track(MotionClip.RotateKind, Humanoid.Hips,
                R(0, 0.16f, ClipEase.Linear), R(1 / 60f, 0.34f, ClipEase.Linear), R(0.075f, 0.37f, ClipEase.Smooth), R(0.25f, 0.53f, ClipEase.Smooth), R(0.75f, 1.42f, ClipEase.Smooth), R(1.1f, 1.45f, ClipEase.Smooth)),
            Track(MotionClip.RotateKind, Humanoid.Chest,
                R(0, 0.08f, ClipEase.Linear), R(1 / 60f, 0.18f, ClipEase.Linear), R(0.075f, 0.18f, ClipEase.Smooth), R(0.25f, 0.12f, ClipEase.Smooth), R(0.75f, 0.05f, ClipEase.Smooth), R(1.1f, 0.05f, ClipEase.Smooth)),
            Track(MotionClip.TargetKind, Humanoid.LeftArm,
                K(0, 0.1f, 0.1f, ClipEase.Linear), K(1 / 60f, 0.24f, 0.18f, ClipEase.Linear), K(0.075f, 0.24f, 0.18f, ClipEase.Smooth), K(0.4f, 0.15f, 0.25f, ClipEase.Smooth), K(1.1f, -0.1f, 0.05f, ClipEase.Smooth)),
            Track(MotionClip.TargetKind, Humanoid.RightArm,
                K(0, -0.05f, 0.06f, ClipEase.Linear), K(1 / 60f, -0.14f, 0.18f, ClipEase.Linear), K(0.075f, -0.14f, 0.18f, ClipEase.Smooth), K(0.4f, 0.15f, 0.25f, ClipEase.Smooth), K(1.1f, -0.05f, 0, ClipEase.Smooth)),
            Track(MotionClip.OrientKind, Humanoid.RightGripSocket,
                new ClipKey { Time = 0, Value = new(0, 0, 0), Ease = ClipEase.Linear }, new ClipKey { Time = 0.075f, Value = new(0, 0, 0), Ease = ClipEase.Smooth }, new ClipKey { Time = 0.75f, Value = new(0, 0, -1.47f), Ease = ClipEase.Smooth }, new ClipKey { Time = 1.1f, Value = new(0, 0, -1.5f), Ease = ClipEase.Linear }),
        ];
        clip.Contacts = [Plant(Humanoid.LeftLeg, 0, 1.1f), Plant(Humanoid.RightLeg, 0, 1.1f)];
        clip.Markers = [new() { Id = "impact", Time = .75f }];
        clip.Plays = [new() { Instance = PersonTemplate.FaceInstance, Keys = [new() { Time = 0, Expression = "hurt" }, new() { Time = .75f, Expression = "knocked-out" }] }];
        return clip;
    }

    /// <summary>
    /// A one-handed spear thrust: pull back (anticipation), drive the hand forward on "strike", hold, and recover after
    /// "recover". The spear socket follows the chest, so the lean reads through the weapon. Both feet stay planted.
    /// </summary>
    public static MotionClip PersonThrust(ResolvedModel model)
    {
        var clip = Clip(model, "person-thrust", "Spear thrust", .9f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, Humanoid.Hips, K(0), K(.32f, -.04f, -.03f), K(.42f, .09f, -.05f, ClipEase.Linear), K(.58f, .09f, -.05f), K(.9f)),
            Track(MotionClip.RotateKind, Humanoid.Chest, R(0, 0), R(.32f, .14f), R(.42f, -.16f, ClipEase.Linear), R(.58f, -.16f), R(.9f, 0)),
            Track(MotionClip.TargetKind, Humanoid.RightArm, K(0), K(.32f, -.33f, .35f), K(.42f, .57f, .5f, ClipEase.Linear), K(.58f, .57f, .5f), K(.9f)),
            Track(MotionClip.TargetKind, Humanoid.LeftArm, K(0), K(.32f, .12f, .1f), K(.42f, -.18f, .12f, ClipEase.Linear), K(.58f, -.18f, .12f), K(.9f)),
        ];
        clip.Contacts = [Plant(Humanoid.LeftLeg, 0, .9f), Plant(Humanoid.RightLeg, 0, .9f)];
        clip.Markers = [new() { Id = "windup", Time = .05f }, new() { Id = "strike", Time = .4f }, new() { Id = "recover", Time = .58f }];
        clip.Plays = [new() { Instance = PersonTemplate.FaceInstance, Keys = [new() { Time = 0, Expression = "focused" }, new() { Time = .36f, Expression = "determined" }, new() { Time = .7f, Expression = "focused" }] }];
        return clip;
    }

    /// <summary>Crouch with planted feet, "launch" (the controller leaves the ground here), tuck through the apex, then reach for the ground and hold.</summary>
    public static MotionClip PersonJump(ResolvedModel model)
    {
        var clip = Clip(model, "person-jump", "Jump", .9f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, Humanoid.Hips, K(0), K(.14f, 0, -.14f), K(.24f, 0, .04f), K(.5f), K(.9f)),
            Track(MotionClip.TargetKind, Humanoid.LeftLeg, K(0), K(.2f), K(.36f, .12f, .32f), K(.9f, .06f, .08f)),
            Track(MotionClip.TargetKind, Humanoid.RightLeg, K(0), K(.2f), K(.36f, -.04f, .24f), K(.9f, .02f, .06f)),
            Track(MotionClip.TargetKind, Humanoid.LeftArm, K(0), K(.14f, -.12f, .08f), K(.26f, .2f, .45f), K(.9f, .06f, .12f)),
            Track(MotionClip.TargetKind, Humanoid.RightArm, K(0), K(.14f, -.12f, .08f), K(.26f, .22f, .42f), K(.9f, .08f, .12f)),
        ];
        clip.Contacts = [Plant(Humanoid.LeftLeg, 0, .2f), Plant(Humanoid.RightLeg, 0, .2f)];
        clip.Markers = [new() { Id = "launch", Time = .2f }, new() { Id = "apex", Time = .5f }];
        return clip;
    }

    public static Part SpearProp() => PartGeometryBuilder.Named(new()
    {
        Id = Spear,
        Name = "Spear",
        Usage = "prop",
        Locations = [new() { Id = EquipmentPoints.Grip }, new() { Id = EquipmentPoints.Tip, X = 1.38f, Y = 0 }, new() { Id = EquipmentPoints.SecondGrip, X = -.3f, Y = 0 }],
        LineWidth = .013f,
        Geometry =
        [
            PartGeometryBuilder.Extrude([new(-.5f, -.024f), new(1.12f, -.021f), new(1.12f, .021f), new(-.5f, .024f)], .039f, "#876646"),
            PartGeometryBuilder.Extrude([new(-.5f, -.031f), new(-.41f, -.031f), new(-.41f, .031f), new(-.5f, .031f)], .05f, "#465963"),
            PartGeometryBuilder.Extrude([new(1.035f, -.039f), new(1.115f, -.039f), new(1.115f, .039f), new(1.035f, .039f)], .064f, "#c5a26b"),
            PartGeometryBuilder.Blade(1.1f, 1.17f, 1.38f, .076f, .022f, "#dce5e7"),
        ],
    });

    // ---- Stalker --------------------------------------------------------------------------------------------------

    /// <summary>A headless three-legged creature built from Empty with the same authoring operations as the editor.</summary>
    public static CharacterModel StalkerModel()
    {
        var model = ModelAuthoring.Empty(Stalker, "Stalker");
        ModelAuthoring.AddControl(model, null, new(0, .9f, 0), "body");
        for (var i = 0; i < 3; i++)
        {
            var x = (i - 1) * .35f;
            var z = (i - 1) * .1f;
            ModelAuthoring.AddControl(model, "body", new(x, .8f, z), $"hip-{i}");
            ModelAuthoring.AddControl(model, $"hip-{i}", new(x + .15f, .4f, z), $"knee-{i}");
            ModelAuthoring.AddControl(model, $"knee-{i}", new(x, 0, z), $"foot-{i}");
            ModelAuthoring.AddIkOverPoints(model, $"foot-{i}");
            ModelAuthoring.AddPart(model, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), $"hip-{i}", $"knee-{i}");
            ModelAuthoring.AddPart(model, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(), $"knee-{i}", $"foot-{i}");
        }
        ModelAuthoring.AddMeasure(model, "leg", ["hip-1", "knee-1", "foot-1"]);
        foreach (var chain in model.IkChains)
        {
            chain.Scale = "leg";
        }

        foreach (var control in model.Controls.Where(c => c.Id == "body" || c.Id.StartsWith("hip-", StringComparison.Ordinal)))
        {
            control.Scale = "leg";
        }

        var shell = ModelAuthoring.AddPart(model, GeometryJson2D.CreateDefault(ShapeKinds2D.Ellipse), "body");
        shell.ScaleX = .95f;
        shell.ScaleY = .38f;
        shell.Material = shell.RenderMaterial with
        {
            Fill = "#c9e0b8"
        };
        shell.Depth = -.05f;
        model.Sockets = [new() { Id = "stinger", Control = "body", OffsetX = .5f, OffsetY = -.02f }];
        model.MotionSets = [new() { Id = "standard", Name = "Standard", Roles = new() { ["idle"] = "stalker-idle", ["walk"] = "stalker-walk", ["hit"] = "stalker-hit", ["death"] = "stalker-death" } }];
        model.Validate();
        return model;
    }

    private static MotionClip StalkerIdle(ResolvedModel model)
    {
        var clip = Clip(model, "stalker-idle", "Stalker idle", 1.5f, true);
        clip.Tracks = [Track(MotionClip.TranslateKind, "body", K(0), K(.75f, 0, -.03f), K(1.5f))];
        clip.Contacts = [Plant("foot-0-chain", 0, 1.5f), Plant("foot-1-chain", 0, 1.5f), Plant("foot-2-chain", 0, 1.5f)];
        return clip;
    }

    /// <summary>Outer legs plant for the first half, the middle leg for the second; each swings through a lift while the others hold.</summary>
    private static MotionClip StalkerWalk(ResolvedModel model)
    {
        var clip = Clip(model, "stalker-walk", "Stalker walk", 1, true);
        clip.Travel.Keys = [new() { Time = 0 }, new() { Time = 1, Value = new(.6f, 0, 0) }];
        static ClipKey L(float t, float x, float y = 0) => K(t, x, y, ClipEase.Linear);
        var outer = new[] { L(0, .15f), L(.5f, -.15f), L(.75f, 0, .12f), L(1, .15f) };
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, "body", K(0), K(.25f, 0, .025f), K(.5f), K(.75f, 0, .025f), K(1)),
            Track(MotionClip.TargetKind, "foot-0-chain", outer), Track(MotionClip.TargetKind, "foot-2-chain", [.. outer.Select(k => k with { })]),
            Track(MotionClip.TargetKind, "foot-1-chain", L(0, -.15f), L(.25f, 0, .12f), L(.5f, .15f), L(1, -.15f)),
        ];
        clip.Contacts = [Plant("foot-0-chain", 0, .5f, .15f), Plant("foot-2-chain", 0, .5f, .15f), Plant("foot-1-chain", .5f, 1, .45f)];
        clip.Markers = [new() { Id = "step", Time = 0 }, new() { Id = "step-middle", Time = .5f }];
        return clip;
    }

    private static MotionClip StalkerLunge(ResolvedModel model)
    {
        var clip = Clip(model, "stalker-lunge", "Stalker lunge", .8f, false);
        clip.Tracks = [Track(MotionClip.TranslateKind, "body", K(0), K(.22f, -.1f, .06f), K(.32f, .24f, -.06f, ClipEase.Linear), K(.5f, .24f, -.06f), K(.8f))];
        clip.Contacts = [Plant("foot-0-chain", 0, .8f), Plant("foot-1-chain", 0, .8f), Plant("foot-2-chain", 0, .8f)];
        clip.Markers = [new() { Id = "windup", Time = .05f }, new() { Id = "strike", Time = .3f }, new() { Id = "recover", Time = .5f }];
        return clip;
    }

    /// <summary>Stand-in: the body jolts back and down on planted feet, then settles.</summary>
    private static MotionClip StalkerHit(ResolvedModel model)
    {
        var clip = Clip(model, "stalker-hit", "Stalker hit", .3f, false);
        clip.Tracks = [Track(MotionClip.TranslateKind, "body", K(0), K(.07f, -.1f, -.08f, ClipEase.Linear), K(.3f))];
        clip.Contacts = [Plant("foot-0-chain", 0, .3f), Plant("foot-1-chain", 0, .3f), Plant("foot-2-chain", 0, .3f)];
        return clip;
    }

    /// <summary>Stand-in: the body sinks onto folding legs and stays down.</summary>
    private static MotionClip StalkerDeath(ResolvedModel model)
    {
        var clip = Clip(model, "stalker-death", "Stalker death", .9f, false);
        clip.Tracks = [Track(MotionClip.TranslateKind, "body", K(0), K(.12f, -.06f, .04f), K(.6f, -.08f, -.5f), K(.9f, -.08f, -.46f))];
        clip.Contacts = [Plant("foot-0-chain", 0, .9f), Plant("foot-1-chain", 0, .9f), Plant("foot-2-chain", 0, .9f)];
        clip.Markers = [new() { Id = "impact", Time = .6f }];
        return clip;
    }

    // ---- Entities -------------------------------------------------------------------------------------------------

    private static EntityActionDef Thrust() => new()
    {
        Id = EntityControllers.Attack,
        Clip = "person-thrust",
        Hits = [new() { Id = "spear-tip", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" }, Collider = "spear-tip" }],
        Events = [new() { Id = "swing", At = new() { Marker = "strike" }, Sound = "swing" }],
    };

    /// <summary>The spear's attack collider: a box just behind the tip, enabled by the thrust's window.</summary>
    private static PartGeometry SpearTip() => new() { Id = "spear-tip", Prop = Spear, Point = EquipmentPoints.Tip, Along = -.14f, Geometry = Rectangle2D.Builder.FromSize(.42f, .26f), Collision = new() { } };

    /// <summary>P9a: the typed behaviors of a starter entity: its health and one default strike per hit group.</summary>
    private static Behavior Behaviors(int health, params string[] groups) => new()
    {
        Damage = new() { Health = health },
        Strikes = groups.ToDictionary(g => g, _ => new StrikeDef(), StringComparer.Ordinal),
    };

    public static Part SpearGuardEntity() => new()
    {
        Usage = "character",
        Id = SpearGuard,
        Name = "Spear guard",
        Model = "tall-thin",
        Equipment = [new() { Prop = Spear, Socket = "right-grip" }],
        Geometry = [new() { Id = ColliderUses.Movement, Collision = new(), A = PartLocation.Origin,
            Geometry = EntityAuthoring.MovementBox(.5f, 1.866f)
         }, SpearTip(), .. PersonHurt(TallThin())],
        Behavior = Behaviors(3, "spear-tip") with
        {
            MotionSet = "deliberate",
            Controller = new()
            {
                Kind = EntityControllers.Walker,
                WalkSpeed = .9f,
                Range = 2.1f,
                Cooldown = 1.1f
            },
            Actions = [Thrust()]
        ,
            Colliders = new() { ["spear-tip"] = ColliderUses.Attack, ["body"] = ColliderUses.Hurt, ["head"] = ColliderUses.Hurt, ["legs"] = ColliderUses.Hurt }
        },
    };

    public static Part PlayerEntity() => new()
    {
        Usage = "character",
        Id = Player,
        Name = "Player",
        Model = PersonTemplate.Id,
        Equipment = [new() { Prop = Spear, Socket = "right-grip" }],
        Geometry = [new() { Id = ColliderUses.Movement, Collision = new(), A = PartLocation.Origin,
            Geometry = EntityAuthoring.MovementBox(.55f, 1.705f)
         }, SpearTip(), .. PersonHurt(ResolvedModel.From(PersonTemplate.Model(face: true), null, PersonTemplate.PartOf), .05f)],
        Behavior = Behaviors(5, "spear-tip") with
        {
            MotionSet = "standard",
            Controller = new()
            {
                Kind = EntityControllers.Platformer,
                WalkSpeed = 1.8f,
                RunSpeed = 4.2f,
                JumpSpeed = 7.5f,
                Range = 1.6f,
                Cooldown = .2f
            },
            Actions = [Thrust(), new() { Id = EntityControllers.Jump, Role = "jump", Events = [new() { Id = EntityControllers.Launch, At = new() { Marker = "launch" } }] }]
        ,
            Colliders = new() { ["spear-tip"] = ColliderUses.Attack, ["body"] = ColliderUses.Hurt, ["head"] = ColliderUses.Hurt, ["legs"] = ColliderUses.Hurt }
        },
    };

    public static Part StalkerEntity() => new()
    {
        Usage = "character",
        Id = StalkerPest,
        Name = "Stalker pest",
        Model = Stalker,
        Geometry = [new() { Id = ColliderUses.Movement, Collision = new(), A = PartLocation.Origin,
            Geometry = EntityAuthoring.MovementBox(1, 1.1f)
         },
            new() { Id = "sting", Socket = "stinger",Geometry = Rectangle2D.Builder.FromSize(.3f, .3f),Collision = new() {  }},
            ColliderAuthoring.HurtBox(ResolvedModel.From(StalkerModel()), "shell", ["body", "hip-0", "hip-2"], .2f),
            ColliderAuthoring.HurtBox(ResolvedModel.From(StalkerModel()), "legs", ["knee-0", "knee-1", "knee-2", "foot-0", "foot-1", "foot-2"], .05f),
        ],
        Behavior = Behaviors(2, "sting") with
        {
            MotionSet = "standard",
            Controller = new()
            {
                Kind = EntityControllers.Walker,
                WalkSpeed = .6f,
                Range = 1.05f,
                Cooldown = 1.4f
            },
            Actions =
        [
            new()
            {
                Id = EntityControllers.Attack, Clip = "stalker-lunge",
                Hits = [new() { Id = "sting", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" }, Collider = "sting" }],
                Events = [new() { Id = "bite", At = new() { Marker = "strike" }, Sound = "bite" }],
            },
        ]
        ,
            Colliders = new() { ["sting"] = ColliderUses.Attack, ["shell"] = ColliderUses.Hurt, ["legs"] = ColliderUses.Hurt }
        },
    };
}

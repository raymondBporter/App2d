using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// The phase 3 starter content, written as ordinary authored assets: Person idle/thrust/jump clips, a spear, a stalker
/// creature with its own clips, and three entities (a spear guard that cannot jump, a player that can, and the stalker).
/// Generated from code so the clips stay reviewable; afterwards they are plain files the editor opens like any other.
/// </summary>
public static class StarterContent
{
    public const string SpearGuard = "spear-guard", Player = "player", Stalker = "stalker", StalkerPest = "stalker-pest", Spear = "spear";
    public const string Upper = "upper", Lower = "lower", HeavyWalk = "person-heavy-walk";

    /// <summary>Sockets, motion sets, hurt layout, control groups and looks the Person template carries. None of them change structure.</summary>
    public static void AddPersonExtras(CharacterModel model)
    {
        // back, back-view, sword-hand and gun-hand belong to the player move set (App2d.CharacterStudio/PlayerMoves); keep them in step.
        model.Sockets =
        [
            new() { Id = "right-grip", Control = "right-hand", Frame = "chest", Angle = .12f }, new() { Id = "left-grip", Control = "left-hand", Frame = "chest", Angle = .12f },
            new() { Id = "back", Control = "chest", Frame = "chest", OffsetX = -.1f, OffsetY = -.08f, Angle = -MathF.PI / 2 - .12f },
            new() { Id = "back-view", Control = "chest", Frame = "chest", OffsetX = -.13f, OffsetY = .12f, Angle = -MathF.PI / 2 + .55f },
            new() { Id = "sword-hand", Control = "right-hand", Frame = "right-shoulder" }, new() { Id = "gun-hand", Control = "right-hand", Frame = "right-shoulder" },
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
            new() { Id = Upper, Targets = [.. PersonLoadout.UpperBody.Order(StringComparer.Ordinal)] },
            new() { Id = Lower, Targets = ["hips", "left-hip", "left-leg", "right-hip", "right-leg"] },
        ];
        model.Looks =
        [
            new() { Id = "sage", Name = "Sage", Parts = new() { ["body"] = new() { Fill = "#d8e9db" } } },
            new() { Id = "slate", Name = "Slate", Parts = new() { ["body"] = new() { Fill = "#b8c4d6" }, ["head"] = new() { Fill = "#efe6da", Face = "focused" } } },
            new() { Id = "ember", Name = "Ember", Parts = new() { ["body"] = new() { Fill = "#eab596" }, ["head"] = new() { Face = "determined" } } },
            new() { Id = "night", Name = "Night", Parts = new() { ["body"] = new() { Fill = "#7d879a" }, ["head"] = new() { Fill = "#e2d6c6", Face = "smug" } } },
        ];
        model.HurtLayouts =
        [
            new()
            {
                Id = "standard", Regions =
                [
                    new() { Id = "body", Controls = ["hips", "chest", "left-shoulder", "right-shoulder"], Pad = .14f },
                    new() { Id = "head", Controls = ["head"], Pad = .3f },
                    new() { Id = "legs", Controls = ["left-hip", "right-hip", "left-knee", "right-knee", "left-foot", "right-foot"], Pad = .07f },
                ],
            },
        ];
    }

    public const string PersonHitClip = "person-hit", PersonDeathClip = "person-death";

    /// <summary>Every Person set reacts with the same stand-in hit and death until a set needs its own.</summary>
    private static Dictionary<string, string> Reactions(Dictionary<string, string> roles)
    {
        roles[EntityControllers.Hit] = PersonHitClip; roles[EntityControllers.Death] = PersonDeathClip; return roles;
    }

    public static void Write(string authoredRoot, CharacterModel person)
    {
        void Save(string folder, string id, string json)
        {
            var directory = Path.Combine(authoredRoot, folder); Directory.CreateDirectory(directory);
            AuthoredAsset.Write(Path.Combine(directory, id + ".json"), json);
        }
        var resolved = ResolvedModel.From(person);
        var clips = new[] { PersonIdle(resolved), PersonThrust(resolved), PersonJump(resolved), PersonHit(resolved), PersonDeath(resolved) };
        foreach (var clip in clips) { clip.Validate(resolved); Save("animations", clip.Id, clip.ToJson()); }
        var spear = SpearProp(); Save("props", spear.Id, spear.ToJson());

        var stalker = StalkerModel(); var stalkerResolved = ResolvedModel.From(stalker);
        Save("models", stalker.Id, stalker.ToJson());
        foreach (var clip in new[] { StalkerIdle(stalkerResolved), StalkerWalk(stalkerResolved), StalkerLunge(stalkerResolved), StalkerHit(stalkerResolved), StalkerDeath(stalkerResolved) }) { clip.Validate(stalkerResolved); Save("animations", clip.Id, clip.ToJson()); }

        foreach (var entity in new[] { SpearGuardEntity(), PlayerEntity(), StalkerEntity() }) { entity.Validate(); Save("entities", entity.Id, entity.ToJson()); }
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
        foreach (var key in clip.Tracks.SelectMany(t => t.Keys).Concat(clip.Travel.Keys)) key.Time *= Slow;
        foreach (var contact in clip.Contacts) { contact.Start *= Slow; contact.Finish *= Slow; }
        foreach (var marker in clip.Markers) marker.Time *= Slow;
        foreach (var key in clip.Faces.SelectMany(f => f.Keys)) key.Time *= Slow;
        ClipTrack Channel(string kind, string target)
        {
            var track = clip.Tracks.FirstOrDefault(t => t.Kind == kind && t.Target == target);
            if (track is null) { track = new() { Kind = kind, Target = target, Keys = [new() { Time = 0 }, new() { Time = clip.Duration }] }; clip.Tracks.Add(track); }
            return track;
        }
        var hips = Channel(MotionClip.TranslateKind, "hips"); var mean = hips.Keys.Average(k => k.Y);
        foreach (var key in hips.Keys) key.Y = mean + (key.Y - mean) * Bob + Sink;
        foreach (var key in Channel(MotionClip.TranslateKind, "chest").Keys) key.X += Lean;
        foreach (var arm in new[] { "left-arm", "right-arm" })
            if (clip.Tracks.FirstOrDefault(t => t.Kind == MotionClip.TargetKind && t.Target == arm) is { } track)
            { var center = track.Keys.Average(k => k.X); foreach (var key in track.Keys) key.X = center + (key.X - center) * Swing; }
        return clip;
    }

    private static ClipKey K(float time, float x = 0, float y = 0, string ease = ClipEase.Smooth) => new() { Time = time, X = x, Y = y, Ease = ease };
    private static ClipKey R(float time, float angle, string ease = ClipEase.Smooth) => new() { Time = time, Angle = angle, Ease = ease };
    private static ClipTrack Track(string kind, string target, params ClipKey[] keys) => new() { Kind = kind, Target = target, Keys = [.. keys] };
    private static ClipContact Plant(string chain, float start, float finish, float x = 0) => new() { Chain = chain, Start = start, Finish = finish, Target = new(x) };

    private static MotionClip Clip(ResolvedModel model, string id, string name, float duration, bool loop) => new()
    {
        Id = id, Name = name, Model = model.Base.Id, StructureRevision = model.Base.StructureRevision, Duration = duration, Loop = loop,
        Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), Travel = new() { Scale = "leg" },
    };

    /// <summary>A quiet two-second breath with both feet planted for the whole cycle.</summary>
    public static MotionClip PersonIdle(ResolvedModel model)
    {
        var clip = Clip(model, "person-idle", "Idle", 2, true);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, "hips", K(0), K(1, 0, -.012f), K(2)),
            Track(MotionClip.RotateKind, "chest", R(0, 0), R(1, .018f), R(2, 0)),
            Track(MotionClip.TargetKind, "left-arm", K(0), K(1, -.01f, .02f), K(2)),
            Track(MotionClip.TargetKind, "right-arm", K(0), K(1, .01f, .02f), K(2)),
        ];
        clip.Contacts = [Plant("left-leg", 0, 2), Plant("right-leg", 0, 2)];
        return clip;
    }

    /// <summary>Stand-in hit reaction: the chest snaps back and the hips dip on planted feet, then settle.</summary>
    public static MotionClip PersonHit(ResolvedModel model)
    {
        var clip = Clip(model, PersonHitClip, "Hit (stand-in)", .3f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, "hips", K(0), K(.06f, -.04f, -.04f, ClipEase.Linear), K(.3f)),
            Track(MotionClip.RotateKind, "chest", R(0, 0), R(.06f, .22f, ClipEase.Linear), R(.3f, 0)),
            Track(MotionClip.TargetKind, "left-arm", K(0), K(.06f, .1f, .12f, ClipEase.Linear), K(.3f)),
            Track(MotionClip.TargetKind, "right-arm", K(0), K(.06f, .1f, .12f, ClipEase.Linear), K(.3f)),
        ];
        clip.Contacts = [Plant("left-leg", 0, .3f), Plant("right-leg", 0, .3f)];
        clip.Faces = [new() { Part = "head", Keys = [new() { Time = 0, Expression = "hurt" }] }];
        return clip;
    }

    /// <summary>Stand-in death: the knees give, the body folds back onto the ground and stays down.</summary>
    public static MotionClip PersonDeath(ResolvedModel model)
    {
        var clip = Clip(model, PersonDeathClip, "Death (stand-in)", 1.1f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, "hips", K(0), K(.25f, -.03f, -.12f), K(.75f, -.28f, -.72f), K(1.1f, -.3f, -.7f)),
            Track(MotionClip.RotateKind, "chest", R(0, 0), R(.25f, -.15f), R(.75f, .9f), R(1.1f, 1.1f)),
            Track(MotionClip.TargetKind, "left-arm", K(0), K(.4f, .15f, .25f), K(1.1f, -.1f, .05f)),
            Track(MotionClip.TargetKind, "right-arm", K(0), K(.4f, .15f, .25f), K(1.1f, -.05f, 0)),
        ];
        clip.Contacts = [Plant("left-leg", 0, 1.1f), Plant("right-leg", 0, 1.1f)];
        clip.Markers = [new() { Id = "impact", Time = .75f }];
        clip.Faces = [new() { Part = "head", Keys = [new() { Time = 0, Expression = "hurt" }, new() { Time = .75f, Expression = "knocked-out" }] }];
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
            Track(MotionClip.TranslateKind, "hips", K(0), K(.32f, -.04f, -.03f), K(.42f, .09f, -.05f, ClipEase.Linear), K(.58f, .09f, -.05f), K(.9f)),
            Track(MotionClip.RotateKind, "chest", R(0, 0), R(.32f, .14f), R(.42f, -.16f, ClipEase.Linear), R(.58f, -.16f), R(.9f, 0)),
            Track(MotionClip.TargetKind, "right-arm", K(0), K(.32f, -.33f, .35f), K(.42f, .57f, .5f, ClipEase.Linear), K(.58f, .57f, .5f), K(.9f)),
            Track(MotionClip.TargetKind, "left-arm", K(0), K(.32f, .12f, .1f), K(.42f, -.18f, .12f, ClipEase.Linear), K(.58f, -.18f, .12f), K(.9f)),
        ];
        clip.Contacts = [Plant("left-leg", 0, .9f), Plant("right-leg", 0, .9f)];
        clip.Markers = [new() { Id = "windup", Time = .05f }, new() { Id = "strike", Time = .4f }, new() { Id = "recover", Time = .58f }];
        clip.Faces = [new() { Part = "head", Keys = [new() { Time = 0, Expression = "focused" }, new() { Time = .36f, Expression = "determined" }, new() { Time = .7f, Expression = "focused" }] }];
        return clip;
    }

    /// <summary>Crouch with planted feet, "launch" (the controller leaves the ground here), tuck through the apex, then reach for the ground and hold.</summary>
    public static MotionClip PersonJump(ResolvedModel model)
    {
        var clip = Clip(model, "person-jump", "Jump", .9f, false);
        clip.Tracks =
        [
            Track(MotionClip.TranslateKind, "hips", K(0), K(.14f, 0, -.14f), K(.24f, 0, .04f), K(.5f), K(.9f)),
            Track(MotionClip.TargetKind, "left-leg", K(0), K(.2f), K(.36f, .12f, .32f), K(.9f, .06f, .08f)),
            Track(MotionClip.TargetKind, "right-leg", K(0), K(.2f), K(.36f, -.04f, .24f), K(.9f, .02f, .06f)),
            Track(MotionClip.TargetKind, "left-arm", K(0), K(.14f, -.12f, .08f), K(.26f, .2f, .45f), K(.9f, .06f, .12f)),
            Track(MotionClip.TargetKind, "right-arm", K(0), K(.14f, -.12f, .08f), K(.26f, .22f, .42f), K(.9f, .08f, .12f)),
        ];
        clip.Contacts = [Plant("left-leg", 0, .2f), Plant("right-leg", 0, .2f)];
        clip.Markers = [new() { Id = "launch", Time = .2f }, new() { Id = "apex", Time = .5f }];
        return clip;
    }

    public static PropAsset SpearProp() => new()
    {
        Id = Spear, Name = "Spear", Grip = new(0, 0), Tip = new(1.38f, 0), SecondGrip = new(-.3f, 0),
        Shapes =
        [
            new() { Kind = "stroke", Points = [new(-.5f, 0), new(1.12f, 0)], Width = .045f, Fill = "#8a6a44" },
            new() { Kind = "polygon", Points = [new(1.1f, -.055f), new(1.38f, 0), new(1.1f, .055f)], Fill = "#c9d2da" },
        ],
    };

    // ---- Stalker --------------------------------------------------------------------------------------------------

    /// <summary>A headless three-legged creature built from Empty with the same authoring operations as the editor.</summary>
    public static CharacterModel StalkerModel()
    {
        var model = ModelAuthoring.Empty(Stalker, "Stalker");
        ModelAuthoring.AddControl(model, null, new(0, .9f, 0), "body");
        for (var i = 0; i < 3; i++)
        {
            var x = (i - 1) * .35f; var z = (i - 1) * .1f;
            ModelAuthoring.AddControl(model, "body", new(x, .8f, z), $"hip-{i}");
            ModelAuthoring.AddControl(model, $"hip-{i}", new(x + .15f, .4f, z), $"knee-{i}");
            ModelAuthoring.AddControl(model, $"knee-{i}", new(x, 0, z), $"foot-{i}");
            ModelAuthoring.AddChain(model, $"foot-{i}");
            ModelAuthoring.AddPart(model, "stroke", $"hip-{i}", $"knee-{i}"); ModelAuthoring.AddPart(model, "stroke", $"knee-{i}", $"foot-{i}");
        }
        ModelAuthoring.AddMeasure(model, "leg", ["hip-1", "knee-1", "foot-1"]);
        foreach (var chain in model.Chains) chain.Scale = "leg";
        foreach (var control in model.Controls.Where(c => c.Id == "body" || c.Id.StartsWith("hip-", StringComparison.Ordinal))) control.Scale = "leg";
        var shell = ModelAuthoring.AddPart(model, "ellipse", "body"); shell.Width = .95f; shell.Height = .38f; shell.Fill = "#c9e0b8"; shell.Depth = -.05f;
        model.Sockets = [new() { Id = "stinger", Control = "body", OffsetX = .5f, OffsetY = -.02f }];
        model.MotionSets = [new() { Id = "standard", Name = "Standard", Roles = new() { ["idle"] = "stalker-idle", ["walk"] = "stalker-walk", ["hit"] = "stalker-hit", ["death"] = "stalker-death" } }];
        model.HurtLayouts = [new() { Id = "standard", Regions = [new() { Id = "shell", Controls = ["body", "hip-0", "hip-2"], Pad = .2f }, new() { Id = "legs", Controls = ["knee-0", "knee-1", "knee-2", "foot-0", "foot-1", "foot-2"], Pad = .05f }] }];
        model.Validate(); return model;
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
        clip.Travel.Keys = [new() { Time = 0 }, new() { Time = 1, X = .6f }];
        ClipKey L(float t, float x, float y = 0) => K(t, x, y, ClipEase.Linear);
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
        Id = EntityControllers.Attack, Clip = "person-thrust",
        Hits = [new() { Id = "spear-tip", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" }, Prop = Spear, Point = PropAsset.TipPoint, Along = -.14f, Width = .42f, Height = .26f }],
        Events = [new() { Id = "swing", At = new() { Marker = "strike" }, Sound = "swing" }],
    };

    public static EntityAsset SpearGuardEntity() => new()
    {
        Id = SpearGuard, Name = "Spear guard", Model = "tall-thin", MotionSet = "deliberate",
        Controller = new() { Kind = EntityControllers.Walker, WalkSpeed = .9f, Range = 2.1f, Cooldown = 1.1f }, Health = 3,
        Movement = new() { Width = .5f, Height = 2.1f }, Hurt = new() { Layout = "standard" },
        Equipment = [new() { Prop = Spear, Socket = "right-grip" }], Actions = [Thrust()],
    };

    public static EntityAsset PlayerEntity() => new()
    {
        Id = Player, Name = "Player", Model = PersonTemplate.Id, MotionSet = "standard",
        Controller = new() { Kind = EntityControllers.Platformer, WalkSpeed = 1.8f, RunSpeed = 4.2f, JumpSpeed = 7.5f, Range = 1.6f, Cooldown = .2f }, Health = 5,
        Movement = new() { Width = .55f, Height = 1.9f }, Hurt = new() { Layout = "standard", Regions = new() { ["legs"] = new() { Pad = .05f } } },
        Equipment = [new() { Prop = Spear, Socket = "right-grip" }],
        Actions = [Thrust(), new() { Id = EntityControllers.Jump, Role = "jump", Events = [new() { Id = EntityControllers.Launch, At = new() { Marker = "launch" } }] }],
    };

    public static EntityAsset StalkerEntity() => new()
    {
        Id = StalkerPest, Name = "Stalker pest", Model = Stalker, MotionSet = "standard",
        Controller = new() { Kind = EntityControllers.Walker, WalkSpeed = .6f, Range = 1.05f, Cooldown = 1.4f }, Health = 2,
        Movement = new() { Width = 1, Height = 1.1f }, Hurt = new() { Layout = "standard" },
        Actions =
        [
            new()
            {
                Id = EntityControllers.Attack, Clip = "stalker-lunge",
                Hits = [new() { Id = "sting", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" }, Socket = "stinger", Width = .3f, Height = .3f }],
                Events = [new() { Id = "bite", At = new() { Marker = "strike" }, Sound = "bite" }],
            },
        ],
    };
}

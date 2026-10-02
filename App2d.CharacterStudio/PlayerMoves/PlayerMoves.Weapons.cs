using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

internal static partial class PlayerMoves
{
    /// <summary>Quick starting performances: preserve the existing arm/strike timing, add depth and blade roll for editing.</summary>
    public static MotionClip WeaponMotion(MotionClip clip)
    {
        void Keys(string socket, params (float Time, float Twist, float Tilt)[] keys)
        {
            clip.Tracks.RemoveAll(t => t.Kind == MotionClip.OrientKind && t.Target == socket);
            foreach (var (time, twist, tilt) in keys)
                ClipAuthoring.SetKey(clip, new(MotionClip.OrientKind, socket), time, new Vector3(twist, tilt, 0) * (MathF.PI / 180));
        }
        switch (clip.Id)
        {
            case "player-sword-sheathe":
                Keys(PersonLoadout.SwordSocket, (0, 20, 0), (.12f, 65, -18), (.22f, 25, -10), (.3f, 0, 0), (.42f, 0, 0)); break;
            case "player-sword-down-attack":
                Keys(PersonLoadout.SwordSocket, (0, 65, 15), (.04f, 15, 0), (.18f, 15, 0), (.25f, 30, 0)); break;
            case "person-hammer-slam":
                Keys(PersonLoadout.SwordSocket, (0, 24, 0), (.55f, 42, -14), (.7f, 42, -14), (.78f, 18, 0), (.95f, 18, 0), (1.5f, 24, 0)); break;
            case "player-gun-aim":
                Keys(PersonLoadout.GunSocket, (0, -12, 10), (clip.Duration, -12, 10)); break;
            case "player-gun-shot":
            case "player-gun-wall-shot":
                Keys(PersonLoadout.GunSocket, (0, -12, 10), (.03f, -26, 14), (clip.Duration, -12, 10)); break;
            case "person-pistol-shot":
                Keys(PersonLoadout.GunSocket, (0, -12, 10), (.58f, -12, 10), (.65f, -26, 14), (clip.Duration, -12, 10)); break;
        }
        return clip;
    }

    /// <summary>Only upgrades the weapon assets and their orientation tracks; leaves body keys and entity settings intact.</summary>
    public static void WriteWeapons(string root)
    {
        var path = Path.Combine(root, "models", "person.json");
        var model = CharacterModel.FromJson(File.ReadAllText(path));

        foreach (var socket in model.Sockets)
        {
            if (socket.Id is PersonLoadout.BackSocket or PersonLoadout.BackViewSocket)
                socket.OffsetZ = socket.Id == PersonLoadout.BackSocket ? .2f : -.2f;
        }

        model.Save(path);
        foreach (var prop in Props().Append(HammerProp()).Append(StarterContent.SpearProp()))
        {
            prop.Save(Path.Combine(root, "props", prop.Id + ".json"));
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "animations"), "*.json"))
        {
            var clip = MotionClip.FromJson(File.ReadAllText(file));
            var before = clip.ToJson();
            WeaponMotion(clip);
            if (before != clip.ToJson())
            {
                clip.Validate(ResolvedModel.From(model));
                clip.Save(file);
            }
        }
    }
}

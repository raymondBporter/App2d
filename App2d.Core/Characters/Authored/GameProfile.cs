using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The game's character bindings as resource data (P9c; one profile, `game`): which authored entity and model the player is, which clip plays
/// each of the player's semantic roles, which props are its gear, and which authored entity each world placement kind
/// spawns. The controller, director and loadout algorithms stay in code and read these by role; nothing here is a clip id
/// baked into a switch. Format <c>app2d-profile</c>, folder <c>profiles/</c>.
/// </summary>
public sealed class GameProfile
{
    public const string FormatId = "app2d-profile";
    public const int CurrentVersion = 1;
    /// <summary>The profile the game and the Person presentation read; a catalog id of its own, apart from the player entity.</summary>
    public const string GameId = "game";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = GameId;
    public string Name { get; set; } = "Game";
    /// <summary>The player's authored entity (its actions, windows, strikes, health and movement box); empty for a profile that only binds spawns.</summary>
    public string Entity { get; set; } = "";
    /// <summary>The model the player's clips are for; empty for a profile that only binds spawns.</summary>
    public string Model { get; set; } = "";
    /// <summary>Whether the profile binds a player (an entity and a model); the Person move set needs one.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasPlayer => Entity.Length > 0;
    /// <summary>Role → clip id for every role the Person director plays (<see cref="PersonRoles"/>).</summary>
    public Dictionary<string, string> Roles { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The props the player carries and wears, by role.</summary>
    public PersonGearIds Gear { get; set; } = new();
    /// <summary>World placement kind name → the authored entity it spawns.</summary>
    public Dictionary<string, string> Spawns { get; set; } = new(StringComparer.Ordinal);

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);

    public static GameProfile FromJson(string json)
    {
        var profile = AuthoredAsset.Parse<GameProfile>(json, "profile");
        profile.Validate();
        return profile;
    }

    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    /// <summary>Checks the file on its own: ids and every required role assigned. References are checked by the catalog.</summary>
    public void Validate()
    {
        var owner = $"Profile '{Id}'";
        if (Format != FormatId || Version != CurrentVersion)
        {
            throw new InvalidDataException($"{owner}: unsupported format/version.");
        }

        AuthoredAsset.RequireId(Id, "profile id");
        if (string.IsNullOrWhiteSpace(Name) || Roles is null || Gear is null || Spawns is null || Entity is null || Model is null)
        {
            throw new InvalidDataException($"{owner}: name, entity, model, roles, gear and spawns are required.");
        }

        if ((Entity.Length > 0) != (Model.Length > 0))
        {
            throw new InvalidDataException($"{owner}: a player binding names both its entity and its model.");
        }

        if (HasPlayer)
        {
            AuthoredAsset.RequireId(Entity, $"{owner} entity");
            AuthoredAsset.RequireId(Model, $"{owner} model");
        }

        foreach (var (role, clip) in Roles)
        {
            AuthoredAsset.RequireId(role, $"{owner} roles key");
            AuthoredAsset.RequireId(clip, $"{owner} roles.{role}");
        }
        Gear.Validate(owner);
        foreach (var (kind, entity) in Spawns)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new InvalidDataException($"{owner} spawns: empty kind.");
            }

            AuthoredAsset.RequireId(entity, $"{owner} spawns.{kind}");
        }
    }

    /// <summary>Checks every reference against the catalog by name: the entity, the model, each role's clip (for the model) and each gear prop.</summary>
    public void Check(AuthoredCatalog catalog)
    {
        var owner = $"Profile '{Id}'";
        if (HasPlayer)
        {
            if (!catalog.Entities.ContainsKey(Entity))
            {
                throw new InvalidDataException($"{owner} entity: '{Entity}' is missing or does not compile.");
            }

            var model = catalog.Resolve(Model);
            foreach (var (role, clipId) in Roles)
            {
                var clip = catalog.Animations.GetValueOrDefault(clipId) ?? throw new InvalidDataException($"{owner} roles.{role}: no animation '{clipId}'.");
                if (clip.Model != model.Base.Id)
                {
                    throw new InvalidDataException($"{owner} roles.{role}: animation '{clipId}' is for model '{clip.Model}', not '{model.Base.Id}'.");
                }
            }
            foreach (var (what, prop) in new[] { ("sword", Gear.Sword), ("pistol", Gear.Pistol), ("hair", Gear.Hair), ("hairBack", Gear.HairBack) })
            {
                if (!catalog.Parts.ContainsKey(prop))
                {
                    throw new InvalidDataException($"{owner} gear.{what}: no prop '{prop}'.");
                }
            }
        }
        else if (Roles.Count > 0)
        {
            throw new InvalidDataException($"{owner}: roles need a player entity and model.");
        }
        foreach (var (kind, entity) in Spawns)
        {
            if (!catalog.Entities.ContainsKey(entity))
            {
                throw new InvalidDataException($"{owner} spawns.{kind}: entity '{entity}' is missing or does not compile.");
            }
        }
    }
}

/// <summary>The player's semantic animation roles: what the Person director asks for, bound to clips by the profile.</summary>
public static class PersonRoles
{
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Run = "run";
    public const string Jump = "jump";
    public const string Fall = "fall";
    public const string Land = "land";
    public const string Dash = "dash";
    public const string ClimbOn = "climb-on";
    public const string Climb = "climb";
    public const string ClimbOff = "climb-off";
    public const string WallGrip = "wall-grip";
    public const string BalanceForward = "balance-forward";
    public const string BalanceBackward = "balance-backward";
    public const string Hit = "hit";
    public const string Death = "death";
    public const string Celebrate = "celebrate";
    public const string Sheathe = "sheathe";
    public const string DownAttack = "down-attack";
    public const string GunAim = "gun-aim";
    public const string GunShot = "gun-shot";
    public const string GunWallShot = "gun-wall-shot";
    public const string GunCharge = "gun-charge";
    public const string HealGather = "heal-gather";
    public static readonly IReadOnlyList<string> All =
    [
        Idle, Walk, Run, Jump, Fall, Land, Dash, ClimbOn, Climb, ClimbOff, WallGrip, BalanceForward, BalanceBackward,
        Hit, Death, Celebrate, Sheathe, DownAttack, GunAim, GunShot, GunWallShot, GunCharge, HealGather,
    ];
}

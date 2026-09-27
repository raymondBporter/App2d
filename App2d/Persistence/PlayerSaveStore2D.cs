using App2d.Core;
using App2d.Core.Assets;
using App2d.Core.IO;
using System.Security;
using System.Text.Json;

namespace App2d.Persistence;

public sealed class PlayerSaveStore2D
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public PlayerSaveStore2D(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public static PlayerSaveStore2D CreateDefault() => new(UserDataLocations.ForCurrentUser().PlayerSave);

    public PlayerSave2D? TryLoad()
    {
        try
        {
            if (!File.Exists(Path))
                return null;

            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<PlayerSave2D>(json, JsonOptions);
        }
        catch (Exception exception) when (IsRecoverableFileException(exception) || exception is JsonException or ArgumentException)
        {
            return null;
        }
    }

    public bool TrySave(PlayerSave2D save)
    {
        ArgGuard.ThrowIfNull(save);
        try
        {
            AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(save, JsonOptions));
            return true;
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return false;
        }
    }

    private static bool IsRecoverableFileException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
}

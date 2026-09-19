using App2d.Core;
using System.Security;
using System.Text.Json;

namespace App2d.Gameplay.Persistence;

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

    public static PlayerSaveStore2D CreateDefault()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new PlayerSaveStore2D(System.IO.Path.Combine(localData, "App2d", "save.json"));
    }

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
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(save, JsonOptions));
            File.Move(temporaryPath, Path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (IsRecoverableFileException(exception))
        {
            return false;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (IsRecoverableFileException(exception))
            {
            }
        }
    }

    private static bool IsRecoverableFileException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
}

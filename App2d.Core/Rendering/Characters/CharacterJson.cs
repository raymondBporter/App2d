using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Core.Rendering.Characters;

internal static class CharacterJson
{
    public static float Number(this JsonElement j, string key, float fallback = 0) => j.TryGetProperty(key, out var value) ? value.GetSingle() : fallback;
    public static string Text(this JsonElement j, string key, string fallback = "") => j.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
    public static bool Flag(this JsonElement j, string key) => j.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    public static float[] Floats(this JsonElement j) => [.. j.EnumerateArray().Select(v => v.GetSingle())];
    public static int[] Ints(this JsonElement j) => [.. j.EnumerateArray().Select(v => v.GetInt32())];
    public static Vector3 Vector(this JsonElement j) { var a = j.Floats(); return new(a[0], a[1], a[2]); }
    public static Color? Rgba(this JsonElement j) => j.ValueKind == JsonValueKind.Null ? null : new Color(j[0].GetSingle(), j[1].GetSingle(), j[2].GetSingle(), j[3].GetSingle());
}

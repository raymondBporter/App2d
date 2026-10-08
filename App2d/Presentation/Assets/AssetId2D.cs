using App2d.Core.Validation;
using System.Runtime.CompilerServices;

namespace App2d.Presentation.Assets;

internal static class AssetId2D
{
    public static void Validate(
        string id,
        [CallerArgumentExpression(nameof(id))] string? paramName = null)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(id, paramName);
        ArgGuard.ThrowIf(id[0] == '-' ||
            id[^1] == '-' ||
            id.Contains("--", StringComparison.Ordinal) ||
            id.Any(character =>
                character is not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and
                    not '-'),
            "Asset IDs must use lowercase kebab case.", paramName);
    }
}

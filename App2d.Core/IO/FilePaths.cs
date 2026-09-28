using App2d.Core.Validation;
namespace App2d.Core.IO;

/// <summary>Shared filesystem path operations; does not create files or directories.</summary>
public static class FilePaths
{
    /// <summary>
    /// Resolves a relative path inside a root, rejecting rooted paths and lexical escapes.
    /// This normalizes path segments; it does not resolve filesystem links.
    /// </summary>
    public static string ResolveUnderRoot(string root, string relativePath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(root);
        ArgGuard.ThrowIfNullOrWhiteSpace(relativePath);
        ArgGuard.ThrowIf(Path.IsPathRooted(relativePath), "Path must be relative to its root.", nameof(relativePath));
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        ArgGuard.ThrowIf(relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot), "Path must stay inside its root.", nameof(relativePath));
        return fullPath;
    }
}

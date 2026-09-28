using App2d.Core.Validation;
using System.Text;

namespace App2d.Core.IO;

/// <summary>Shared per-file saving. Serialization, validation, and recovery policy belong to the caller.</summary>
public static class AtomicFile
{
    /// <summary>Writes UTF-8 text without a BOM, then replaces the destination using a sibling temporary file.</summary>
    public static void WriteAllText(string path, string text)
    {
        ArgGuard.ThrowIfNull(text);
        Write(path, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true);
            writer.Write(text);
        });
    }

    /// <summary>
    /// Creates parent directories and writes synchronously to a unique sibling file. A failed writer
    /// leaves an existing destination intact. This method owns the stream and cleans up the temporary
    /// file on failure. Concurrent successful saves use last-replacement-wins semantics.
    /// </summary>
    public static void Write(string path, Action<Stream> write)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        ArgGuard.ThrowIfNull(write);
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(directory);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            // Cleanup must not hide the original write/replace error or report a committed save as failed.
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
    }
}

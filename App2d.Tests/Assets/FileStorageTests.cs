using App2d.Core.IO;
using System.Text;

namespace App2d.Tests.Assets;

public sealed class FileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-storage-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TextSaveCreatesParentsReplacesContentsAndLeavesNoTemporaryFiles()
    {
        var path = Path.Combine(_root, "nested", "asset.json");
        AtomicFile.WriteAllText(path, "first");
        AtomicFile.WriteAllText(path, "grass 🌱");
        Assert.Equal(Encoding.UTF8.GetBytes("grass 🌱"), File.ReadAllBytes(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void FailedSerializationPreservesOldContentsAndRemovesPartialTemporaryFile()
    {
        var path = Path.Combine(_root, "asset.bin");
        AtomicFile.WriteAllText(path, "original");
        var failure = new InvalidDataException("Serializer failed.");
        Assert.Same(failure, Assert.Throws<InvalidDataException>(() => AtomicFile.Write(path, stream =>
        {
            stream.Write([1, 2, 3]);
            throw failure;
        })));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_root));
    }

    [Fact]
    public void FailedReplacementCleansUpWithoutTouchingTheDestination()
    {
        var path = Path.Combine(_root, "asset.json");
        AtomicFile.WriteAllText(path, "original");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => AtomicFile.WriteAllText(path, "replacement"));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_root));
    }

    [Fact]
    public void ReentrantSavesUseIndependentTemporaryFiles()
    {
        var path = Path.Combine(_root, "asset.txt");
        AtomicFile.Write(path, stream =>
        {
            AtomicFile.WriteAllText(path, "inner");
            stream.Write(Encoding.UTF8.GetBytes("outer"));
        });
        Assert.Equal("outer", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_root));
    }

    [Fact]
    public void InvalidUtf8TextDoesNotReplaceAValidDocument()
    {
        var path = Path.Combine(_root, "asset.txt");
        AtomicFile.WriteAllText(path, "original");
        Assert.Throws<EncoderFallbackException>(() => AtomicFile.WriteAllText(path, "\ud800"));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("folder/../../outside.png")]
    [InlineData("../app2d-assets-other/file.png")]
    public void RelativePathsCannotEscapeTheirRoot(string relativePath) =>
        Assert.Throws<ArgumentException>(() => FilePaths.ResolveUnderRoot(_root, relativePath));

    [Fact]
    public void RootedPathsAreRejectedAndInternalSegmentsNormalize()
    {
        Assert.Throws<ArgumentException>(() => FilePaths.ResolveUnderRoot(_root, Path.Combine(_root, "image.png")));
        Assert.Equal(Path.Combine(_root, "textures", "image.png"),
            FilePaths.ResolveUnderRoot(_root, "textures/temp/../image.png"));
        Assert.False(Directory.Exists(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

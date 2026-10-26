using App2d.Core.Rendering;

namespace App2d.Tests.Rendering;

public sealed class ScreenTextWrapping2DTests
{
    [Theory]
    [InlineData("one two three", 7, "one two\nthree")]
    [InlineData("abcdefghijk", 5, "abcde\nfghij\nk")]
    [InlineData("abc   \ndef", 3, "abc\ndef")]
    [InlineData("one\r\n\r\ntwo", 10, "one\n\ntwo")]
    [InlineData("one\n", 10, "one\n")]
    [InlineData("   one   ", 3, "one")]
    [InlineData("", 3, "")]
    public void WrappingPreservesWordsExplicitBreaksAndEveryHardBrokenCharacter(string text, int width, string expected)
    {
        Assert.Equal(expected, ScreenTextWrapping2D.Wrap(text, width, int.MaxValue, _ => 1));
    }

    [Theory]
    [InlineData("WWW ii", 9, int.MaxValue, "WWW\nii")]
    [InlineData("abcdefghijk", 5, 2, "abcde\nfg...")]
    [InlineData("one two", 2, 1, "..")]
    [InlineData("Wiii", 2, int.MaxValue, "W\nii\ni")]
    public void GlyphWidthsAndEllipsisRespectTheAvailableWidth(string text, int width, int maxLines, string expected)
    {
        static float Advance(char character) => character == 'W' ? 3 : 1;
        var wrapped = ScreenTextWrapping2D.Wrap(text, width, maxLines, Advance);
        Assert.Equal(expected, wrapped);
        Assert.All(wrapped.Split('\n'), line => Assert.True(line.Sum(Advance) <= width || line.Length == 1));
    }
}

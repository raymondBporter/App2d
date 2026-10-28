using System.Text;

namespace App2d.Core.Rendering;

/// <summary>Word wrapping against the same per-character advances used to draw the font atlas.</summary>
internal static class ScreenTextWrapping2D
{
    // Arguments are validated by Renderer2D. A single glyph wider than the limit occupies its own line.
    internal static string Wrap(string text, float width, int maxLines, Func<char, float> advance)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
        var result = new StringBuilder();
        var start = 0;
        for (var lineIndex = 0; lineIndex < maxLines; lineIndex++)
        {
            while (start < text.Length && text[start] != '\n' && char.IsWhiteSpace(text[start])) start++;
            var end = start;
            var lastSpace = -1;
            var lineWidth = 0f;
            while (end < text.Length && text[end] != '\n')
            {
                var nextWidth = lineWidth + advance(text[end]);
                if (end > start && nextWidth > width) break;
                lineWidth = nextWidth;
                if (char.IsWhiteSpace(text[end])) lastSpace = end;
                end++;
            }

            var newline = end < text.Length && text[end] == '\n';
            int next;
            if (end < text.Length && !newline && !char.IsWhiteSpace(text[end]) && lastSpace >= start)
            {
                next = lastSpace + 1;
                end = lastSpace;
            }
            else
            {
                next = end + (newline ? 1 : 0);
            }

            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
            if (!newline)
            {
                while (next < text.Length && text[next] != '\n' && char.IsWhiteSpace(text[next])) next++;
                // Trailing spaces at a soft wrap must not turn one explicit break into a blank line.
                if (next < text.Length && text[next] == '\n') { newline = true; next++; }
            }
            var hasMore = newline || next < text.Length;
            var line = text[start..end];
            if (lineIndex == maxLines - 1 && hasMore) line = Ellipsize(line, width, advance);
            if (lineIndex > 0) result.Append('\n');
            result.Append(line);
            if (!hasMore) break;
            start = next;
        }
        return result.ToString();
    }

    private static string Ellipsize(string line, float width, Func<char, float> advance)
    {
        var dotWidth = advance('.');
        var dots = 3;
        while (dots > 0 && dots * dotWidth > width) dots--;
        var end = line.Length;
        var lineWidth = 0f;
        foreach (var character in line) lineWidth += advance(character);
        while (end > 0 && lineWidth + dots * dotWidth > width) lineWidth -= advance(line[--end]);
        return line[..end].TrimEnd() + new string('.', dots);
    }
}

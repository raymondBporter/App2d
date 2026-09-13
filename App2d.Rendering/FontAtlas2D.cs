using System.Drawing.Text;
using Texture2D = App2d.Rendering.Textures.Texture2D;

namespace App2d.Rendering;

/// <summary>Rasterizes a small font atlas once; all on-screen text is drawn by MonoGame.</summary>
internal sealed class FontAtlas2D : IDisposable
{
    private const int Cell = 48;
    private readonly Dictionary<char, Glyph> _glyphs = [];
    public Texture2D Texture { get; }
    public float Ascent { get; }
    public float LineHeight { get; }

    public FontAtlas2D()
    {
        using var font = new Font(FontFamily.GenericSansSerif, 28f, GraphicsUnit.Pixel);
        Ascent = font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);
        LineHeight = font.GetHeight();
        using var bitmap = new Bitmap(Cell * 16, Cell * 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        var characters = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96))
            .Select(value => (char)value).Concat("—–…←→↑↓×").Distinct().ToArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            var x = index % 16 * Cell;
            var y = index / 16 * Cell;
            var text = character.ToString();
            var width = graphics.MeasureString(text, font, PointF.Empty, format).Width;
            graphics.DrawString(text, font, Brushes.White, x + 4, y + 4, format);
            _glyphs.Add(character, new Glyph(new ScreenRectangle2D(x, y, x + Cell, y + Cell), width));
        }
        Texture = Texture2D.FromBitmap("HUD font atlas", bitmap);
    }

    public Glyph GetGlyph(char character) => _glyphs.GetValueOrDefault(character, _glyphs['?']);
    public float Measure(string text)
    {
        var width = 0f;
        var maximum = 0f;
        foreach (var character in text)
        {
            if (character == '\n') { maximum = Math.Max(maximum, width); width = 0f; }
            else width += GetGlyph(character).Advance;
        }
        return Math.Max(maximum, width);
    }
    public void Dispose() => Texture.Dispose();
    internal readonly record struct Glyph(ScreenRectangle2D Bounds, float Advance);
}

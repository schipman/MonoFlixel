using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>One character of a bitmap font: where its picture is, and how to place it.</summary>
public sealed class FlxGlyph
{
    /// <summary>Which of the font's pictures (pages) it is on.</summary>
    public int Page;

    /// <summary>The glyph's picture inside the page.</summary>
    public Rectangle Source;

    /// <summary>Shift of the picture from where the character starts, in pixels (BMFont: xoffset / yoffset from the top of the line).</summary>
    public int XOffset, YOffset;

    /// <summary>How far the pen moves after this character.</summary>
    public int XAdvance;
}

/// <summary>
/// A font made of pictures: every character is a rectangle of a texture. Port of Flixel's FlxBitmapFont.
/// Load one with:
/// <list type="bullet">
/// <item><see cref="FromMonospace"/>: a grid of equally sized characters.</item>
/// <item><see cref="FromXna"/>: characters in a row, separated by lines of a border color (XNA / "bitmap font maker" sheets).</item>
/// <item><see cref="FromBMFont"/>: AngelCode BMFont (.fnt text or XML + page images): proportional, with offsets and kerning.</item>
/// <item><see cref="Load"/>: the atlas + json pair used by the built-in default font.</item>
/// </list>
/// Draw it with <see cref="FlxBitmapText"/> (or FlxText, which uses <see cref="Default"/> unless you change its Font).
/// </summary>
public sealed class FlxBitmapFont
{
    readonly Dictionary<int, FlxGlyph> _glyphs = new();
    readonly Dictionary<long, int> _kerning = new();

    /// <summary>The pictures the glyphs are on (most fonts have one).</summary>
    public Texture2D[] Pages { get; }

    /// <summary>The first page.</summary>
    public Texture2D Atlas => Pages[0];

    public string Name { get; set; } = "";

    /// <summary>Distance from one line to the next, in pixels.</summary>
    public int LineHeight { get; set; }

    /// <summary>The font's native size in pixels (what "size 1x" means).</summary>
    public int Size { get; set; }

    /// <summary>Fonts that only have capital letters: lower case text is drawn as capitals.</summary>
    public bool UpperCase { get; set; }

    /// <summary>How far the pen moves for a space when the font has no space glyph.</summary>
    public int SpaceWidth { get; set; }

    /// <summary>Number of characters in the font.</summary>
    public int GlyphCount => _glyphs.Count;

    static FlxBitmapFont? _default;

    /// <summary>The default font: Flixel's Nokia Cellphone FC at 8 pixels (Assets/flixel/nokia8).</summary>
    public static FlxBitmapFont Default => _default ??= Load("flixel/nokia8");

    public FlxBitmapFont(Texture2D[] pages, int lineHeight, int size = 0)
    {
        if (pages.Length == 0) throw new ArgumentException("A font needs at least one page.", nameof(pages));
        Pages = pages;
        LineHeight = lineHeight;
        Size = size > 0 ? size : lineHeight;
        SpaceWidth = Math.Max(1, Size / 2);
    }

    // ------------------------------------------------------------------ glyphs

    public void AddGlyph(int character, FlxGlyph glyph) => _glyphs[character] = glyph;

    public void AddGlyph(int character, Rectangle source, int xOffset, int yOffset, int xAdvance, int page = 0) =>
        _glyphs[character] = new FlxGlyph { Page = page, Source = source, XOffset = xOffset, YOffset = yOffset, XAdvance = xAdvance };

    public bool HasGlyph(int character) => _glyphs.ContainsKey(character);

    /// <summary>The glyph for a character, a '?' if the font doesn't have it, or null if it has neither.</summary>
    public FlxGlyph? GetGlyph(int character)
    {
        if (UpperCase && character < 0x10000) character = char.ToUpperInvariant((char)character);
        if (_glyphs.TryGetValue(character, out var g)) return g;
        if (character <= ' ') return null;       // whitespace the font lacks is just a gap
        return _glyphs.TryGetValue('?', out var q) ? q : null;
    }

    /// <summary>How far the pen moves after a character (a space-sized gap for characters the font lacks).</summary>
    public int Advance(char c)
    {
        var g = GetGlyph(c);
        if (g != null) return g.XAdvance;
        return SpaceWidth;
    }

    public void SetKerning(int first, int second, int amount) => _kerning[Key(first, second)] = amount;

    /// <summary>Extra pen movement between two characters (usually negative: "AV" fits closer than "AA").</summary>
    public int GetKerning(int first, int second)
    {
        if (_kerning.Count == 0) return 0;
        if (UpperCase)
        {
            first = char.ToUpperInvariant((char)first);
            second = char.ToUpperInvariant((char)second);
        }
        return _kerning.TryGetValue(Key(first, second), out int k) ? k : 0;
    }

    static long Key(int first, int second) => ((long)first << 32) | (uint)second;

    /// <summary>Width of a single line of text at 1x, including kerning (no letter spacing).</summary>
    public float Measure(string text)
    {
        float w = 0;
        for (int i = 0; i < text.Length; i++)
        {
            w += Advance(text[i]);
            if (i + 1 < text.Length) w += GetKerning(text[i], text[i + 1]);
        }
        return w;
    }

    // ------------------------------------------------------------------ loading: atlas + json (the default font)

    /// <summary>
    /// Loads Assets/{baseName}.png and Assets/{baseName}.json: a grid of cells starting at firstChar, with an
    /// advance per character.
    /// </summary>
    public static FlxBitmapFont Load(string baseName)
    {
        var atlas = FlxAssets.GetTexture(FlxAssets.Get(baseName + ".png"));
        using var doc = JsonDocument.Parse(File.ReadAllText(FlxAssets.Get(baseName + ".json")));
        var meta = doc.RootElement;

        int cellW = meta.GetProperty("cellWidth").GetInt32();
        int cellH = meta.GetProperty("cellHeight").GetInt32();
        int columns = meta.GetProperty("columns").GetInt32();
        int first = meta.GetProperty("firstChar").GetInt32();
        var advances = meta.GetProperty("advances").EnumerateArray().Select(a => a.GetInt32()).ToArray();

        var font = new FlxBitmapFont(new[] { atlas }, meta.GetProperty("lineHeight").GetInt32(), 8) { Name = Path.GetFileName(baseName) };
        for (int i = 0; i < advances.Length; i++)
            font.AddGlyph(first + i, new Rectangle((i % columns) * cellW, (i / columns) * cellH, cellW, cellH), 0, 0, advances[i]);

        font.SpaceWidth = font.HasGlyph(' ') ? font.GetGlyph(' ')!.XAdvance : 3;
        return font;
    }

    // ------------------------------------------------------------------ loading: monospace grid

    /// <summary>
    /// A font from a grid of equally sized characters.
    /// </summary>
    /// <param name="letters">The characters in the order they appear in the picture (left to right, top to bottom), e.g. " !\"#$%&amp;..." .</param>
    /// <param name="spacing">Extra pixels between characters / lines when drawing (xSpacing widens the advance, ySpacing the line height).</param>
    /// <param name="gap">Pixels between the cells in the picture itself.</param>
    /// <param name="region">The part of the picture that holds the grid (default: all of it).</param>
    public static FlxBitmapFont FromMonospace(Texture2D texture, string letters, int charWidth, int charHeight,
        Point? spacing = null, Point? gap = null, Rectangle? region = null)
    {
        var sp = spacing ?? Point.Zero;
        var gp = gap ?? Point.Zero;
        var area = region ?? new Rectangle(0, 0, texture.Width, texture.Height);

        int cols = Math.Max(1, (area.Width + gp.X) / (charWidth + gp.X));

        var font = new FlxBitmapFont(new[] { texture }, charHeight + sp.Y, charHeight) { SpaceWidth = charWidth + sp.X };
        for (int i = 0; i < letters.Length; i++)
        {
            var src = new Rectangle(area.X + (i % cols) * (charWidth + gp.X), area.Y + (i / cols) * (charHeight + gp.Y), charWidth, charHeight);
            if (src.Right > area.Right || src.Bottom > area.Bottom) break;      // the picture has fewer cells than letters
            font.AddGlyph(letters[i], src, 0, 0, charWidth + sp.X);
        }

        font.UpperCase = !letters.Any(char.IsLower) && letters.Any(char.IsUpper);
        return font;
    }

    // ------------------------------------------------------------------ loading: XNA style sheet

    /// <summary>
    /// A font from an XNA style sheet: the characters sit in cells separated by full lines of one border color
    /// (the color of the top-left pixel). Pixels of the border color inside a cell become transparent.
    /// </summary>
    /// <param name="letters">The characters in the order of the cells (left to right, top to bottom).</param>
    /// <param name="spacing">Extra pixels added to each character's advance.</param>
    public static FlxBitmapFont FromXna(Texture2D texture, string letters, int spacing = 0)
    {
        int w = texture.Width, h = texture.Height;
        var px = new Color[w * h];
        texture.GetData(px);
        var border = px[0];

        // border color -> transparent, in a copy of the sheet so the original stays as it was
        var clean = new Color[px.Length];
        for (int i = 0; i < px.Length; i++) clean[i] = px[i] == border ? Color.Transparent : px[i];
        var sheet = new Texture2D(texture.GraphicsDevice, w, h);
        sheet.SetData(clean);

        bool RowIsBorder(int y) { for (int x = 0; x < w; x++) if (px[y * w + x] != border) return false; return true; }
        bool ColumnIsBorder(int x, int y0, int y1) { for (int y = y0; y < y1; y++) if (px[y * w + x] != border) return false; return true; }

        var font = new FlxBitmapFont(new[] { sheet }, 0) { SpaceWidth = 0 };
        int letter = 0, maxHeight = 0;

        int row = 0;
        while (row < h && letter < letters.Length)
        {
            while (row < h && RowIsBorder(row)) row++;
            if (row >= h) break;
            int top = row;
            while (row < h && !RowIsBorder(row)) row++;
            int bottom = row;                                  // this band of rows holds one line of cells

            int col = 0;
            while (col < w && letter < letters.Length)
            {
                while (col < w && ColumnIsBorder(col, top, bottom)) col++;
                if (col >= w) break;
                int left = col;
                while (col < w && !ColumnIsBorder(col, top, bottom)) col++;

                font.AddGlyph(letters[letter], new Rectangle(left, top, col - left, bottom - top), 0, 0, col - left + spacing);
                maxHeight = Math.Max(maxHeight, bottom - top);
                letter++;
            }
        }

        font.LineHeight = maxHeight;
        font.Size = maxHeight;
        font.SpaceWidth = font.HasGlyph(' ') ? font.GetGlyph(' ')!.XAdvance : Math.Max(1, maxHeight / 2);
        font.UpperCase = !letters.Any(char.IsLower) && letters.Any(char.IsUpper);
        return font;
    }

    // ------------------------------------------------------------------ loading: AngelCode BMFont

    /// <summary>
    /// Loads an AngelCode BMFont: the .fnt file (text or XML format) and the page images it names, which are looked
    /// up next to the .fnt file. Pass the full path (FlxAssets.Get("fonts/my.fnt")).
    /// </summary>
    public static FlxBitmapFont FromBMFont(string fntPath)
    {
        string content = File.ReadAllText(fntPath);
        string dir = Path.GetDirectoryName(fntPath) ?? "";

        var pageFiles = ReadPageFiles(content);
        var pages = pageFiles.Select(f => FlxAssets.GetTexture(Path.GetFullPath(Path.Combine(dir, f)))).ToArray();
        var font = FromBMFont(pages, content);
        if (font.Name.Length == 0) font.Name = Path.GetFileNameWithoutExtension(fntPath);   // the .fnt names its face; the file name is the fallback
        return font;
    }

    /// <summary>Parses .fnt text (text or XML format) for pictures you loaded yourself (one per page, in page order).</summary>
    public static FlxBitmapFont FromBMFont(Texture2D[] pages, string fntContent)
    {
        var data = fntContent.TrimStart().StartsWith('<') ? ParseXml(fntContent) : ParseText(fntContent);

        var font = new FlxBitmapFont(pages, data.LineHeight, data.Size) { Name = data.Face };
        foreach (var c in data.Chars)
        {
            if (c.Page < 0 || c.Page >= pages.Length) throw new InvalidDataException($"Character {c.Id} is on page {c.Page}, but only {pages.Length} page(s) were given.");
            font.AddGlyph(c.Id, new Rectangle(c.X, c.Y, c.Width, c.Height), c.XOffset, c.YOffset, c.XAdvance, c.Page);
        }
        foreach (var (first, second, amount) in data.Kerning) font.SetKerning(first, second, amount);

        font.SpaceWidth = font.HasGlyph(' ') ? font.GetGlyph(' ')!.XAdvance : Math.Max(1, data.Size / 2);
        return font;
    }

    sealed record CharData(int Id, int X, int Y, int Width, int Height, int XOffset, int YOffset, int XAdvance, int Page);

    sealed class FontData
    {
        public string Face = "";
        public int Size, LineHeight;
        public readonly List<CharData> Chars = new();
        public readonly List<(int, int, int)> Kerning = new();
    }

    static List<string> ReadPageFiles(string content)
    {
        var files = new SortedDictionary<int, string>();
        if (content.TrimStart().StartsWith('<'))
        {
            foreach (var p in XDocument.Parse(content).Descendants("page"))
                files[(int?)p.Attribute("id") ?? files.Count] = (string?)p.Attribute("file") ?? "";
        }
        else
        {
            foreach (var line in content.Split('\n'))
            {
                var t = Tokens(line);
                if (t.Count > 0 && t[0].Key == "page")
                {
                    var kv = ToDictionary(t);
                    files[Int(kv, "id")] = kv.GetValueOrDefault("file", "");
                }
            }
        }

        if (files.Count == 0) throw new InvalidDataException("The .fnt file names no page images.");
        return files.Values.ToList();
    }

    static FontData ParseText(string content)
    {
        var d = new FontData();
        foreach (var raw in content.Split('\n'))
        {
            var t = Tokens(raw);
            if (t.Count == 0) continue;
            var kv = ToDictionary(t);

            switch (t[0].Key)
            {
                case "info": d.Face = kv.GetValueOrDefault("face", ""); d.Size = Math.Abs(Int(kv, "size")); break;
                case "common": d.LineHeight = Int(kv, "lineHeight"); break;
                case "char":
                    d.Chars.Add(new CharData(Int(kv, "id"), Int(kv, "x"), Int(kv, "y"), Int(kv, "width"), Int(kv, "height"),
                        Int(kv, "xoffset"), Int(kv, "yoffset"), Int(kv, "xadvance"), Int(kv, "page")));
                    break;
                case "kerning": d.Kerning.Add((Int(kv, "first"), Int(kv, "second"), Int(kv, "amount"))); break;
            }
        }
        if (d.Chars.Count == 0) throw new InvalidDataException("The .fnt file has no characters.");
        if (d.Size == 0) d.Size = d.LineHeight;
        return d;
    }

    static FontData ParseXml(string content)
    {
        var doc = XDocument.Parse(content);
        var d = new FontData();

        var info = doc.Descendants("info").FirstOrDefault();
        d.Face = (string?)info?.Attribute("face") ?? "";
        d.Size = Math.Abs((int?)info?.Attribute("size") ?? 0);
        d.LineHeight = (int?)doc.Descendants("common").FirstOrDefault()?.Attribute("lineHeight") ?? 0;

        foreach (var c in doc.Descendants("char"))
            d.Chars.Add(new CharData((int)c.Attribute("id")!, (int?)c.Attribute("x") ?? 0, (int?)c.Attribute("y") ?? 0,
                (int?)c.Attribute("width") ?? 0, (int?)c.Attribute("height") ?? 0, (int?)c.Attribute("xoffset") ?? 0,
                (int?)c.Attribute("yoffset") ?? 0, (int?)c.Attribute("xadvance") ?? 0, (int?)c.Attribute("page") ?? 0));
        foreach (var k in doc.Descendants("kerning"))
            d.Kerning.Add(((int)k.Attribute("first")!, (int)k.Attribute("second")!, (int)k.Attribute("amount")!));

        if (d.Chars.Count == 0) throw new InvalidDataException("The .fnt file has no characters.");
        if (d.Size == 0) d.Size = d.LineHeight;
        return d;
    }

    /// <summary>Splits `char id=65 x=1 face="A B"` into key/value pairs (the first one is the line type, with a null value).</summary>
    static List<KeyValuePair<string, string?>> Tokens(string line)
    {
        var result = new List<KeyValuePair<string, string?>>();
        int i = 0;
        line = line.Trim();

        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            int start = i;
            while (i < line.Length && line[i] != '=' && !char.IsWhiteSpace(line[i])) i++;
            string key = line[start..i];
            if (key.Length == 0) break;

            if (i < line.Length && line[i] == '=')
            {
                i++;
                string value;
                if (i < line.Length && line[i] == '"')
                {
                    int end = line.IndexOf('"', i + 1);
                    if (end < 0) end = line.Length;
                    value = line[(i + 1)..end];
                    i = Math.Min(end + 1, line.Length);
                }
                else
                {
                    int vs = i;
                    while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                    value = line[vs..i];
                }
                result.Add(new(key, value));
            }
            else result.Add(new(key, null));
        }
        return result;
    }

    static Dictionary<string, string> ToDictionary(List<KeyValuePair<string, string?>> tokens)
    {
        var d = new Dictionary<string, string>();
        foreach (var t in tokens) if (t.Value != null) d[t.Key] = t.Value;
        return d;
    }

    static int Int(Dictionary<string, string> kv, string key) =>
        kv.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>One picture of a sprite: a rectangle of a texture, possibly trimmed of transparent borders.</summary>
public sealed class FlxFrame
{
    /// <summary>Name in the atlas (grid frames have none).</summary>
    public string Name = "";

    /// <summary>The rectangle of the texture that holds the pixels.</summary>
    public Rectangle Source;

    /// <summary>
    /// Where the (possibly trimmed) pixels sit inside the full, untrimmed frame. (0, 0) for frames that weren't trimmed.
    /// </summary>
    public Point Offset;

    /// <summary>The full frame size before trimming; this is the size the sprite treats the frame as.</summary>
    public Point SourceSize;

    /// <summary>Duration from the atlas (Aseprite), in seconds; 0 if it has none.</summary>
    public float Duration;

    public override string ToString() => $"{Name} {Source}";
}

/// <summary>An animation defined inside an atlas file (an Aseprite tag).</summary>
public sealed class FlxAtlasAnimation
{
    public string Name = "";
    public int From, To;

    /// <summary>"forward", "reverse", "pingpong" or "pingpong_reverse".</summary>
    public string Direction = "forward";
}

/// <summary>
/// The frames of one texture. A sprite shows one of them at a time. Build one from a grid (<see cref="FromTiles"/>) or an
/// atlas file (<see cref="FlxAtlasFrames"/>) and give it to <c>sprite.LoadFrames(frames)</c>.
/// </summary>
public sealed class FlxFramesCollection
{
    readonly List<FlxFrame> _frames = new();
    readonly Dictionary<string, FlxFrame> _byName = new();

    public Texture2D Texture { get; }

    /// <summary>Animations stored in the atlas file (Aseprite tags).</summary>
    public List<FlxAtlasAnimation> Animations { get; } = new();

    public IReadOnlyList<FlxFrame> Frames => _frames;
    public int Count => _frames.Count;

    /// <summary>The size of the biggest (untrimmed) frame: what a sprite using these frames measures.</summary>
    public Point MaxSize { get; private set; }

    public FlxFramesCollection(Texture2D texture) { Texture = texture; }

    public FlxFrame this[int index] => _frames[index];

    public FlxFrame Add(FlxFrame frame)
    {
        _frames.Add(frame);
        if (frame.Name.Length > 0) _byName[frame.Name] = frame;
        MaxSize = new Point(Math.Max(MaxSize.X, frame.SourceSize.X), Math.Max(MaxSize.Y, frame.SourceSize.Y));
        return frame;
    }

    public bool Has(string name) => _byName.ContainsKey(name);

    public FlxFrame? GetByName(string name) => _byName.GetValueOrDefault(name);

    public int IndexOf(string name)
    {
        var f = GetByName(name);
        return f == null ? -1 : _frames.IndexOf(f);
    }

    /// <summary>Indices of the frames whose names start with <paramref name="prefix"/>, ordered by the number after it ("run0001", "run0002"...).</summary>
    public List<int> IndicesByPrefix(string prefix, string postfix = "")
    {
        var found = new List<(int Index, int Number)>();
        for (int i = 0; i < _frames.Count; i++)
        {
            string n = _frames[i].Name;
            if (!n.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (postfix.Length > 0 && !n.EndsWith(postfix, StringComparison.Ordinal)) continue;

            string middle = n.Substring(prefix.Length, n.Length - prefix.Length - postfix.Length);
            found.Add((i, int.TryParse(middle, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : int.MaxValue));
        }
        return found.OrderBy(f => f.Number).ThenBy(f => f.Index).Select(f => f.Index).ToList();
    }

    /// <summary>Indices of the named frames, in the order given. Throws if one is missing.</summary>
    public List<int> IndicesByNames(IEnumerable<string> names)
    {
        var list = new List<int>();
        foreach (var n in names)
        {
            int i = IndexOf(n);
            if (i < 0) throw new KeyNotFoundException($"These frames have no frame called '{n}'.");
            list.Add(i);
        }
        return list;
    }

    /// <summary>
    /// A grid of equally sized frames. <paramref name="spacing"/> is the gap between frames, <paramref name="margin"/> the border
    /// around the whole grid, <paramref name="region"/> the part of the texture the grid is in.
    /// </summary>
    public static FlxFramesCollection FromTiles(Texture2D texture, int tileWidth, int tileHeight, int spacing = 0, int margin = 0, Rectangle? region = null)
    {
        if (tileWidth <= 0 || tileHeight <= 0) throw new ArgumentException("Frames need a size.");
        var area = region ?? new Rectangle(0, 0, texture.Width, texture.Height);

        int cols = Math.Max(0, (area.Width - 2 * margin + spacing) / (tileWidth + spacing));
        int rows = Math.Max(0, (area.Height - 2 * margin + spacing) / (tileHeight + spacing));

        var c = new FlxFramesCollection(texture);
        for (int r = 0; r < rows; r++)
            for (int col = 0; col < cols; col++)
                c.Add(new FlxFrame
                {
                    Source = new Rectangle(area.X + margin + col * (tileWidth + spacing), area.Y + margin + r * (tileHeight + spacing), tileWidth, tileHeight),
                    SourceSize = new Point(tileWidth, tileHeight)
                });
        if (c.Count == 0) throw new ArgumentException($"A {tileWidth}x{tileHeight} frame doesn't fit in the {area.Width}x{area.Height} area.");
        return c;
    }

    /// <summary>One frame covering the whole texture.</summary>
    public static FlxFramesCollection FromImage(Texture2D texture)
    {
        var c = new FlxFramesCollection(texture);
        c.Add(new FlxFrame { Source = new Rectangle(0, 0, texture.Width, texture.Height), SourceSize = new Point(texture.Width, texture.Height) });
        return c;
    }
}

/// <summary>
/// Loads atlas files, the way sprite packers and editors export them: <see cref="FromSparrow"/> (Sparrow / Starling XML),
/// <see cref="FromTexturePackerXml"/>, <see cref="FromTexturePackerJson"/> (hash and array layouts), <see cref="FromAseprite"/> (with
/// animation tags and frame durations) and <see cref="FromSpriteSheetPacker"/>. Trimmed frames are handled: the sprite still
/// measures the full frame and the picture is placed where it belongs.
/// <code>
/// var frames = FlxAtlasFrames.FromSparrow(FlxAssets.Get("atlas/shapes.png"), FlxAssets.Get("atlas/shapes.xml"));
/// sprite.LoadFrames(frames);
/// sprite.Animation.AddByPrefix("pulse", "orb", fps: 12);
/// </code>
/// Rotated atlas frames are not supported: export with rotation turned off.
/// </summary>
public static class FlxAtlasFrames
{
    // ------------------------------------------------------------------ Sparrow / Starling XML

    /// <summary>Sparrow / Starling XML: &lt;SubTexture name x y width height frameX frameY frameWidth frameHeight/&gt;.</summary>
    public static FlxFramesCollection FromSparrow(string imagePath, string xmlPath) =>
        FromSparrowXml(FlxAssets.GetTexture(imagePath), File.ReadAllText(xmlPath));

    public static FlxFramesCollection FromSparrowXml(Texture2D texture, string xml)
    {
        var frames = new FlxFramesCollection(texture);
        var doc = XDocument.Parse(xml);

        foreach (var e in doc.Descendants().Where(d => d.Name.LocalName == "SubTexture"))
        {
            if (Bool(e.Attribute("rotated"))) throw RotatedNotSupported((string?)e.Attribute("name"));

            int w = Int(e, "width"), h = Int(e, "height");
            int fx = Int(e, "frameX"), fy = Int(e, "frameY");
            int fw = e.Attribute("frameWidth") != null ? Int(e, "frameWidth") : w;
            int fh = e.Attribute("frameHeight") != null ? Int(e, "frameHeight") : h;

            frames.Add(new FlxFrame
            {
                Name = (string?)e.Attribute("name") ?? "",
                Source = new Rectangle(Int(e, "x"), Int(e, "y"), w, h),
                Offset = new Point(-fx, -fy),            // Sparrow stores the shift of the frame over the picture, which is the opposite
                SourceSize = new Point(fw, fh)
            });
        }
        if (frames.Count == 0) throw new InvalidDataException("The XML has no SubTexture elements.");
        return frames;
    }

    // ------------------------------------------------------------------ TexturePacker XML

    /// <summary>TexturePacker's generic XML: &lt;sprite n x y w h oX oY oW oH r/&gt;.</summary>
    public static FlxFramesCollection FromTexturePackerXml(string imagePath, string xmlPath) =>
        FromTexturePackerXmlContent(FlxAssets.GetTexture(imagePath), File.ReadAllText(xmlPath));

    public static FlxFramesCollection FromTexturePackerXmlContent(Texture2D texture, string xml)
    {
        var doc = XDocument.Parse(xml);

        // some exporters use the Sparrow element names
        if (doc.Descendants().Any(d => d.Name.LocalName == "SubTexture")) return FromSparrowXml(texture, xml);

        var frames = new FlxFramesCollection(texture);
        foreach (var e in doc.Descendants().Where(d => d.Name.LocalName == "sprite"))
        {
            string? rot = (string?)e.Attribute("r");
            if (rot == "y" || rot == "true") throw RotatedNotSupported((string?)e.Attribute("n"));

            int w = Int(e, "w"), h = Int(e, "h");
            frames.Add(new FlxFrame
            {
                Name = (string?)e.Attribute("n") ?? "",
                Source = new Rectangle(Int(e, "x"), Int(e, "y"), w, h),
                Offset = new Point(Int(e, "oX"), Int(e, "oY")),
                SourceSize = new Point(e.Attribute("oW") != null ? Int(e, "oW") : w, e.Attribute("oH") != null ? Int(e, "oH") : h)
            });
        }
        if (frames.Count == 0) throw new InvalidDataException("The XML has no sprite elements.");
        return frames;
    }

    // ------------------------------------------------------------------ TexturePacker JSON / Aseprite JSON

    /// <summary>TexturePacker JSON (the "hash" layout {"frames": {"name": {...}}} or the "array" layout).</summary>
    public static FlxFramesCollection FromTexturePackerJson(string imagePath, string jsonPath) =>
        FromJson(FlxAssets.GetTexture(imagePath), File.ReadAllText(jsonPath), withAnimations: false);

    /// <summary>
    /// Aseprite JSON export (hash or array layout). The tags become <see cref="FlxFramesCollection.Animations"/>, and frame durations
    /// (milliseconds in the file) are kept; <c>sprite.Animation.AddFromAtlas()</c> turns them into animations.
    /// </summary>
    public static FlxFramesCollection FromAseprite(string imagePath, string jsonPath) =>
        FromJson(FlxAssets.GetTexture(imagePath), File.ReadAllText(jsonPath), withAnimations: true);

    public static FlxFramesCollection FromJson(Texture2D texture, string json, bool withAnimations = true)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("frames", out var framesNode)) throw new InvalidDataException("The JSON has no \"frames\".");

        var frames = new FlxFramesCollection(texture);

        void AddOne(string name, JsonElement f)
        {
            if (f.TryGetProperty("rotated", out var r) && r.ValueKind == JsonValueKind.True) throw RotatedNotSupported(name);

            var rect = f.GetProperty("frame");
            var source = new Rectangle(rect.GetProperty("x").GetInt32(), rect.GetProperty("y").GetInt32(), rect.GetProperty("w").GetInt32(), rect.GetProperty("h").GetInt32());

            var offset = Point.Zero;
            var size = new Point(source.Width, source.Height);
            if (f.TryGetProperty("spriteSourceSize", out var sss)) offset = new Point(sss.GetProperty("x").GetInt32(), sss.GetProperty("y").GetInt32());
            if (f.TryGetProperty("sourceSize", out var ss)) size = new Point(ss.GetProperty("w").GetInt32(), ss.GetProperty("h").GetInt32());

            float duration = f.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetSingle() / 1000f : 0f;
            frames.Add(new FlxFrame { Name = name, Source = source, Offset = offset, SourceSize = size, Duration = duration });
        }

        if (framesNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in framesNode.EnumerateObject()) AddOne(p.Name, p.Value);        // hash layout (order of the file)
        }
        else
        {
            int i = 0;
            foreach (var f in framesNode.EnumerateArray())
                AddOne(f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : $"frame{i++}", f);
        }

        if (frames.Count == 0) throw new InvalidDataException("The JSON has no frames.");

        if (withAnimations && root.TryGetProperty("meta", out var meta) && meta.TryGetProperty("frameTags", out var tags))
            foreach (var t in tags.EnumerateArray())
                frames.Animations.Add(new FlxAtlasAnimation
                {
                    Name = t.GetProperty("name").GetString() ?? "",
                    From = t.GetProperty("from").GetInt32(),
                    To = t.GetProperty("to").GetInt32(),
                    Direction = t.TryGetProperty("direction", out var dir) ? dir.GetString() ?? "forward" : "forward"
                });
        return frames;
    }

    // ------------------------------------------------------------------ SpriteSheetPacker text

    /// <summary>SpriteSheetPacker's text format: one line per frame, "name = x y width height".</summary>
    public static FlxFramesCollection FromSpriteSheetPacker(string imagePath, string txtPath)
    {
        var frames = new FlxFramesCollection(FlxAssets.GetTexture(imagePath));
        foreach (var raw in File.ReadAllLines(txtPath))
        {
            var m = Regex.Match(raw.Trim(), @"^(?<name>.+?)\s*=\s*(?<x>-?\d+)\s+(?<y>-?\d+)\s+(?<w>\d+)\s+(?<h>\d+)$");
            if (!m.Success) continue;

            int w = int.Parse(m.Groups["w"].Value), h = int.Parse(m.Groups["h"].Value);
            frames.Add(new FlxFrame
            {
                Name = m.Groups["name"].Value,
                Source = new Rectangle(int.Parse(m.Groups["x"].Value), int.Parse(m.Groups["y"].Value), w, h),
                SourceSize = new Point(w, h)
            });
        }
        if (frames.Count == 0) throw new InvalidDataException("No frames found in the text file.");
        return frames;
    }

    // ------------------------------------------------------------------ helpers

    static int Int(XElement e, string attr) =>
        int.TryParse((string?)e.Attribute(attr), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v
        : (int)MathF.Round(float.TryParse((string?)e.Attribute(attr), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f);

    static bool Bool(XAttribute? a) => a != null && (a.Value == "true" || a.Value == "1" || a.Value == "y");

    static NotSupportedException RotatedNotSupported(string? name) =>
        new($"Frame '{name}' is rotated in the atlas. Rotated atlas frames aren't supported: export the atlas with rotation turned off.");
}

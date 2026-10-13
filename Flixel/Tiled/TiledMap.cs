using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Tiled;

// Reads Tiled's .tmx map format (https://doc.mapeditor.org/en/stable/reference/tmx-map-format/).
//   Supported:  orthogonal maps; tile layers (CSV, Base64 + none/gzip/zlib, plain XML tiles); tile flip flags;
//               object layers (rectangles, ellipses, points, polygons, polylines, text, tile objects);
//               image layers; group layers (flattened); tilesets from one image, image collections and
//               external .tsx files; tile animations; custom properties on everything; layer offsets/opacity.
//   Not supported (clear error): infinite maps (chunks), zstd compression.

/// <summary>Custom properties (name/value pairs). Values are kept as text with typed getters.</summary>
public sealed class TiledProperties : IEnumerable<KeyValuePair<string, string>>
{
    readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public int Count => _values.Count;
    public bool Contains(string name) => _values.ContainsKey(name);
    public string? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;
    public string Get(string name, string fallback) => Get(name) ?? fallback;

    public float GetFloat(string name, float fallback = 0f) =>
        float.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

    public int GetInt(string name, int fallback = 0) =>
        int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

    public bool GetBool(string name, bool fallback = false) =>
        Get(name) is { } s ? s.Equals("true", StringComparison.OrdinalIgnoreCase) : fallback;

    internal void Set(string name, string value) => _values[name] = value;

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public readonly record struct TiledAnimationFrame(int TileId, int DurationMs);

/// <summary>An image belonging to one tile of an "image collection" tileset.</summary>
public sealed class TiledImageTile
{
    public string Source = "";      // relative to the map's folder
    public int Width, Height;
}

/// <summary>Per-tile data inside a tileset (only tiles that have something special are listed).</summary>
public sealed class TiledTileInfo
{
    public int Id;                  // local id (gid - FirstGid)
    public string Type = "";
    public TiledProperties Properties = new();
    public List<TiledAnimationFrame> Animation = new();
    public TiledImageTile? Image;   // image collections only
}

public sealed class TiledTileSet
{
    public string Name = "";
    public int FirstGid;
    public int TileWidth, TileHeight, TileCount, Columns, Margin, Spacing;

    /// <summary>Tileset image, relative to the map's folder (empty for image collections).</summary>
    public string ImageSource = "";
    public int ImageWidth, ImageHeight;

    public TiledProperties Properties = new();
    public Dictionary<int, TiledTileInfo> Tiles = new();

    /// <summary>True for tilesets where every tile is its own image file.</summary>
    public bool IsImageCollection => string.IsNullOrEmpty(ImageSource);

    public bool Contains(int gid) => gid >= FirstGid && gid < FirstGid + TileCount;

    public TiledTileInfo? GetTile(int gid) => Tiles.TryGetValue(gid - FirstGid, out var t) ? t : null;

    /// <summary>The image of a tile in an image-collection tileset.</summary>
    public TiledImageTile? GetImageSourceByGid(int gid) => GetTile(gid)?.Image;
}

public enum TiledLayerType { Tile, Object, Image }

public abstract class TiledLayer
{
    public int Id;
    public string Name = "";
    public float Opacity = 1f;
    public bool Visible = true;
    public float OffsetX, OffsetY;
    public TiledProperties Properties = new();
    public abstract TiledLayerType Type { get; }
}

public sealed class TiledTileLayer : TiledLayer
{
    public override TiledLayerType Type => TiledLayerType.Tile;
    public int Width, Height;

    /// <summary>Global tile ids, row by row, 0 = empty, with the flip bits already removed.</summary>
    public int[] TileArray = Array.Empty<int>();

    /// <summary>Per-tile flip bits: 1 = horizontal, 2 = vertical, 4 = diagonal (swap x/y).</summary>
    public byte[] Flags = Array.Empty<byte>();

    public bool HasFlips => Flags.Any(f => f != 0);
    public int GetTile(int column, int row) => TileArray[row * Width + column];
}

public enum TiledObjectShape { Rectangle, Ellipse, Point, Polygon, Polyline, Text }

public sealed class TiledObject
{
    public int Id;
    public string Name = "";

    /// <summary>The object's type (called "class" in newer versions of Tiled; both are read).</summary>
    public string Type = "";

    /// <summary>Position in pixels. Tile objects (see HasGid) are positioned by their BOTTOM-left corner.</summary>
    public float X, Y, Width, Height;

    /// <summary>Clockwise degrees around (X, Y).</summary>
    public float Rotation;

    /// <summary>Global tile id for tile/image objects, 0 if the object is not a tile object.</summary>
    public int Gid;
    public bool FlippedHorizontally, FlippedVertically, FlippedDiagonally;

    public bool Visible = true;
    public TiledObjectShape Shape = TiledObjectShape.Rectangle;
    public List<Vector2> Points = new();     // polygon / polyline, relative to (X, Y)
    public string Text = "";
    public TiledProperties Properties = new();

    public bool HasGid => Gid > 0;
}

public sealed class TiledObjectLayer : TiledLayer
{
    public override TiledLayerType Type => TiledLayerType.Object;
    public string DrawOrder = "topdown";
    public List<TiledObject> Objects = new();
}

public sealed class TiledImageLayer : TiledLayer
{
    public override TiledLayerType Type => TiledLayerType.Image;

    /// <summary>Image file, relative to the map's folder.</summary>
    public string ImageSource = "";
    public int ImageWidth, ImageHeight;
    public bool RepeatX, RepeatY;
}

public sealed class TiledMap
{
    const uint FlipH = 0x80000000, FlipV = 0x40000000, FlipD = 0x20000000, GidMask = 0x0FFFFFFF;

    public string FilePath = "";
    public string Directory = "";
    public string Orientation = "orthogonal";
    public string RenderOrder = "right-down";
    public int Width, Height, TileWidth, TileHeight;
    public Color? BackgroundColor;
    public TiledProperties Properties = new();
    public List<TiledTileSet> Tilesets = new();
    public List<TiledLayer> Layers = new();

    public int FullWidth => Width * TileWidth;
    public int FullHeight => Height * TileHeight;

    // ================================================================ lookups

    public TiledTileSet? GetTileSet(string name) => Tilesets.FirstOrDefault(t => t.Name == name);

    /// <summary>The tileset a global tile id belongs to.</summary>
    public TiledTileSet? GetGidOwner(int gid)
    {
        TiledTileSet? owner = null;
        foreach (var t in Tilesets)
            if (t.FirstGid <= gid && (owner == null || t.FirstGid > owner.FirstGid)) owner = t;
        return owner != null && owner.Contains(gid) ? owner : null;
    }

    public TiledLayer? GetLayer(string name) => Layers.FirstOrDefault(l => l.Name == name);
    public IEnumerable<T> LayersOfType<T>() where T : TiledLayer => Layers.OfType<T>();

    /// <summary>Turns a path stored in the map (relative to the map's folder) into a full path.</summary>
    public string ResolvePath(string relativePath) => Path.GetFullPath(Path.Combine(Directory, relativePath));

    // ================================================================ loading

    public static TiledMap Load(string path)
    {
        string full = Path.GetFullPath(path);
        XDocument doc;
        try { doc = XDocument.Load(full); }
        catch (FileNotFoundException) { throw new FileNotFoundException($"Tiled map not found: {full}", full); }
        return Parse(doc.Root, Path.GetDirectoryName(full) ?? "", full);
    }

    /// <summary>Parse TMX text. baseDirectory is used to find images and external tilesets.</summary>
    public static TiledMap ParseXml(string xml, string baseDirectory = "") =>
        Parse(XDocument.Parse(xml).Root, baseDirectory, "");

    static TiledMap Parse(XElement? root, string directory, string file)
    {
        if (root == null || root.Name.LocalName != "map")
            throw new InvalidDataException("Not a Tiled map: the root element must be <map> (is this a .tmx file?).");
        if (Str(root, "infinite") == "1")
            throw new NotSupportedException("Infinite Tiled maps are not supported. In Tiled use Map > Map Properties and turn off 'Infinite', or resize the map to a fixed size.");

        var map = new TiledMap
        {
            Directory = directory,
            FilePath = file,
            Orientation = Str(root, "orientation") ?? "orthogonal",
            RenderOrder = Str(root, "renderorder") ?? "right-down",
            Width = Int(root, "width"),
            Height = Int(root, "height"),
            TileWidth = Int(root, "tilewidth"),
            TileHeight = Int(root, "tileheight"),
            BackgroundColor = ParseColor(Str(root, "backgroundcolor")),
            Properties = ReadProperties(root.Element("properties"))
        };

        ReadChildren(root, map, 0, 0, true);
        map.Tilesets.Sort((a, b) => a.FirstGid.CompareTo(b.FirstGid));
        return map;
    }

    static void ReadChildren(XElement parent, TiledMap map, float offsetX, float offsetY, bool visible)
    {
        foreach (var el in parent.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "tileset":
                    map.Tilesets.Add(ReadTileset(el, map));
                    break;
                case "layer":
                    map.Layers.Add(ReadTileLayer(el, offsetX, offsetY, visible));
                    break;
                case "objectgroup":
                    map.Layers.Add(ReadObjectLayer(el, offsetX, offsetY, visible));
                    break;
                case "imagelayer":
                    map.Layers.Add(ReadImageLayer(el, offsetX, offsetY, visible));
                    break;
                case "group":
                    // group layers are flattened: children inherit the group's offset and visibility
                    ReadChildren(el, map,
                        offsetX + Float(el, "offsetx"), offsetY + Float(el, "offsety"),
                        visible && Int(el, "visible", 1) != 0);
                    break;
            }
        }
    }

    // ================================================================ tilesets

    static TiledTileSet ReadTileset(XElement el, TiledMap map)
    {
        int firstGid = Int(el, "firstgid", 1);
        string? source = Str(el, "source");

        if (source == null) return ReadTilesetBody(el, firstGid, map.Directory, map.Directory);

        // external tileset (.tsx): its image paths are relative to the .tsx file
        string tsxPath = Path.GetFullPath(Path.Combine(map.Directory, source));
        XElement? tsx;
        try { tsx = XDocument.Load(tsxPath).Root; }
        catch (FileNotFoundException) { throw new FileNotFoundException($"External tileset not found: {tsxPath}", tsxPath); }
        if (tsx == null || tsx.Name.LocalName != "tileset") throw new InvalidDataException($"{tsxPath} is not a Tiled tileset (.tsx).");
        return ReadTilesetBody(tsx, firstGid, Path.GetDirectoryName(tsxPath) ?? "", map.Directory);
    }

    static TiledTileSet ReadTilesetBody(XElement el, int firstGid, string tilesetDir, string mapDir)
    {
        var ts = new TiledTileSet
        {
            Name = Str(el, "name") ?? "",
            FirstGid = firstGid,
            TileWidth = Int(el, "tilewidth"),
            TileHeight = Int(el, "tileheight"),
            Margin = Int(el, "margin"),
            Spacing = Int(el, "spacing"),
            Properties = ReadProperties(el.Element("properties"))
        };

        string MapRelative(string src) =>
            tilesetDir == mapDir ? src : Path.GetRelativePath(mapDir, Path.GetFullPath(Path.Combine(tilesetDir, src)));

        if (el.Element("image") is { } image)
        {
            ts.ImageSource = MapRelative(Str(image, "source") ?? "");
            ts.ImageWidth = Int(image, "width");
            ts.ImageHeight = Int(image, "height");
        }

        foreach (var t in el.Elements("tile"))
        {
            var info = new TiledTileInfo
            {
                Id = Int(t, "id"),
                Type = Str(t, "type") ?? Str(t, "class") ?? "",
                Properties = ReadProperties(t.Element("properties"))
            };

            if (t.Element("animation") is { } anim)
                foreach (var f in anim.Elements("frame"))
                    info.Animation.Add(new TiledAnimationFrame(Int(f, "tileid"), Int(f, "duration", 100)));

            if (t.Element("image") is { } timg)
                info.Image = new TiledImageTile { Source = MapRelative(Str(timg, "source") ?? ""), Width = Int(timg, "width"), Height = Int(timg, "height") };

            ts.Tiles[info.Id] = info;
        }

        // derive what older files leave out
        if (!ts.IsImageCollection && ts.TileWidth > 0 && ts.TileHeight > 0 && ts.ImageWidth > 0)
        {
            int cols = (ts.ImageWidth - 2 * ts.Margin + ts.Spacing) / (ts.TileWidth + ts.Spacing);
            int rows = (ts.ImageHeight - 2 * ts.Margin + ts.Spacing) / (ts.TileHeight + ts.Spacing);
            ts.Columns = cols;
            ts.TileCount = Int(el, "tilecount", cols * rows);
        }
        else
        {
            ts.Columns = 0;
            ts.TileCount = Int(el, "tilecount", ts.Tiles.Count == 0 ? 0 : ts.Tiles.Keys.Max() + 1);
        }
        return ts;
    }

    // ================================================================ layers

    static void ReadCommon(TiledLayer l, XElement el, float offsetX, float offsetY, bool visible)
    {
        l.Id = Int(el, "id");
        l.Name = Str(el, "name") ?? "";
        l.Opacity = Float(el, "opacity", 1f);
        l.Visible = visible && Int(el, "visible", 1) != 0;
        l.OffsetX = Float(el, "offsetx") + offsetX;
        l.OffsetY = Float(el, "offsety") + offsetY;
        l.Properties = ReadProperties(el.Element("properties"));
    }

    static TiledTileLayer ReadTileLayer(XElement el, float offsetX, float offsetY, bool visible)
    {
        var layer = new TiledTileLayer { Width = Int(el, "width"), Height = Int(el, "height") };
        ReadCommon(layer, el, offsetX, offsetY, visible);

        var data = el.Element("data") ?? throw new InvalidDataException($"Tile layer '{layer.Name}' has no <data>.");
        uint[] raw = DecodeData(data, layer.Width * layer.Height, layer.Name);

        layer.TileArray = new int[raw.Length];
        layer.Flags = new byte[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            uint g = raw[i];
            layer.TileArray[i] = (int)(g & GidMask);
            layer.Flags[i] = (byte)(((g & FlipH) != 0 ? 1 : 0) | ((g & FlipV) != 0 ? 2 : 0) | ((g & FlipD) != 0 ? 4 : 0));
        }
        return layer;
    }

    static uint[] DecodeData(XElement data, int expected, string layerName)
    {
        string? encoding = Str(data, "encoding");
        string? compression = Str(data, "compression");
        uint[] result;

        if (encoding == "csv")
        {
            var parts = data.Value.Split(new[] { ',', '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            result = new uint[parts.Length];
            for (int i = 0; i < parts.Length; i++) result[i] = uint.Parse(parts[i], CultureInfo.InvariantCulture);
        }
        else if (encoding == "base64")
        {
            byte[] bytes = Convert.FromBase64String(string.Concat(data.Value.Where(c => !char.IsWhiteSpace(c))));
            bytes = Decompress(bytes, compression, layerName);
            if (bytes.Length % 4 != 0) throw new InvalidDataException($"Layer '{layerName}': tile data is {bytes.Length} bytes, not a multiple of 4.");
            result = new uint[bytes.Length / 4];
            for (int i = 0; i < result.Length; i++) result[i] = BitConverter.ToUInt32(bytes, i * 4);   // little endian
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Big-endian CPUs are not supported.");
        }
        else if (encoding == null)
        {
            result = data.Elements("tile").Select(t => uint.Parse(Str(t, "gid") ?? "0", CultureInfo.InvariantCulture)).ToArray();
        }
        else throw new NotSupportedException($"Layer '{layerName}': unknown tile data encoding \"{encoding}\".");

        if (result.Length != expected)
            throw new InvalidDataException($"Layer '{layerName}': expected {expected} tiles but found {result.Length}.");
        return result;
    }

    static byte[] Decompress(byte[] bytes, string? compression, string layerName)
    {
        if (string.IsNullOrEmpty(compression)) return bytes;

        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        switch (compression)
        {
            case "gzip":
                using (var gz = new GZipStream(input, CompressionMode.Decompress)) gz.CopyTo(output);
                break;
            case "zlib":
                using (var z = new ZLibStream(input, CompressionMode.Decompress)) z.CopyTo(output);
                break;
            default:
                throw new NotSupportedException(
                    $"Layer '{layerName}' uses \"{compression}\" compression, which is not supported. " +
                    "In Tiled: Map Properties > Tile Layer Format, choose Base64 (zlib), Base64 (gzip), Base64 (uncompressed) or CSV.");
        }
        return output.ToArray();
    }

    static TiledObjectLayer ReadObjectLayer(XElement el, float offsetX, float offsetY, bool visible)
    {
        var layer = new TiledObjectLayer { DrawOrder = Str(el, "draworder") ?? "topdown" };
        ReadCommon(layer, el, offsetX, offsetY, visible);

        foreach (var o in el.Elements("object"))
        {
            var obj = new TiledObject
            {
                Id = Int(o, "id"),
                Name = Str(o, "name") ?? "",
                Type = Str(o, "type") ?? Str(o, "class") ?? "",
                X = Float(o, "x"),
                Y = Float(o, "y"),
                Width = Float(o, "width"),
                Height = Float(o, "height"),
                Rotation = Float(o, "rotation"),
                Visible = Int(o, "visible", 1) != 0,
                Properties = ReadProperties(o.Element("properties"))
            };

            if (Str(o, "gid") is { } gidText)
            {
                uint raw = uint.Parse(gidText, CultureInfo.InvariantCulture);
                obj.Gid = (int)(raw & GidMask);
                obj.FlippedHorizontally = (raw & FlipH) != 0;
                obj.FlippedVertically = (raw & FlipV) != 0;
                obj.FlippedDiagonally = (raw & FlipD) != 0;
            }

            if (o.Element("ellipse") != null) obj.Shape = TiledObjectShape.Ellipse;
            else if (o.Element("point") != null) obj.Shape = TiledObjectShape.Point;
            else if (o.Element("polygon") is { } poly) { obj.Shape = TiledObjectShape.Polygon; obj.Points = ReadPoints(poly); }
            else if (o.Element("polyline") is { } line) { obj.Shape = TiledObjectShape.Polyline; obj.Points = ReadPoints(line); }
            else if (o.Element("text") is { } text) { obj.Shape = TiledObjectShape.Text; obj.Text = text.Value; }

            layer.Objects.Add(obj);
        }
        return layer;
    }

    static List<Vector2> ReadPoints(XElement el)
    {
        var points = new List<Vector2>();
        foreach (var pair in (Str(el, "points") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = pair.Split(',');
            if (xy.Length == 2)
                points.Add(new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture)));
        }
        return points;
    }

    static TiledImageLayer ReadImageLayer(XElement el, float offsetX, float offsetY, bool visible)
    {
        var layer = new TiledImageLayer { RepeatX = Int(el, "repeatx") == 1, RepeatY = Int(el, "repeaty") == 1 };
        ReadCommon(layer, el, offsetX, offsetY, visible);

        // older Tiled versions write x/y instead of offsetx/offsety
        if (Str(el, "offsetx") == null) layer.OffsetX = Float(el, "x") + offsetX;
        if (Str(el, "offsety") == null) layer.OffsetY = Float(el, "y") + offsetY;

        if (el.Element("image") is { } image)
        {
            layer.ImageSource = Str(image, "source") ?? "";
            layer.ImageWidth = Int(image, "width");
            layer.ImageHeight = Int(image, "height");
        }
        return layer;
    }

    // ================================================================ small helpers

    static TiledProperties ReadProperties(XElement? el)
    {
        var props = new TiledProperties();
        if (el == null) return props;
        foreach (var p in el.Elements("property"))
        {
            string? name = Str(p, "name");
            if (name != null) props.Set(name, Str(p, "value") ?? p.Value);   // long text values live in the element body
        }
        return props;
    }

    static string? Str(XElement el, string attribute) => (string?)el.Attribute(attribute);

    static int Int(XElement el, string attribute, int fallback = 0) =>
        int.TryParse(Str(el, attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    static float Float(XElement el, string attribute, float fallback = 0f) =>
        float.TryParse(Str(el, attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    static Color? ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string h = text.TrimStart('#');
        if (h.Length == 6) h = "FF" + h;
        if (h.Length != 8 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)) return null;
        return new Color((int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF), (int)((argb >> 24) & 0xFF));
    }
}

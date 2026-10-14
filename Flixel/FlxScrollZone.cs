using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Scrolls parts of a sprite's picture in any direction, endlessly: parallax layers inside one picture, conveyor belts, moving water.
/// Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxScrollZone</c> (see NOTICE-flixel-power-tools.txt).
/// <code>
/// var scroller = new FlxScrollZone(sky);                                      // sky is an ordinary sprite
/// scroller.AddZone(new Rectangle(0, 0, 320, 40), -0.5f, 0);                   // the clouds drift left at half a pixel a frame
/// scroller.AddZone(new Rectangle(0, 40, 320, 40), -2, 0);                     // the hills in front go faster
/// Add(sky);
/// Add(scroller);                                                              // it scrolls while it is in the state
/// </code>
/// A zone takes the pixels of its region as they are when the zone is added and repeats them over the region, shifting by the
/// distance each frame. The sprite gets its own copy of the picture, so shared textures are not changed. Differences from the original:
/// it is an object you add to a state (not a static plugin), and the draw matrix of a zone is left out.
/// </summary>
public class FlxScrollZone : FlxBasic
{
    sealed class Zone
    {
        public Rectangle Region;
        public FxBitmap Texture = null!;
        public float DistanceX, DistanceY, OffsetX, OffsetY;
        public bool ClearRegion;
    }

    readonly List<Zone> _zones = new();
    readonly FxBitmap _canvas;
    readonly Texture2D _texture;

    /// <summary>The sprite whose picture scrolls.</summary>
    public FlxSprite Source { get; }

    /// <summary>False pauses all scrolling.</summary>
    public bool Scrolling = true;

    /// <summary>Only scroll while the sprite is on screen (saves time; default true).</summary>
    public bool OnlyScrollOnScreen;

    public int ZoneCount => _zones.Count;

    public FlxScrollZone(FlxSprite source, bool onlyScrollOnScreen = true)
    {
        Source = source;
        OnlyScrollOnScreen = onlyScrollOnScreen;
        _canvas = FxBitmap.FromSprite(source);
        _texture = new Texture2D(FlxG.GraphicsDevice, _canvas.Width, _canvas.Height);
        _texture.SetData(_canvas.Data);
        source.LoadGraphic(_texture);
    }

    /// <summary>
    /// Adds a scrolling region. A negative distance scrolls left or up, a positive one right or down; fractions are fine (0.5 is half
    /// speed). With <paramref name="clearRegion"/> the region is wiped before each draw, for pictures with see-through parts. Returns the zone's index.
    /// </summary>
    public int AddZone(Rectangle region, float distanceX, float distanceY, bool clearRegion = false)
    {
        region = Rectangle.Intersect(region, _canvas.Bounds);
        var texture = new FxBitmap(region.Width, region.Height);
        texture.CopyPixels(_canvas, region, Point.Zero, 1f, replace: true);
        _zones.Add(new Zone { Region = region, Texture = texture, DistanceX = distanceX, DistanceY = distanceY, ClearRegion = clearRegion });
        return _zones.Count - 1;
    }

    /// <summary>Changes the speed of a zone (pixels per frame).</summary>
    public void SetDistance(int zone, float distanceX, float distanceY)
    {
        _zones[zone].DistanceX = distanceX;
        _zones[zone].DistanceY = distanceY;
    }

    public void SetDistanceX(int zone, float distanceX) => _zones[zone].DistanceX = distanceX;
    public void SetDistanceY(int zone, float distanceY) => _zones[zone].DistanceY = distanceY;

    public void StartScrolling() => Scrolling = true;
    public void StopScrolling() => Scrolling = false;

    public override void Update(float elapsed)
    {
        if (!Scrolling || !Source.Exists) return;
        if (OnlyScrollOnScreen && !Source.IsOnScreen()) return;

        foreach (var zone in _zones) Scroll(zone);
        _texture.SetData(_canvas.Data);
    }

    void Scroll(Zone zone)
    {
        zone.OffsetX += zone.DistanceX;
        zone.OffsetY += zone.DistanceY;

        var region = zone.Region;
        var tex = zone.Texture;
        if (tex.Width == 0 || tex.Height == 0) return;
        if (zone.ClearRegion) _canvas.FillRect(region, Color.Transparent);

        // each pixel of the region shows the repeating picture, moved by the offset
        int ox = (int)MathF.Floor(zone.OffsetX), oy = (int)MathF.Floor(zone.OffsetY);
        var layer = new FxBitmap(region.Width, region.Height);
        for (int y = 0; y < region.Height; y++)
        {
            int sy = FlxMath.Wrap(y - oy, 0, tex.Height - 1);
            for (int x = 0; x < region.Width; x++)
                layer.Data[y * region.Width + x] = tex.Data[sy * tex.Width + FlxMath.Wrap(x - ox, 0, tex.Width - 1)];
        }
        _canvas.BlendOver(layer, layer.Bounds, new Point(region.X, region.Y));
    }

    public override void Destroy()
    {
        _zones.Clear();
        base.Destroy();
    }
}

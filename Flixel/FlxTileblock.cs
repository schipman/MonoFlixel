using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A solid rectangle filled with randomly chosen tiles: a quick platform, wall or floor without a tilemap. Port of Flixel's
/// FlxTileblock. It does not move and does not give way when things collide with it (Immovable).
/// <code>
/// var platform = new FlxTileblock(40, 200, 96, 16).LoadTiles(FlxAssets.Get("tiles.png"), 16, 16);
/// FlxG.Collide(player, platform);
/// </code>
/// </summary>
public class FlxTileblock : FlxSprite
{
    public FlxTileblock(float x, float y, int width, int height) : base(x, y)
    {
        Width = width;
        Height = height;
        Immovable = true;
        Moves = false;
    }

    /// <summary>
    /// Fills the block with tiles from a sheet, each cell getting a random frame.
    /// <paramref name="empties"/> adds that many "blank" choices to the draw: with 8 frames and empties 2, a cell is blank 20% of the time.
    /// </summary>
    public FlxTileblock LoadTiles(string tilesPath, int tileWidth = 0, int tileHeight = 0, int empties = 0) =>
        LoadTiles(FlxAssets.GetTexture(tilesPath), tileWidth, tileHeight, empties);

    public FlxTileblock LoadTiles(Texture2D tiles, int tileWidth = 0, int tileHeight = 0, int empties = 0)
    {
        if (tileWidth <= 0) tileWidth = tiles.Height;
        if (tileHeight <= 0) tileHeight = tileWidth;

        var sheet = FlxFramesCollection.FromTiles(tiles, Math.Min(tileWidth, tiles.Width), Math.Min(tileHeight, tiles.Height));
        var source = new Color[tiles.Width * tiles.Height];
        tiles.GetData(source);

        int w = (int)Width, h = (int)Height;
        var baked = new Color[w * h];

        for (int cy = 0; cy < h; cy += tileHeight)
            for (int cx = 0; cx < w; cx += tileWidth)
            {
                // blank with probability empties / (frames + empties)
                if (FlxG.Random.Float() * (sheet.Count + empties) <= empties) continue;

                var src = sheet[FlxG.Random.Int(0, sheet.Count - 1)].Source;
                int copyW = Math.Min(tileWidth, w - cx), copyH = Math.Min(tileHeight, h - cy);      // tiles at the edge are cut off
                for (int y = 0; y < copyH; y++)
                    for (int x = 0; x < copyW; x++)
                        baked[(cy + y) * w + cx + x] = source[(src.Y + y) * tiles.Width + src.X + x];
            }

        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        tex.SetData(baked);
        LoadGraphic(tex);
        return this;
    }
}

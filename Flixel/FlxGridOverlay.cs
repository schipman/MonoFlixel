using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Checkerboard and stripe pictures, made by code: a background that shows how big things are, or a grid laid over a sprite.
/// Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxGridOverlay</c> (see NOTICE-flixel-power-tools.txt).
/// <code>
/// Add(FlxGridOverlay.Create(8, 8));                                           // a screen-sized 8x8 checkerboard
/// FlxGridOverlay.Overlay(map, 16, 16, map.FrameWidth, map.FrameHeight);       // a see-through grid on top of a picture
/// </code>
/// The unfinished legend ("addLegend") of the original is left out.
/// </summary>
public static class FlxGridOverlay
{
    public static readonly Color DefaultColor1 = new(0xe7, 0xe6, 0xe6), DefaultColor2 = new(0xd9, 0xd5, 0xd5);

    /// <summary>
    /// A sprite filled with a checkerboard. Each cell is cellWidth x cellHeight and the two colors alternate.
    /// <paramref name="alternate"/> false makes stripes instead (every row the same). A width or height of -1 is the size of the game.
    /// Null when the sprite would be smaller than one cell.
    /// </summary>
    public static FlxSprite? Create(int cellWidth, int cellHeight, int width = -1, int height = -1, bool alternate = true, Color? color1 = null, Color? color2 = null)
    {
        if (width == -1) width = FlxG.Width;
        if (height == -1) height = FlxG.Height;
        if (width < cellWidth || height < cellHeight) return null;

        var texture = new Texture2D(FlxG.GraphicsDevice, width, height);
        texture.SetData(CreateGrid(cellWidth, cellHeight, width, height, alternate, color1 ?? DefaultColor1, color2 ?? DefaultColor2));
        return new FlxSprite().LoadGraphic(texture);
    }

    /// <summary>
    /// Lays a (by default see-through) checkerboard over the picture of a sprite and returns the sprite. This changes the sprite's
    /// whole picture, so do not use it on animated sprites. Null when the grid would be smaller than one cell.
    /// </summary>
    public static FlxSprite? Overlay(FlxSprite source, int cellWidth, int cellHeight, int width = -1, int height = -1, bool alternate = true, Color? color1 = null, Color? color2 = null)
    {
        if (width == -1) width = FlxG.Width;
        if (height == -1) height = FlxG.Height;
        if (width < cellWidth || height < cellHeight || source.Pixels == null) return null;

        var canvas = FxBitmap.FromTexture(source.Pixels);
        var grid = new FxBitmap(width, height);
        Array.Copy(CreateGrid(cellWidth, cellHeight, width, height, alternate, color1 ?? new Color(0xe7, 0xe6, 0xe6, 0x88), color2 ?? new Color(0xd9, 0xd5, 0xd5, 0x88)), grid.Data, width * height);
        canvas.BlendOver(grid, new Rectangle(0, 0, Math.Min(width, canvas.Width), Math.Min(height, canvas.Height)), Point.Zero);

        var texture = new Texture2D(FlxG.GraphicsDevice, canvas.Width, canvas.Height);
        texture.SetData(canvas.Data);
        return source.LoadGraphic(texture);
    }

    /// <summary>The pixels of a grid: a cell is color1 when (column + row) is even, otherwise color2 (stripes: only the column counts).</summary>
    public static Color[] CreateGrid(int cellWidth, int cellHeight, int width, int height, bool alternate, Color color1, Color color2)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            int row = y / cellHeight;
            for (int x = 0; x < width; x++)
            {
                int column = x / cellWidth;
                bool first = alternate ? (column + row) % 2 == 0 : column % 2 == 0;
                pixels[y * width + x] = first ? color1 : color2;
            }
        }
        return pixels;
    }
}

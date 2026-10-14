using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Small display helpers: space sprites out, wrap them around the screen, cut a picture out with a mask. Port of Richard Davey's
/// (Photon Storm) Flixel Power Tools <c>FlxDisplay</c> (see NOTICE-flixel-power-tools.txt). <c>screenCenter</c> is left out
/// (<see cref="FlxSprite.ScreenCenter"/> does it), and so are <c>pad</c>, <c>flip</c> and <c>rotateClockwise</c>, which were empty in the original.
/// </summary>
public static class FlxDisplay
{
    /// <summary>
    /// Puts sprites in a row (or column, or diagonal) with equal spacing: sprite i goes to start + i * spacing. With
    /// <paramref name="spaceFromBounds"/> the spacing is the gap between sprites instead, so wide sprites do not overlap. (The original
    /// forgot to add up the sizes; this does.)
    /// </summary>
    public static void Space(IList<FlxSprite> sprites, float startX, float startY, float horizontalSpacing = 0, float verticalSpacing = 0, bool spaceFromBounds = false)
    {
        float x = startX, y = startY;
        for (int i = 0; i < sprites.Count; i++)
        {
            var s = sprites[i];
            s.X = x;
            s.Y = y;
            x += (spaceFromBounds ? s.Width : 0) + horizontalSpacing;
            y += (spaceFromBounds ? s.Height : 0) + verticalSpacing;
        }
    }

    /// <summary>Keeps a sprite inside the screen by letting it come back in on the opposite side (like Asteroids).</summary>
    public static void ScreenWrap(FlxSprite source)
    {
        if (source.X < 0) source.X = FlxG.Width;
        else if (source.X > FlxG.Width) source.X = 0;

        if (source.Y < 0) source.Y = FlxG.Height;
        else if (source.Y > FlxG.Height) source.Y = 0;
    }

    /// <summary>
    /// Puts a picture into <paramref name="output"/> with only the shape of a mask showing: the colors come from the source, the alpha
    /// from the mask. Both pictures should be the same size. A big black circle with a soft edge in the mask gives a round picture
    /// with a soft edge.
    /// </summary>
    public static FlxSprite AlphaMask(Texture2D source, Texture2D mask, FlxSprite output)
    {
        int w = Math.Min(source.Width, mask.Width), h = Math.Min(source.Height, mask.Height);
        var s = FxPixels.Get(source);
        var m = FxPixels.Get(mask);
        var result = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = s[y * source.Width + x];
                result[y * w + x] = new Color(c.R, c.G, c.B, m[y * mask.Width + x].A);
            }

        var texture = new Texture2D(FlxG.GraphicsDevice, w, h);
        texture.SetData(result);
        return output.LoadGraphic(texture);
    }

    /// <summary>The same with the pictures of two sprites (their whole texture; do not use animated sprites).</summary>
    public static FlxSprite AlphaMask(FlxSprite source, FlxSprite mask, FlxSprite output) =>
        AlphaMask(source.Pixels ?? throw new ArgumentException("The source sprite has no picture."), mask.Pixels ?? throw new ArgumentException("The mask sprite has no picture."), output);

    /// <summary>The same with picture files (paths below Assets).</summary>
    public static FlxSprite AlphaMask(string sourcePath, string maskPath, FlxSprite output) =>
        AlphaMask(FlxAssets.GetTexture(sourcePath), FlxAssets.GetTexture(maskPath), output);
}

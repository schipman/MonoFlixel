using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>Where <see cref="FlxCollision.CreateCameraWall"/> puts the walls.</summary>
public enum FlxCameraWall { Outside, Inside }

/// <summary>
/// Pixel perfect collision tests and camera walls. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxCollision</c>
/// (see NOTICE-flixel-power-tools.txt). The pixel tests look at what is really drawn, so they work with rotated, scaled, mirrored and
/// animated sprites: a pixel counts when its alpha is at least the tolerance.
/// <code>
/// if (FlxCollision.PixelPerfectCheck(player, rock)) player.Hurt(1);                    // the pictures touch, not just the boxes
/// if (FlxCollision.PixelPerfectPointCheck(FlxG.Mouse.WorldPosition, button)) ...       // is the mouse on a solid pixel?
/// Add(FlxCollision.CreateCameraWall(FlxG.Camera, FlxCameraWall.Inside, 8));           // four walls to keep things on screen
/// </code>
/// Differences from the original: points are world coordinates (Vector2) and are checked against the sprite as drawn, and the
/// debug picture and <c>adjustWorldBounds</c> (this engine has no world bounds) are left out, and the bottom camera wall is placed from the
/// camera's Y (the original used its height only). A mirrored sprite is assumed to turn
/// around the middle of its picture.
/// </summary>
public static class FlxCollision
{
    /// <summary>
    /// True when the drawn pictures of two sprites share a pixel. It checks the bounds first and only then the pixels of the area
    /// where they overlap. The order of the sprites does not matter.
    /// </summary>
    /// <param name="alphaTolerance">Pixels with at least this alpha (0-255) count. 255 = only fully opaque pixels.</param>
    /// <param name="camera">The camera the sprites are seen through (the main one by default).</param>
    public static bool PixelPerfectCheck(FlxSprite contact, FlxSprite target, int alphaTolerance = 255, FlxCamera? camera = null)
    {
        var a = contact.GetDrawTransform(camera);
        var b = target.GetDrawTransform(camera);
        if (!a.Valid || !b.Valid) return false;

        var (aMin, aMax) = a.ScreenBounds();
        var (bMin, bMax) = b.ScreenBounds();
        var min = Vector2.Max(aMin, bMin);
        var max = Vector2.Min(aMax, bMax);
        if (max.X <= min.X || max.Y <= min.Y) return false;

        var pixelsA = FxPixels.Get(contact.Pixels!);
        var pixelsB = FxPixels.Get(target.Pixels!);
        int widthA = contact.Pixels!.Width, widthB = target.Pixels!.Width;

        // look at the middle of every screen pixel of the overlapping area
        int x0 = (int)MathF.Floor(min.X), y0 = (int)MathF.Floor(min.Y), x1 = (int)MathF.Ceiling(max.X), y1 = (int)MathF.Ceiling(max.Y);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                if (a.TryFromScreen(p, out var ta) && pixelsA[ta.Y * widthA + ta.X].A >= alphaTolerance &&
                    b.TryFromScreen(p, out var tb) && pixelsB[tb.Y * widthB + tb.X].A >= alphaTolerance)
                    return true;
            }
        return false;
    }

    /// <summary>True when a point in the world lies on a pixel of the sprite's drawn picture that has at least this alpha.</summary>
    public static bool PixelPerfectPointCheck(Vector2 worldPoint, FlxSprite target, int alphaTolerance = 255, FlxCamera? camera = null)
    {
        camera ??= FlxG.Camera;
        var t = target.GetDrawTransform(camera);
        if (!t.Valid) return false;

        // world to the camera's view, the way the sprite itself is placed
        var screen = new Vector2(worldPoint.X - camera.ScrollX * target.ScrollFactor.X, worldPoint.Y - camera.ScrollY * target.ScrollFactor.Y);
        if (!t.TryFromScreen(screen, out var texel)) return false;
        return FxPixels.Get(target.Pixels!)[texel.Y * target.Pixels!.Width + texel.X].A >= alphaTolerance;
    }

    /// <summary>
    /// Four immovable blocks around (or just inside the edge of) a camera's view, to keep sprites on screen with FlxG.Collide.
    /// Add the group to your state.
    /// </summary>
    public static FlxGroup CreateCameraWall(FlxCamera camera, FlxCameraWall placement, int thickness)
    {
        FlxTileblock left, right, top, bottom;
        if (placement == FlxCameraWall.Outside)
        {
            left = new FlxTileblock(camera.X - thickness, camera.Y + thickness, thickness, camera.Height - thickness * 2);
            right = new FlxTileblock(camera.X + camera.Width, camera.Y + thickness, thickness, camera.Height - thickness * 2);
            top = new FlxTileblock(camera.X - thickness, camera.Y - thickness, camera.Width + thickness * 2, thickness);
            bottom = new FlxTileblock(camera.X - thickness, camera.Y + camera.Height, camera.Width + thickness * 2, thickness);
        }
        else
        {
            left = new FlxTileblock(camera.X, camera.Y + thickness, thickness, camera.Height - thickness * 2);
            right = new FlxTileblock(camera.X + camera.Width - thickness, camera.Y + thickness, thickness, camera.Height - thickness * 2);
            top = new FlxTileblock(camera.X, camera.Y, camera.Width, thickness);
            bottom = new FlxTileblock(camera.X, camera.Y + camera.Height - thickness, camera.Width, thickness);
        }

        var walls = new FlxGroup(4);
        foreach (var wall in new[] { left, right, top, bottom })
        {
            wall.Immovable = true;
            walls.Add(wall);
        }
        return walls;
    }
}

/// <summary>The pixels of a texture, read from the graphics card once and remembered for as long as the texture lives.</summary>
internal static class FxPixels
{
    static readonly ConditionalWeakTable<Texture2D, Color[]> Cache = new();

    public static Color[] Get(Texture2D texture)
    {
        if (Cache.TryGetValue(texture, out var pixels)) return pixels;
        pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        Cache.Add(texture, pixels);
        return pixels;
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoFlixel.Tiled;

namespace MonoFlixel.Demo;

/// <summary>
/// Port of the HaxeFlixel TiledEditor demo's TiledLevel: loads a .tmx and builds the Flixel objects from it.
/// Layers named "images" and "objects" are handled by name; tile layers become tilemaps
/// ("nocollide" layers go behind the player, the others collide, "animated" layers play tile animations).
/// </summary>
public class TiledLevel
{
    public readonly TiledMap Map;

    public readonly FlxGroup ForegroundTiles = new();
    public readonly FlxGroup ObjectsLayer = new();
    public readonly FlxGroup BackgroundLayer = new();
    public readonly FlxGroup ImagesLayer = new();

    readonly List<FlxTilemap> _collidableTileLayers = new();

    public TiledLevel(string tmxPath, TiledDemoState state)
    {
        Map = TiledMap.Load(tmxPath);

        FlxG.Camera.Bounds = new Rectangle(0, 0, Map.FullWidth, Map.FullHeight);

        LoadImages();
        LoadObjects(state);

        // tile layers
        foreach (var layer in Map.LayersOfType<TiledTileLayer>())
        {
            var tilemap = Map.CreateTilemap(layer,
                animateTiles: layer.Properties.Contains("animated"),
                randomizeAnimationPhase: true);

            if (layer.Properties.Contains("nocollide"))
            {
                BackgroundLayer.Add(tilemap);
            }
            else
            {
                ForegroundTiles.Add(tilemap);
                _collidableTileLayers.Add(tilemap);
            }
        }
    }

    void LoadObjects(TiledDemoState state)
    {
        foreach (var layer in Map.LayersOfType<TiledObjectLayer>())
        {
            // decorative images
            if (layer.Name == "images")
                foreach (var o in layer.Objects) LoadImageObject(o);

            // gameplay objects
            if (layer.Name == "objects")
                foreach (var o in layer.Objects) LoadObject(state, o);
        }
    }

    /// <summary>A tile object that uses an image from the map's "image collection" tileset.</summary>
    void LoadImageObject(TiledObject o)
    {
        var image = Map.GetGidOwner(o.Gid)?.GetImageSourceByGid(o.Gid);
        if (image == null) return;      // an ordinary tile object, not a picture

        var sprite = new FlxSprite(0, 0, Map.ResolvePath(image.Source));

        // Tiled positions tile objects by their BOTTOM-left corner, and scales and rotates them around it.
        float w = o.Width > 0 ? o.Width : sprite.FrameWidth;
        float h = o.Height > 0 ? o.Height : sprite.FrameHeight;
        sprite.Scale = new Vector2(w / sprite.FrameWidth, h / sprite.FrameHeight);
        sprite.Origin = new Vector2(0, sprite.FrameHeight);
        sprite.X = o.X;
        sprite.Y = o.Y - sprite.FrameHeight;
        sprite.FlipX = o.FlippedHorizontally;
        sprite.FlipY = o.FlippedVertically;
        sprite.Angle = o.Rotation;

        // custom property "depth" = parallax factor
        if (o.Properties.Contains("depth"))
        {
            float depth = o.Properties.GetFloat("depth", 1f);
            sprite.ScrollFactor = new Vector2(depth, depth);
        }

        BackgroundLayer.Add(sprite);
    }

    void LoadObject(TiledDemoState state, TiledObject o)
    {
        float x = o.X;
        float y = o.Y;

        // tile objects are aligned bottom-left in Tiled (top-left in Flixel)
        if (o.HasGid) y -= Map.GetGidOwner(o.Gid)?.TileHeight ?? 0;

        switch (o.Type.ToLowerInvariant())
        {
            case "player_start":
            {
                // MegaManPlayer has its own input, gravity and animations. Its hitbox is 19x34: center it
                // horizontally on the 32x32 start marker and line its feet up with the marker's bottom.
                var player = new MegaManPlayer(x + (32 - 19) / 2f, y + 32 - 34);
                FlxG.Camera.Follow(player);
                state.Player = player;
                ObjectsLayer.Add(player);
                break;
            }

            case "floor":
                state.Floor = new FlxObject(x, y, o.Width, o.Height);
                break;

            case "coin":
            {
                var tileset = Map.GetGidOwner(o.Gid) ?? throw new InvalidOperationException($"Coin object {o.Id} has no tile (gid {o.Gid}).");
                state.Coins.Add(new FlxSprite(x, y, Map.ResolvePath(tileset.ImageSource)));
                break;
            }

            case "exit":
            {
                // the level exit, hidden until every coin is collected
                var exit = new FlxSprite(x, y).MakeGraphic(32, 32, new Color(0x3f, 0x3f, 0x3f));
                exit.Exists = false;
                state.Exit = exit;
                ObjectsLayer.Add(exit);
                break;
            }
        }
    }

    void LoadImages()
    {
        foreach (var image in Map.LayersOfType<TiledImageLayer>())
            ImagesLayer.Add(new FlxSprite(image.OffsetX, image.OffsetY, Map.ResolvePath(image.ImageSource)));
    }

    /// <summary>
    /// Collide an object with every solid tile layer.
    /// (Always collide the map with the object, not the other way around: it avoids 1px separation errors.)
    /// </summary>
    public bool CollideWithLevel(FlxObject obj, Action<FlxObject, FlxObject>? notify = null)
    {
        foreach (var map in _collidableTileLayers)
            if (FlxG.Collide(map, obj, notify)) return true;
        return false;
    }
}

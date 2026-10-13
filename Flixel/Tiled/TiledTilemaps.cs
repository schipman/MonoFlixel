using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Tiled;

/// <summary>Turns Tiled layers into Flixel objects.</summary>
public static class TiledTilemaps
{
    /// <summary>
    /// Builds an FlxTilemap from a Tiled tile layer: the tileset image, tile size, margin/spacing, flip flags,
    /// layer offset, opacity and visibility are all applied. Tile value = Tiled global id, so tile 0 is empty,
    /// and every non-empty tile collides (use SetTileProperties to change that).
    /// </summary>
    /// <param name="tileset">Which tileset the layer draws from. Default: the tileset named by the layer's
    /// "tileset" custom property, otherwise the one that owns the layer's first tile.</param>
    /// <param name="animateTiles">Play the tileset's tile animations.</param>
    /// <param name="randomizeAnimationPhase">Start each animated tile at a random point of its cycle.</param>
    public static FlxTilemap CreateTilemap(this TiledMap map, TiledTileLayer layer, TiledTileSet? tileset = null,
        bool animateTiles = true, bool randomizeAnimationPhase = false)
    {
        if (!string.Equals(map.Orientation, "orthogonal", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Only orthogonal maps are supported (this one is \"{map.Orientation}\").");

        tileset ??= FindTileset(map, layer);
        if (tileset.IsImageCollection)
            throw new InvalidOperationException($"Tileset '{tileset.Name}' is a collection of separate images; it cannot be used as a tile layer's sheet.");

        // every tile must come from the tileset we draw with, otherwise the frames would be wrong
        foreach (int gid in layer.TileArray)
            if (gid != 0 && !tileset.Contains(gid))
                throw new InvalidDataException(
                    $"Layer '{layer.Name}' uses tile {gid}, which is not in tileset '{tileset.Name}' " +
                    $"(tiles {tileset.FirstGid}-{tileset.FirstGid + tileset.TileCount - 1}). " +
                    "A tilemap can use one tileset: put tiles from each tileset on their own layer.");

        var texture = FlxAssets.GetTexture(map.ResolvePath(tileset.ImageSource));
        var tilemap = new FlxTilemap { TilesetMargin = tileset.Margin, TilesetSpacing = tileset.Spacing };
        tilemap.LoadMapFromArray(layer.TileArray, layer.Width, layer.Height, texture,
            tileset.TileWidth, tileset.TileHeight, FlxAutoTiling.Off, drawIndex: 1, collideIndex: 1, startingIndex: tileset.FirstGid);

        if (layer.OffsetX != 0 || layer.OffsetY != 0) tilemap.Reset(layer.OffsetX, layer.OffsetY);
        if (layer.HasFlips) tilemap.SetTileFlags(layer.Flags);
        if (layer.Opacity < 1f) tilemap.Tint = new Color(255, 255, 255, (int)(255 * Math.Clamp(layer.Opacity, 0f, 1f)));
        tilemap.Visible = layer.Visible;

        if (animateTiles)
        {
            foreach (var tile in tileset.Tiles.Values)
            {
                if (tile.Animation.Count == 0) continue;
                tilemap.SetTileAnimation(
                    tileset.FirstGid + tile.Id,
                    tile.Animation.Select(f => tileset.FirstGid + f.TileId).ToList(),
                    tile.Animation.Select(f => f.DurationMs).ToList(),
                    randomizeAnimationPhase);
            }
        }
        return tilemap;
    }

    static TiledTileSet FindTileset(TiledMap map, TiledTileLayer layer)
    {
        if (layer.Properties.Get("tileset") is { } name)
            return map.GetTileSet(name)
                ?? throw new InvalidOperationException(
                    $"Tileset '{name}' not found. Did you misspell the 'tileset' property of layer '{layer.Name}'?");

        int first = layer.TileArray.FirstOrDefault(g => g != 0);
        if (first != 0)
            return map.GetGidOwner(first)
                ?? throw new InvalidDataException($"Layer '{layer.Name}' uses tile {first}, which belongs to no tileset.");

        return map.Tilesets.FirstOrDefault(t => !t.IsImageCollection)
            ?? throw new InvalidOperationException($"Layer '{layer.Name}' is empty and the map has no image tileset to use.");
    }
}

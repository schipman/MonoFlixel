using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>A grid of animated tiles that grows to cover the screen, or shrinks away. Port of flixel-addons' TransitionTiles.</summary>
public class TransitionTiles : TransitionEffect
{
    readonly FlxTypedGroup<FlxTransitionSprite> _sprites = new();

    public TransitionTiles(TransitionData data) : base(data)
    {
        data.TileData ??= new TransitionTileData(null, 32, 32);
        var tile = data.TileData;
        var region = data.GetRegion();

        int tilesX = (int)MathF.Ceiling(region.Width / (float)tile.Width);
        int tilesY = (int)MathF.Ceiling(region.Height / (float)tile.Height);
        int maxTiles = Math.Max(tilesX, tilesY);

        // how long a tile waits more than the one before it, along each axis (so the tiles ripple in the wipe direction)
        float dTime = data.Duration / maxTiles;
        float xDelay = dTime * MathF.Abs(data.Direction.X);
        float yDelay = dTime * MathF.Abs(data.Direction.Y);

        int addX = tile.Width, addY = tile.Height;
        int startX = region.X, startY = region.Y;

        // wiping from the right / bottom: start at the far corner and walk back
        if (data.Direction.X < 0) { addX = -addX; startX += region.Width + addX; }
        if (data.Direction.Y < 0) { addY = -addY; startY += region.Height + addY; }

        int tx = startX, ty = startY;
        float delay = 0;

        for (int iy = 0; iy < tilesY; iy++)
        {
            for (int ix = 0; ix < tilesX; ix++)
            {
                var ts = new FlxTransitionSprite(tx, ty, delay, tile.Asset, tile.Width, tile.Height, tile.FrameRate)
                {
                    Tint = data.Color,
                    ScrollFactor = Vector2.Zero
                };
                _sprites.Add(ts);
                tx += addX;
                delay += xDelay;
            }
            ty += addY;
            tx = startX;
            delay = (iy + 1) * yDelay;
        }
        Add(_sprites);
    }

    public override void Start(TransitionStatus newStatus)
    {
        base.Start(newStatus);
        foreach (var t in _sprites.Members) t.Start(newStatus);
    }

    public override void SetStatus(TransitionStatus newStatus)
    {
        base.SetStatus(newStatus);
        foreach (var t in _sprites.Members) t.SetStatus(newStatus);
    }

    public override void Update(float elapsed)
    {
        base.Update(elapsed);

        if (!Started) return;

        foreach (var sprite in _sprites.Members)
            if (sprite.Status != TransitionStatus.Null && sprite.Status != EndStatus)
                return;       // some tile is still moving

        Started = false;
        DelayThenFinish();
    }
}

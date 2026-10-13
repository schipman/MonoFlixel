using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

public class FlxSprite : FlxObject
{
    public Texture2D? Pixels;
    public Color Tint = Color.White;
    public float Alpha = 1f;
    public Vector2 Scale = Vector2.One;
    public Vector2 Offset;            // shifts the graphic relative to the hitbox
    public Vector2 Origin;            // pivot (in frame pixels) for rotation and scaling; defaults to the frame center
    public bool FlipX, FlipY;
    public int Frame;
    public float Health = 1f;
    public FlxDirection Facing = FlxDirection.Right;

    /// <summary>How the sprite's colors combine with what is below it (default: normal alpha blending).</summary>
    public FlxBlend Blend = FlxBlend.Alpha;

    /// <summary>
    /// Shows only this part of the frame (in frame pixels, before scaling and rotation): a quick way to make a bar fill up,
    /// reveal a picture, or cut a sprite off. null shows everything.
    /// </summary>
    public Rectangle? ClipRect;

    /// <summary>Size of one (untrimmed) frame: what the sprite measures when the graphic is loaded.</summary>
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    /// <summary>The frames of the loaded graphic (null before a graphic is loaded).</summary>
    public FlxFramesCollection? Frames { get; private set; }

    public int NumFrames => Frames?.Count ?? 0;

    public readonly FlxAnimationController Animation;

    Dictionary<FlxDirection, (bool X, bool Y)>? _facingFlip;
    float _bakedStep;                 // degrees between baked rotation frames (0 = not baked)

    /// <summary>Optionally pass the full path of a PNG to load (like new FlxSprite(x, y, graphic)).</summary>
    public FlxSprite(float x = 0, float y = 0, string? graphic = null) : base(x, y)
    {
        Animation = new FlxAnimationController(this);
        if (graphic != null) LoadGraphic(graphic);
    }

    // ------------------------------------------------------------ graphics

    public FlxSprite MakeGraphic(int width, int height, Color color)
    {
        var tex = new Texture2D(FlxG.GraphicsDevice, width, height);
        var data = new Color[width * height];
        Array.Fill(data, color);
        tex.SetData(data);
        return SetFrames(FlxFramesCollection.FromImage(tex));
    }

    /// <summary>
    /// Load a PNG straight from disk (no content pipeline; textures are cached and shared).
    /// With animated=true the sheet is cut into frames; like Flixel, frames default to squares
    /// (width = image height, height = width) when you pass 0.
    /// </summary>
    public FlxSprite LoadGraphic(string path, bool animated = false, int frameWidth = 0, int frameHeight = 0) =>
        LoadGraphic(FlxAssets.GetTexture(path), animated, frameWidth, frameHeight);

    /// <summary>Use a texture you loaded yourself (e.g. via Content.Load).</summary>
    public FlxSprite LoadGraphic(Texture2D tex, bool animated = false, int frameWidth = 0, int frameHeight = 0)
    {
        if (!animated) return SetFrames(FlxFramesCollection.FromImage(tex));

        if (frameWidth <= 0) frameWidth = Math.Min(tex.Height, tex.Width);
        if (frameHeight <= 0) frameHeight = Math.Min(frameWidth, tex.Height);
        return SetFrames(FlxFramesCollection.FromTiles(tex, Math.Min(frameWidth, tex.Width), Math.Min(frameHeight, tex.Height)));
    }

    /// <summary>A sprite sheet with gaps between frames (<paramref name="spacing"/>) and a border around it (<paramref name="margin"/>).</summary>
    public FlxSprite LoadTiles(Texture2D tex, int frameWidth, int frameHeight, int spacing = 0, int margin = 0, Rectangle? region = null) =>
        SetFrames(FlxFramesCollection.FromTiles(tex, frameWidth, frameHeight, spacing, margin, region));

    /// <summary>Use the frames of an atlas (see FlxAtlasFrames). The sprite measures the biggest frame.</summary>
    public FlxSprite LoadFrames(FlxFramesCollection frames) => SetFrames(frames);

    /// <summary>
    /// Loads an Aseprite export (the PNG sheet and its JSON) and adds an animation for every tag in it, with the
    /// durations set in Aseprite. Play them by tag name.
    /// </summary>
    public FlxSprite LoadAseprite(string imagePath, string jsonPath)
    {
        SetFrames(FlxAtlasFrames.FromAseprite(imagePath, jsonPath));
        Animation.AddFromAtlas();
        return this;
    }

    FlxSprite SetFrames(FlxFramesCollection frames)
    {
        Frames = frames;
        Pixels = frames.Texture;
        FrameWidth = frames.MaxSize.X; FrameHeight = frames.MaxSize.Y;
        Width = FrameWidth; Height = FrameHeight;
        Frame = 0;
        _bakedStep = 0;
        Origin = new Vector2(FrameWidth * 0.5f, FrameHeight * 0.5f);
        return this;
    }

    /// <summary>
    /// Loads a graphic and bakes <paramref name="rotations"/> pre-rotated copies of it. The sprite then shows the copy nearest to
    /// its Angle instead of rotating at draw time: sharper for pixel art (no resampling) at the cost of a few steps in the angle.
    /// The hitbox stays the size of the original picture.
    /// </summary>
    /// <param name="frame">Which frame of the picture to use when it is a sheet (-1 = the whole picture).</param>
    public FlxSprite LoadRotatedGraphic(Texture2D tex, int rotations = 16, int frame = -1, int frameWidth = 0, int frameHeight = 0)
    {
        rotations = Math.Max(2, rotations);

        // the picture to rotate
        Rectangle area = new(0, 0, tex.Width, tex.Height);
        if (frame >= 0)
        {
            var sheet = FlxFramesCollection.FromTiles(tex, frameWidth > 0 ? frameWidth : Math.Min(tex.Width, tex.Height), frameHeight > 0 ? frameHeight : Math.Min(tex.Width, tex.Height));
            area = sheet[Math.Min(frame, sheet.Count - 1)].Source;
        }

        var src = new Color[tex.Width * tex.Height];
        tex.GetData(src);

        int size = (int)MathF.Ceiling(MathF.Sqrt(area.Width * area.Width + area.Height * area.Height));   // big enough for any angle
        int cols = Math.Min(rotations, 8), rows = (rotations + cols - 1) / cols;
        var sheetPixels = new Color[cols * size * rows * size];
        int sheetW = cols * size;

        float cxSrc = area.Width / 2f, cySrc = area.Height / 2f, cxDst = size / 2f, cyDst = size / 2f;
        for (int i = 0; i < rotations; i++)
        {
            float rad = MathHelper.ToRadians(360f * i / rotations);
            float cos = MathF.Cos(rad), sin = MathF.Sin(rad);
            int ox = (i % cols) * size, oy = (i / cols) * size;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // turn the destination pixel back into the picture's own space (nearest pixel)
                    float dx = x + 0.5f - cxDst, dy = y + 0.5f - cyDst;
                    int sx = (int)MathF.Floor(dx * cos + dy * sin + cxSrc);
                    int sy = (int)MathF.Floor(-dx * sin + dy * cos + cySrc);
                    if (sx < 0 || sy < 0 || sx >= area.Width || sy >= area.Height) continue;
                    sheetPixels[(oy + y) * sheetW + ox + x] = src[(area.Y + sy) * tex.Width + area.X + sx];
                }
        }

        var baked = new Texture2D(FlxG.GraphicsDevice, sheetW, rows * size);
        baked.SetData(sheetPixels);

        var frames = FlxFramesCollection.FromTiles(baked, size, size);
        // keep only as many frames as rotations (the last row may be partly empty)
        var exact = new FlxFramesCollection(baked);
        for (int i = 0; i < rotations; i++) exact.Add(frames[i]);

        SetFrames(exact);
        Width = area.Width; Height = area.Height;       // the hitbox is the original picture
        CenterOffsets();
        _bakedStep = 360f / rotations;
        return this;
    }

    public FlxSprite LoadRotatedGraphic(string path, int rotations = 16, int frame = -1, int frameWidth = 0, int frameHeight = 0) =>
        LoadRotatedGraphic(FlxAssets.GetTexture(path), rotations, frame, frameWidth, frameHeight);

    // ------------------------------------------------------------ hitbox / transform helpers

    /// <summary>Resize the hitbox to match the current Scale (like FlxSprite.updateHitbox).</summary>
    public void UpdateHitbox()
    {
        Width = MathF.Abs(Scale.X) * FrameWidth;
        Height = MathF.Abs(Scale.Y) * FrameHeight;
        CenterOffsets();
        CenterOrigin();
    }

    /// <summary>Center the graphic on the hitbox (after the hitbox was resized).</summary>
    public void CenterOffsets()
    {
        Offset = new Vector2(-0.5f * (Width - FrameWidth), -0.5f * (Height - FrameHeight));
    }

    public void CenterOrigin() => Origin = new Vector2(FrameWidth * 0.5f, FrameHeight * 0.5f);

    /// <summary>Scale the graphic to a pixel size. Pass 0 for one axis to keep the aspect ratio.</summary>
    public void SetGraphicSize(int width = 0, int height = 0)
    {
        if (width <= 0 && height <= 0) return;
        float sx = width / (float)FrameWidth;
        float sy = height / (float)FrameHeight;
        if (width <= 0) sx = sy;
        else if (height <= 0) sy = sx;
        Scale = new Vector2(sx, sy);
        UpdateHitbox();
    }

    public void ScreenCenter(bool horizontal = true, bool vertical = true)
    {
        if (horizontal) X = (FlxG.Width - Width) * 0.5f;
        if (vertical) Y = (FlxG.Height - Height) * 0.5f;
    }

    /// <summary>When Facing changes, also flip the graphic (e.g. SetFacingFlip(Left, true, false)).</summary>
    public void SetFacingFlip(FlxDirection direction, bool flipX, bool flipY)
    {
        _facingFlip ??= new();
        _facingFlip[direction] = (flipX, flipY);
    }

    // ------------------------------------------------------------ health

    public virtual void Hurt(float damage)
    {
        Health -= damage;
        if (Health <= 0) Kill();
    }

    // ------------------------------------------------------------ update / draw

    public override void Update(float elapsed)
    {
        Animation.Update(elapsed);
        base.Update(elapsed);
    }

    public override void Draw()
    {
        if (Pixels == null || Frames == null || Alpha <= 0) return;
        var cam = FlxG.Camera;

        float px = MathF.Round(X - Offset.X - cam.ScrollX * ScrollFactor.X);
        float py = MathF.Round(Y - Offset.Y - cam.ScrollY * ScrollFactor.Y);

        // which frame: baked rotations pick one by angle (and are not rotated again)
        int index = Frame;
        float rotation = MathHelper.ToRadians(Angle);
        if (_bakedStep > 0)
        {
            index = (int)MathF.Round(((Angle % 360f) + 360f) % 360f / _bakedStep) % Frames.Count;
            rotation = 0;
        }
        var frame = Frames[Math.Clamp(index, 0, Frames.Count - 1)];

        bool fx = FlipX, fy = FlipY;
        if (_facingFlip != null && _facingFlip.TryGetValue(Facing, out var f)) { fx ^= f.X; fy ^= f.Y; }
        if (Animation.Current is { } anim) { fx ^= anim.FlipX; fy ^= anim.FlipY; }

        // Where the frame's pixels sit inside the full (untrimmed) frame. SpriteBatch mirrors the picture inside its own
        // rectangle, so for a mirrored sprite that rectangle has to move to the mirrored place.
        var src = frame.Source;
        int fw = FrameWidth, fh = FrameHeight;
        var area = new Rectangle(
            fx ? fw - frame.Offset.X - src.Width : frame.Offset.X,
            fy ? fh - frame.Offset.Y - src.Height : frame.Offset.Y,
            src.Width, src.Height);

        if (ClipRect is { } clip)
        {
            if (fx) clip.X = fw - clip.X - clip.Width;
            if (fy) clip.Y = fh - clip.Y - clip.Height;

            var shown = Rectangle.Intersect(area, clip);
            if (shown.Width <= 0 || shown.Height <= 0) return;

            // the matching part of the picture (counted from the opposite edge when mirrored)
            src.X += fx ? area.Right - shown.Right : shown.X - area.X;
            src.Y += fy ? area.Bottom - shown.Bottom : shown.Y - area.Y;
            src.Width = shown.Width;
            src.Height = shown.Height;
            area = shown;
        }

        var effects = SpriteEffects.None;
        if (fx) effects |= SpriteEffects.FlipHorizontally;
        if (fy) effects |= SpriteEffects.FlipVertically;

        var c = Tint;
        c.A = (byte)(c.A * MathHelper.Clamp(Alpha, 0f, 1f));

        FlxG.SpriteBatch.Draw(Pixels, new Vector2(px, py) + Origin, src, c,
            rotation, Origin - new Vector2(area.X, area.Y), Scale, effects, 0f);
    }
}

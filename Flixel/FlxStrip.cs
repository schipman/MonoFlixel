using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// A sprite made of textured triangles: bend, wave and stretch a picture by moving its vertices. Port of Flixel's FlxStrip.
/// <see cref="Vertices"/> are positions in pixels relative to the sprite's X / Y, <see cref="Uvs"/> say which part of the picture
/// (0..1 across the current frame) each vertex shows, and <see cref="Indices"/> lists the triangles (three vertices each).
/// Tint, Alpha, Blend, ScrollFactor and the current frame work as for any sprite; Scale and Angle are not applied
/// (move the vertices instead).
/// <code>
/// var flag = FlxStrip.CreateGrid(texture, columns: 16, rows: 4, width: 128, height: 64);
/// // each frame: flag.Vertices[i].Y += MathF.Sin(...) ; flag.UpdateVertices();
/// </code>
/// </summary>
public class FlxStrip : FlxSprite
{
    static BasicEffect? _effect;
    VertexPositionColorTexture[] _buffer = Array.Empty<VertexPositionColorTexture>();

    public Vector2[] Vertices = Array.Empty<Vector2>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    public int[] Indices = Array.Empty<int>();

    /// <summary>Optional color per vertex, multiplied with Tint (null = just Tint).</summary>
    public Color[]? VertexColors;

    public FlxStrip(float x = 0, float y = 0, Texture2D? texture = null) : base(x, y)
    {
        if (texture != null) LoadGraphic(texture);
        Moves = false;
    }

    /// <summary>
    /// A flat grid of (columns x rows) quads showing the whole picture, <paramref name="width"/> x <paramref name="height"/> pixels big.
    /// Moving the grid's vertices makes waves, flags, wobbling shapes.
    /// </summary>
    public static FlxStrip CreateGrid(Texture2D texture, int columns, int rows, float width, float height)
    {
        if (columns < 1 || rows < 1) throw new ArgumentException("A grid needs at least one column and one row.");

        var strip = new FlxStrip(0, 0, texture);
        int vx = columns + 1, vy = rows + 1;
        strip.Vertices = new Vector2[vx * vy];
        strip.Uvs = new Vector2[vx * vy];
        strip.Indices = new int[columns * rows * 6];

        for (int y = 0; y < vy; y++)
            for (int x = 0; x < vx; x++)
            {
                float u = x / (float)columns, v = y / (float)rows;
                strip.Vertices[y * vx + x] = new Vector2(u * width, v * height);
                strip.Uvs[y * vx + x] = new Vector2(u, v);
            }

        int n = 0;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * vx + x, b = a + 1, c = a + vx, d = c + 1;
                strip.Indices[n++] = a; strip.Indices[n++] = b; strip.Indices[n++] = c;
                strip.Indices[n++] = b; strip.Indices[n++] = d; strip.Indices[n++] = c;
            }

        strip.Width = width; strip.Height = height;
        return strip;
    }

    public override void Draw()
    {
        if (Pixels == null || Frames == null || Alpha <= 0 || Indices.Length < 3 || Vertices.Length == 0) return;
        if (Uvs.Length < Vertices.Length) throw new InvalidOperationException("Every vertex needs a UV.");

        var cam = FlxG.Camera;
        float px = X - cam.ScrollX * ScrollFactor.X;
        float py = Y - cam.ScrollY * ScrollFactor.Y;

        var frame = Frames[Math.Clamp(Frame, 0, Frames.Count - 1)];
        var tex = Pixels;

        var tint = Tint; tint.A = (byte)(tint.A * MathHelper.Clamp(Alpha, 0f, 1f));

        if (_buffer.Length != Vertices.Length) _buffer = new VertexPositionColorTexture[Vertices.Length];
        for (int i = 0; i < Vertices.Length; i++)
        {
            var color = VertexColors != null && i < VertexColors.Length ? Multiply(tint, VertexColors[i]) : tint;
            var uv = new Vector2(
                (frame.Source.X + Uvs[i].X * frame.Source.Width) / tex.Width,
                (frame.Source.Y + Uvs[i].Y * frame.Source.Height) / tex.Height);

            _buffer[i] = new VertexPositionColorTexture(new Vector3(px + Vertices[i].X, py + Vertices[i].Y, 0), color, uv);
        }

        FlxG.BreakBatch(() =>
        {
            var gd = FlxG.GraphicsDevice;
            _effect ??= new BasicEffect(gd) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };

            var vp = gd.Viewport;
            _effect.World = FlxG.BatchTransform;
            _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);
            _effect.Texture = tex;

            gd.BlendState = FlxG.BatchBlendState;
            gd.SamplerStates[0] = SamplerState.PointClamp;
            gd.RasterizerState = FlxG.BatchRasterizer ?? RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _buffer, 0, _buffer.Length, Indices, 0, Indices.Length / 3);
            }
        });
    }

    static Color Multiply(Color a, Color b) => new(a.R * b.R / 255, a.G * b.G / 255, a.B * b.B / 255, (int)(a.A * b.A / 255));
}

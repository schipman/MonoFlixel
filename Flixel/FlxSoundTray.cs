using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// The little volume display that slides down from the top of the screen when the volume changes (keys + - 0, or FlxG.Sound.ChangeVolume).
/// Drawn by the game above everything else, also above post-processing. Port of Flixel's FlxSoundTray.
/// </summary>
public sealed class FlxSoundTray
{
    const float BoxWidth = 80, BoxHeight = 28;
    const float VisibleSeconds = 1f;           // how long it stays after the last change
    const float SlideSpeed = 160f;             // game pixels per second

    float _y = -BoxHeight, _timer;
    bool _active;
    FlxText? _label;

    /// <summary>True while the tray is on the screen.</summary>
    public bool Visible => _active;

    internal void Show(bool up)
    {
        _timer = 0;
        _active = true;
    }

    internal void Update(float realSeconds)
    {
        if (!_active) return;

        if (_timer < VisibleSeconds)
        {
            _timer += realSeconds;
            _y = MathF.Min(0, _y + SlideSpeed * realSeconds);       // slide in
        }
        else
        {
            _y -= SlideSpeed * realSeconds;                         // slide out
            if (_y <= -BoxHeight)
            {
                _y = -BoxHeight;
                _active = false;
            }
        }
    }

    /// <summary>Draws the tray over the game picture. <paramref name="dest"/> is where the picture is on the window.</summary>
    internal void Draw(SpriteBatch batch, Rectangle dest, int gameWidth, int gameHeight)
    {
        if (!_active) return;

        float sx = dest.Width / (float)gameWidth, sy = dest.Height / (float)gameHeight;
        var sound = FlxG.Sound;
        int bars = sound.Muted ? 0 : (int)MathF.Round(sound.Volume * 10f);

        _label ??= new FlxText(0, 0, BoxWidth, "", 8) { Alignment = FlxTextAlign.Center, ScrollFactor = Vector2.Zero, Tint = Color.White };
        _label.Text = sound.Muted ? "MUTED" : "VOLUME";

        float x = MathF.Round((gameWidth - BoxWidth) / 2f), y = MathF.Round(_y);
        _label.X = x;
        _label.Y = y + 3;

        batch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.NonPremultiplied,
            transformMatrix: Matrix.CreateScale(sx, sy, 1f) * Matrix.CreateTranslation(dest.X, dest.Y, 0f));

        var px = FlxG.Pixel;
        batch.Draw(px, new Rectangle((int)x, (int)y, (int)BoxWidth, (int)BoxHeight), new Color(0, 0, 0, 190));
        _label.Draw();

        // ten bars that grow taller from left to right
        const int barWidth = 4, gap = 2;
        int left = (int)(x + (BoxWidth - (10 * barWidth + 9 * gap)) / 2f);
        int bottom = (int)y + (int)BoxHeight - 4;
        for (int i = 0; i < 10; i++)
        {
            int h = 3 + i;
            batch.Draw(px, new Rectangle(left + i * (barWidth + gap), bottom - h, barWidth, h), i < bars ? Color.White : new Color(255, 255, 255, 80));
        }

        batch.End();
    }
}

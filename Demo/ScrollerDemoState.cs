using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel.Demo;

/// <summary>
/// Demo scene: Flixel Power Tools' FlxScrollZone (four parallax bands inside one picture), FlxScrollingText (a ticker),
/// FlxFlectrum (a spectrum meter for the music) and FlxGridOverlay (the checkerboard under it all).
/// SPACE stops and starts the scrolling, M changes the meter's mode, D its direction, G the grid, F12 saves a screenshot, ESC goes back.
/// </summary>
public class ScrollerDemoState : FlxState
{
    const int W = 480, H = 270;

    FlxScrollZone _zones = null!;
    FlxScrollingText _ticker = null!, _slow = null!;
    FlxFlectrum _meter = null!, _meterRight = null!;
    FlxSprite _grid = null!;
    FlxText _info = null!;
    float _time;

    public override void Create()
    {
        FlxG.Game.Window.Title = "Power Tools: demo scene";
        FlxG.Cameras.BgColor = new Color(8, 6, 24);
        FlxG.Mouse.Visible = true;

        // a checkerboard under everything, made by code
        _grid = FlxGridOverlay.Create(16, 16, W, H, true, new Color(14, 12, 36), new Color(20, 16, 48))!;
        Add(_grid);

        // one picture with four bands; each band scrolls at its own speed
        var sky = new FlxSprite(0, 20);
        sky.LoadGraphic(MakeLandscape(W, 96));
        _zones = new FlxScrollZone(sky);
        _zones.AddZone(new Rectangle(0, 0, W, 28), -0.25f, 0);        // stars
        _zones.AddZone(new Rectangle(0, 28, W, 24), -0.8f, 0);        // far hills
        _zones.AddZone(new Rectangle(0, 52, W, 24), -1.8f, 0);        // near hills
        _zones.AddZone(new Rectangle(0, 76, W, 20), -3.5f, 0);        // the ground
        Add(sky);
        Add(_zones);

        // the music and a meter on each side of it
        FlxG.Sound.PlayMusic("sound/mode.ogg", 0.5f);
        var music = FlxG.Sound.Music!;
        _meter = new FlxFlectrum(24, 134, music, 12, 8, 2, 20, 4, 1) { Mode = FlxFlectrumMode.Peaks };
        _meterRight = new FlxFlectrum(W - 24 - (12 * 10 - 2), 134, music, 12, 8, 2, 20, 4, 1) { Mode = FlxFlectrumMode.Peaks };
        Add(_meter);
        Add(_meterRight);

        // two tickers: a fast one, and a slow smooth one
        _ticker = new FlxScrollingText(FlxBitmapFont.Default, new Rectangle(0, H - 20, W, 12), pixels: 2,
            text: "FLXSCROLLZONE AND FLXSCROLLINGTEXT, FLXFLECTRUM AND FLXGRIDOVERLAY    PORTED FROM THE FLIXEL POWER TOOLS BY PHOTON STORM    ");
        _ticker.Tint = new Color(255, 230, 120);
        Add(_ticker.Sprite);
        Add(_ticker);

        _slow = new FlxScrollingText(FlxBitmapFont.Default, new Rectangle(0, 124, W, 12), pixels: 1, steps: 1,
            text: "SPACE: STOP / START     M: METER MODE     D: DIRECTION     G: GRID     F12: SCREENSHOT     ESC: BACK    ");
        _slow.Tint = new Color(130, 190, 255);
        Add(_slow.Sprite);
        Add(_slow);

        _info = new FlxText(8, 4, W - 16, "");
        Add(_info);

        FlxScreenGrab.DefineHotKey(Keys.F12, saveToFile: true);
        UpdateInfo();
    }

    /// <summary>Stars, two rows of hills and a striped ground, as one picture of four bands (each band tiles sideways).</summary>
    static Texture2D MakeLandscape(int w, int h)
    {
        var px = new Color[w * h];
        void Set(int x, int y, Color c) { if ((uint)x < (uint)w && (uint)y < (uint)h) px[y * w + x] = c; }

        // band 0 (0-27): sky with stars
        for (int y = 0; y < 28; y++)
            for (int x = 0; x < w; x++) Set(x, y, Color.Lerp(new Color(20, 14, 60), new Color(70, 30, 110), y / 27f));
        var rng = new Random(7);
        for (int i = 0; i < 70; i++) Set(rng.Next(w), rng.Next(28), i % 5 == 0 ? Color.White : new Color(170, 170, 230));

        // bands 1 and 2: hills that join up at the sides (the shape repeats across the width). Their sky is opaque: a zone that is not
        // cleared (AddZone clearRegion) keeps what was there before, so see-through parts would smear.
        void Hills(int top, int height, float freq, float amp, Color color, Color edge, Color sky)
        {
            for (int x = 0; x < w; x++)
            {
                float a = MathF.Sin(x * freq * MathF.PI * 2 / w) * amp + MathF.Sin(x * freq * 2.7f * MathF.PI * 2 / w) * amp * 0.4f;
                int surface = top + (int)(height * 0.45f - a);
                for (int y = top; y < top + height; y++)
                {
                    bool inside = y >= surface;
                    Set(x, y, inside ? (y == surface ? edge : color) : sky);
                }
            }
        }
        Hills(28, 24, 3, 8, new Color(60, 40, 120), new Color(110, 80, 190), new Color(112, 80, 188));
        Hills(52, 24, 5, 7, new Color(40, 90, 80), new Color(100, 190, 150), new Color(100, 190, 150));

        // band 3 (76-95): ground with stripes
        for (int y = 76; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(x, y, ((x / 12) + (y - 76) / 5) % 2 == 0 ? new Color(70, 50, 30) : new Color(100, 76, 44));

        var tex = new Texture2D(FlxG.GraphicsDevice, w, h);
        tex.SetData(px);
        return tex;
    }

    void UpdateInfo() =>
        _info.Text = $"scrolling {(_zones.Scrolling ? "on" : "off")}   meter {_meter.Mode} {_meter.Direction}   grid {(_grid.Visible ? "on" : "off")}   " +
                     (FlxScreenGrab.LastSavedPath is { } p ? $"saved {System.IO.Path.GetFileName(p)}" : "");

    public override void Update(float elapsed)
    {
        base.Update(elapsed);
        _time += elapsed;
        var k = FlxG.Keys;

        if (k.JustPressed(Keys.Space))
        {
            bool on = !_zones.Scrolling;
            _zones.Scrolling = on;
            _ticker.Scrolling = _slow.Scrolling = on;
            UpdateInfo();
        }
        if (k.JustPressed(Keys.M))
        {
            var mode = _meter.Mode == FlxFlectrumMode.Peaks ? FlxFlectrumMode.Meter : FlxFlectrumMode.Peaks;
            _meter.Mode = _meterRight.Mode = mode;
            UpdateInfo();
        }
        if (k.JustPressed(Keys.D))
        {
            // the meters are made in one direction; switching direction means making them again
            var next = (FlxFlectrumDirection)(((int)_meter.Direction + 1) % 4);
            ReplaceMeters(next);
            UpdateInfo();
        }
        if (k.JustPressed(Keys.G)) { _grid.Visible = !_grid.Visible; UpdateInfo(); }
        if (k.JustPressed(Keys.F12)) new FlxTimer().Start(0.3f, _ => UpdateInfo());

        // the colors of the right meter drift
        _meterRight.Colors = new[] { FlxColor.FromHsb(_time * 40f, 0.8f, 1f), FlxColor.FromHsb(_time * 40f + 60, 0.8f, 1f), FlxColor.FromHsb(_time * 40f + 120, 0.8f, 1f), FlxColor.FromHsb(_time * 40f + 180, 0.8f, 1f) };

        if (k.JustPressed(Keys.Escape))
        {
            FlxScreenGrab.ClearHotKey();
            FlxG.Sound.Music?.Stop();
            LauncherState.Return();
        }
    }

    void ReplaceMeters(FlxFlectrumDirection direction)
    {
        var music = FlxG.Sound.Music!;
        var mode = _meter.Mode;
        Remove(_meter);
        Remove(_meterRight);
        _meter.Destroy();
        _meterRight.Destroy();

        bool horizontal = direction is FlxFlectrumDirection.Left or FlxFlectrumDirection.Right;
        int size = horizontal ? 6 : 8, gap = horizontal ? 1 : 2;       // sideways, the twelve columns are stacked: keep them short
        _meter = new FlxFlectrum(24, horizontal ? 152 : 134, music, 12, size, gap, 20, 4, 1, direction) { Mode = mode };
        _meterRight = new FlxFlectrum(horizontal ? 270 : W - 24 - 118, horizontal ? 152 : 134, music, 12, size, gap, 20, 4, 1, direction) { Mode = mode };
        Add(_meter);
        Add(_meterRight);
    }
}

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>
/// Takes screenshots of the game, optionally saved as PNG files. Port of Richard Davey's (Photon Storm) Flixel Power Tools
/// <c>FlxScreenGrab</c> (see NOTICE-flixel-power-tools.txt).
/// <code>
/// FlxScreenGrab.DefineHotKey(Keys.F12, saveToFile: true);     // F12 saves a PNG to Pictures\MonoFlixel
/// FlxScreenGrab.Grab();                                       // or grab from code; read FlxScreenGrab.Screenshot afterwards
/// </code>
/// The picture is taken from the finished frame (post-processing and a custom mouse cursor included), so <see cref="Grab"/> takes effect at
/// the end of the current frame: use <see cref="Grabbed"/> or <see cref="Screenshot"/> afterwards. By default only the game area is kept (not the
/// black bars around it). Differences from the original: files are saved straight into <see cref="Folder"/> (no Save As window) and
/// "hide mouse" is left out (the operating system's cursor is never part of the picture).
/// </summary>
public static class FlxScreenGrab
{
    /// <summary>The last picture taken (window pixels, or the region asked for).</summary>
    public static Texture2D? Screenshot { get; private set; }

    /// <summary>Where saved pictures go (grab_YYYYMMDD_HHMMSS.png). Default: Pictures\MonoFlixel.</summary>
    public static string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MonoFlixel");

    /// <summary>The file of the last picture that was saved.</summary>
    public static string? LastSavedPath { get; private set; }

    /// <summary>True: keep only the part of the window the game is drawn in. False: the whole window.</summary>
    public static bool GameAreaOnly = true;

    /// <summary>Called with every picture taken, at the end of the frame.</summary>
    public static event Action<Texture2D>? Grabbed;

    static bool _hooked, _pending, _pendingSave, _autoSave;
    static Rectangle? _region, _pendingRegion;
    static Keys? _hotkey;

    /// <summary>A part of the window (in window pixels) to capture every time, instead of the game area.</summary>
    public static void DefineCaptureRegion(Rectangle region) => _region = region;
    public static void ClearCaptureRegion() => _region = null;

    /// <summary>The key that takes a screenshot (when let go, as in the original), saving it to a file with <paramref name="saveToFile"/>.</summary>
    public static void DefineHotKey(Keys key, bool saveToFile = false)
    {
        Hook();
        _hotkey = key;
        _autoSave = saveToFile;
    }

    public static void ClearHotKey()
    {
        _hotkey = null;
        _autoSave = false;
    }

    /// <summary>Takes a screenshot at the end of this frame. <paramref name="region"/> (window pixels) overrides the defined region and the game area.</summary>
    public static void Grab(bool saveToFile = false, Rectangle? region = null)
    {
        Hook();
        _pending = true;
        _pendingSave |= saveToFile;
        _pendingRegion = region;
    }

    static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        FlxG.Signals.PreUpdate.Add(() =>
        {
            if (_hotkey is { } key && FlxG.Keys.JustReleased(key)) Grab(_autoSave);
        });
        FlxG.Signals.PostDraw.Add(Capture);
    }

    static void Capture()
    {
        if (!_pending) return;
        bool save = _pendingSave;
        var asked = _pendingRegion;
        _pending = _pendingSave = false;
        _pendingRegion = null;

        var device = FlxG.GraphicsDevice;
        var pp = device.PresentationParameters;
        int bw = pp.BackBufferWidth, bh = pp.BackBufferHeight;
        var window = new Color[bw * bh];
        device.GetBackBufferData(window);

        var area = Rectangle.Intersect(new Rectangle(0, 0, bw, bh), asked ?? _region ?? (GameAreaOnly ? FlxG.Game.GameArea : new Rectangle(0, 0, bw, bh)));
        if (area.Width <= 0 || area.Height <= 0) return;

        var pixels = new Color[area.Width * area.Height];
        for (int y = 0; y < area.Height; y++)
            for (int x = 0; x < area.Width; x++)
            {
                var c = window[(area.Y + y) * bw + area.X + x];
                pixels[y * area.Width + x] = new Color(c.R, c.G, c.B, (byte)255);
            }

        Screenshot?.Dispose();
        Screenshot = new Texture2D(device, area.Width, area.Height);
        Screenshot.SetData(pixels);

        if (save) Save();
        Grabbed?.Invoke(Screenshot);
    }

    /// <summary>Saves the last screenshot as a PNG in <see cref="Folder"/> (a name like "grab_20260101_120000" is made up when none is given).</summary>
    public static string? Save(string? fileName = null)
    {
        if (Screenshot == null) return null;
        Directory.CreateDirectory(Folder);
        fileName = string.IsNullOrWhiteSpace(fileName) ? $"grab_{DateTime.Now:yyyyMMdd_HHmmss}" : fileName;
        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) fileName += ".png";

        string path = Path.Combine(Folder, Path.GetFileName(fileName));
        using var stream = File.Create(path);
        Screenshot.SaveAsPng(stream, Screenshot.Width, Screenshot.Height);
        LastSavedPath = path;
        return path;
    }
}

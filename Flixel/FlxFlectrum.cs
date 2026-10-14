using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

public enum FlxFlectrumDirection { Up, Down, Left, Right }
public enum FlxFlectrumMode { Meter, Peaks }

/// <summary>
/// Measures the loudness of a sound at different pitches while it plays (an FFT of the audio around the playing position) and
/// gives the result as numbers from 0 to 1: the data behind <see cref="FlxFlectrum"/>, usable for anything that should react to music.
/// <code>
/// var analyzer = new FlxSpectrumAnalyzer(music);
/// float[] bands = new float[16];
/// analyzer.GetBands(bands);       // each frame: bands[0] is the deepest bass, bands[15] the highest treble
/// </code>
/// </summary>
public sealed class FlxSpectrumAnalyzer
{
    const int Size = 1024;                 // FFT size: 43 Hz per bin at 44.1 kHz

    readonly FlxSound _sound;
    readonly float[] _re = new float[Size], _im = new float[Size], _window = new float[Size];
    readonly float[] _cos = new float[Size / 2], _sin = new float[Size / 2];
    byte[] _bytes = new byte[Size * 4];
    FlxAudioClip? _clip;
    FlxAudioReader? _reader;

    /// <summary>Loudest level that is shown as 1, and quietest as 0, in decibels.</summary>
    public float MaxDb = 0f, MinDb = -60f;

    /// <summary>Lowest and highest pitch (Hz) of the bands.</summary>
    public float LowHz = 40f, HighHz = 14000f;

    public FlxSpectrumAnalyzer(FlxSound sound)
    {
        _sound = sound;
        for (int i = 0; i < Size; i++) _window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (Size - 1));   // Hann
        for (int i = 0; i < Size / 2; i++)
        {
            _cos[i] = MathF.Cos(-2 * MathF.PI * i / Size);
            _sin[i] = MathF.Sin(-2 * MathF.PI * i / Size);
        }
    }

    /// <summary>
    /// Fills <paramref name="bands"/> (any length) with the levels, low pitches first. Returns false (and zeros) while the sound is not playing.
    /// </summary>
    public bool GetBands(float[] bands)
    {
        Array.Clear(bands);
        var clip = _sound.Clip;
        if (clip == null || !_sound.Playing) return false;

        if (!ReferenceEquals(clip, _clip))
        {
            _reader?.Dispose();
            _clip = clip;
            _reader = clip.OpenReader();
            _bytes = new byte[Size * clip.BlockAlign];
        }

        // read the audio at the playing position
        long target = (long)(_sound.Time * clip.SampleRate / 1000.0);
        long ahead = target - _reader!.Position;
        if (ahead < 0 || ahead > 8192) _reader.Seek(target);
        else if (ahead > 0) SkipFrames((int)ahead);

        int frames = _reader.Read(_bytes, Size);
        if (frames <= 0) return false;

        int channels = clip.Channels;
        for (int i = 0; i < Size; i++)
        {
            float sum = 0;
            if (i < frames)
                for (int c = 0; c < channels; c++)
                    sum += (short)(_bytes[(i * channels + c) * 2] | (_bytes[(i * channels + c) * 2 + 1] << 8)) / 32768f;
            _re[i] = sum / channels * _window[i];
            _im[i] = 0;
        }

        Fft();

        // log spaced bands between LowHz and HighHz, each showing its loudest bin
        float binHz = clip.SampleRate / (float)Size;
        float high = Math.Min(HighHz, clip.SampleRate / 2f - binHz);
        for (int b = 0; b < bands.Length; b++)
        {
            float f0 = LowHz * MathF.Pow(high / LowHz, b / (float)bands.Length);
            float f1 = LowHz * MathF.Pow(high / LowHz, (b + 1) / (float)bands.Length);
            int i0 = Math.Clamp((int)MathF.Floor(f0 / binHz), 1, Size / 2 - 1);
            int i1 = Math.Clamp((int)MathF.Ceiling(f1 / binHz), i0 + 1, Size / 2);

            float peak = 0;
            for (int i = i0; i < i1; i++)
                peak = MathF.Max(peak, MathF.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]) / (Size / 4f));   // a full scale sine reads 1

            float db = 20f * MathF.Log10(MathF.Max(peak, 1e-6f));
            bands[b] = MathHelper.Clamp((db - MinDb) / (MaxDb - MinDb), 0f, 1f);
        }
        return true;
    }

    void SkipFrames(int frames)
    {
        var scratch = new byte[Math.Min(frames, 4096) * _clip!.BlockAlign];
        while (frames > 0)
        {
            int got = _reader!.Read(scratch, Math.Min(frames, 4096));
            if (got <= 0) break;
            frames -= got;
        }
    }

    void Fft()
    {
        // bit reversal
        for (int i = 1, j = 0; i < Size; i++)
        {
            int bit = Size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (_re[i], _re[j]) = (_re[j], _re[i]);
                (_im[i], _im[j]) = (_im[j], _im[i]);
            }
        }

        for (int len = 2; len <= Size; len <<= 1)
        {
            int half = len / 2, step = Size / len;
            for (int i = 0; i < Size; i += len)
                for (int k = 0; k < half; k++)
                {
                    float wr = _cos[k * step], wi = _sin[k * step];
                    int a = i + k, b = a + half;
                    float tr = _re[b] * wr - _im[b] * wi, ti = _re[b] * wi + _im[b] * wr;
                    _re[b] = _re[a] - tr; _im[b] = _im[a] - ti;
                    _re[a] += tr; _im[a] += ti;
                }
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _reader = null;
    }
}

/// <summary>
/// A spectrum meter (the equalizer bars of an old hi-fi) that shows the sound that is playing: a grid of columns and rows of small
/// lights, each column one pitch band. Port of Richard Davey's (Photon Storm) Flixel Power Tools <c>FlxFlectrum</c> (see
/// NOTICE-flixel-power-tools.txt), which used the Neoart Flectrum library; this one analyses the audio itself (see <see cref="FlxSpectrumAnalyzer"/>).
/// <code>
/// var music = FlxG.Sound.PlayMusic("music/theme.ogg");
/// Add(new FlxFlectrum(10, 10, music, columns: 16, columnSize: 8, columnSpacing: 2, rows: 24, rowSize: 3, rowSpacing: 1));
/// </code>
/// It is a sprite (position it, scale it, tint it). Colors run from top to bottom through <see cref="Colors"/> (red at the top and green at the
/// bottom by default). With <see cref="FlxFlectrumMode.Peaks"/> a light stays up at the highest level a column reached and falls slowly.
/// Differences from the original: no bitmap meters, and no background beat.
/// </summary>
public class FlxFlectrum : FlxSprite
{
    readonly FlxSpectrumAnalyzer _analyzer;
    readonly FxBitmap _canvas;
    readonly Texture2D _texture;
    readonly float[] _levels, _peaks, _raw;
    Color[] _rowColors;

    public int Columns { get; }
    public int Rows { get; }
    public int ColumnSize, ColumnSpacing, RowSize, RowSpacing;

    /// <summary>Which way the bars grow.</summary>
    public FlxFlectrumDirection Direction;

    public FlxFlectrumMode Mode = FlxFlectrumMode.Peaks;

    /// <summary>Show the lights that are off, faintly.</summary>
    public bool ShowBackground = true;
    public float BackgroundAlpha = 0.2f;

    /// <summary>How fast a bar falls (full height per second) and how fast a peak light falls.</summary>
    public float FallSpeed = 1.6f, PeakFallSpeed = 0.35f;

    /// <summary>The analyzer behind the meter: change its range or pitches.</summary>
    public FlxSpectrumAnalyzer Analyzer => _analyzer;

    public Color[] Colors
    {
        get => _stops;
        set { _stops = value; _rowColors = FlxGradient.CreatePixels(1, Rows, value); }
    }

    Color[] _stops = { new(0xff, 0x39, 0x39), new(0xff, 0xb3, 0x20), new(0xff, 0xf8, 0x20), new(0x50, 0xd0, 0x20) };

    public FlxFlectrum(float x, float y, FlxSound sound, int columns = 15, int columnSize = 10, int columnSpacing = 0, int rows = 32, int rowSize = 3, int rowSpacing = 0,
        FlxFlectrumDirection direction = FlxFlectrumDirection.Up) : base(x, y)
    {
        Columns = columns;
        Rows = rows;
        ColumnSize = columnSize;
        ColumnSpacing = columnSpacing;
        RowSize = rowSize;
        RowSpacing = rowSpacing;
        Direction = direction;
        _analyzer = new FlxSpectrumAnalyzer(sound);
        _levels = new float[columns];
        _peaks = new float[columns];
        _raw = new float[columns];
        _rowColors = FlxGradient.CreatePixels(1, rows, _stops);

        bool horizontal = direction is FlxFlectrumDirection.Left or FlxFlectrumDirection.Right;
        int along = columns * (columnSize + columnSpacing) - columnSpacing;      // the length of the row of columns
        int up = rows * (rowSize + rowSpacing) - rowSpacing;                      // how tall a bar can be
        _canvas = new FxBitmap(horizontal ? up : along, horizontal ? along : up);
        _texture = new Texture2D(FlxG.GraphicsDevice, _canvas.Width, _canvas.Height);
        LoadGraphic(_texture);
        Solid = false;
        Moves = false;
        Redraw();
    }

    public override void Update(float elapsed)
    {
        _analyzer.GetBands(_raw);

        for (int c = 0; c < Columns; c++)
        {
            // rise at once, fall smoothly
            _levels[c] = _raw[c] >= _levels[c] ? _raw[c] : MathF.Max(_raw[c], _levels[c] - FallSpeed * elapsed);
            _peaks[c] = _levels[c] >= _peaks[c] ? _levels[c] : MathF.Max(_levels[c], _peaks[c] - PeakFallSpeed * elapsed);
        }

        Redraw();
        base.Update(elapsed);
    }

    void Redraw()
    {
        _canvas.Clear(Color.Transparent);
        for (int c = 0; c < Columns; c++)
        {
            int lit = (int)MathF.Round(_levels[c] * Rows);
            int peak = Math.Clamp((int)MathF.Round(_peaks[c] * Rows) - 1, 0, Rows - 1);
            for (int r = 0; r < Rows; r++)
            {
                var color = _rowColors[Rows - 1 - r];     // gradient runs top to bottom, rows count up from the base
                bool on = r < lit || (Mode == FlxFlectrumMode.Peaks && r == peak && _peaks[c] > 0.02f);
                if (!on && !ShowBackground) continue;
                _canvas.FillRect(Cell(c, r), on ? color : color * BackgroundAlpha);
            }
        }
        _texture.SetData(_canvas.Data);
    }

    Rectangle Cell(int column, int row)
    {
        int along = column * (ColumnSize + ColumnSpacing);
        int rise = row * (RowSize + RowSpacing);
        int w = _canvas.Width, h = _canvas.Height;
        return Direction switch
        {
            FlxFlectrumDirection.Up => new Rectangle(along, h - rise - RowSize, ColumnSize, RowSize),
            FlxFlectrumDirection.Down => new Rectangle(along, rise, ColumnSize, RowSize),
            FlxFlectrumDirection.Right => new Rectangle(rise, along, RowSize, ColumnSize),
            _ => new Rectangle(w - rise - RowSize, along, RowSize, ColumnSize),
        };
    }

    public override void Destroy()
    {
        _analyzer.Dispose();
        _texture.Dispose();
        base.Destroy();
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>Which kind of noise to use.</summary>
public enum FlxNoiseType
{
    /// <summary>Classic gradient noise: smooth, a little blocky along the grid axes.</summary>
    Perlin,
    /// <summary>Simplex noise: smoother and more even in every direction than Perlin, and cheaper in 3D.</summary>
    Simplex,
    /// <summary>A handful of sine waves with random frequencies, directions and phases: soft, regular looking waves.</summary>
    Sine,
    /// <summary>
    /// Fractal Brownian motion: layers of noise (FbmBase, default Simplex) with FbmOctaves octaves, optionally bent by itself (FbmWarp).
    /// Rich in detail at every scale, like real terrain and clouds. Set the FbmXxx properties of the FlxNoise to shape it.
    /// </summary>
    Fbm
}

/// <summary>How several layers (octaves) of noise are added up.</summary>
public enum FlxFractalMode
{
    /// <summary>Fractional Brownian motion: big smooth shapes with finer detail on top (default).</summary>
    Fbm,
    /// <summary>Sharp ridges where the noise crosses zero: mountain ranges, veins, winding tunnels.</summary>
    Ridged,
    /// <summary>Billowy, cloud-like shapes with creases.</summary>
    Turbulence
}

/// <summary>What shape the noise based cave takes.</summary>
public enum FlxNoiseCaveStyle
{
    /// <summary>Rounded caverns and rock masses.</summary>
    Blobs,
    /// <summary>Winding tunnels: the open space follows the lines where the noise is zero.</summary>
    Tunnels
}

/// <summary>
/// Smooth random numbers that change gradually from place to place: use them for terrain, caves, clouds, wobbling and wandering
/// movement. Every function returns about -1 to 1 and gives the same answer for the same input and seed.
/// <code>
/// var noise = new FlxNoise(1234);
/// float h = noise.Perlin(x * 0.05f, y * 0.05f);                        // one sample
/// float f = noise.Fractal(FlxNoiseType.Simplex, x * 0.02f, y * 0.02f, octaves: 5);  // layered, more detail
/// sprite.Velocity = noise.Wander(time);                                  // smooth random movement
/// float w = noise.RandomWalk(time);                                      // a random walk: drifts and wanders off
/// int[] dug = noise.GenerateWalkCave(200, 100);                          // a cave dug by a stumbling walker
/// int[] cave = noise.GenerateCave(200, 100, FlxNoiseType.Perlin);        // 1 = wall, 0 = open
/// </code>
/// Small input steps give small changes: multiply your coordinates by a small number (the "scale") to stretch features out.
/// </summary>
public sealed class FlxNoise
{
    // ---------------------------------------------------------------- tables

    readonly byte[] _perm = new byte[512];
    readonly byte[] _permMod12 = new byte[512];

    // the sine waves: amplitude, frequency, direction (as a unit vector) and phase of each
    const int MaxWaves = 8;
    readonly float[] _waveAmp = new float[MaxWaves], _waveFreq = new float[MaxWaves], _wavePhase = new float[MaxWaves];
    readonly Vector3[] _waveDir = new Vector3[MaxWaves];

    public int Seed { get; }

    // ---- settings for FlxNoiseType.Fbm

    FlxNoiseType _fbmBase = FlxNoiseType.Simplex;

    /// <summary>The noise the Fbm type layers up (Perlin, Simplex or Sine; default Simplex).</summary>
    public FlxNoiseType FbmBase
    {
        get => _fbmBase;
        set
        {
            if (value == FlxNoiseType.Fbm) throw new ArgumentException("Fbm is made of another noise: pick Perlin, Simplex or Sine.", nameof(value));
            _fbmBase = value;
        }
    }

    /// <summary>How many layers Fbm adds up (default 5): more means finer detail.</summary>
    public int FbmOctaves = 5;

    /// <summary>Each layer has this times the frequency of the one before (default 2).</summary>
    public float FbmLacunarity = 2f;

    /// <summary>Each layer has this times the strength of the one before (default 0.5): higher is rougher.</summary>
    public float FbmGain = 0.5f;

    /// <summary>How the layers are added: smooth (Fbm), Ridged or Turbulence.</summary>
    public FlxFractalMode FbmMode = FlxFractalMode.Fbm;

    /// <summary>
    /// Domain warping (2D and 3D): bends the place being sampled by another layer of the same noise, which turns smooth blobs into swirls,
    /// veins and folded rock. 0 = off (default); about 0.5 to 2 is a lot of warp.
    /// </summary>
    public float FbmWarp;

    // ---- the random walk table (made when first needed)
    const int WalkLength = 256;
    float[]? _walk;

    // 2D simplex corner gradients and the 12 3D gradient directions
    static readonly (float X, float Y)[] Grad2 = { (1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (0, 1), (0, -1) };
    static readonly (int X, int Y, int Z)[] Grad3 =
    {
        (1, 1, 0), (-1, 1, 0), (1, -1, 0), (-1, -1, 0), (1, 0, 1), (-1, 0, 1), (1, 0, -1), (-1, 0, -1), (0, 1, 1), (0, -1, 1), (0, 1, -1), (0, -1, -1)
    };

    // Bring each noise into the range -1..1 (measured over millions of samples).
    const float Perlin1DScale = 2f, Perlin2DScale = 1.0f, Perlin3DScale = 1.0f;
    const float Simplex1DScale = 0.395f, Simplex2DScale = 70f, Simplex3DScale = 32f;

    /// <param name="seed">Same seed, same noise. Leave out for a random one.</param>
    public FlxNoise(int? seed = null)
    {
        Seed = seed ?? Random.Shared.Next();
        var rnd = new Random(Seed);

        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;
        for (int i = 255; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++)
        {
            _perm[i] = p[i & 255];
            _permMod12[i] = (byte)(_perm[i] % 12);
        }

        // waves of rising, slightly irregular frequencies; lower ones are stronger
        float total = 0;
        for (int i = 0; i < MaxWaves; i++)
        {
            _waveFreq[i] = (1f + i * 1.37f) * (0.85f + (float)rnd.NextDouble() * 0.3f);
            _waveAmp[i] = 1f / MathF.Pow(i + 1, 0.7f);
            _wavePhase[i] = (float)(rnd.NextDouble() * Math.PI * 2);

            // a random direction on the sphere, so waves cross the plane (and time) at all sorts of angles
            double a = rnd.NextDouble() * Math.PI * 2, z = rnd.NextDouble() * 2 - 1, r = Math.Sqrt(1 - z * z);
            _waveDir[i] = new Vector3((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)), (float)z);
            total += _waveAmp[i];
        }
        for (int i = 0; i < MaxWaves; i++) _waveAmp[i] /= total;      // the sum can never leave -1..1
    }

    // ---------------------------------------------------------------- Perlin

    static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    static float Lerp(float a, float b, float t) => a + t * (b - a);

    /// <summary>1D Perlin noise, -1 to 1. It is 0 at every whole number.</summary>
    public float Perlin(float x)
    {
        int xi = (int)MathF.Floor(x);
        float xf = x - xi;
        xi &= 255;

        float g0 = (_perm[xi] & 1) == 0 ? xf : -xf;
        float g1 = (_perm[xi + 1] & 1) == 0 ? xf - 1 : -(xf - 1);
        return Lerp(g0, g1, Fade(xf)) * Perlin1DScale;
    }

    /// <summary>2D Perlin noise, -1 to 1. It is 0 at every whole number point.</summary>
    public float Perlin(float x, float y)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float xf = x - xi, yf = y - yi;
        xi &= 255; yi &= 255;

        float u = Fade(xf), v = Fade(yf);
        int a = _perm[xi] + yi, b = _perm[xi + 1] + yi;

        float n00 = Grad2D(_perm[a], xf, yf);
        float n10 = Grad2D(_perm[b], xf - 1, yf);
        float n01 = Grad2D(_perm[a + 1], xf, yf - 1);
        float n11 = Grad2D(_perm[b + 1], xf - 1, yf - 1);

        return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v) * Perlin2DScale;
    }

    static float Grad2D(int hash, float x, float y)
    {
        // eight directions: four diagonals and four straight
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }

    /// <summary>3D Perlin noise (Ken Perlin's improved version), -1 to 1. The third value is handy as time.</summary>
    public float Perlin(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float xf = x - xi, yf = y - yi, zf = z - zi;
        xi &= 255; yi &= 255; zi &= 255;

        float u = Fade(xf), v = Fade(yf), w = Fade(zf);
        int a = _perm[xi] + yi, aa = _perm[a] + zi, ab = _perm[a + 1] + zi;
        int b = _perm[xi + 1] + yi, ba = _perm[b] + zi, bb = _perm[b + 1] + zi;

        float x1 = Lerp(Grad3D(_perm[aa], xf, yf, zf), Grad3D(_perm[ba], xf - 1, yf, zf), u);
        float x2 = Lerp(Grad3D(_perm[ab], xf, yf - 1, zf), Grad3D(_perm[bb], xf - 1, yf - 1, zf), u);
        float y1 = Lerp(x1, x2, v);

        x1 = Lerp(Grad3D(_perm[aa + 1], xf, yf, zf - 1), Grad3D(_perm[ba + 1], xf - 1, yf, zf - 1), u);
        x2 = Lerp(Grad3D(_perm[ab + 1], xf, yf - 1, zf - 1), Grad3D(_perm[bb + 1], xf - 1, yf - 1, zf - 1), u);
        float y2 = Lerp(x1, x2, v);

        return Lerp(y1, y2, w) * Perlin3DScale;
    }

    static float Grad3D(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14) ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    // ---------------------------------------------------------------- Simplex (after Stefan Gustavson's reference implementation)

    /// <summary>1D simplex noise, -1 to 1.</summary>
    public float Simplex(float x)
    {
        int i0 = (int)MathF.Floor(x), i1 = i0 + 1;
        float x0 = x - i0, x1 = x0 - 1f;

        float t0 = 1f - x0 * x0; t0 *= t0;
        float n0 = t0 * t0 * Grad1D(_perm[i0 & 255], x0);

        float t1 = 1f - x1 * x1; t1 *= t1;
        float n1 = t1 * t1 * Grad1D(_perm[i1 & 255], x1);

        return (n0 + n1) * Simplex1DScale;
    }

    static float Grad1D(int hash, float x)
    {
        int h = hash & 15;
        float g = 1f + (h & 7);          // a gradient of 1..8
        if ((h & 8) != 0) g = -g;
        return g * x;
    }

    /// <summary>2D simplex noise, -1 to 1.</summary>
    public float Simplex(float x, float y)
    {
        const float F2 = 0.3660254037844386f, G2 = 0.21132486540518713f;

        // skew the input onto a grid of triangles, find which cell it is in
        float s = (x + y) * F2;
        int i = (int)MathF.Floor(x + s), j = (int)MathF.Floor(y + s);
        float t = (i + j) * G2;
        float x0 = x - (i - t), y0 = y - (j - t);

        int i1 = x0 > y0 ? 1 : 0, j1 = x0 > y0 ? 0 : 1;
        float x1 = x0 - i1 + G2, y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + 2f * G2, y2 = y0 - 1f + 2f * G2;

        int ii = i & 255, jj = j & 255;
        float n0 = Corner2D(x0, y0, _permMod12[ii + _perm[jj]]);
        float n1 = Corner2D(x1, y1, _permMod12[ii + i1 + _perm[jj + j1]]);
        float n2 = Corner2D(x2, y2, _permMod12[ii + 1 + _perm[jj + 1]]);

        return (n0 + n1 + n2) * Simplex2DScale;
    }

    static float Corner2D(float x, float y, int gradient)
    {
        float t = 0.5f - x * x - y * y;
        if (t < 0) return 0;
        t *= t;
        var g = Grad2[gradient];
        return t * t * (g.X * x + g.Y * y);
    }

    /// <summary>3D simplex noise, -1 to 1. The third value is handy as time.</summary>
    public float Simplex(float x, float y, float z)
    {
        const float F3 = 1f / 3f, G3 = 1f / 6f;

        float s = (x + y + z) * F3;
        int i = (int)MathF.Floor(x + s), j = (int)MathF.Floor(y + s), k = (int)MathF.Floor(z + s);
        float t = (i + j + k) * G3;
        float x0 = x - (i - t), y0 = y - (j - t), z0 = z - (k - t);

        // which of the six tetrahedra of the cell are we in
        int i1, j1, k1, i2, j2, k2;
        if (x0 >= y0)
        {
            if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
            else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
        }
        else
        {
            if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
        }

        float x1 = x0 - i1 + G3, y1 = y0 - j1 + G3, z1 = z0 - k1 + G3;
        float x2 = x0 - i2 + 2f * G3, y2 = y0 - j2 + 2f * G3, z2 = z0 - k2 + 2f * G3;
        float x3 = x0 - 1f + 3f * G3, y3 = y0 - 1f + 3f * G3, z3 = z0 - 1f + 3f * G3;

        int ii = i & 255, jj = j & 255, kk = k & 255;
        float n0 = Corner3D(x0, y0, z0, _permMod12[ii + _perm[jj + _perm[kk]]]);
        float n1 = Corner3D(x1, y1, z1, _permMod12[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]]);
        float n2 = Corner3D(x2, y2, z2, _permMod12[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]]);
        float n3 = Corner3D(x3, y3, z3, _permMod12[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]]);

        return (n0 + n1 + n2 + n3) * Simplex3DScale;
    }

    static float Corner3D(float x, float y, float z, int gradient)
    {
        float t = 0.6f - x * x - y * y - z * z;
        if (t < 0) return 0;
        t *= t;
        var g = Grad3[gradient];
        return t * t * (g.X * x + g.Y * y + g.Z * z);
    }

    // ---------------------------------------------------------------- sine waves

    /// <summary>
    /// Simple wave noise in 1D: a few sine waves added together, -1 to 1. Gentler and more regular than Perlin: good for
    /// bobbing, swaying, rolling hills. <paramref name="waves"/> (1 to 8) is how many waves are added: more is less regular.
    /// </summary>
    public float Sine(float x, int waves = 4)
    {
        waves = Math.Clamp(waves, 1, MaxWaves);
        float sum = 0, amp = 0;
        for (int i = 0; i < waves; i++)
        {
            sum += _waveAmp[i] * MathF.Sin(x * _waveFreq[i] + _wavePhase[i]);
            amp += _waveAmp[i];
        }
        return sum / amp;
    }

    /// <summary>Wave noise in 2D: waves that cross the plane in different directions.</summary>
    public float Sine(float x, float y, int waves = 4)
    {
        waves = Math.Clamp(waves, 1, MaxWaves);
        float sum = 0, amp = 0;
        for (int i = 0; i < waves; i++)
        {
            sum += _waveAmp[i] * MathF.Sin((x * _waveDir[i].X + y * _waveDir[i].Y) * _waveFreq[i] + _wavePhase[i]);
            amp += _waveAmp[i];
        }
        return sum / amp;
    }

    /// <summary>Wave noise in 3D (the third value is handy as time).</summary>
    public float Sine(float x, float y, float z, int waves = 4)
    {
        waves = Math.Clamp(waves, 1, MaxWaves);
        float sum = 0, amp = 0;
        for (int i = 0; i < waves; i++)
        {
            sum += _waveAmp[i] * MathF.Sin((x * _waveDir[i].X + y * _waveDir[i].Y + z * _waveDir[i].Z) * _waveFreq[i] + _wavePhase[i]);
            amp += _waveAmp[i];
        }
        return sum / amp;
    }

    // ---------------------------------------------------------------- choose the type at run time

    public float Get(FlxNoiseType type, float x) => type switch
    {
        FlxNoiseType.Simplex => Simplex(x),
        FlxNoiseType.Sine => Sine(x),
        FlxNoiseType.Fbm => Fractal(FlxNoiseType.Fbm, x, FbmOctaves, FbmLacunarity, FbmGain, FbmMode),
        _ => Perlin(x)
    };

    public float Get(FlxNoiseType type, float x, float y) => type switch
    {
        FlxNoiseType.Simplex => Simplex(x, y),
        FlxNoiseType.Sine => Sine(x, y),
        FlxNoiseType.Fbm => Fractal(FlxNoiseType.Fbm, x, y, FbmOctaves, FbmLacunarity, FbmGain, FbmMode),
        _ => Perlin(x, y)
    };

    public float Get(FlxNoiseType type, float x, float y, float z) => type switch
    {
        FlxNoiseType.Simplex => Simplex(x, y, z),
        FlxNoiseType.Sine => Sine(x, y, z),
        FlxNoiseType.Fbm => Fractal(FlxNoiseType.Fbm, x, y, z, FbmOctaves, FbmLacunarity, FbmGain, FbmMode),
        _ => Perlin(x, y, z)
    };

    // ---------------------------------------------------------------- fractal noise

    /// <summary>
    /// Several layers of noise added up: each octave has <paramref name="lacunarity"/> times the frequency and
    /// <paramref name="gain"/> times the strength of the one before. The result is about -1 to 1. With type Fbm the layers are made of
    /// FbmBase (and FbmWarp bends the coordinates in 2D and 3D); the octave settings you pass are used, not the FbmXxx ones.
    /// </summary>
    public float Fractal(FlxNoiseType type, float x, int octaves = 4, float lacunarity = 2f, float gain = 0.5f, FlxFractalMode mode = FlxFractalMode.Fbm)
    {
        if (type == FlxNoiseType.Fbm) type = _fbmBase;
        return Layers(octaves, lacunarity, gain, mode, (f, o) => Get(type, x * f + o));
    }

    public float Fractal(FlxNoiseType type, float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f, FlxFractalMode mode = FlxFractalMode.Fbm)
    {
        if (type == FlxNoiseType.Fbm)
        {
            type = _fbmBase;
            if (FbmWarp != 0)
            {
                // domain warping: two more layers of the noise decide how far to push the place we look at
                float wx = Layers(octaves, lacunarity, gain, FlxFractalMode.Fbm, (f, o) => Get(type, (x + 5.2f) * f + o, (y + 1.3f) * f + o));
                float wy = Layers(octaves, lacunarity, gain, FlxFractalMode.Fbm, (f, o) => Get(type, (x + 9.7f) * f + o, (y + 3.1f) * f + o));
                x += FbmWarp * wx;
                y += FbmWarp * wy;
            }
        }
        return Layers(octaves, lacunarity, gain, mode, (f, o) => Get(type, x * f + o, y * f + o));
    }

    public float Fractal(FlxNoiseType type, float x, float y, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.5f, FlxFractalMode mode = FlxFractalMode.Fbm)
    {
        if (type == FlxNoiseType.Fbm)
        {
            type = _fbmBase;
            if (FbmWarp != 0)
            {
                float wx = Layers(octaves, lacunarity, gain, FlxFractalMode.Fbm, (f, o) => Get(type, (x + 5.2f) * f + o, (y + 1.3f) * f + o, (z + 7.9f) * f + o));
                float wy = Layers(octaves, lacunarity, gain, FlxFractalMode.Fbm, (f, o) => Get(type, (x + 9.7f) * f + o, (y + 3.1f) * f + o, (z + 2.4f) * f + o));
                float wz = Layers(octaves, lacunarity, gain, FlxFractalMode.Fbm, (f, o) => Get(type, (x + 3.3f) * f + o, (y + 8.6f) * f + o, (z + 6.1f) * f + o));
                x += FbmWarp * wx; y += FbmWarp * wy; z += FbmWarp * wz;
            }
        }
        return Layers(octaves, lacunarity, gain, mode, (f, o) => Get(type, x * f + o, y * f + o, z * f + o));
    }

    /// <summary>
    /// Adds up the octaves. <paramref name="sample"/> gets the octave's frequency and a shift for that octave (the first has none):
    /// without the shifts every octave would be zero at the same grid points, which leaves visible flat spots.
    /// </summary>
    static float Layers(int octaves, float lacunarity, float gain, FlxFractalMode mode, Func<float, float, float> sample)
    {
        octaves = Math.Max(1, octaves);
        float sum = 0, amp = 1, total = 0, freq = 1;

        for (int i = 0; i < octaves; i++)
        {
            float n = Math.Clamp(sample(freq, i * 31.7f), -1f, 1f);
            sum += amp * mode switch
            {
                FlxFractalMode.Ridged => 1f - MathF.Abs(n),           // 0..1, 1 on the ridge
                FlxFractalMode.Turbulence => MathF.Abs(n),            // 0..1
                _ => n                                                // -1..1
            };
            total += amp;
            amp *= gain;
            freq *= lacunarity;
        }

        float v = sum / total;
        return mode == FlxFractalMode.Fbm ? v : v * 2f - 1f;          // bring 0..1 modes to -1..1 too
    }

    // ---------------------------------------------------------------- maps and lines

    /// <summary>
    /// A grid of noise values from 0 to 1, row by row (index = y * width + x). <paramref name="scale"/> is how much of the noise one
    /// cell covers: 0.05 gives features about 20 cells big. <paramref name="offsetX"/> / <paramref name="offsetY"/> move the window over the noise.
    /// </summary>
    public float[] GenerateMap(int width, int height, FlxNoiseType type = FlxNoiseType.Perlin, float scale = 0.05f, int octaves = 4,
        float lacunarity = 2f, float gain = 0.5f, FlxFractalMode mode = FlxFractalMode.Fbm, float offsetX = 0, float offsetY = 0)
    {
        var map = new float[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[y * width + x] = Math.Clamp(Fractal(type, offsetX + x * scale, offsetY + y * scale, octaves, lacunarity, gain, mode) * 0.5f + 0.5f, 0f, 1f);
        return map;
    }

    /// <summary>
    /// A line of noise values from 0 to 1: heights for a side-scrolling landscape (index = x).
    /// </summary>
    public float[] GenerateLine(int count, FlxNoiseType type = FlxNoiseType.Perlin, float scale = 0.05f, int octaves = 4, float offset = 0,
        FlxFractalMode mode = FlxFractalMode.Fbm)
    {
        var line = new float[count];
        for (int x = 0; x < count; x++)
            line[x] = Math.Clamp(Fractal(type, offset + x * scale, octaves, 2f, 0.5f, mode) * 0.5f + 0.5f, 0f, 1f);
        return line;
    }

    /// <summary>
    /// A cave as 1 = wall, 0 = open, row by row, ready for FlxTilemap.LoadMapFromArray (like FlxCaveGenerator).
    /// <paramref name="wallRatio"/> is the share of cells that become wall, chosen exactly (not just "above some value"), so every
    /// seed gives the cave the same openness. <paramref name="scale"/> sets the size of the features: 0.05 means caverns about
    /// 20 tiles wide. Tunnels follow the lines where the noise is zero; blobs are the rounded high and low areas.
    /// <paramref name="widen"/> then makes the open space bigger that many steps (see <see cref="Widen"/>): useful for tunnels, which are only a tile wide
    /// at high wall ratios. The wall share is exact only with widen 0.
    /// </summary>
    public int[] GenerateCave(int columns, int rows, FlxNoiseType type = FlxNoiseType.Perlin, float scale = 0.05f, int octaves = 4,
        float wallRatio = 0.5f, FlxNoiseCaveStyle style = FlxNoiseCaveStyle.Blobs, int widen = 0, float offsetX = 0, float offsetY = 0)
    {
        int n = columns * rows;
        var value = new float[n];
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                float v = Fractal(type, offsetX + x * scale, offsetY + y * scale, octaves);
                value[y * columns + x] = style == FlxNoiseCaveStyle.Tunnels ? MathF.Abs(v) : v;
            }

        wallRatio = Math.Clamp(wallRatio, 0f, 1f);
        var sorted = (float[])value.Clone();
        Array.Sort(sorted);

        var cave = new int[n];
        if (style == FlxNoiseCaveStyle.Blobs)
        {
            // the highest wallRatio share of values is rock
            float threshold = sorted[Math.Clamp((int)((1f - wallRatio) * n), 0, n - 1)];
            for (int i = 0; i < n; i++) cave[i] = value[i] > threshold ? 1 : 0;
        }
        else
        {
            // open where the noise is closest to zero: the lowest (1 - wallRatio) share of |noise|
            float threshold = sorted[Math.Clamp((int)((1f - wallRatio) * n), 0, n - 1)];
            for (int i = 0; i < n; i++) cave[i] = value[i] < threshold ? 0 : 1;
        }

        if (widen > 0) Widen(cave, columns, rows, widen);
        return cave;
    }

    /// <summary>
    /// Makes the open space of a cave (value 0) bigger: every wall next to an open cell (up, down, left or right) opens up, <paramref name="steps"/> times.
    /// Thin tunnels become wide enough to walk through and diagonal gaps get joined. Changes the array in place.
    /// </summary>
    public static void Widen(int[] cave, int columns, int rows, int steps = 1)
    {
        var copy = new int[cave.Length];
        for (int s = 0; s < steps; s++)
        {
            Array.Copy(cave, copy, cave.Length);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int i = y * columns + x;
                    if (copy[i] == 0) continue;
                    if ((x > 0 && copy[i - 1] == 0) || (x < columns - 1 && copy[i + 1] == 0) ||
                        (y > 0 && copy[i - columns] == 0) || (y < rows - 1 && copy[i + columns] == 0))
                        cave[i] = 0;
                }
        }
    }

    // ---------------------------------------------------------------- movement

    /// <summary>
    /// A smooth, never repeating, wandering offset over time: add it to a position for drifting, floating or flying about.
    /// <paramref name="frequency"/> is how fast it changes (1 = about one turn a second), <paramref name="amplitude"/> how far it goes.
    /// Different <paramref name="id"/>s give different paths from the same noise.
    /// </summary>
    public Vector2 Wander(float time, float frequency = 1f, float amplitude = 1f, int id = 0, FlxNoiseType type = FlxNoiseType.Simplex)
    {
        float t = time * frequency;
        float o = id * 37.31f;
        return new Vector2(Get(type, t, o + 11.7f), Get(type, t, o + 83.9f)) * amplitude;
    }

    /// <summary>A smooth changing number over time, -amplitude to amplitude: flicker, sway, bobbing.</summary>
    public float Smooth(float time, float frequency = 1f, float amplitude = 1f, int id = 0, FlxNoiseType type = FlxNoiseType.Perlin) =>
        Get(type, time * frequency, id * 37.31f + 5.3f) * amplitude;

    // ---------------------------------------------------------------- random walk

    float[] WalkTable()
    {
        if (_walk != null) return _walk;

        // a Brownian path: each step moves a random amount up or down; then squeezed into -1..1
        var rnd = new Random(unchecked(Seed * 7919 + 101));
        var w = new float[WalkLength];
        float v = 0, min = 0, max = 0;
        for (int i = 0; i < WalkLength; i++)
        {
            v += (float)(rnd.NextDouble() * 2 - 1);
            w[i] = v;
            min = MathF.Min(min, v);
            max = MathF.Max(max, v);
        }

        float mid = (min + max) / 2f, half = MathF.Max(0.0001f, (max - min) / 2f);
        for (int i = 0; i < WalkLength; i++) w[i] = (w[i] - mid) / half;
        return _walk = w;
    }

    /// <summary>
    /// A random walk as a function of time: a path that drifts up and down by random steps, -1 to 1. Unlike Perlin or Simplex it wanders
    /// off and does not come back to an average: it can spend a long time high or low. One unit is one step; the walk is 256 steps long
    /// and bounces back from its ends (so it also never ends). Different <paramref name="id"/>s read different parts of it.
    /// </summary>
    public float RandomWalk(float t, int id = 0)
    {
        var w = WalkTable();
        float last = WalkLength - 1, period = 2 * last;

        float pos = (t + id * 911.3f) % period;            // fold the time back and forth along the table
        if (pos < 0) pos += period;
        if (pos > last) pos = period - pos;

        int i = (int)pos, j = Math.Min(i + 1, WalkLength - 1);
        float f = pos - i;
        f = f * f * (3f - 2f * f);                          // smooth between the steps
        return w[i] + (w[j] - w[i]) * f;
    }

    /// <summary>A wandering offset made of two random walks, like <see cref="Wander"/> but drifting further and less evenly.</summary>
    public Vector2 WalkWander(float time, float frequency = 1f, float amplitude = 1f, int id = 0) =>
        new Vector2(RandomWalk(time * frequency, id * 2), RandomWalk(time * frequency, id * 2 + 1)) * amplitude;

    /// <summary>
    /// A new random walk of <paramref name="count"/> steps, scaled to 0..1: the skyline of a side-scrolling landscape. With
    /// <paramref name="persistence"/> above 0 each step keeps some of the previous one, so the line is smoother and hills last longer.
    /// <paramref name="variant"/> picks another walk from the same seed.
    /// </summary>
    public float[] GenerateWalkLine(int count, float persistence = 0f, int variant = 0)
    {
        var rnd = new Random(unchecked(Seed * 31 + variant * 7919 + 5));
        var line = new float[count];
        float v = 0, step = 0, min = 0, max = 0;
        persistence = Math.Clamp(persistence, 0f, 0.98f);

        for (int i = 0; i < count; i++)
        {
            step = step * persistence + (float)(rnd.NextDouble() * 2 - 1) * (1f - persistence);
            v += step;
            line[i] = v;
            min = MathF.Min(min, v);
            max = MathF.Max(max, v);
        }

        float range = MathF.Max(0.0001f, max - min);
        for (int i = 0; i < count; i++) line[i] = (line[i] - min) / range;
        return line;
    }

    /// <summary>
    /// The points of a random walk in 2D (starting at 0, 0): Brownian movement. <paramref name="persistence"/> (0 to 1) is how much the
    /// heading is kept from step to step: 0 is a jittery cloud of points, 0.9 a path that curves slowly. Feed it to FlxObject.FollowPath.
    /// </summary>
    public Vector2[] GenerateWalkPath(int steps, float stepLength = 1f, float persistence = 0.5f, int variant = 0)
    {
        var rnd = new Random(unchecked(Seed * 131 + variant * 7919 + 9));
        var path = new Vector2[Math.Max(1, steps)];
        persistence = Math.Clamp(persistence, 0f, 1f);

        float heading = (float)(rnd.NextDouble() * Math.PI * 2);
        for (int i = 1; i < path.Length; i++)
        {
            heading += (float)((rnd.NextDouble() * 2 - 1) * Math.PI * (1f - persistence));
            path[i] = path[i - 1] + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * stepLength;
        }
        return path;
    }

    /// <summary>
    /// A cave dug by drunkards: a walker starts in the middle and stumbles around, opening every cell it passes, until
    /// <paramref name="openRatio"/> of the map is open. Because it is one walker's trail, the whole cave is connected.
    /// </summary>
    /// <param name="persistence">How much of its heading the walker keeps at every step (0 to 1): 0 turns randomly each step and digs a clump,
    /// 0.8 curves about and spreads out, 0.95 digs long winding tunnels.</param>
    /// <param name="brush">Radius of the hole dug at each step (0 = one cell, 1 = 3x3, 2 = a round 5x5...).</param>
    /// <param name="branchEvery">After this many steps the walker jumps to a random spot it already opened and digs on from there (branches).</param>
    /// <param name="variant">Another cave from the same seed.</param>
    /// <returns>1 = wall, 0 = open, row by row; the border is always wall.</returns>
    public int[] GenerateWalkCave(int columns, int rows, float openRatio = 0.45f, float persistence = 0.8f, int brush = 1, int branchEvery = 150, int variant = 0)
    {
        var rnd = new Random(unchecked(Seed * 31 + variant * 7919 + 17));
        var cave = new int[Math.Max(0, columns * rows)];
        Array.Fill(cave, 1);
        if (columns < 3 || rows < 3) return cave;           // no room for anything but the border

        int target = (int)(Math.Clamp(openRatio, 0f, 0.9f) * (columns - 2) * (rows - 2));
        int open = 0, sinceBranch = 0;
        var dug = new List<int>();
        persistence = Math.Clamp(persistence, 0f, 1f);

        // a brush that doesn't fit inside the border would leave the walker nowhere to stand
        brush = Math.Clamp(brush, 0, (Math.Min(columns, rows) - 3) / 2);
        int minX = 1 + brush, maxX = columns - 2 - brush, minY = 1 + brush, maxY = rows - 2 - brush;

        void Dig(int cx, int cy)
        {
            for (int oy = -brush; oy <= brush; oy++)
                for (int ox = -brush; ox <= brush; ox++)
                {
                    if (ox * ox + oy * oy > brush * brush + 1) continue;
                    int x = cx + ox, y = cy + oy;
                    if (x < 1 || y < 1 || x > columns - 2 || y > rows - 2) continue;       // never the border

                    int idx = y * columns + x;
                    if (cave[idx] == 0) continue;
                    cave[idx] = 0;
                    open++;
                    dug.Add(idx);
                }
        }

        float fx = Math.Clamp(columns / 2f, minX, maxX), fy = Math.Clamp(rows / 2f, minY, maxY);
        float heading = (float)(rnd.NextDouble() * Math.PI * 2), turn = 0;
        int cellX = (int)MathF.Round(fx), cellY = (int)MathF.Round(fy);
        Dig(cellX, cellY);

        long maxSteps = (long)target * 60 + 1000;
        for (long step = 0; open < target && step < maxSteps; step++)
        {
            // steer: the turning itself changes gradually (inertia), so tunnels curve smoothly instead of zigzagging;
            // the random push is small when the walker holds its heading well
            turn = turn * 0.9f + (float)((rnd.NextDouble() * 2 - 1) * Math.PI * 0.25 * (1.0 - persistence));
            heading += turn;
            fx += MathF.Cos(heading);
            fy += MathF.Sin(heading);

            if (fx < minX || fx > maxX || fy < minY || fy > maxY)
            {
                // the edge: put the walker back inside and send it back towards the middle (with some wobble)
                fx = Math.Clamp(fx, minX, maxX);
                fy = Math.Clamp(fy, minY, maxY);
                heading = MathF.Atan2(rows / 2f - fy, columns / 2f - fx) + (float)((rnd.NextDouble() * 2 - 1) * Math.PI / 3);
            }

            int nx = (int)MathF.Round(fx), ny = (int)MathF.Round(fy);
            if (nx != cellX && ny != cellY) Dig(nx, cellY);        // a diagonal step also opens a side cell, so the trail stays connected
            Dig(nx, ny);
            cellX = nx; cellY = ny;

            if (++sinceBranch >= branchEvery && dug.Count > 0)
            {
                int from = dug[rnd.Next(dug.Count)];
                fx = Math.Clamp(from % columns, minX, maxX);
                fy = Math.Clamp(from / columns, minY, maxY);
                cellX = (int)MathF.Round(fx); cellY = (int)MathF.Round(fy);
                heading = (float)(rnd.NextDouble() * Math.PI * 2);
                turn = 0;
                sinceBranch = 0;
            }
        }
        return cave;
    }
}

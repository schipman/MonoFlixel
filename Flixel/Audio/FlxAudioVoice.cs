using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MonoFlixel;

/// <summary>
/// Plays a queue of sample buffers. A sound feeds its voice a little ahead of time, which is what makes seeking, loop points and exact
/// playback positions possible. There is a real one (the sound card) and a virtual one (a silent stand-in that "plays" in real time).
/// </summary>
interface IFlxVoice : IDisposable
{
    /// <summary>Buffers that were submitted and have not finished playing.</summary>
    int PendingBuffers { get; }

    void Submit(byte[] data, int length);
    void Play();
    void Pause();
    void Resume();

    /// <summary>Stops and throws away everything that was queued.</summary>
    void Stop();

    /// <summary>Called every frame with the real time passed (the virtual voice uses it to "play").</summary>
    void Update(double seconds);

    float Volume { get; set; }
    float Pan { get; set; }

    /// <summary>Playback speed as a multiplier (1 = normal, 2 = an octave up, 0.5 = an octave down).</summary>
    float Pitch { get; set; }
}

/// <summary>A voice on the sound card (a streaming instance fed by buffers).</summary>
sealed class FlxDeviceVoice : IFlxVoice
{
    readonly DynamicSoundEffectInstance _instance;
    float _pitch = 1f;

    public FlxDeviceVoice(int sampleRate, int channels)
    {
        _instance = new DynamicSoundEffectInstance(sampleRate, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
    }

    public int PendingBuffers => _instance.PendingBufferCount;

    public void Submit(byte[] data, int length) => _instance.SubmitBuffer(data, 0, length);
    public void Play() => _instance.Play();
    public void Pause() => _instance.Pause();
    public void Resume() => _instance.Resume();
    public void Stop() => _instance.Stop();
    public void Update(double seconds) { }

    public float Volume { get => _instance.Volume; set => _instance.Volume = MathHelper.Clamp(value, 0f, 1f); }
    public float Pan { get => _instance.Pan; set => _instance.Pan = MathHelper.Clamp(value, -1f, 1f); }

    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = MathHelper.Clamp(value, 0.5f, 2f);                        // the sound card allows an octave either way
            _instance.Pitch = MathF.Log2(_pitch);
        }
    }

    public void Dispose()
    {
        _instance.Stop();
        _instance.Dispose();
    }
}

/// <summary>
/// A voice that makes no noise: it takes buffers and "plays" them in real time, so the sound logic (positions, loops, callbacks) behaves
/// exactly as with a sound card. Used when there is no audio device, when FlxG.Sound.Silent is on, and in tests.
/// </summary>
sealed class FlxVirtualVoice : IFlxVoice
{
    readonly int _rate, _blockAlign;
    readonly Queue<int> _frames = new();       // frames of every queued buffer
    double _playedInHead;                      // frames of the first buffer already played
    bool _playing;

    public FlxVirtualVoice(int sampleRate, int channels)
    {
        _rate = sampleRate;
        _blockAlign = channels * 2;
    }

    public int PendingBuffers => _frames.Count;

    public void Submit(byte[] data, int length) => _frames.Enqueue(length / _blockAlign);
    public void Play() => _playing = true;
    public void Pause() => _playing = false;
    public void Resume() => _playing = true;

    public void Stop()
    {
        _playing = false;
        _frames.Clear();
        _playedInHead = 0;
    }

    public void Update(double seconds)
    {
        if (!_playing) return;

        double budget = seconds * _rate * Pitch;
        while (budget > 0 && _frames.Count > 0)
        {
            double left = _frames.Peek() - _playedInHead;
            if (budget >= left)
            {
                budget -= left;
                _frames.Dequeue();
                _playedInHead = 0;
            }
            else
            {
                _playedInHead += budget;
                budget = 0;
            }
        }
    }

    public float Volume { get; set; } = 1f;
    public float Pan { get; set; }
    public float Pitch { get; set; } = 1f;

    public void Dispose() { }
}

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>
/// One sound: a clip with its own position, volume, pan and pitch. Port of Flixel's FlxSound. Get them from FlxG.Sound (they are pooled):
/// <code>
/// var engine = FlxG.Sound.Load("sound/jet.ogg", volume: 0.8f, looped: true).Proximity(x, y, player, radius: 300);
/// engine.Play();
/// engine.FadeOut(2f);
/// FlxG.Sound.Play("sound/hit.ogg", onComplete: () => score++);
/// </code>
/// Behind it is a streaming voice that is fed a little ahead of time, which is what makes <see cref="Time"/> seeking, loop points
/// (<see cref="LoopTime"/>, <see cref="EndTime"/>) and exact callbacks possible. Without an audio device it all still works, silently.
/// </summary>
public sealed class FlxSound : FlxBasic
{
    const int QueueDepth = 4;                 // buffers kept queued ahead
    const double ChunkSeconds = 0.06;         // length of each buffer

    struct Chunk
    {
        public long Start;          // first frame of this buffer in the clip
        public int Frames;
        public bool EndsPass;       // the last buffer of one pass through the sound (a loop or the end)
    }

    FlxAudioClip? _clip;
    FlxAudioReader? _reader;
    IFlxVoice? _voice;
    int _voiceRate, _voiceChannels;
    bool _voiceVirtual;

    readonly Queue<Chunk> _queue = new();
    bool _playing, _paused, _endReached;
    long _seekFrame;
    int _passesQueued;
    double _clock, _headStart;       // real seconds, only counting while playing

    float _volume = 1f, _volumeAdjust = 1f, _pan, _pitch = 1f;
    FlxSoundGroup? _group;

    // proximity
    FlxObject? _target;
    float _radius;
    bool _proximityPan;

    // fade
    bool _fading;
    float _fadeFrom, _fadeTo, _fadeDuration, _fadeTime;
    Action<FlxSound>? _fadeDone;

    /// <summary>Where the sound is, for proximity (see <see cref="Proximity"/>).</summary>
    public float X, Y;

    /// <summary>Keep playing when the state changes (default false: sounds stop on a state switch; music and one-shot effects set it).</summary>
    public bool Persist;

    /// <summary>When the sound ends (or Stop is called) it is recycled for the next FlxG.Sound call.</summary>
    public bool AutoDestroy;

    /// <summary>Start again when the end is reached.</summary>
    public bool Looped;

    /// <summary>Number of times the sound has looped since it was played.</summary>
    public int LoopCount { get; private set; }

    /// <summary>If not -1, only loop this many times.</summary>
    public int LoopUntil = -1;

    /// <summary>Where a looping sound starts over, in milliseconds (default 0: the beginning). Lets an intro play once and the rest repeat.</summary>
    public double LoopTime;

    /// <summary>Where the sound ends (or loops), in milliseconds; null plays to the end of the clip.</summary>
    public double? EndTime;

    /// <summary>Called when the sound reaches its end. A looping sound calls it every time it starts over, like Flixel.</summary>
    public Action? OnComplete;

    public string Name => _clip?.Name ?? "";

    /// <summary>True while the sound is playing (a paused sound is not playing).</summary>
    public bool Playing => _playing && !_paused;

    public bool Paused => _paused;

    /// <summary>Length of the clip in milliseconds.</summary>
    public double Length => _clip?.LengthMs ?? 0;

    public FlxSound()
    {
        Exists = false;      // not loaded yet
        Alive = false;
    }

    // ------------------------------------------------------------------ properties

    /// <summary>The sound's own volume, 0 to 1 (the group's and FlxG.Sound's volumes and proximity come on top).</summary>
    public float Volume
    {
        get => _volume;
        set { _volume = MathHelper.Clamp(value, 0f, 1f); ApplyTransform(); }
    }

    /// <summary>Left (-1) to right (+1).</summary>
    public float Pan
    {
        get => _pan;
        set { _pan = MathHelper.Clamp(value, -1f, 1f); ApplyTransform(); }
    }

    /// <summary>Speed and pitch: 1 is normal, 2 is an octave up and twice as fast. The sound card allows 0.5 to 2.</summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = MathHelper.Clamp(value, 0.5f, 2f);
            if (_voice != null) _voice.Pitch = _pitch;
        }
    }

    /// <summary>The group this sound belongs to (changes its volume together with the others).</summary>
    public FlxSoundGroup? Group => _group;

    internal void SetGroup(FlxSoundGroup? group) { _group = group; ApplyTransform(); }

    /// <summary>The volume that is really used: own volume, proximity, group, and the global volume.</summary>
    public float ActualVolume => _volume * _volumeAdjust;

    /// <summary>The position in the sound in milliseconds (0 when stopped). Set it to jump to another place, also while paused.</summary>
    public double Time
    {
        get => _clip == null ? 0 : CurrentFrame() * 1000.0 / _clip.SampleRate;
        set
        {
            if (_clip == null) return;
            long frame = Math.Clamp((long)(value * _clip.SampleRate / 1000.0), 0, _clip.TotalFrames);

            if (!_playing)
            {
                _seekFrame = frame;
                return;
            }

            bool wasPaused = _paused;
            StartAt(value);
            if (wasPaused) Pause();
        }
    }

    // ------------------------------------------------------------------ loading

    /// <summary>Loads a sound by file name (below the Assets folder; .wav or .ogg). Replaces whatever was loaded.</summary>
    public FlxSound Load(string path, bool looped = false, bool autoDestroy = false, Action? onComplete = null) =>
        Load(FlxG.Sound.GetClip(path), looped, autoDestroy, onComplete);

    public FlxSound Load(FlxAudioClip clip, bool looped = false, bool autoDestroy = false, Action? onComplete = null)
    {
        Reset();
        Revive();
        Active = true;

        _clip = clip;
        _reader?.Dispose();
        _reader = clip.OpenReader();
        Looped = looped;
        AutoDestroy = autoDestroy;
        OnComplete = onComplete;

        EnsureVoice();
        ApplyTransform();
        return this;
    }

    void EnsureVoice()
    {
        if (_clip == null) return;
        bool wantVirtual = FlxG.Sound.UsesVirtualVoices;

        if (_voice != null && (_voiceRate != _clip.SampleRate || _voiceChannels != _clip.Channels || _voiceVirtual != wantVirtual))
        {
            _voice.Dispose();
            _voice = null;
        }

        if (_voice == null)
        {
            _voice = FlxG.Sound.CreateVoice(_clip.SampleRate, _clip.Channels, out _voiceVirtual);
            _voiceRate = _clip.SampleRate;
            _voiceChannels = _clip.Channels;
            _voice.Pitch = _pitch;
        }
    }

    // ------------------------------------------------------------------ play, pause, stop

    /// <summary>
    /// Plays the sound; also resumes a paused one.
    /// </summary>
    /// <param name="forceRestart">Start over even if it is playing or paused (default: a playing sound keeps going).</param>
    /// <param name="startTime">Where to start, in milliseconds (default: the beginning, or the place you set with Time while it was stopped).</param>
    /// <param name="endTime">Where to end (or loop), in milliseconds; null plays to the end.</param>
    public FlxSound Play(bool forceRestart = false, double? startTime = null, double? endTime = null)
    {
        if (!Exists || _clip == null) return this;

        // a position set with Time on a stopped sound is used when no start time is given (a restart goes back to the beginning)
        double start = startTime ?? (forceRestart ? 0 : _seekFrame * 1000.0 / _clip.SampleRate);

        if (forceRestart) StopInternal();
        else if (Playing) return this;

        EndTime = endTime;

        if (_paused) Resume();
        else
        {
            LoopCount = 0;
            StartAt(start);
        }
        return this;
    }

    public FlxSound Pause()
    {
        if (!Playing) return this;
        _voice?.Pause();
        _paused = true;
        return this;
    }

    public FlxSound Resume()
    {
        if (!_paused) return this;
        _paused = false;
        _voice?.Resume();
        return this;
    }

    /// <summary>Stops the sound and goes back to its start. A sound with AutoDestroy is recycled.</summary>
    public FlxSound Stop()
    {
        StopInternal();
        if (AutoDestroy) Kill();
        return this;
    }

    void StopInternal()
    {
        _voice?.Stop();
        _queue.Clear();
        _playing = false;
        _paused = false;
        _endReached = false;
        _seekFrame = 0;
        LoopCount = 0;
    }

    void StartAt(double ms)
    {
        if (_clip == null || _reader == null || _voice == null) return;

        long frame = Math.Clamp((long)(ms * _clip.SampleRate / 1000.0), 0, _clip.TotalFrames);

        _voice.Stop();
        _queue.Clear();
        _reader.Seek(frame);
        _seekFrame = frame;
        _endReached = false;
        _passesQueued = 0;
        _paused = false;
        _playing = true;
        _clock = _headStart = 0;

        ApplyTransform();
        Fill();
        _voice.Play();
    }

    public override void Kill()
    {
        StopInternal();
        CancelFade();
        base.Kill();
    }

    public override void Destroy()
    {
        Kill();
        _group?.Remove(this);
        _group = null;
        _target = null;
        OnComplete = null;

        _reader?.Dispose();
        _reader = null;
        _voice?.Dispose();
        _voice = null;
        _clip = null;
        base.Destroy();
    }

    /// <summary>Brings a pooled sound back to a clean state before it loads something new.</summary>
    void Reset()
    {
        StopInternal();
        CancelFade();
        _group?.Remove(this);
        _group = null;
        _target = null;
        _radius = 0;
        _proximityPan = false;
        _volume = 1f; _volumeAdjust = 1f; _pan = 0f;
        X = Y = 0;
        Looped = false;
        LoopTime = 0;
        LoopUntil = -1;
        LoopCount = 0;
        EndTime = null;
        AutoDestroy = false;
        Persist = false;
        OnComplete = null;
    }

    // ------------------------------------------------------------------ feeding the voice

    long ChunkFrames => Math.Max(256, (long)(_clip!.SampleRate * ChunkSeconds));

    long EndFrame()
    {
        long total = _clip!.TotalFrames;
        if (EndTime is > 0) return Math.Min(total, (long)(EndTime.Value * _clip.SampleRate / 1000.0));
        return total;
    }

    long LoopFrame()
    {
        long f = Math.Clamp((long)(LoopTime * _clip!.SampleRate / 1000.0), 0, _clip.TotalFrames);
        return f >= EndFrame() ? 0 : f;
    }

    bool MayLoopAgain => Looped && (LoopUntil == -1 || _passesQueued < LoopUntil);

    /// <summary>Keeps the voice's queue full.</summary>
    void Fill()
    {
        if (_clip == null || _reader == null || _voice == null) return;

        int block = _clip.BlockAlign;
        while (!_endReached && _voice.PendingBuffers < QueueDepth && _queue.Count < QueueDepth)
        {
            long endFrame = EndFrame();
            long left = endFrame - _reader.Position;

            if (left <= 0)
            {
                // nothing left in this pass (a seek to or past the end)
                FinishPassWithoutData();
                continue;
            }

            long start = _reader.Position;
            int want = (int)Math.Min(ChunkFrames, left);
            var buffer = new byte[want * block];
            int got = _reader.Read(buffer, want);

            if (got <= 0)
            {
                FinishPassWithoutData();         // the file was shorter than its header said
                continue;
            }

            _voice.Submit(buffer, got * block);
            bool endsPass = _reader.Position >= endFrame || got < want;
            _queue.Enqueue(new Chunk { Start = start, Frames = got, EndsPass = endsPass });

            if (endsPass) NextPass();
        }
    }

    /// <summary>A pass ended: loop (seamlessly, the next buffer comes from the loop point) or stop feeding.</summary>
    void NextPass()
    {
        if (MayLoopAgain)
        {
            _passesQueued++;
            _reader!.Seek(LoopFrame());
        }
        else _endReached = true;
    }

    void FinishPassWithoutData()
    {
        // mark the last queued buffer as the end of the pass, or make an empty one so the end is still reported
        if (_queue.Count > 0)
        {
            var arr = _queue.ToArray();
            arr[^1].EndsPass = true;
            _queue.Clear();
            foreach (var c in arr) _queue.Enqueue(c);
        }
        else _queue.Enqueue(new Chunk { Start = _reader!.Position, Frames = 0, EndsPass = true });

        NextPass();
        if (!_endReached && _reader!.Position >= EndFrame()) _endReached = true;       // a loop point at the very end: don't spin
    }

    /// <summary>Called every frame with the real time passed: moves the voice along and reports finished buffers.</summary>
    internal void Pump(double seconds)
    {
        if (!Exists || _clip == null || _voice == null || !_playing) return;

        if (!_paused) _clock += seconds;
        _voice.Update(seconds);

        // buffers the voice has finished
        while (_queue.Count > _voice.PendingBuffers)
        {
            var chunk = _queue.Dequeue();
            _headStart = _clock;

            if (chunk.EndsPass)
            {
                bool last = _endReached && _queue.Count == 0;
                if (last) { Complete(); return; }

                LoopCount++;
                OnComplete?.Invoke();         // a looping sound reports every pass
                if (!_playing) return;        // the callback may have stopped it
            }
        }

        if (_endReached && _queue.Count == 0) { Complete(); return; }
        if (_playing && !_paused) Fill();
    }

    void Complete()
    {
        var done = OnComplete;
        _voice?.Stop();
        _queue.Clear();
        _playing = false;
        _paused = false;
        _endReached = false;
        _seekFrame = 0;

        done?.Invoke();

        // the callback may have started the sound again: only recycle it if it is still finished
        if (AutoDestroy && !_playing) Kill();
    }

    long CurrentFrame()
    {
        if (_clip == null) return 0;
        if (!_playing) return _seekFrame;
        if (_queue.Count == 0) return _reader?.Position ?? 0;

        // the buffer that is playing now, and how far into it we are (estimated from the time passed)
        var head = _queue.Peek();
        double into = Math.Min(head.Frames, (_clock - _headStart) * _clip.SampleRate * _pitch);
        return head.Start + (long)into;
    }

    // ------------------------------------------------------------------ fades

    /// <summary>
    /// Fades the volume from <paramref name="from"/> to <paramref name="to"/> over <paramref name="duration"/> seconds (game time).
    /// Starts the sound if it isn't playing. A sound that has faded in keeps its volume.
    /// </summary>
    public FlxSound FadeIn(float duration = 1f, float from = 0f, float to = 1f, Action<FlxSound>? onComplete = null)
    {
        if (!Playing) Play();
        StartFade(from, to, duration, onComplete);
        return this;
    }

    /// <summary>Fades the volume from where it is to <paramref name="to"/> (0 by default; the sound keeps playing, silently: stop it in onComplete if you want).</summary>
    public FlxSound FadeOut(float duration = 1f, float to = 0f, Action<FlxSound>? onComplete = null)
    {
        StartFade(_volume, to, duration, onComplete);
        return this;
    }

    public FlxSound Fade(float duration, float from, float to, Action<FlxSound>? onComplete = null)
    {
        StartFade(from, to, duration, onComplete);
        return this;
    }

    public void CancelFade() { _fading = false; _fadeDone = null; }

    public bool Fading => _fading;

    void StartFade(float from, float to, float duration, Action<FlxSound>? onComplete)
    {
        _fading = true;
        _fadeFrom = from; _fadeTo = to;
        _fadeDuration = Math.Max(0.0001f, duration);
        _fadeTime = 0;
        _fadeDone = onComplete;
        Volume = from;
    }

    // ------------------------------------------------------------------ proximity

    /// <summary>
    /// Makes the volume depend on how far an object is from the sound's place: full volume at the sound, silent at <paramref name="radius"/> pixels
    /// away. With <paramref name="pan"/> the sound also moves between the speakers (left of the sound = louder on the left).
    /// </summary>
    public FlxSound Proximity(float x, float y, FlxObject target, float radius, bool pan = true)
    {
        X = x; Y = y;
        _target = target;
        _radius = Math.Max(0.0001f, radius);
        _proximityPan = pan;
        UpdateProximity();
        return this;
    }

    /// <summary>Moves the sound (matters for proximity).</summary>
    public void SetPosition(float x = 0, float y = 0)
    {
        X = x; Y = y;
        UpdateProximity();
    }

    void UpdateProximity()
    {
        _volumeAdjust = 1f;

        if (_target != null)
        {
            var mid = _target.Midpoint;
            float dist = Vector2.Distance(mid, new Vector2(X, Y)) / _radius;
            _volumeAdjust = 1f - MathHelper.Clamp(dist, 0f, 1f);

            if (_proximityPan) _pan = MathHelper.Clamp((X - mid.X) / _radius, -1f, 1f);
        }
        ApplyTransform();
    }

    // ------------------------------------------------------------------ update

    /// <summary>Handles fades and proximity (every frame, with the game's elapsed time).</summary>
    public override void Update(float elapsed)
    {
        if (!Exists) return;

        if (_fading)
        {
            _fadeTime += elapsed;
            float t = MathHelper.Clamp(_fadeTime / _fadeDuration, 0f, 1f);
            Volume = _fadeFrom + (_fadeTo - _fadeFrom) * t;

            if (t >= 1f)
            {
                _fading = false;
                var done = _fadeDone;
                _fadeDone = null;
                done?.Invoke(this);
            }
        }

        if (_target != null) UpdateProximity();
        else ApplyTransform();          // the global volume or the group may have changed
    }

    /// <summary>Pushes volume and pan to the voice. Called when anything that matters changes.</summary>
    internal void ApplyTransform()
    {
        if (_voice == null) return;

        float volume = _volume * _volumeAdjust * (_group?.Volume ?? 1f);
        volume = FlxG.Sound.Muted ? 0f : FlxG.Sound.ApplyCurve(FlxG.Sound.Volume * volume);

        _voice.Volume = volume;
        _voice.Pan = _pan;
    }

    // for tests
    internal float VoiceVolume => _voice?.Volume ?? -1f;
    internal float VoicePan => _voice?.Pan ?? 0f;
    internal float VoicePitch => _voice?.Pitch ?? 0f;
    internal bool VoiceIsVirtual => _voiceVirtual;

    public override string ToString() => $"FlxSound({Name}, {(Playing ? "playing" : Paused ? "paused" : "stopped")}, {Time:0} / {Length:0} ms)";
}

/// <summary>
/// A set of sounds that share a volume (all the effects, all the music...). Port of Flixel's FlxSoundGroup:
/// <c>FlxG.Sound.DefaultSoundGroup</c> and <c>DefaultMusicGroup</c> are the ones the engine uses; make your own for anything else.
/// </summary>
public sealed class FlxSoundGroup
{
    float _volume = 1f;

    public readonly List<FlxSound> Sounds = new();

    public FlxSoundGroup(float volume = 1f) { _volume = MathHelper.Clamp(volume, 0f, 1f); }

    /// <summary>The group's volume, 0 to 1: multiplies the volume of every sound in it.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = MathHelper.Clamp(value, 0f, 1f);
            foreach (var s in Sounds) s.ApplyTransform();
        }
    }

    /// <summary>Adds a sound (taking it out of the group it was in).</summary>
    public bool Add(FlxSound sound)
    {
        if (Sounds.Contains(sound)) return false;
        sound.Group?.Remove(sound);
        Sounds.Add(sound);
        sound.SetGroup(this);
        return true;
    }

    public bool Remove(FlxSound sound)
    {
        if (!Sounds.Remove(sound)) return false;
        sound.SetGroup(null);
        return true;
    }

    public void Pause() { foreach (var s in Sounds.ToArray()) s.Pause(); }
    public void Resume() { foreach (var s in Sounds.ToArray()) s.Resume(); }
    public void Stop() { foreach (var s in Sounds.ToArray()) s.Stop(); }
}

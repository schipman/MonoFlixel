using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>
/// FlxG.Sound: loads, caches and plays sounds and music. Port of Flixel's SoundFrontEnd.
/// Sound files live below the Assets folder (.wav, .ogg or .mp3; the extension may be left out), e.g. "sound/hit.ogg" or "mode/sounds/jump".
/// <code>
/// FlxG.Sound.Play("sound/hit.ogg");                                   // fire and forget (recycled when it ends)
/// FlxG.Sound.PlayMusic("sound/mode.ogg", volume: 0.7f);               // looping music (replaces the current one)
/// var s = FlxG.Sound.Load("sound/jet.ogg", looped: true);             // a sound you keep a handle to
/// FlxG.Sound.DefaultSoundGroup.Volume = 0.5f;                         // all effects at half volume
/// </code>
/// The volume keys work by themselves: + and - change the volume (with an on-screen tray), 0 mutes. With no audio device sounds still
/// "play" silently, so game logic that waits for a sound to end keeps working.
/// </summary>
public sealed class FlxSoundFrontEnd
{
    readonly Dictionary<string, FlxAudioClip> _clips = new();
    readonly HashSet<string> _reported = new();
    readonly List<FlxSound> _pausedByFocus = new();
    float _volume = 1f;
    int _deviceState;           // 0 = not checked yet, 1 = there is a device, 2 = none

    /// <summary>Every sound that is not music (pooled: a finished sound is reused by the next Load / Play).</summary>
    public readonly FlxTypedGroup<FlxSound> List = new();

    /// <summary>The current music, if any.</summary>
    public FlxSound? Music { get; private set; }

    /// <summary>The group effects go into unless you pass another, and the one music goes into.</summary>
    public FlxSoundGroup DefaultSoundGroup { get; } = new();
    public FlxSoundGroup DefaultMusicGroup { get; } = new();

    public bool Muted;

    /// <summary>The master volume, 0 to 1.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = MathHelper.Clamp(value, 0f, 1f);
            OnVolumeChange.Dispatch(Muted ? 0f : _volume);
        }
    }

    /// <summary>Called whenever the master volume or the muted state changes (with the volume now in effect).</summary>
    public readonly FlxSignal<float> OnVolumeChange = new();

    /// <summary>Changes how volume is turned into loudness (default: unchanged). E.g. <c>v => v * v</c> for a more natural feel.</summary>
    public Func<float, float> SoundCurve = v => v;

    internal float ApplyCurve(float volume) => SoundCurve(MathHelper.Clamp(volume, 0f, 1f));

    // keys
    public bool VolumeKeysEnabled = true;
    public Keys[] VolumeUpKeys = { Keys.OemPlus, Keys.Add };
    public Keys[] VolumeDownKeys = { Keys.OemMinus, Keys.Subtract };
    public Keys[] MuteKeys = { Keys.D0, Keys.NumPad0 };

    /// <summary>The on-screen volume display.</summary>
    public FlxSoundTray Tray { get; } = new();

    /// <summary>Show the tray when the volume changes (default true).</summary>
    public bool TrayEnabled = true;

    /// <summary>Pause the sounds while the window is in the background and resume them when it comes back (default true).</summary>
    public bool AutoPause = true;

    /// <summary>
    /// Make new sounds play silently (no sound card is used; positions, loops and callbacks still work). Handy for tests and quiet runs;
    /// sounds that already exist keep the voice they have.
    /// </summary>
    public bool Silent;

    /// <summary>True if no sound card could be used and sounds play silently.</summary>
    public bool NoDevice => _deviceState == 2;

    internal bool UsesVirtualVoices => Silent || _deviceState == 2;

    // ------------------------------------------------------------------ voices and files

    internal IFlxVoice CreateVoice(int sampleRate, int channels, out bool isVirtual)
    {
        isVirtual = true;
        if (UsesVirtualVoices) return new FlxVirtualVoice(sampleRate, channels);

        try
        {
            var voice = new FlxDeviceVoice(sampleRate, channels);
            _deviceState = 1;
            isVirtual = false;
            return voice;
        }
        catch (NoAudioHardwareException)
        {
            _deviceState = 2;
            Console.Error.WriteLine("[FlxSound] no audio device: sounds play silently.");
        }
        catch (InvalidOperationException e)       // e.g. too many voices
        {
            Console.Error.WriteLine($"[FlxSound] could not make a voice ({e.Message}): playing silently.");
        }
        return new FlxVirtualVoice(sampleRate, channels);
    }

    /// <summary>The full path of a sound: as given, or below Assets; a missing extension is looked up as .ogg, .wav then .mp3.</summary>
    static string ResolvePath(string name)
    {
        string full = Path.IsPathRooted(name) ? name : FlxAssets.Get(name);
        if (File.Exists(full) || Path.HasExtension(full)) return full;

        foreach (var ext in new[] { ".ogg", ".wav", ".mp3" })
            if (File.Exists(full + ext)) return full + ext;
        return full + ".ogg";           // not found either way: the error message then names a real file
    }

    /// <summary>The decoded clip for a file (cached). Throws if the file can't be read or is not a supported sound.</summary>
    public FlxAudioClip GetClip(string name)
    {
        string path = ResolvePath(name);
        if (_clips.TryGetValue(path, out var clip)) return clip;
        return _clips[path] = FlxAudioClip.Load(path);
    }

    /// <summary>Loads a sound file ahead of time so playing it later doesn't stutter. Returns false if it can't be loaded.</summary>
    public bool Cache(string name)
    {
        try { GetClip(name); return true; }
        catch (Exception e) when (e is IOException or NotSupportedException or InvalidDataException or UnauthorizedAccessException) { Report(name, e); return false; }
    }

    public void CacheAll(IEnumerable<string> names) { foreach (var n in names) Cache(n); }

    /// <summary>Forgets the cached clips (sounds already loaded keep theirs).</summary>
    public void ClearCache() => _clips.Clear();

    void Report(string name, Exception e)
    {
        if (_reported.Add(name)) Console.Error.WriteLine($"[FlxSound] could not load '{name}': {e.Message}");
    }

    // ------------------------------------------------------------------ making and playing sounds

    /// <summary>
    /// A sound loaded from a file, in the default sound group. A finished or recycled sound is reused, so this does not pile up voices.
    /// It is not playing yet unless <paramref name="autoPlay"/>. If the file can't be loaded you get a silent sound that does nothing.
    /// </summary>
    public FlxSound Load(string name, float volume = 1f, bool looped = false, FlxSoundGroup? group = null, bool autoDestroy = false,
        bool autoPlay = false, Action? onComplete = null)
    {
        FlxAudioClip clip;
        try { clip = GetClip(name); }
        catch (Exception e) when (e is IOException or NotSupportedException or InvalidDataException or UnauthorizedAccessException)
        {
            Report(name, e);
            var failed = List.GetFirstAvailable();
            if (failed == null) { failed = new FlxSound(); List.Add(failed); }
            failed.Kill();
            return failed;           // not loaded: Play() on it does nothing
        }
        return Load(clip, volume, looped, group, autoDestroy, autoPlay, onComplete);
    }

    /// <summary>A sound made from a clip you already have (for example FlxAudioClip.FromPcm for a generated sound).</summary>
    public FlxSound Load(FlxAudioClip clip, float volume = 1f, bool looped = false, FlxSoundGroup? group = null, bool autoDestroy = false,
        bool autoPlay = false, Action? onComplete = null)
    {
        var sound = List.GetFirstAvailable();
        if (sound == null)
        {
            sound = new FlxSound();
            List.Add(sound);
        }

        sound.Load(clip, looped, autoDestroy, onComplete);
        sound.Volume = volume;
        (group ?? DefaultSoundGroup).Add(sound);
        if (autoPlay) sound.Play();
        return sound;
    }

    /// <summary>
    /// Plays a sound effect and forgets about it: it is recycled when it ends. Effects keep playing across a state switch; looped
    /// ones (and anything you load yourself) stop unless you set <see cref="FlxSound.Persist"/>.
    /// </summary>
    public FlxSound Play(string name, float volume = 1f, bool looped = false, FlxSoundGroup? group = null, bool autoDestroy = true, Action? onComplete = null)
    {
        var sound = Load(name, volume, looped, group, autoDestroy, autoPlay: false, onComplete);
        sound.Persist = autoDestroy && !looped;
        return sound.Play();
    }

    /// <summary>Plays music (replacing the current music). Loops by default and keeps playing across state switches.</summary>
    public FlxSound? PlayMusic(string name, float volume = 1f, bool looped = true, FlxSoundGroup? group = null)
    {
        Music?.Destroy();
        Music = null;

        FlxAudioClip clip;
        try { clip = GetClip(name); }
        catch (Exception e) when (e is IOException or NotSupportedException or InvalidDataException or UnauthorizedAccessException)
        {
            Report(name, e);
            return null;
        }

        var music = new FlxSound().Load(clip, looped);
        music.Persist = true;
        music.Volume = volume;
        (group ?? DefaultMusicGroup).Add(music);
        music.Play();
        Music = music;
        return music;
    }

    // ------------------------------------------------------------------ all sounds at once

    /// <summary>Pauses the music and every sound.</summary>
    public void Pause()
    {
        Music?.Pause();
        foreach (var s in List.Members) if (s.Exists) s.Pause();
    }

    public void Resume()
    {
        Music?.Resume();
        foreach (var s in List.Members) if (s.Exists) s.Resume();
    }

    /// <summary>
    /// Stops sounds that don't have Persist set (the engine does this when the state changes). With <paramref name="forceDestroy"/> every sound
    /// goes, music included.
    /// </summary>
    public void Destroy(bool forceDestroy = false)
    {
        if (Music != null && (forceDestroy || !Music.Persist))
        {
            Music.Destroy();
            Music = null;
        }

        foreach (var s in List.Members)
            if (s.Exists && (forceDestroy || !s.Persist)) s.Kill();
    }

    /// <summary>Called by the engine when the state changes.</summary>
    internal void OnStateSwitch() => Destroy(false);

    internal void Shutdown()
    {
        Music?.Destroy();
        foreach (var s in List.Members) s.Destroy();
        List.Members.Clear();
        _clips.Clear();
    }

    // ------------------------------------------------------------------ volume

    /// <summary>Mutes or unmutes, and shows the tray.</summary>
    public void ToggleMuted()
    {
        Muted = !Muted;
        OnVolumeChange.Dispatch(Muted ? 0f : _volume);
        ShowTray(up: !Muted);
    }

    /// <summary>Changes the master volume by an amount (e.g. 0.1), unmutes, and shows the tray.</summary>
    public void ChangeVolume(float amount)
    {
        Muted = false;
        Volume += amount;
        ShowTray(amount > 0);
    }

    public void ShowTray(bool up = false)
    {
        if (TrayEnabled) Tray.Show(up);
    }

    // ------------------------------------------------------------------ every frame

    static bool Any(Keys[] keys) => FlxG.Keys.AnyJustPressed(keys);

    /// <summary>
    /// Called by the game every frame. <paramref name="realSeconds"/> keeps the audio flowing even when FlxG.TimeScale is 0;
    /// <paramref name="gameSeconds"/> (scaled) drives fades and proximity.
    /// </summary>
    internal void Update(double realSeconds, float gameSeconds)
    {
        if (VolumeKeysEnabled)
        {
            if (Any(MuteKeys)) ToggleMuted();
            else if (Any(VolumeDownKeys)) ChangeVolume(-0.1f);
            else if (Any(VolumeUpKeys)) ChangeVolume(0.1f);
        }

        Tray.Update((float)realSeconds);

        var sounds = List.Members.ToArray();       // callbacks may play more sounds
        foreach (var s in sounds)
        {
            if (!s.Exists) continue;
            s.Pump(realSeconds);
            s.Update(gameSeconds);
        }

        if (Music is { Exists: true } music)
        {
            music.Pump(realSeconds);
            music.Update(gameSeconds);
        }
    }

    internal void FocusLost()
    {
        if (!AutoPause) return;

        _pausedByFocus.Clear();
        if (Music is { Playing: true }) { Music.Pause(); _pausedByFocus.Add(Music); }
        foreach (var s in List.Members)
            if (s.Exists && s.Playing) { s.Pause(); _pausedByFocus.Add(s); }
    }

    internal void FocusGained()
    {
        foreach (var s in _pausedByFocus) if (s.Exists) s.Resume();
        _pausedByFocus.Clear();
    }
}

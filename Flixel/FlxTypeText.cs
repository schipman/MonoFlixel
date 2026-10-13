using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>
/// Text that types itself, one character at a time, like a typewriter or a dialogue box. It can blink a cursor, tick a sound for every
/// letter, type at an uneven human rhythm, wait and then erase itself, and be skipped with a key. Port of flixel-addons' FlxTypeText
/// (loosely based on Noel Berry's TypeText for Ludum Dare 22).
/// <code>
/// var text = new FlxTypeText(20, 20, 300, "Welcome to the end of the world.") { ShowCursor = true, UseDefaultSound = true };
/// Add(text);
/// text.Start(delay: 0.04f, callback: () =&gt; Console.WriteLine("done"));
/// </code>
/// The text starts out empty: <see cref="Start"/> begins the typing. Words are never split across lines while they are typed (the
/// final text is wrapped first). Not ported: <c>applyMarkup</c> (this engine's text has no markup).
/// </summary>
public class FlxTypeText : FlxText
{
    /// <summary>Seconds between one character and the next.</summary>
    public float Delay = 0.05f;

    /// <summary>Seconds between one character erased and the next.</summary>
    public float EraseDelay = 0.02f;

    /// <summary>Show a blinking cursor at the end of the text.</summary>
    public bool ShowCursor;

    /// <summary>The character that blinks at the end (only its first character is used).</summary>
    public string CursorCharacter = "|";

    /// <summary>How long one blink takes, in seconds (on for half of it).</summary>
    public float CursorBlinkSpeed = 0.5f;

    /// <summary>Text at the start that is there at once, not typed.</summary>
    public string Prefix = "";

    /// <summary>Erase the message when it is complete (after <see cref="WaitTime"/>).</summary>
    public bool AutoErase;

    /// <summary>How long to wait after typing before erasing, in seconds. Only used with AutoErase.</summary>
    public float WaitTime = 1f;

    /// <summary>While true nothing is typed or erased. Start and Erase turn it off.</summary>
    public bool Paused;

    /// <summary>Sounds that are played (one of them, at random) when a letter is added.</summary>
    public FlxSound[]? Sounds;

    /// <summary>Use the built-in typing tick (a short click that is made in code) instead of <see cref="Sounds"/>.</summary>
    public bool UseDefaultSound;

    /// <summary>Volume of the built-in tick (0 to 1).</summary>
    public float DefaultSoundVolume = 0.35f;

    /// <summary>
    /// True: a sound plays to its end even when the next letter comes; false: the next letter restarts it. For longer sounds true usually
    /// makes more sense.
    /// </summary>
    public bool FinishSounds;

    /// <summary>These keys finish the typing at once (and skip a wait or an erase).</summary>
    public Keys[] SkipKeys = Array.Empty<Keys>();

    /// <summary>A click of the left mouse button also skips (handy for dialogue boxes).</summary>
    public bool SkipOnClick;

    /// <summary>Called when the whole message is typed.</summary>
    public Action? CompleteCallback;

    /// <summary>Called when the message has been erased (if it is erased).</summary>
    public Action? EraseCallback;

    string _finalText;
    float _timer, _waitTimer, _cursorTimer;
    int _length;
    bool _typing, _erasing, _waiting, _variation;
    float _variationPercent = 0.5f;
    FlxSound? _sound;
    bool _wrapped;

    static FlxAudioClip? _tick;

    /// <param name="text">The text that will be shown in the end.</param>
    public FlxTypeText(float x = 0, float y = 0, float fieldWidth = 0, string text = "", int size = 8) : base(x, y, fieldWidth, "", size)
    {
        _finalText = text;
    }

    /// <summary>The whole text that is being (or will be) typed.</summary>
    public string FinalText => _finalText;

    /// <summary>True while the typing is going on.</summary>
    public bool Typing => _typing;

    /// <summary>True once everything is typed.</summary>
    public bool IsComplete => _length >= _finalText.Length && !_typing && !_erasing;

    /// <summary>How many characters of the text are showing.</summary>
    public int Length => _length;

    // ------------------------------------------------------------------ starting, erasing, resetting

    /// <summary>Starts the typing.</summary>
    /// <param name="delay">Seconds between characters (also settable as <see cref="Delay"/>).</param>
    /// <param name="forceRestart">Begin again from nothing, even if it is already typing.</param>
    /// <param name="autoErase">Erase the message when it is done.</param>
    /// <param name="skipKeys">Keys that finish the typing.</param>
    /// <param name="callback">Called when the typing is complete.</param>
    public void Start(float? delay = null, bool forceRestart = false, bool autoErase = false, Keys[]? skipKeys = null, Action? callback = null)
    {
        if (delay != null) Delay = delay.Value;

        _typing = true;
        _erasing = false;
        Paused = false;
        _waiting = false;

        if (forceRestart)
        {
            Text = "";
            _length = 0;
        }

        AutoErase = autoErase;
        if (skipKeys != null) SkipKeys = skipKeys;
        if (callback != null) CompleteCallback = callback;

        WrapFinalText();
        if (UseDefaultSound) LoadDefaultSound();
    }

    /// <summary>Starts erasing, a character at a time from the end.</summary>
    public void Erase(float? delay = null, bool forceRestart = false, Keys[]? skipKeys = null, Action? callback = null)
    {
        _erasing = true;
        _typing = false;
        Paused = false;
        _waiting = false;

        if (delay != null) EraseDelay = delay.Value;

        if (forceRestart)
        {
            _length = _finalText.Length;
            Text = _finalText;
        }

        if (skipKeys != null) SkipKeys = skipKeys;
        EraseCallback = callback;

        if (UseDefaultSound) LoadDefaultSound();
    }

    /// <summary>Gives it a new text. Typing and erasing stop; call Start to type it.</summary>
    public void ResetText(string text)
    {
        Text = Prefix;
        _finalText = text;
        _typing = false;
        _erasing = false;
        Paused = false;
        _waiting = false;
        _length = 0;
        _wrapped = false;
    }

    /// <summary>
    /// Makes the typing rhythm uneven: each letter comes up to <paramref name="amount"/> (0 to 1, a share of the delay) sooner or later,
    /// which feels more like a person, especially with sound.
    /// </summary>
    public void SetTypingVariation(float amount = 0.5f, bool on = true)
    {
        _variation = on;
        _variationPercent = Math.Clamp(amount, 0f, 1f);
    }

    /// <summary>Finishes at once: the whole text is there (or, while erasing or waiting, it is all gone).</summary>
    public void Skip()
    {
        if (_erasing || _waiting)
        {
            _length = 0;
            _waiting = false;
        }
        else if (_typing)
        {
            _length = _finalText.Length;
        }
    }

    /// <summary>Puts a line break wherever the field would wrap the text, so a word is never typed at the end of one line and then jumps to the next.</summary>
    void WrapFinalText()
    {
        if (FieldWidth <= 0) { _wrapped = true; return; }

        string combined = Prefix + _finalText;
        string wrapped = WrapText(combined);
        if (wrapped.StartsWith(Prefix, StringComparison.Ordinal)) _finalText = wrapped[Prefix.Length..];
        _wrapped = true;
    }

    // ------------------------------------------------------------------ the typing

    void OnComplete()
    {
        _timer = 0;
        _typing = false;

        if (UseDefaultSound) _sound?.Stop();
        else if (Sounds != null) foreach (var s in Sounds) s.Stop();

        CompleteCallback?.Invoke();

        if (AutoErase && WaitTime <= 0) _erasing = true;
        else if (AutoErase)
        {
            _waitTimer = WaitTime;
            _waiting = true;
        }
    }

    void OnErased()
    {
        _timer = 0;
        _erasing = false;
        EraseCallback?.Invoke();
    }

    public override void Update(float elapsed)
    {
        // a skip key (or click) finishes the animation
        if ((SkipKeys.Length > 0 && FlxG.Keys.AnyJustPressed(SkipKeys)) || (SkipOnClick && FlxG.Mouse.Left.JustPressed)) Skip();

        if (_waiting && !Paused)
        {
            _waitTimer -= elapsed;
            if (_waitTimer <= 0)
            {
                _waiting = false;
                _erasing = true;
            }
        }

        // as long as we should be animating, the timer grows
        if (!_waiting && !Paused)
        {
            if (_length < _finalText.Length && _typing) _timer += elapsed;
            if (_length > 0 && _erasing) _timer += elapsed;
        }

        // when the timer passes the delay, the length grows (or shrinks)
        if (_typing || _erasing)
        {
            if (_typing && _timer >= Delay)
            {
                _length += Delay > 0 ? (int)(_timer / Delay) : _finalText.Length;
                if (_length > _finalText.Length) _length = _finalText.Length;
            }

            if (_erasing && _timer >= EraseDelay)
            {
                _length -= EraseDelay > 0 ? (int)(_timer / EraseDelay) : _length;
                if (_length < 0) _length = 0;
            }

            bool typed = _typing && _timer >= Delay, erased = _erasing && _timer >= EraseDelay;
            if (typed || erased)
            {
                float d = typed ? Delay : EraseDelay;
                if (_variation) _timer = FlxG.Random.Float(-d * _variationPercent / 2f, d * _variationPercent / 2f);
                else _timer = d > 0 ? _timer % d : 0;

                PlayLetterSound();
            }
        }

        // what the text should be now
        string shown = Prefix + _finalText.Substring(0, Math.Min(_length, _finalText.Length));

        // the blinking cursor
        if (ShowCursor)
        {
            _cursorTimer += elapsed;

            // not in front of a line break: that would wrap the line early
            string all = Prefix + _finalText;
            bool beforeBreak = shown.Length < all.Length && all[shown.Length] == '\n';

            if (_cursorTimer > CursorBlinkSpeed / 2f && !beforeBreak && CursorCharacter.Length > 0)
                shown += CursorCharacter[0];

            if (_cursorTimer > CursorBlinkSpeed) _cursorTimer = 0;
        }

        // if it changed, show it, and see if it is complete
        if (shown != Text)
        {
            Text = shown;

            if (_length >= _finalText.Length && _typing && !_waiting && !_erasing) OnComplete();
            if (_length == 0 && _erasing && !_typing && !_waiting) OnErased();
        }

        base.Update(elapsed);
    }

    void PlayLetterSound()
    {
        if (Sounds is { Length: > 0 } && !UseDefaultSound)
        {
            if (!FinishSounds) foreach (var s in Sounds) s.Stop();
            FlxG.Random.GetObject(Sounds).Play(forceRestart: !FinishSounds);
        }
        else if (UseDefaultSound && _sound != null)
        {
            _sound.Volume = DefaultSoundVolume;
            _sound.Play(forceRestart: !FinishSounds);
        }
    }

    void LoadDefaultSound()
    {
        _sound = FlxG.Sound.Load(Tick(), DefaultSoundVolume);
    }

    /// <summary>The built-in typing sound: a very short click (a burst of noise and a falling tone), made in code.</summary>
    static FlxAudioClip Tick()
    {
        if (_tick != null) return _tick;

        const int rate = 22050, frames = rate / 36;
        var pcm = new byte[frames * 2];
        var noise = new Random(5);
        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)frames;
            float envelope = MathF.Pow(1f - t, 3f);
            float tone = MathF.Sin(2f * MathF.PI * (1700f - 900f * t) * i / rate);
            float hiss = (float)(noise.NextDouble() * 2 - 1);
            short v = (short)(Math.Clamp((tone * 0.6f + hiss * 0.4f) * envelope, -1f, 1f) * 14000);
            pcm[i * 2] = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return _tick = FlxAudioClip.FromPcm(pcm, rate, 1, "type");
    }

    public override void Destroy()
    {
        CompleteCallback = null;
        EraseCallback = null;
        _sound?.Stop();
        base.Destroy();
    }
}

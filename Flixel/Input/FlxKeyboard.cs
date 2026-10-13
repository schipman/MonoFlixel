using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace MonoFlixel;

/// <summary>
/// Keyboard state (FlxG.Keys). Keys are MonoGame's <see cref="Keys"/>. Besides pressed / just pressed / just released it can tell you which
/// key was hit (for "press a key to bind"), and gives the text typed this frame (for text fields).
/// <code>
/// if (FlxG.Keys.JustPressed(Keys.Space)) Jump();
/// if (FlxG.Keys.FirstJustPressed is { } key) Bind(key);
/// string typed = FlxG.Keys.TypedText;       // characters typed this frame, including \b for backspace and \r for enter
/// </code>
/// </summary>
public class FlxKeyboard
{
    KeyboardState _cur, _prev;
    readonly StringBuilder _typedPending = new();
    string _typed = "";

    /// <summary>Where the key states come from (default: the real keyboard). Replace it to feed in your own, e.g. in tests or for replays.</summary>
    public Func<KeyboardState> Source = Keyboard.GetState;

    /// <summary>Switch the keyboard off (everything reads as not pressed), e.g. while a text field has the focus.</summary>
    public bool Enabled = true;

    /// <summary>Called for every typed character (with the key that produced it), also characters that don't have a key.</summary>
    public event Action<char, Keys>? TextInput;

    internal void Update()
    {
        _prev = _cur;
        _cur = Enabled ? Source() : default;

        _typed = _typedPending.ToString();
        _typedPending.Clear();
    }

    /// <summary>Called by the game for characters the window receives.</summary>
    internal void OnTextInput(char character, Keys key)
    {
        _typedPending.Append(character);
        TextInput?.Invoke(character, key);
    }

    /// <summary>Forgets what was pressed (nothing is down, nothing was just pressed).</summary>
    public void Reset()
    {
        _cur = _prev = default;
        _typed = "";
        _typedPending.Clear();
    }

    public bool Pressed(Keys k) => _cur.IsKeyDown(k);
    public bool Released(Keys k) => _cur.IsKeyUp(k);
    public bool JustPressed(Keys k) => _cur.IsKeyDown(k) && _prev.IsKeyUp(k);
    public bool JustReleased(Keys k) => _cur.IsKeyUp(k) && _prev.IsKeyDown(k);

    public bool AnyPressed(params Keys[] keys) { foreach (var k in keys) if (_cur.IsKeyDown(k)) return true; return false; }
    public bool AnyJustPressed(params Keys[] keys) { foreach (var k in keys) if (JustPressed(k)) return true; return false; }
    public bool AnyJustReleased(params Keys[] keys) { foreach (var k in keys) if (JustReleased(k)) return true; return false; }

    /// <summary>True if any key is held down.</summary>
    public bool AnyKey => _cur.GetPressedKeyCount() > 0;

    /// <summary>True if any key went down this frame.</summary>
    public bool AnyKeyJustPressed => JustPressedKeys.Count > 0;

    /// <summary>All the keys that are down.</summary>
    public Keys[] PressedKeys => _cur.GetPressedKeys();

    /// <summary>The keys that went down this frame.</summary>
    public IReadOnlyList<Keys> JustPressedKeys => _cur.GetPressedKeys().Where(k => _prev.IsKeyUp(k)).ToList();

    /// <summary>The keys that came up this frame.</summary>
    public IReadOnlyList<Keys> JustReleasedKeys => _prev.GetPressedKeys().Where(k => _cur.IsKeyUp(k)).ToList();

    /// <summary>The first key that is down, or null.</summary>
    public Keys? FirstPressed => _cur.GetPressedKeys() is { Length: > 0 } k ? k[0] : null;

    /// <summary>The first key that went down this frame, or null. Handy for rebinding: "press the key you want".</summary>
    public Keys? FirstJustPressed => JustPressedKeys is { Count: > 0 } k ? k[0] : null;

    /// <summary>The characters typed since the last frame (backspace is '\b', enter '\r'). Empty most of the time.</summary>
    public string TypedText => _typed;

    /// <summary>Asks about a key by name ("A", "Space", "Left", "Enter", "1"...).</summary>
    public bool Pressed(string keyName) => FlxKey.TryParse(keyName, out var k) && Pressed(k);
    public bool JustPressed(string keyName) => FlxKey.TryParse(keyName, out var k) && JustPressed(k);
}

/// <summary>Names for keys: for showing them to the player and for saving key bindings.</summary>
public static class FlxKey
{
    static readonly Dictionary<Keys, string> Friendly = new()
    {
        [Keys.D0] = "0", [Keys.D1] = "1", [Keys.D2] = "2", [Keys.D3] = "3", [Keys.D4] = "4", [Keys.D5] = "5", [Keys.D6] = "6", [Keys.D7] = "7", [Keys.D8] = "8", [Keys.D9] = "9",
        [Keys.OemPlus] = "+", [Keys.OemMinus] = "-", [Keys.OemComma] = ",", [Keys.OemPeriod] = ".", [Keys.OemQuestion] = "/", [Keys.OemSemicolon] = ";",
        [Keys.OemQuotes] = "'", [Keys.OemOpenBrackets] = "[", [Keys.OemCloseBrackets] = "]", [Keys.OemPipe] = "\\", [Keys.OemTilde] = "`",
        [Keys.Add] = "NumPad +", [Keys.Subtract] = "NumPad -", [Keys.Multiply] = "NumPad *", [Keys.Divide] = "NumPad /", [Keys.Decimal] = "NumPad .",
        [Keys.NumPad0] = "NumPad 0", [Keys.NumPad1] = "NumPad 1", [Keys.NumPad2] = "NumPad 2", [Keys.NumPad3] = "NumPad 3", [Keys.NumPad4] = "NumPad 4",
        [Keys.NumPad5] = "NumPad 5", [Keys.NumPad6] = "NumPad 6", [Keys.NumPad7] = "NumPad 7", [Keys.NumPad8] = "NumPad 8", [Keys.NumPad9] = "NumPad 9",
        [Keys.Escape] = "Esc", [Keys.Back] = "Backspace", [Keys.PageUp] = "Page Up", [Keys.PageDown] = "Page Down", [Keys.LeftShift] = "Left Shift",
        [Keys.RightShift] = "Right Shift", [Keys.LeftControl] = "Left Ctrl", [Keys.RightControl] = "Right Ctrl", [Keys.LeftAlt] = "Left Alt", [Keys.RightAlt] = "Right Alt",
    };

    static readonly Lazy<Dictionary<string, Keys>> ByName = new(() =>
    {
        var map = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in Enum.GetValues<Keys>()) map.TryAdd(k.ToString(), k);
        foreach (var (k, name) in Friendly) map.TryAdd(name, k);
        string[] words = { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };      // Flixel's names for the digits
        for (int i = 0; i < 10; i++) map.TryAdd(words[i], Keys.D0 + i);
        map.TryAdd("ESCAPE", Keys.Escape); map.TryAdd("BACKSPACE", Keys.Back); map.TryAdd("PLUS", Keys.OemPlus); map.TryAdd("MINUS", Keys.OemMinus);
        return map;
    });

    /// <summary>A name fit for the player: "Space", "Esc", "1", "Left Shift".</summary>
    public static string Name(Keys key) => Friendly.TryGetValue(key, out var n) ? n : key.ToString();

    /// <summary>Reads a key from its name (any case; "Space", "esc", "1", "ONE", "Left Shift", or an exact Keys name).</summary>
    public static bool TryParse(string name, out Keys key) => ByName.Value.TryGetValue(name.Trim(), out key);

    public static Keys Parse(string name) =>
        TryParse(name, out var k) ? k : throw new ArgumentException($"'{name}' is not a key name.", nameof(name));
}

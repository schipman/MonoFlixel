namespace MonoFlixel;

/// <summary>The state of a button, key or other digital input.</summary>
public enum FlxInputState
{
    /// <summary>Not held down.</summary>
    Released,
    /// <summary>Held down (also true on the frame it went down).</summary>
    Pressed,
    /// <summary>Went down this frame.</summary>
    JustPressed,
    /// <summary>Came up this frame.</summary>
    JustReleased
}

/// <summary>Something that can be pressed: a key, a mouse button, a gamepad button, a touch.</summary>
public interface IFlxInput
{
    bool Pressed { get; }
    bool JustPressed { get; }
    bool Released { get; }
    bool JustReleased { get; }
}

public static class FlxInputExtensions
{
    /// <summary>Asks an input about a state: <c>mouse.Left.Check(FlxInputState.JustPressed)</c>.</summary>
    public static bool Check(this IFlxInput input, FlxInputState state) => state switch
    {
        FlxInputState.Pressed => input.Pressed,
        FlxInputState.JustPressed => input.JustPressed,
        FlxInputState.JustReleased => input.JustReleased,
        _ => input.Released
    };

    /// <summary>Picks the matching state out of "is down now" and "was down before".</summary>
    public static bool Matches(this FlxInputState state, bool down, bool wasDown) => state switch
    {
        FlxInputState.Pressed => down,
        FlxInputState.JustPressed => down && !wasDown,
        FlxInputState.JustReleased => !down && wasDown,
        _ => !down
    };
}

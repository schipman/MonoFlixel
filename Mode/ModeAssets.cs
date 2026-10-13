namespace MonoFlixel.Mode;

/// <summary>Where Mode's files live (replaces Haxe's generated AssetPaths class).</summary>
public static class ModeAssets
{
    /// <summary>Full path of an image, e.g. Image("bot").</summary>
    public static string Image(string name) => FlxAssets.Get($"mode/images/{name}.png");

    /// <summary>Sound name for FlxG.Sound, e.g. Sound("jump").</summary>
    public static string Sound(string name) => $"mode/sounds/{name}.wav";
}

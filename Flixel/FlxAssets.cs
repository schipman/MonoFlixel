using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>
/// Where loose asset files live. Files in the project's Assets folder are copied next to the exe
/// (see the csproj), so this works from Visual Studio, `dotnet run` and published builds.
/// </summary>
public static class FlxAssets
{
    static readonly Dictionary<string, Texture2D> Textures = new();

    public static string Root { get; set; } = Path.Combine(AppContext.BaseDirectory, "Assets");

    /// <summary>Full path of a file below the Assets folder, e.g. Get("mode/images/bot.png").</summary>
    public static string Get(string fileName) => Path.Combine(Root, fileName);

    /// <summary>Loads a PNG once and shares it between every sprite that asks for it. A path that is not rooted is taken as below the Assets folder.</summary>
    public static Texture2D GetTexture(string fullPath)
    {
        if (!Path.IsPathRooted(fullPath)) fullPath = Get(fullPath);
        if (!Textures.TryGetValue(fullPath, out var tex))
        {
            using var stream = File.OpenRead(fullPath);
            tex = Texture2D.FromStream(FlxG.GraphicsDevice, stream);
            Textures[fullPath] = tex;
        }
        return tex;
    }
}

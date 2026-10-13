using System.Collections.Generic;

namespace MonoFlixel.Mode;

/// <summary>Registry: score and high scores shared between states.</summary>
public static class Reg
{
    public static readonly List<int> Scores = new();
    public static int Score;
}

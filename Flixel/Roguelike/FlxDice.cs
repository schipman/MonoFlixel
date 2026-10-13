using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonoFlixel;

/// <summary>
/// Dice, in the notation of tabletop games: <c>3d6+2</c> is three six sided dice added up, plus two. <c>d20</c>, <c>2d8-1</c> and <c>5</c>
/// work too. Rolls use FlxG.Random (or a random you pass).
/// <code>
/// int damage = FlxDice.Roll("1d6+1");
/// var sword = FlxDice.Parse("2d4");  sword.Roll(); sword.Min; sword.Max; sword.Average;
/// </code>
/// </summary>
public sealed class FlxDice
{
    static readonly Regex Pattern = new(@"^\s*(?:(\d*)\s*[dD]\s*(\d+)|(\d+))\s*(?:([+-])\s*(\d+))?\s*$", RegexOptions.Compiled);

    /// <summary>How many dice.</summary>
    public int Count { get; }

    /// <summary>How many sides each has.</summary>
    public int Sides { get; }

    /// <summary>What is added to the total (negative to subtract).</summary>
    public int Bonus { get; }

    public FlxDice(int count, int sides, int bonus = 0)
    {
        if (count < 0 || sides < 0) throw new ArgumentException("Dice need a count and sides of 0 or more.");
        Count = count; Sides = sides; Bonus = bonus;
    }

    /// <summary>Reads dice from text such as "3d6+2". Throws a FormatException if it does not make sense.</summary>
    public static FlxDice Parse(string text) =>
        TryParse(text, out var dice) ? dice : throw new FormatException($"'{text}' is not dice notation (for example 3d6+2).");

    public static bool TryParse(string text, out FlxDice dice)
    {
        dice = null!;
        var m = Pattern.Match(text ?? "");
        if (!m.Success) return false;

        int count, sides, bonus = 0;
        if (m.Groups[3].Success) { count = 0; sides = 0; bonus = int.Parse(m.Groups[3].Value); }       // a plain number
        else
        {
            count = m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value);
            sides = int.Parse(m.Groups[2].Value);
        }

        if (m.Groups[4].Success)
        {
            int extra = int.Parse(m.Groups[5].Value);
            bonus += m.Groups[4].Value == "-" ? -extra : extra;
        }
        dice = new FlxDice(count, sides, bonus);
        return true;
    }

    /// <summary>Rolls dice written as text, once.</summary>
    public static int Roll(string text) => Parse(text).Roll();

    /// <summary>Throws the dice.</summary>
    public int Roll(FlxRandom? random = null)
    {
        random ??= FlxG.Random;
        int total = Bonus;
        for (int i = 0; i < Count; i++) total += Sides <= 0 ? 0 : random.Int(1, Sides);
        return total;
    }

    /// <summary>Throws the dice and gives each die separately (the bonus is not included).</summary>
    public List<int> RollEach(FlxRandom? random = null)
    {
        random ??= FlxG.Random;
        var list = new List<int>(Count);
        for (int i = 0; i < Count; i++) list.Add(Sides <= 0 ? 0 : random.Int(1, Sides));
        return list;
    }

    public int Min => Count + Bonus;
    public int Max => Count * Sides + Bonus;
    public float Average => Count * (Sides + 1) / 2f + Bonus;

    public override string ToString() =>
        Count == 0 ? Bonus.ToString() : $"{Count}d{Sides}{(Bonus > 0 ? "+" + Bonus : Bonus < 0 ? Bonus.ToString() : "")}";
}

namespace MonoFlixel;

/// <summary>
/// Comparison functions for FlxTypedGroup.Sort. Typical use is drawing things lower on the screen in front:
/// <c>group.Sort(FlxSort.ByY)</c>.
/// </summary>
public static class FlxSort
{
    public const int Ascending = -1;
    public const int Descending = 1;

    /// <summary>Compares two numbers. With Ascending the smaller one sorts first.</summary>
    public static int ByValues(int order, float a, float b)
    {
        if (a < b) return order;
        if (a > b) return -order;
        return 0;
    }

    public static int ByY(int order, FlxObject a, FlxObject b) => ByValues(order, a.Y, b.Y);

    public static int ByX(int order, FlxObject a, FlxObject b) => ByValues(order, a.X, b.X);
}

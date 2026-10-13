using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace MonoFlixel.Demo.Drown;

public enum ItemKind { Oceanite, Territe, Magmite, Lantern }

/// <summary>What an item is: one table instead of a class per item.</summary>
public sealed record ItemInfo(string Name, string Description, float Weight, int MaxStack, string Icon, bool Usable);

/// <summary>A stack of one kind of item.</summary>
public sealed class ItemStack
{
    public ItemKind Kind;
    public int Count;
    public ItemInfo Info => DrownData.Items[Kind];
    public float Weight => Info.Weight * Count;
}

/// <summary>What goes in and what comes out when crafting.</summary>
public sealed record Recipe(ItemKind Result, (ItemKind Kind, int Count)[] Ingredients);

/// <summary>A bag of item stacks with a carry limit (by weight) and a limit of stacks.</summary>
public sealed class Inventory
{
    public readonly List<ItemStack> Stacks = new();
    public readonly int MaxStacks;
    public readonly float MaxWeight;

    public Inventory(int maxStacks, float maxWeight) { MaxStacks = maxStacks; MaxWeight = maxWeight; }

    public float Weight => Stacks.Sum(s => s.Weight);

    public int Count(ItemKind kind) => Stacks.Where(s => s.Kind == kind).Sum(s => s.Count);

    /// <summary>Adds as many as fit (by stack size, number of stacks and weight) and returns how many were added.</summary>
    public int Add(ItemKind kind, int count = 1)
    {
        var info = DrownData.Items[kind];
        int added = 0;

        while (added < count)
        {
            if (Weight + info.Weight > MaxWeight + 0.0001f) break;

            var stack = Stacks.Find(s => s.Kind == kind && s.Count < info.MaxStack);
            if (stack == null)
            {
                if (Stacks.Count >= MaxStacks) break;
                stack = new ItemStack { Kind = kind };
                Stacks.Add(stack);
            }
            stack.Count++;
            added++;
        }
        return added;
    }

    /// <summary>Takes items away (from the last stacks first); false (and nothing taken) if there are too few.</summary>
    public bool Remove(ItemKind kind, int count = 1)
    {
        if (Count(kind) < count) return false;

        for (int i = Stacks.Count - 1; i >= 0 && count > 0; i--)
        {
            var s = Stacks[i];
            if (s.Kind != kind) continue;
            int take = Math.Min(count, s.Count);
            s.Count -= take;
            count -= take;
            if (s.Count == 0) Stacks.RemoveAt(i);
        }
        return true;
    }

    public void Clear() => Stacks.Clear();

    /// <summary>Saved as "Kind:count" pairs.</summary>
    public string Serialize() => string.Join(";", Stacks.GroupBy(s => s.Kind).Select(g => $"{g.Key}:{g.Sum(s => s.Count)}"));

    public void Deserialize(string text)
    {
        Clear();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':');
            if (bits.Length == 2 && Enum.TryParse<ItemKind>(bits[0], out var kind) && int.TryParse(bits[1], out int n)) Add(kind, n);
        }
    }
}

/// <summary>Game-wide tables, what the player carries between dives, and the sound names.</summary>
public static class DrownData
{
    public static readonly Dictionary<ItemKind, ItemInfo> Items = new()
    {
        [ItemKind.Oceanite] = new("Oceanite Shard", "A small, broken shard of an Oceanite crystal. Harbors significant energy levels inside it. Must be refined on the ship before use.", 0.3f, 10, "drown/GUI/oceanite_icon.png", false),
        [ItemKind.Territe] = new("Territe Shard", "A shard of Territe: a dense, earthy crystal.", 0.3f, 10, "drown/GUI/territe_icon.png", false),
        [ItemKind.Magmite] = new("Magmite Shard", "A warm shard of Magmite, dropped by the creatures of the deep.", 0.3f, 10, "drown/GUI/magmite_icon.png", false),
        [ItemKind.Lantern] = new("Lantern", "A deployable, stationary light. Press L (or click it in the inventory).", 0.5f, 10, "drown/GUI/magmite_icon.png", true),
    };

    public static readonly Recipe[] Recipes =
    {
        new(ItemKind.Lantern, new[] { (ItemKind.Territe, 3), (ItemKind.Oceanite, 1), (ItemKind.Magmite, 2) }),
    };

    /// <summary>What the player carries: it stays when the player goes up to the map and down again.</summary>
    public static readonly Inventory Bag = new(10, 20);

    /// <summary>The components laid out in the crafting window.</summary>
    public static readonly Inventory Components = new(5, 9999);

    /// <summary>The first recipe the components are enough for, or null.</summary>
    public static Recipe? Craftable() =>
        Recipes.FirstOrDefault(r => r.Ingredients.All(i => Components.Count(i.Kind) >= i.Count));

    /// <summary>Uses up the components of a recipe and puts the result in the bag.</summary>
    public static bool Craft(Recipe recipe)
    {
        if (recipe.Ingredients.Any(i => Components.Count(i.Kind) < i.Count)) return false;
        if (Bag.Add(recipe.Result) == 0) return false;
        foreach (var (kind, count) in recipe.Ingredients) Components.Remove(kind, count);
        return true;
    }

    // ------------------------------------------------------------------ sounds (WAV files in Assets/drown/audio)

    public static FlxSound? Sfx(string name, float volume = 1f)
    {
        try { return FlxG.Sound.Play($"drown/audio/{name}.wav", volume); }
        catch (Exception) { return null; }          // a missing sound is not worth stopping the game
    }

    public static FlxSound? Loop(string name, float volume = 1f)
    {
        try
        {
            var s = FlxG.Sound.Load($"drown/audio/{name}.wav", volume, looped: true);
            s.Persist = false;
            return s;
        }
        catch (Exception) { return null; }
    }

    // ------------------------------------------------------------------ saves: the bag and each dive site

    public static void SaveBag()
    {
        FlxG.Save.Set("drown.bag", Bag.Serialize());
        FlxG.Save.Set("drown.components", Components.Serialize());
        FlxG.Save.Flush();
    }

    public static void LoadBag()
    {
        Bag.Deserialize(FlxG.Save.Get("drown.bag", ""));
        Components.Deserialize(FlxG.Save.Get("drown.components", ""));
    }

    static string SiteKey(int x, int y, int depth) => $"drown.site.{x}_{y}_{depth}";

    /// <summary>What changed in a dive site since it was generated (mined tiles, taken resources), as JSON.</summary>
    public static void SaveSite(int x, int y, int depth, SiteChanges changes)
    {
        FlxG.Save.Set(SiteKey(x, y, depth), JsonSerializer.Serialize(changes));
        FlxG.Save.Flush();
    }

    public static SiteChanges LoadSite(int x, int y, int depth)
    {
        string json = FlxG.Save.Get(SiteKey(x, y, depth), "");
        if (json.Length == 0) return new SiteChanges();
        try { return JsonSerializer.Deserialize<SiteChanges>(json) ?? new SiteChanges(); }
        catch (JsonException) { return new SiteChanges(); }
    }

    public static void ForgetSite(int x, int y, int depth)
    {
        FlxG.Save.Remove(SiteKey(x, y, depth));
        FlxG.Save.Flush();
    }
}

/// <summary>
/// The differences between a freshly generated dive site and how the player left it. The site itself is not saved: it comes from its
/// seed again, so only the mined tiles and the things taken have to be remembered.
/// </summary>
public sealed class SiteChanges
{
    public List<int> MinedTiles { get; set; } = new();
    public List<int> TakenEntities { get; set; } = new();
    public List<int> Lanterns { get; set; } = new();
}

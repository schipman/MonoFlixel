using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MonoFlixel.Demo.PostApoc;

/// <summary>What a kind of item is. The original read these from items.xml; they are a table here.</summary>
public sealed record ItemDef(string Name, string Texture, string Description,
    int Hunger = 0, int Thirst = 0, int Health = 0, bool Usable = true, int Fatigue = 0, bool Camp = false);

/// <summary>The biomes a room can be (the original had more planned: desert, forest, rural...).</summary>
public enum Biome { City, Seaside }

/// <summary>What the survivor achieved in one run (for the game over screen).</summary>
public sealed class RunStats
{
    /// <summary>One room is one mile: 2560 pixels.</summary>
    public const float PixelsPerMile = 2560f;

    public float Distance;
    public int Eaten, Drank, Kills, Rests;

    public float Miles => Distance / PixelsPerMile;

    /// <summary>
    /// The score: 100 for every mile, 25 for each kill, 10 for each rest, 5 for every meal and every drink. Distance matters most,
    /// but staying alive (and fed) on the way adds up.
    /// </summary>
    public int Score => (int)(Miles * 100) + Kills * 25 + Rests * 10 + Eaten * 5 + Drank * 5;
}

/// <summary>Game-wide tables, the things the player carries, and the save.</summary>
public static class PostApocData
{
    public const string Root = "postapoc/";

    public static readonly ItemDef Soda = new("Soda", Root + "items/item_soda.png", "A can of soda. Warm, but wet.", Thirst: -40);
    public static readonly ItemDef Beans = new("Beans", Root + "items/item_can_beans.png", "Canned beans.", Hunger: -25);
    public static readonly ItemDef Veggies = new("Veggies", Root + "items/item_can_veggies.png", "Canned vegetables.", Hunger: -20, Health: 3);
    public static readonly ItemDef Firewood = new("Firewood", Root + "items/item_firewood.png", "Firewood. Use it to make camp and rest: much less tired.", Fatigue: -55, Camp: true);
    public static readonly ItemDef CannedMeat = new("Canned Meat", Root + "items/item_cannedmeat.png", "Canned meat. Better not to ask.", Hunger: -40, Health: 5, Fatigue: -5);
    public static readonly ItemDef CandyBar = new("Candy Bar", Root + "items/item_candybar.png", "A candy bar from before.", Hunger: -10, Health: 8, Fatigue: -6);

    public static readonly ItemDef[] AllItems = { Soda, Beans, Veggies, Firewood, CannedMeat, CandyBar };

    /// <summary>Something to find on the road, by how common it is: wood is everywhere, food is rare.</summary>
    public static ItemDef RandomItem()
    {
        (ItemDef Item, float Weight)[] table = { (Firewood, 30), (Soda, 22), (Beans, 12), (CandyBar, 10), (Veggies, 14), (CannedMeat, 8) };
        float roll = FlxG.Random.Float() * table.Sum(t => t.Weight);
        foreach (var (item, weight) in table)
        {
            if (roll < weight) return item;
            roll -= weight;
        }
        return Firewood;
    }

    public static ItemDef? Find(string name) => AllItems.FirstOrDefault(i => i.Name == name);

    // ------------------------------------------------------------------ what the player carries (it stays from room to room)

    public const int StartingAmmo = 10;

    public const int BagSlots = 16, EquipSlots = 12, QuickSlots = 5;

    public static readonly ItemDef?[] Bag = new ItemDef?[BagSlots];

    /// <summary>Slots 1 to 5 are quick-use slots (keys 1 to 5). The weapon and armor slots (6 to 12) are there, as in the original, but nothing fits in them yet.</summary>
    public static readonly ItemDef?[] Equipment = new ItemDef?[EquipSlots];

    public static int BagCount => Bag.Count(i => i != null);

    public static bool AddToBag(ItemDef item)
    {
        int i = Array.FindIndex(Bag, s => s == null);
        if (i < 0) return false;
        Bag[i] = item;
        return true;
    }

    public static void ClearItems()
    {
        Array.Clear(Bag);
        Array.Clear(Equipment);
    }

    // ------------------------------------------------------------------ the save (FlxG.Save; the original wrote save0.dat as XML)

    public sealed class SaveData
    {
        public int Health { get; set; } = 100;
        public int Hunger { get; set; }
        public int Thirst { get; set; }
        public int Fatigue { get; set; }
        public int Ammo { get; set; } = StartingAmmo;
        public float Distance { get; set; }
        public int Eaten { get; set; }
        public int Drank { get; set; }
        public int Kills { get; set; }
        public int Rests { get; set; }
        public bool Female { get; set; }
        public int Room { get; set; } = 1;
        public string[] Bag { get; set; } = Array.Empty<string>();
        public string[] Equipment { get; set; } = Array.Empty<string>();
    }

    const string SaveKey = "postapoc.save";

    public static bool HasSave => FlxG.Save.Get(SaveKey, "").Length > 0;

    public static void Save(SaveData data)
    {
        data.Bag = Bag.Select(i => i?.Name ?? "").ToArray();
        data.Equipment = Equipment.Select(i => i?.Name ?? "").ToArray();
        FlxG.Save.Set(SaveKey, JsonSerializer.Serialize(data));
        FlxG.Save.Flush();
    }

    public static SaveData? Load()
    {
        string json = FlxG.Save.Get(SaveKey, "");
        if (json.Length == 0) return null;
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json);
            if (data == null) return null;
            for (int i = 0; i < BagSlots; i++) Bag[i] = i < data.Bag.Length ? Find(data.Bag[i]) : null;
            for (int i = 0; i < EquipSlots; i++) Equipment[i] = i < data.Equipment.Length ? Find(data.Equipment[i]) : null;
            return data;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>The best score so far (kept when a run ends, unlike the save of the run).</summary>
    public static int HighScore
    {
        get => FlxG.Save.Get("postapoc.highscore", 0);
        set { FlxG.Save.Set("postapoc.highscore", value); FlxG.Save.Flush(); }
    }

    public static void EraseSave()
    {
        FlxG.Save.Remove(SaveKey);
        FlxG.Save.Flush();
    }

    // ------------------------------------------------------------------ sounds

    public static FlxSound? Sfx(string name, float volume = 1f)
    {
        try { return FlxG.Sound.Play($"{Root}audio/{name}.wav", volume); }
        catch (Exception) { return null; }
    }
}

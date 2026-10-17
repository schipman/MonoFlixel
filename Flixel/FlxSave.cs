using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace MonoFlixel;

public enum FlxSaveStatus { Empty, Bound, Error }

/// <summary>
/// Saves small bits of game data (scores, settings, progress) between runs, as a JSON file in the user's
/// application-data folder. Port of Flixel's FlxSave; <c>FlxG.Save</c> is the game-wide one, bound for you.
/// <code>
/// int best = FlxG.Save.Get("highScore", 0);
/// FlxG.Save.Set("highScore", 120);
/// FlxG.Save.Flush();                     // writes the file (also happens automatically when the game exits)
/// </code>
/// Any value System.Text.Json can handle works: numbers, strings, bools, lists, dictionaries, your own classes.
/// <para>
/// The static methods at the end save and load whole objects to files of your choice, in text, JSON, XML or binary form, which is how game data
/// (monsters, items, levels) can live in files that players edit:
/// <code>
/// FlxSave.Save("data/monsters/goblin.json", goblin);        // the format follows the extension: .json .xml .txt .bin
/// var back = FlxSave.Load&lt;Monster&gt;("data/monsters/goblin.json");
/// var all  = FlxSave.LoadAll&lt;Monster&gt;("data/monsters", "*.json");
/// </code>
/// </para>
/// </summary>
public sealed class FlxSave
{
    JsonObject _data = new();
    bool _dirty;

    public FlxSaveStatus Status { get; private set; } = FlxSaveStatus.Empty;

    /// <summary>The save's name (its file name, without extension).</summary>
    public string? Name { get; private set; }

    /// <summary>Full path of the save file once bound.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Last error message, if Status is Error.</summary>
    public string? Error { get; private set; }

    /// <summary>True if the data changed since the last Flush.</summary>
    public bool IsDirty => _dirty;

    /// <summary>Folder used when Bind is called without a path: %AppData%/MonoFlixel on Windows.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonoFlixel");

    /// <summary>
    /// Opens (or creates) the save with this name and loads its data. Returns true if it worked.
    /// Pass a folder to keep saves somewhere else (e.g. next to the game while testing).
    /// </summary>
    public bool Bind(string name, string? folder = null)
    {
        Close();
        try
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A save needs a name.", nameof(name));

            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string dir = folder ?? DefaultFolder;
            Directory.CreateDirectory(dir);

            Name = name;
            FilePath = Path.Combine(dir, name + ".json");

            _data = new JsonObject();
            if (File.Exists(FilePath))
            {
                // a damaged file shouldn't stop the game: start with empty data and keep the broken file as .bad
                try { _data = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? new JsonObject(); }
                catch (JsonException) { File.Copy(FilePath, FilePath + ".bad", overwrite: true); _data = new JsonObject(); }
            }

            _dirty = false;
            Status = FlxSaveStatus.Bound;
            Error = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = FlxSaveStatus.Error;
            Error = e.Message;
            return false;
        }
    }

    // ------------------------------------------------------------------ data

    public bool Has(string key) => _data.ContainsKey(key);

    public IEnumerable<string> Keys
    {
        get { foreach (var kv in _data) yield return kv.Key; }
    }

    /// <summary>Reads a value, or <paramref name="fallback"/> if it was never saved (or can't be read as this type).</summary>
    public T Get<T>(string key, T fallback = default!)
    {
        if (!_data.TryGetPropertyValue(key, out var node) || node == null) return fallback;
        try { return node.Deserialize<T>() ?? fallback; }
        catch (JsonException) { return fallback; }
        catch (InvalidOperationException) { return fallback; }
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default!;
        if (!_data.TryGetPropertyValue(key, out var node) || node == null) return false;
        try
        {
            var v = node.Deserialize<T>();
            if (v == null) return false;
            value = v;
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public void Set<T>(string key, T value)
    {
        _data[key] = JsonSerializer.SerializeToNode(value);
        _dirty = true;
    }

    public bool Remove(string key)
    {
        bool removed = _data.Remove(key);
        if (removed) _dirty = true;
        return removed;
    }

    // ------------------------------------------------------------------ file

    /// <summary>Writes the data to disk (only if it changed, unless <paramref name="force"/>). Returns true if the file is up to date.</summary>
    public bool Flush(bool force = false)
    {
        if (Status != FlxSaveStatus.Bound || FilePath == null) return false;
        if (!_dirty && !force) return true;

        try
        {
            // write next to the file first, so a crash mid-write can't leave a half-written save
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, _data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
            _dirty = false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = FlxSaveStatus.Error;
            Error = e.Message;
            return false;
        }
    }

    /// <summary>Deletes all data and the save file.</summary>
    public bool Erase()
    {
        _data = new JsonObject();
        _dirty = false;
        try
        {
            if (FilePath != null && File.Exists(FilePath)) File.Delete(FilePath);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = FlxSaveStatus.Error;
            Error = e.Message;
            return false;
        }
    }

    /// <summary>Flushes (if bound) and unbinds. Get/Set still work in memory afterwards but nothing is written.</summary>
    public void Close()
    {
        if (Status == FlxSaveStatus.Bound) Flush();
        Status = FlxSaveStatus.Empty;
        Name = null;
        FilePath = null;
    }

    // ------------------------------------------------------------------ whole files: text, JSON, XML, binary

    /// <summary>
    /// How JSON files are read and written: indented, names in any case, // comments and trailing commas allowed (so players can annotate the
    /// files they edit), and enums as words.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    const string BinaryMagic = "FLXB";

    public static bool Exists(string path) => File.Exists(path);

    /// <summary>The kinds of file the Save and Load methods tell apart by extension.</summary>
    public enum Format { Json, Xml, Text, Binary }

    /// <summary>.json is JSON, .xml is XML, .txt and .text are plain text (only for strings), and anything else (.bin, .dat, .sav) is binary.</summary>
    public static Format FormatOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".json" => Format.Json,
        ".xml" => Format.Xml,
        ".txt" or ".text" => Format.Text,
        _ => Format.Binary,
    };

    /// <summary>Saves an object in the format the extension of the path says; the folders are made if they are missing.</summary>
    public static void Save<T>(string path, T data)
    {
        switch (FormatOf(path))
        {
            case Format.Json: SaveJson(path, data); break;
            case Format.Xml: SaveXml(path, data); break;
            case Format.Text: SaveText(path, data?.ToString() ?? ""); break;
            default: SaveBinary(path, data); break;
        }
    }

    /// <summary>Loads an object the way <see cref="Save{T}"/> saved it. Throws if the file is missing or damaged; see <see cref="TryLoad{T}"/>.</summary>
    public static T Load<T>(string path)
    {
        switch (FormatOf(path))
        {
            case Format.Json: return LoadJson<T>(path);
            case Format.Xml: return LoadXml<T>(path);
            case Format.Text:
                if (typeof(T) != typeof(string)) throw new NotSupportedException("A text file holds a string; use .json, .xml or .bin for other things.");
                return (T)(object)LoadText(path);
            default: return LoadBinary<T>(path);
        }
    }

    /// <summary>Like Load, but instead of throwing it says what went wrong and returns false.</summary>
    public static bool TryLoad<T>(string path, out T? data, out string? error)
    {
        data = default;
        error = null;
        try { data = Load<T>(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            error = e is InvalidOperationException { InnerException: { } inner } ? inner.Message : e.Message;
            return false;
        }
    }

    /// <summary>
    /// Loads every file of a folder that matches a pattern (a folder of monsters, one file each). Files that can not be read are skipped, and
    /// <paramref name="onError"/> is told which and why. The files come in the order of their names.
    /// </summary>
    public static List<(string Path, T Data)> LoadAll<T>(string folder, string pattern = "*.json", Action<string, string>? onError = null)
    {
        var result = new List<(string, T)>();
        if (!Directory.Exists(folder)) return result;
        var files = Directory.GetFiles(folder, pattern);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (TryLoad<T>(file, out var data, out var error) && data != null) result.Add((file, data));
            else onError?.Invoke(file, error ?? "empty");
        }
        return result;
    }

    // ---- text

    public static void SaveText(string path, string text) => WriteFile(path, Encoding.UTF8.GetBytes(text));
    public static string LoadText(string path) => File.ReadAllText(path, Encoding.UTF8);

    // ---- JSON

    public static void SaveJson<T>(string path, T data) => WriteFile(path, JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions));

    public static T LoadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), JsonOptions) ?? throw new JsonException($"{path} is empty.");

    // ---- XML (what XmlSerializer takes: public classes with a constructor that takes nothing, public fields and properties, lists and arrays,
    //      but no dictionaries; use JSON or binary for those)

    public static void SaveXml<T>(string path, T data)
    {
        using var buffer = new MemoryStream();
        new XmlSerializer(typeof(T)).Serialize(buffer, data);
        WriteFile(path, buffer.ToArray());
    }

    public static T LoadXml<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return (T?)new XmlSerializer(typeof(T)).Deserialize(stream) ?? throw new InvalidDataException($"{path} is empty.");
    }

    // ---- binary: raw bytes, or an object (its JSON, compressed, behind a four letter mark so a wrong file is noticed)

    public static void SaveBytes(string path, byte[] bytes) => WriteFile(path, bytes);
    public static byte[] LoadBytes(string path) => File.ReadAllBytes(path);

    public static void SaveBinary<T>(string path, T data)
    {
        using var buffer = new MemoryStream();
        buffer.Write(Encoding.ASCII.GetBytes(BinaryMagic));
        using (var zip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            zip.Write(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions));
        WriteFile(path, buffer.ToArray());
    }

    public static T LoadBinary<T>(string path)
    {
        using var stream = File.OpenRead(path);
        var mark = new byte[BinaryMagic.Length];
        if (stream.Read(mark, 0, mark.Length) != mark.Length || Encoding.ASCII.GetString(mark) != BinaryMagic)
            throw new InvalidDataException($"{path} is not a file saved by FlxSave.");
        using var zip = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(zip, JsonOptions) ?? throw new InvalidDataException($"{path} is empty.");
    }

    /// <summary>Writes next to the file first, so a crash half way can not leave a half written file.</summary>
    static void WriteFile(string path, byte[] bytes)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }
}

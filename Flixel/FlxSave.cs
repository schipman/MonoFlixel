using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

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
}

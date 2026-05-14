using System.Text.Json;

namespace GTA1MapEditor.App;

/// <summary>
/// Tracks the last <see cref="MaxEntries"/> opened .CMP file paths in a
/// per-user JSON file under %LocalAppData%/GTA1MapEditor/recent.json.
/// </summary>
public static class RecentFiles
{
    public const int MaxEntries = 5;

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GTA1MapEditor", "recent.json");

    private sealed class Store { public List<string> Paths { get; set; } = new(); }

    /// <summary>Read the saved list. Missing or malformed file yields an empty list.</summary>
    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return new List<string>();
            var json = File.ReadAllText(StorePath);
            var store = JsonSerializer.Deserialize<Store>(json);
            return store?.Paths?.Where(File.Exists).Take(MaxEntries).ToList() ?? new();
        }
        catch { return new List<string>(); }
    }

    /// <summary>Bump <paramref name="path"/> to the top of the list (deduped, trimmed to MaxEntries) and persist.</summary>
    public static void Add(string path)
    {
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        Save(list);
    }

    public static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var json = JsonSerializer.Serialize(new Store { Paths = list },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StorePath, json);
        }
        catch { /* best-effort; ignore I/O failures */ }
    }
}

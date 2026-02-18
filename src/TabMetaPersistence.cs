using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace gPad;

public static class TabMetaPersistence
{
    private static string TabMetaPath => Path.Combine(PathHelper.GetConfigDirectory(), "tabmeta.json");
    private static string TabIconsDir => Path.Combine(PathHelper.GetConfigDirectory(), "tabicons");

    public static Dictionary<string, TabMeta> Load()
    {
        var result = new Dictionary<string, TabMeta>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(TabMetaPath)) return result;
            var json = File.ReadAllText(TabMetaPath);
            var list = JsonSerializer.Deserialize<List<TabMetaEntry>>(json);
            if (list == null) return result;
            foreach (var e in list)
                if (!string.IsNullOrEmpty(e.Path))
                    result[e.Path] = new TabMeta(e.IconPath, e.Color);
        }
        catch { /* ignore */ }
        return result;
    }

    public static void Save(Dictionary<string, TabMeta> meta)
    {
        try
        {
            var list = new List<TabMetaEntry>();
            foreach (var kv in meta)
                list.Add(new TabMetaEntry { Path = kv.Key, IconPath = kv.Value.IconPath, Color = kv.Value.Color });
            var dir = Path.GetDirectoryName(TabMetaPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(TabMetaPath, JsonSerializer.Serialize(list));
        }
        catch { /* ignore */ }
    }

    /// <summary>Copy icon file to app config and return the local path.</summary>
    public static string? StoreIcon(string sourceFilePath)
    {
        try
        {
            Directory.CreateDirectory(TabIconsDir);
            var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var destName = Path.GetFileNameWithoutExtension(sourceFilePath) + "_" + Guid.NewGuid().ToString("N")[..8] + ext;
            var destPath = Path.Combine(TabIconsDir, destName);
            File.Copy(sourceFilePath, destPath, true);
            return destPath;
        }
        catch { return null; }
    }

    private class TabMetaEntry
    {
        public string Path { get; set; } = "";
        public string? IconPath { get; set; }
        public string? Color { get; set; }
    }
}

public sealed class TabMeta
{
    public string? IconPath { get; set; }
    public string? Color { get; set; }

    public TabMeta(string? iconPath, string? color)
    {
        IconPath = iconPath;
        Color = color;
    }
}

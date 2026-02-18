using System.IO;
using System.Text.Json;

namespace gPad;

public static class SettingsPersistence
{
    private static string SettingsPath => Path.Combine(PathHelper.GetConfigDirectory(), "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            var o = JsonSerializer.Deserialize<AppSettings>(json);
            return o ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings s)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
        }
        catch { /* ignore */ }
    }
}

public sealed class AppSettings
{
    public double Opacity { get; set; } = 1.0;
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 14;
    /// <summary>Legacy single corner radius; used when loading old config. When saving we use the four corner values.</summary>
    public double CornerRadius { get; set; } = 0;
    public double CornerRadiusTopLeft { get; set; }
    public double CornerRadiusTopRight { get; set; }
    public double CornerRadiusBottomRight { get; set; }
    public double CornerRadiusBottomLeft { get; set; }
}

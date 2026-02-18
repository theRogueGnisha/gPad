using System.IO;
using System.Windows;

namespace gPad;

public static class WindowStatePersistence
{
    public static void Save(Window window)
    {
        try
        {
            var path = PathHelper.WindowStatePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var content = $"{window.Left}\n{window.Top}\n{window.Width}\n{window.Height}\n{(window.WindowState == WindowState.Maximized ? 1 : 0)}";
            File.WriteAllText(path, content);
        }
        catch
        {
            // Ignore persistence errors
        }
    }

    public static void Restore(Window window)
    {
        try
        {
            var path = PathHelper.WindowStatePath;
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path);
            if (lines.Length < 4)
                return;

            if (double.TryParse(lines[0], out var left) &&
                double.TryParse(lines[1], out var top) &&
                double.TryParse(lines[2], out var width) &&
                double.TryParse(lines[3], out var height))
            {
                window.Left = left;
                window.Top = top;
                window.Width = Math.Max(200, width);
                window.Height = Math.Max(150, height);
                window.WindowStartupLocation = WindowStartupLocation.Manual;
            }

            if (lines.Length >= 5 && lines[4] == "1")
                window.WindowState = WindowState.Maximized;
        }
        catch
        {
            // Use default position
        }
    }
}

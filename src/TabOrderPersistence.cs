using System.Collections.Generic;
using System.IO;

namespace gPad;

public static class TabOrderPersistence
{
    private static string TabOrderPath => Path.Combine(PathHelper.GetConfigDirectory(), "taborder.txt");

    public static void Save(IEnumerable<string> filePaths)
    {
        try
        {
            File.WriteAllLines(TabOrderPath, filePaths);
        }
        catch
        {
            // Ignore
        }
    }

    public static List<string> Load()
    {
        var result = new List<string>();
        try
        {
            if (!File.Exists(TabOrderPath))
                return result;
            foreach (var line in File.ReadAllLines(TabOrderPath))
            {
                var path = line.Trim();
                if (path.Length > 0)
                    result.Add(path);
            }
        }
        catch
        {
            // Ignore
        }
        return result;
    }
}

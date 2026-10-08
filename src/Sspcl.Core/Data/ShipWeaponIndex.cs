using System.Text;
using Sspcl.Core.Mods;

namespace Sspcl.Core.Data;

/// <summary>为配装界面提供已安装舰船和武器的轻量索引。</summary>
public static class ShipWeaponIndex
{
    public static (List<string> Ships, List<string> Weapons) Scan(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("游戏目录不能为空。", nameof(gameDirectory));

        var ships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var weapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFromRoot(Path.Combine(gameDirectory, "starsector-core"), ships, weapons);

        var enabled = new HashSet<string>(EnabledModsFile.Read(gameDirectory), StringComparer.OrdinalIgnoreCase);
        foreach (var mod in ModScanner.Scan(Path.Combine(gameDirectory, "mods")))
        {
            if (string.IsNullOrWhiteSpace(mod.Id) ||
                (enabled.Count > 0 && !enabled.Contains(mod.Id))) continue;
            AddFromRoot(mod.Path, ships, weapons);
        }

        return (ships.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                weapons.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void AddFromRoot(string root, HashSet<string> ships, HashSet<string> weapons)
    {
        AddCsv(Path.Combine(root, "data", "hulls", "ship_data.csv"), ships);
        AddCsv(Path.Combine(root, "data", "weapons", "weapon_data.csv"), weapons);
    }

    private static void AddCsv(string path, HashSet<string> destination)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                continue;
            var columns = ParseCsvLine(line);
            if (columns.Count < 2) continue;
            var name = columns[0].Trim();
            var id = columns[1].Trim();
            if (string.IsNullOrEmpty(id) || id.Equals("id", StringComparison.OrdinalIgnoreCase))
                continue;
            destination.Add(string.IsNullOrEmpty(name) ? id : $"{name} ({id})");
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var columns = new List<string>();
        var value = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                columns.Add(value.ToString());
                value.Clear();
            }
            else value.Append(c);
        }
        columns.Add(value.ToString());
        return columns;
    }
}

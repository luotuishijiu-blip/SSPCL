using System.Globalization;
using System.Text.Json.Nodes;
using Sspcl.Core.Jsonc;
using Sspcl.Core.Versioning;

namespace Sspcl.Core.Mods;

/// <summary>扫描一个 mods 目录，宽容解析每个 mod_info.json（行为与 scripts/jsonc_dialect.py 一致）。</summary>
public static class ModScanner
{
    public static IReadOnlyList<ModSpec> Scan(string modsDirectory)
    {
        var list = new List<ModSpec>();
        if (!Directory.Exists(modsDirectory)) return list;
        foreach (var dir in Directory.EnumerateDirectories(modsDirectory))
        {
            var folder = Path.GetFileName(dir);
            var infoPath = Path.Combine(dir, "mod_info.json");
            if (!File.Exists(infoPath)) continue; // 无 mod_info.json 的目录 = 非 mod，跳过（与游戏本体一致）

            string raw;
            try { raw = File.ReadAllText(infoPath); }
            catch (Exception ex)
            {
                list.Add(new ModSpec { Folder = folder, Path = dir, Error = $"读取失败: {ex.Message}" });
                continue;
            }

            try
            {
                var spec = ParseModInfo(raw, dir, folder);
                list.Add(string.IsNullOrWhiteSpace(spec.Id) ? spec with { Error = "缺少 id 字段" } : spec);
            }
            catch (Exception ex)
            {
                list.Add(new ModSpec { Folder = folder, Path = dir, Error = $"解析失败: {ex.Message}" });
            }
        }
        return list;
    }

    public static ModSpec ParseModInfo(string raw, string path, string folder)
    {
        var root = JsoncParser.Parse(raw) as JsonObject
            ?? throw new System.Text.Json.JsonException("根节点不是对象");

        var (shape, versionRaw, version) = ParseVersion(root);
        string gameVersionRaw = Str(root, "gameVersion");

        return new ModSpec
        {
            Folder = folder,
            Path = path,
            Id = Str(root, "id"),
            Name = Str(root, "name"),
            Description = Str(root, "description"),
            Authors = ParseAuthors(root),
            VersionShape = shape,
            VersionRaw = versionRaw,
            Version = version,
            GameVersionRaw = gameVersionRaw,
            GameVersion = GameVersion.Parse(gameVersionRaw),
            Utility = GetBool(root, "utility"),
            TotalConversion = GetBool(root, "totalConversion"),
            Jars = ParseStringArray(root, "jars"),
            Dependencies = ParseDependencies(root),
            ModPlugin = Str(root, "modPlugin"),
        };
    }

    private static (string Shape, string Raw, ModVersion Version) ParseVersion(JsonObject root)
    {
        if (root["version"] is JsonObject vo)
        {
            var v = new ModVersion(Int(vo, "major"), Int(vo, "minor"), Int(vo, "patch"));
            return ("dict", v.ToString(), v);
        }
        if (root["version"] is JsonValue vv)
        {
            if (vv.TryGetValue<string>(out var s))
                return ("str", s, ModVersion.FromString(s));
            if (vv.TryGetValue<int>(out var i))
                return ("int", i.ToString(CultureInfo.InvariantCulture), new ModVersion(i, 0, 0));
            if (vv.TryGetValue<double>(out var d))
                return ("float", d.ToString(CultureInfo.InvariantCulture), new ModVersion((int)d, 0, 0));
        }
        return ("", "", ModVersion.Unspecified);
    }

    private static IReadOnlyList<string> ParseAuthors(JsonObject root)
    {
        var list = new List<string>();
        if (root["author"] is JsonValue a && a.TryGetValue<string>(out var s) && !string.IsNullOrEmpty(s))
            list.Add(s);
        if (root["authors"] is JsonArray arr)
            foreach (var x in arr)
                if (x is JsonValue xv && xv.TryGetValue<string>(out var s2))
                    list.Add(s2);
        return list;
    }

    private static IReadOnlyList<ModDependency> ParseDependencies(JsonObject root)
    {
        var list = new List<ModDependency>();
        if (root["dependencies"] is JsonArray arr)
            foreach (var x in arr)
                if (x is JsonObject dep)
                    list.Add(new ModDependency(Str(dep, "id"), Str(dep, "name"), Str(dep, "version")));
        return list;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonObject root, string key)
    {
        var list = new List<string>();
        if (root[key] is JsonArray arr)
            foreach (var x in arr)
                if (x is JsonValue v && v.TryGetValue<string>(out var s))
                    list.Add(s);
        return list;
    }

    private static string Str(JsonObject o, string key)
    {
        var n = o[key];
        return n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    }

    private static int Int(JsonObject o, string key)
    {
        var n = o[key];
        if (n is JsonValue v)
        {
            if (v.TryGetValue<int>(out var i)) return i;
            if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var i2)) return i2;
        }
        return 0;
    }

    private static bool GetBool(JsonObject o, string key)
    {
        var n = o[key];
        if (n is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<string>(out var s)) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

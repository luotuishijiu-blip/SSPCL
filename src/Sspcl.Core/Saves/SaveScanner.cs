using System.Xml;

namespace Sspcl.Core.Saves;

/// <summary>扫描 saves\ 目录并解析 descriptor.xml（只读，不写存档）。</summary>
public static class SaveScanner
{
    public static bool IsSaveDirectory(string dir) => File.Exists(Path.Combine(dir, "descriptor.xml"));

    public static IReadOnlyList<SaveInfo> Scan(string savesDirectory)
    {
        var list = new List<SaveInfo>();
        if (!Directory.Exists(savesDirectory)) return list;
        foreach (var dir in Directory.EnumerateDirectories(savesDirectory))
        {
            var descriptor = Path.Combine(dir, "descriptor.xml");
            if (!File.Exists(descriptor)) continue; // common\ / missions\ 等非存档目录
            try
            {
                list.Add(ParseDescriptor(dir, descriptor));
            }
            catch
            {
                // 个别损坏的存档跳过，不拖垮整表
            }
        }
        return list;
    }

    public static SaveInfo ParseDescriptor(string dir, string descriptorPath)
    {
        var doc = new XmlDocument();
        doc.Load(descriptorPath);

        string T(string name) =>
            doc.SelectSingleNode($"/SaveGameData/{name}")?.InnerText.Trim() ?? "";

        int level = int.TryParse(T("characterLevel"), out var l) ? l : 0;

        return new SaveInfo
        {
            Folder = Path.GetFileName(dir),
            Path = dir,
            CharacterName = T("characterName"),
            CharacterLevel = level,
            GameVersion = T("gameVersion"),
            SaveDate = T("saveDate"),
            Difficulty = T("difficulty"),
            IronMode = T("isIronMode").Equals("true", StringComparison.OrdinalIgnoreCase),
            Compressed = T("compressed").Equals("true", StringComparison.OrdinalIgnoreCase),
            EnabledModIds = ResolveEnabledModIds(doc),
            AllModsEverEnabled = AllEverIds(doc),
            DescriptorSize = new FileInfo(descriptorPath).Length,
        };
    }

    /// <summary>
    /// allModsEverEnabled 里是完整定义（&lt;spec z="N"&gt;&lt;id&gt;…），
    /// enabledMods 里只放引用（&lt;spec ref="N"&gt;）。按 spec 的 z 值做映射解析出真实 id。
    /// </summary>
    private static IReadOnlyList<string> ResolveEnabledModIds(XmlDocument doc)
    {
        var byZ = new Dictionary<string, string>(StringComparer.Ordinal);
        var allSpecs = doc.SelectNodes("/SaveGameData/allModsEverEnabled/EnabledModData/spec");
        if (allSpecs != null)
        {
            foreach (XmlNode spec in allSpecs)
            {
                var z = spec.Attributes?["z"]?.Value ?? "";
                var id = spec.SelectSingleNode("id")?.InnerText.Trim() ?? "";
                if (z.Length > 0 && id.Length > 0) byZ[z] = id;
            }
        }

        var list = new List<string>();
        var enabledSpecs = doc.SelectNodes("/SaveGameData/enabledMods/EnabledModData/spec");
        if (enabledSpecs != null)
        {
            foreach (XmlNode spec in enabledSpecs)
            {
                var inline = spec.SelectSingleNode("id")?.InnerText.Trim() ?? "";
                if (inline.Length > 0)
                {
                    list.Add(inline); // 防御：个别存档可能内联 id
                    continue;
                }
                var refAttr = spec.Attributes?["ref"]?.Value ?? "";
                if (refAttr.Length > 0 && byZ.TryGetValue(refAttr, out var id) && id.Length > 0)
                    list.Add(id);
            }
        }
        return list;
    }

    private static IReadOnlyList<string> AllEverIds(XmlDocument doc)
    {
        var list = new List<string>();
        var specs = doc.SelectNodes("/SaveGameData/allModsEverEnabled/EnabledModData/spec");
        if (specs != null)
        {
            foreach (XmlNode spec in specs)
            {
                var id = spec.SelectSingleNode("id")?.InnerText.Trim() ?? "";
                if (id.Length > 0) list.Add(id);
            }
        }
        return list;
    }
}

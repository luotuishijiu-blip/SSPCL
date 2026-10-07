using Sspcl.Core.Versioning;

namespace Sspcl.Core.Mods;

public sealed class DependencyReport
{
    public Dictionary<string, List<string>> Duplicates { get; } = new();
    public List<(string ModId, string Folder, string DepId)> Missing { get; } = new();
    public List<string[]> Cycles { get; } = new();
    public List<(string Id, string GameVersionRaw)> OverVersion { get; } = new();
    public List<(string ModId, string DepId, string Required, string Installed)> VersionMismatch { get; } = new();

    public bool HasProblems =>
        Duplicates.Count > 0 || Missing.Count > 0 || Cycles.Count > 0 ||
        OverVersion.Count > 0 || VersionMismatch.Count > 0;
}

public static class DependencyResolver
{
    public static DependencyReport Analyze(IReadOnlyList<ModSpec> mods, GameVersion installVersion)
    {
        var report = new DependencyReport();
        var byId = mods
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var kv in byId.Where(kv => kv.Value.Count > 1))
            report.Duplicates[kv.Key] = kv.Value.Select(m => m.Folder).ToList();

        var installed = new HashSet<string>(byId.Keys, StringComparer.Ordinal);
        var installedVersion = byId.ToDictionary(kv => kv.Key, kv => kv.Value[0].Version, StringComparer.Ordinal);

        foreach (var m in mods)
        {
            foreach (var dep in m.Dependencies)
            {
                if (string.IsNullOrEmpty(dep.Id)) continue;
                if (!installed.Contains(dep.Id))
                {
                    report.Missing.Add((m.Id, m.Folder, dep.Id));
                }
                else if (!string.IsNullOrWhiteSpace(dep.Version))
                {
                    var required = ModVersion.FromString(dep.Version);
                    if (required > installedVersion[dep.Id])
                        report.VersionMismatch.Add((m.Id, dep.Id, dep.Version, installedVersion[dep.Id].ToString()));
                }
            }

            if (m.GameVersion.CompareTo(installVersion) > 0)
                report.OverVersion.Add((m.Id, m.GameVersionRaw));
        }

        report.Cycles.AddRange(FindCycles(mods));
        return report;
    }

    /// <summary>返回某批目标 mod 的传递闭包（含其全部已安装的硬前置，去重）。</summary>
    public static ISet<string> Closure(IEnumerable<string> targetIds, IReadOnlyList<ModSpec> mods)
    {
        var index = mods
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var result = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(targetIds.Where(id => index.ContainsKey(id)).Distinct(StringComparer.Ordinal));
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id)) continue;
            if (!index.TryGetValue(id, out var mod)) continue;
            foreach (var dep in mod.Dependencies)
                if (!string.IsNullOrEmpty(dep.Id) && index.ContainsKey(dep.Id))
                    stack.Push(dep.Id);
        }
        return result;
    }

    /// <summary>DFS 找环，返回每个环上的 id 链。</summary>
    private static IEnumerable<string[]> FindCycles(IReadOnlyList<ModSpec> mods)
    {
        var index = mods
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0=未访问 1=在栈 2=完成
        var onStack = new List<string>();
        var cycles = new List<string[]>();

        void Dfs(string id)
        {
            state[id] = 1;
            onStack.Add(id);
            if (index.TryGetValue(id, out var mod))
            {
                foreach (var dep in mod.Dependencies)
                {
                    if (string.IsNullOrEmpty(dep.Id) || !index.ContainsKey(dep.Id)) continue;
                    if (!state.TryGetValue(dep.Id, out var st) || st == 0)
                    {
                        Dfs(dep.Id);
                    }
                    else if (st == 1)
                    {
                        int start = onStack.IndexOf(dep.Id);
                        if (start >= 0)
                        {
                            var cycle = onStack.Skip(start).Append(dep.Id).ToArray();
                            cycles.Add(cycle);
                        }
                    }
                }
            }
            onStack.RemoveAt(onStack.Count - 1);
            state[id] = 2;
        }

        foreach (var id in index.Keys)
            if (!state.ContainsKey(id))
                Dfs(id);

        return cycles;
    }
}

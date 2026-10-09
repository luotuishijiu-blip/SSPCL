namespace Sspcl.Core.Mods;

/// <summary>按 MOD ID 启用；已安装的文件复用，停用仅更新启用清单。</summary>
public static class ModActivationService
{
    public static HashSet<string> Apply(string gameDir, IReadOnlyList<ModSpec> pool, IEnumerable<string> requested)
    {
        string modsDir = Path.Combine(gameDir, "mods");
        var installed = ModScanner.Scan(modsDir).Where(m => !string.IsNullOrWhiteSpace(m.Id)).OrderBy(m => m.Folder, StringComparer.Ordinal).ToList();
        // 优先复用游戏中已有的同 ID MOD，避免因目录名不同而复制重复版本。
        var available = installed.Concat(pool).Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(m => m.Id, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var desired = new HashSet<string>(requested.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        desired.UnionWith(DependencyResolver.Closure(desired, available));
        var installedIds = new HashSet<string>(installed.Select(m => m.Id), StringComparer.Ordinal);
        var moved = new List<(string Source, string Backup)>();
        Directory.CreateDirectory(modsDir);
        try
        {
            foreach (var group in installed.Where(m => desired.Contains(m.Id)).GroupBy(m => m.Id, StringComparer.Ordinal))
            {
                foreach (var duplicate in group.Skip(1))
                {
                    string backupRoot = Path.Combine(gameDir, "Sspcl", "ModBackups");
                    Directory.CreateDirectory(backupRoot);
                    string backup = Path.Combine(backupRoot, duplicate.Folder + "-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(duplicate.Path, backup);
                    moved.Add((duplicate.Path, backup));
                }
            }
            foreach (var mod in available.Where(m => desired.Contains(m.Id) && !installedIds.Contains(m.Id)))
            {
                string folder = Path.GetFileName(mod.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string destination = Path.Combine(modsDir, folder);
                if (Directory.Exists(destination) || File.Exists(destination))
                    throw new IOException("游戏中已有同名目录，无法启用 " + mod.Name + "：" + folder);
                string stage = Path.Combine(modsDir, ".sspcl-enable-" + Guid.NewGuid().ToString("N"));
                try
                {
                    CopyDirectory(mod.Path, stage);
                    Directory.Move(stage, destination);
                    installedIds.Add(mod.Id);
                }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            }
            EnabledModsFile.Write(gameDir, desired);
            return desired;
        }
        catch
        {
            foreach (var item in moved.AsEnumerable().Reverse())
                if (!Directory.Exists(item.Source) && Directory.Exists(item.Backup)) Directory.Move(item.Backup, item.Source);
            throw;
        }
    }

    internal static void CopyDirectory(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("MOD 包含链接目录，请先使用普通文件夹：" + source);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("MOD 包含链接文件：" + file);
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}

using Sspcl.Core.Install;
using Sspcl.Core.Versioning;

namespace Sspcl.Core.Mods;

public static class ModUpdateService
{
    public static bool IsCompatible(string declared, string gameVersion)
    {
        var target = GameVersion.Parse(declared);
        var game = GameVersion.Parse(gameVersion);
        return target != GameVersion.Unknown && game != GameVersion.Unknown &&
            target.Major == game.Major && target.Minor == game.Minor &&
            (target.Letter == 0 || target.Letter == game.Letter) &&
            (target.Rc == 0 || target.Rc == game.Rc);
    }

    /// <summary>先验证并暂存完整新包，再替换旧目录；成功后删除旧版，失败恢复。</summary>
    public static InstallResult UpdateArchive(string archivePath, string expectedId, string poolDir, string gameDir)
    {
        var result = new InstallResult();
        string unpacked = Path.Combine(Path.GetTempPath(), "sspcl-update-" + Guid.NewGuid().ToString("N"));
        var stages = new List<string>();
        var moved = new List<(string Original, string Backup)>();
        var placed = new List<string>();
        try
        {
            if (string.IsNullOrWhiteSpace(expectedId)) throw new InvalidDataException("缺少要更新的 MOD ID。");
            Directory.CreateDirectory(unpacked);
            ModInstaller.ExtractArchive(archivePath, unpacked);
            var specs = Directory.GetFiles(unpacked, "mod_info.json", SearchOption.AllDirectories)
                .Select(path => ModScanner.ParseModInfo(File.ReadAllText(path), Path.GetDirectoryName(path)!, Path.GetFileName(Path.GetDirectoryName(path)!))).ToList();
            var matches = specs.Where(mod => mod.Id == expectedId).ToList();
            if (matches.Count != 1) throw new InvalidDataException("更新包必须包含唯一的目标 MOD：" + expectedId + "。补丁或其他 MOD 不能用于替换。");
            var replacement = matches[0];
            if (Path.GetFullPath(replacement.Path).Equals(Path.GetFullPath(unpacked), StringComparison.OrdinalIgnoreCase))
            {
                var previous = ModScanner.Scan(poolDir).Concat(ModScanner.Scan(Path.Combine(gameDir, "mods"))).FirstOrDefault(mod => mod.Id == expectedId);
                if (previous != null) replacement = replacement with { Folder = previous.Folder };
            }
            string gameVersion = InstallationDetector.Detect(gameDir).Version;
            if (!IsCompatible(replacement.GameVersionRaw, gameVersion))
                throw new InvalidDataException("更新包适配游戏版本 " + replacement.GameVersionRaw + "，当前游戏为 " + gameVersion + "，已保留旧版 MOD。");
            foreach (string jar in replacement.Jars)
            {
                string jarPath = Path.GetFullPath(Path.Combine(replacement.Path, jar));
                if (!jarPath.StartsWith(Path.GetFullPath(replacement.Path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(jarPath))
                    throw new InvalidDataException("更新包缺少声明的 JAR 文件，可能不是完整 MOD：" + jar);
            }
            string gameMods = Path.Combine(gameDir, "mods");
            var roots = new List<string> { Path.GetFullPath(poolDir) };
            if (ModScanner.Scan(gameMods).Any(mod => mod.Id == expectedId) || EnabledModsFile.Read(gameDir).Contains(expectedId))
                roots.Add(Path.GetFullPath(gameMods));
            var plans = new List<(string Stage, string Destination, List<ModSpec> Old)>();
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(root);
                var old = ModScanner.Scan(root).Where(mod => mod.Id == expectedId).ToList();
                string destination = Path.Combine(root, replacement.Folder);
                if ((Directory.Exists(destination) || File.Exists(destination)) && !old.Any(mod => Path.GetFullPath(mod.Path).Equals(destination, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("更新目标与其他目录同名：" + replacement.Folder);
                string stage = Path.Combine(root, ".sspcl-new-" + Guid.NewGuid().ToString("N"));
                stages.Add(stage);
                ModActivationService.CopyDirectory(replacement.Path, stage);
                plans.Add((stage, destination, old));
            }
            foreach (var plan in plans)
            {
                foreach (var old in plan.Old)
                {
                    string backup = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(old.Path)!)!, ".sspcl-old-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(old.Path, backup);
                    moved.Add((old.Path, backup));
                    result.RemovedFolders.Add(old.Folder);
                }
                Directory.Move(plan.Stage, plan.Destination);
                placed.Add(plan.Destination);
            }
            result.Success = true;
            result.WasUpdate = moved.Count > 0;
            result.ModId = replacement.Id;
            result.Name = replacement.Name;
            result.TargetFolder = replacement.Folder;
            foreach (var old in moved)
            {
                try { Directory.Delete(old.Backup, true); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { result.Error = "新版本已安装，但旧版备份未能完全清理：" + old.Backup + "（" + ex.Message + "）"; }
            }
        }
        catch (Exception ex)
        {
            // 清除本次新目录后恢复旧版；清单从未变更，启用状态保持。
            foreach (string destination in placed.AsEnumerable().Reverse())
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            foreach (var old in moved.AsEnumerable().Reverse())
                if (Directory.Exists(old.Backup) && !Directory.Exists(old.Original)) Directory.Move(old.Backup, old.Original);
            result.Error = ex.Message;
        }
        finally
        {
            foreach (string stage in stages) { try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { } }
            try { if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true); } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }
        return result;
    }
}

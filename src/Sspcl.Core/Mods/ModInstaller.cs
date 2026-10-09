using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Sspcl.Core.Utility;

namespace Sspcl.Core.Mods;

public sealed class InstallResult
{
    public bool Success { get; set; }
    public string ModId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetFolder { get; set; } = "";
    public bool WasUpdate { get; set; }
    public List<string> RemovedFolders { get; } = new();
    public string Error { get; set; } = "";
}

/// <summary>
/// 安装 / 更新 / 删除 mod：
///   - zip 原生解压；7z/rar 借 WinRAR/7-Zip 解压
///   - 识别「压缩包内多一层同名目录」
///   - 同 id 已存在 → 视为更新：旧版先进回收站再装新版
///   - 删除 mod 目录走回收站
/// </summary>
public static class ModInstaller
{
    public static void RecycleDir(string path)
    {
        if (!Directory.Exists(path)) return;
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,
        };
        SHFileOperation(ref op);
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    /// <summary>从一个已解压的 mod 文件夹安装（读取其 mod_info.json，处理同名/同 id 覆盖）。</summary>
    public static InstallResult InstallFolder(string sourceFolder, string modsDir)
    {
        var r = new InstallResult();
        try
        {
            string infoPath = Path.Combine(sourceFolder, "mod_info.json");
            if (!File.Exists(infoPath)) { r.Error = "该文件夹内没有 mod_info.json"; return r; }

            var spec = ModScanner.ParseModInfo(File.ReadAllText(infoPath), sourceFolder, Path.GetFileName(sourceFolder));
            r.ModId = spec.Id;
            r.Name = string.IsNullOrWhiteSpace(spec.Name) ? Path.GetFileName(sourceFolder) : spec.Name;
            r.TargetFolder = Path.GetFileName(sourceFolder.TrimEnd('\\', '/'));

            var target = Path.Combine(modsDir, r.TargetFolder);

            // 同 id 或同名目录已存在 → 旧版进回收站（更新）
            foreach (var m in ModScanner.Scan(modsDir))
                if (spec.Id.Length > 0 && m.Id == spec.Id && !PathsEqual(m.Path, target))
                {
                    RecycleDir(m.Path);
                    r.RemovedFolders.Add(m.Folder);
                    r.WasUpdate = true;
                }
            if (Directory.Exists(target))
            {
                RecycleDir(target);
                r.RemovedFolders.Add(r.TargetFolder);
                r.WasUpdate = true;
            }

            Directory.CreateDirectory(modsDir);
            CopyDirectory(sourceFolder, target);
            r.Success = true;
            return r;
        }
        catch (Exception ex)
        {
            r.Error = ex.Message;
            return r;
        }
    }

    /// <summary>从压缩包安装：先解压到临时目录，找到 mod_info.json 所在目录，再走 InstallFolder。</summary>
    public static InstallResult InstallArchive(string archivePath, string modsDir)
    {
        string ext = Path.GetExtension(archivePath).ToLowerInvariant();
        var tmp = Path.Combine(Path.GetTempPath(), "sspcl-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            ExtractArchive(archivePath, tmp);

            var modRoot = FindModRoot(tmp);
            if (modRoot == null)
                return new InstallResult { Error = "压缩包内没有 mod_info.json" };

            return InstallFolder(modRoot, modsDir);
        }
        catch (Exception ex)
        {
            return new InstallResult { Error = ex.Message };
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    internal static void ExtractArchive(string archivePath, string destination)
    {
        string extension = Path.GetExtension(archivePath).ToLowerInvariant();
        if (extension == ".zip") ZipFile.ExtractToDirectory(archivePath, destination);
        else if (extension is ".7z" or ".rar") ExtractWithExternalTool(archivePath, destination);
        else throw new InvalidDataException("仅支持 .zip / .7z / .rar 压缩包");
    }

    private static string? FindModRoot(string dir)
    {
        // 优先取最浅层的 mod_info.json
        foreach (var path in Directory.EnumerateFiles(dir, "mod_info.json", SearchOption.AllDirectories))
            return Path.GetDirectoryName(path);
        return null;
    }

    private static void ExtractWithExternalTool(string archivePath, string destDir)
    {
        // 优先内置 7z.exe（随 sspcl 发布），其次系统 7-Zip，最后 WinRAR
        string bundled = Path.Combine(AppContext.BaseDirectory, "7z.exe");
        var candidates = new[]
        {
            bundled,
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
            @"C:\Program Files\WinRAR\WinRAR.exe",
            @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
        };
        string tool = candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("未找到解压工具（7-Zip / WinRAR），请先解压成 .zip");

        bool is7z = Path.GetFileName(tool).StartsWith("7z", StringComparison.OrdinalIgnoreCase);
        var psi = new ProcessStartInfo
        {
            FileName = tool,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (is7z)
        {
            psi.Arguments = CommandLine.Quote(new[] { "x", "-y", "-o" + destDir, archivePath });
        }
        else
        {
            psi.Arguments = CommandLine.Quote(new[] { "x", "-y", "-inul", archivePath, destDir + Path.DirectorySeparatorChar });
        }

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("无法启动解压工具");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"解压失败（退出码 {p.ExitCode}）");
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string rel = f.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destFile = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(f, destFile, overwrite: true);
        }
    }
}

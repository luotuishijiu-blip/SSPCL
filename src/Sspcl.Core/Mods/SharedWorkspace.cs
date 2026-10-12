using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;

namespace Sspcl.Core.Mods;
public sealed class WorkspacePreferences
{
    public string GamePath { get; set; } = "";
    public string ModsPath { get; set; } = "";
    public string ProfileId { get; set; } = "";
}
public static class SharedWorkspace
{
    public static WorkspacePreferences Read(string path)
    {
        if(!File.Exists(path))return new();
        try { return JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(path)) ?? throw new InvalidDataException("目录配置为空。"); }
        catch(JsonException) when(File.Exists(path+".bak")) { return JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(path+".bak")) ?? throw new InvalidDataException("目录配置备份为空。"); }
    }
    public static void Save(string path, WorkspacePreferences preferences)
    { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); ModProfileStore.AtomicWrite(path,JsonSerializer.Serialize(preferences)); }
    // NTFS 目录联接不需要管理员权限；游戏入口始终为 mods，原目录先保留为备份。
    public static string ConnectMods(string game, string selected)
    {
        string Normalize(string path) { string full=Path.GetFullPath(path);return full.Length==Path.GetPathRoot(full)!.Length?full:full.TrimEnd(Path.DirectorySeparatorChar); }
        game=Normalize(game); selected=Normalize(selected);
        string entry=Path.Combine(game,"mods");
        if(selected.Equals(entry,StringComparison.OrdinalIgnoreCase)) return Directory.Exists(entry) && (File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0 ? Target(entry) : entry;
        if(!Directory.Exists(selected) || selected.Equals(game,StringComparison.OrdinalIgnoreCase) || entry.StartsWith(selected.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || selected.StartsWith(entry+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("请选择独立的 MOD 文件夹，不能是游戏目录、mods 的父目录或子目录。");
        selected=Target(selected);
        if(entry.StartsWith(selected.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || selected.StartsWith(entry+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("所选目录联接会导致目录循环，请选择独立的真实 MOD 文件夹。");
        string? previous=null, backup=null;
        if(Directory.Exists(entry))
        {
            if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0) { previous=Target(entry);if(previous.Equals(selected,StringComparison.OrdinalIgnoreCase))return selected;Directory.Delete(entry); }
            else { backup=Path.Combine(game,"mods-backup-"+Guid.NewGuid().ToString("N"));Directory.Move(entry,backup); }
        }
        try { Junction(entry,selected);return selected; }
        catch { if(Directory.Exists(entry))Directory.Delete(entry);if(backup!=null)Directory.Move(backup,entry);else if(previous!=null)Junction(entry,previous);throw; }
    }
    public static bool IsConnected(string game,string selected)
    {
        string entry=Path.GetFullPath(Path.Combine(game,"mods"));
        if(entry.TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(selected).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))return true;
        return Directory.Exists(entry) && Directory.Exists(selected) && Target(entry).Equals(Target(selected),StringComparison.OrdinalIgnoreCase);
    }
    private static string Target(string path)
    {
        using var handle=CreateFile(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
        if(handle.IsInvalid)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var text=new StringBuilder(32768);if(GetFinalPathNameByHandle(handle,text,text.Capacity,0)==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        string value=text.ToString();return value.StartsWith("\\\\?\\UNC\\")?"\\\\"+value.Substring(8):value.StartsWith("\\\\?\\")?value.Substring(4):value;
    }
    private static void Junction(string entry,string target)
    {
        Directory.CreateDirectory(entry);
        using var handle=CreateFile(entry,0x40000000,0,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
        if(handle.IsInvalid)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        byte[] substitute=Encoding.Unicode.GetBytes("\\??\\"+target),print=Encoding.Unicode.GetBytes(target);
        byte[] buffer=new byte[16+substitute.Length+2+print.Length+2];
        void Put(int offset,ushort value){Array.Copy(BitConverter.GetBytes(value),0,buffer,offset,2);}
        Array.Copy(BitConverter.GetBytes(0xA0000003u),buffer,4);Put(4,(ushort)(buffer.Length-8));Put(10,(ushort)substitute.Length);Put(12,(ushort)(substitute.Length+2));Put(14,(ushort)print.Length);
        Array.Copy(substitute,0,buffer,16,substitute.Length);Array.Copy(print,0,buffer,18+substitute.Length,print.Length);
        if(!DeviceIoControl(handle,0x000900A4,buffer,buffer.Length,IntPtr.Zero,0,out _,IntPtr.Zero))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,StringBuilder text,int length,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle handle,uint code,byte[] input,int inputSize,IntPtr output,int outputSize,out uint returned,IntPtr overlap);
}

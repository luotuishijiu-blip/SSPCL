using System.Text;

namespace Sspcl.Core.Install;

/// <summary>从 .class 文件的常量池中提取 UTF-8 字符串（用于读取 Version.class 里的版本号）。</summary>
internal static class ClassFileConstantPool
{
    public static IReadOnlyList<string> ReadUtf8Strings(byte[] data)
    {
        var result = new List<string>();
        if (data.Length < 10) return result;

        int i = 8; // 跳过 magic(4) + minor(2) + major(2)
        int count = ((data[i] << 8) | data[i + 1]);
        i += 2;
        int read = 1;

        while (read < count && i < data.Length)
        {
            byte tag = data[i++];
            read++;
            switch (tag)
            {
                case 1: // Utf8
                {
                    if (i + 2 > data.Length) return result;
                    int len = (data[i] << 8) | data[i + 1];
                    i += 2;
                    len = Math.Min(len, data.Length - i);
                    string s = Encoding.UTF8.GetString(data, i, len);
                    i += len;
                    if (s.All(c => c == '\t' || (c >= 0x20 && c <= 0x7E))) result.Add(s);
                    break;
                }
                case 3: // Integer
                case 4: // Float
                    i += 4;
                    break;
                case 5: // Long
                case 6: // Double
                    i += 8;
                    read++; // 占两个常量池槽位
                    break;
                case 7: // Class
                case 8: // String
                case 16: // MethodType
                case 19: // Module
                case 20: // Package
                    i += 2;
                    break;
                case 9: // Fieldref
                case 10: // Methodref
                case 11: // InterfaceMethodref
                case 12: // NameAndType
                case 17: // Dynamic
                case 18: // InvokeDynamic
                    i += 4;
                    break;
                case 15: // MethodHandle
                    i += 3;
                    break;
                default:
                    return result;
            }
        }
        return result;
    }

    public static string? FindVersion(IReadOnlyList<string> strings)
    {
        var candidates = strings
            .Where(s => s.Length >= 3
                        && s[0] >= '0' && s[0] <= '9'
                        && s.Contains('.')
                        && System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+\.\d+[a-zA-Z][0-9a-zA-Z\-]*$"))
            .OrderBy(s => s.Length)
            .ToList();
        return candidates.FirstOrDefault();
    }
}

using System.Reflection;

namespace Sspcl.Core.Store.Forum;

/// <summary>统一的论坛客户端标识，版本来自 Core 项目的发布版本配置。</summary>
public static class ForumClientIdentity
{
    public static string UserAgent { get; } = "sspcl/" +
        (typeof(ForumClientIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
         ?? typeof(ForumClientIdentity).Assembly.GetName().Version?.ToString(3)
         ?? "0.0.0");
}

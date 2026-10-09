using System.Text.Json.Serialization;

namespace Sspcl.Core.Store.Forum;

/// <summary>论坛 API 的 MOD 索引。三种来源共用字段，保留来源标记与翻译信息。</summary>
public sealed class ForumMod
{
    [JsonPropertyName("mod_info_type")]
    public string InfoType { get; set; } = "original";
    [JsonPropertyName("mod_id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("mod_name_cn")]
    public string ChineseName { get; set; } = "";
    [JsonPropertyName("mod_name_en")]
    public string? EnglishName { get; set; }
    [JsonPropertyName("mod_author_names")]
    public List<string> Authors { get; set; } = new();
    [JsonPropertyName("mod_translator_names")]
    public List<string> Translators { get; set; } = new();
    [JsonPropertyName("mod_category")]
    public string Category { get; set; } = "";
    [JsonPropertyName("mod_game_versions")]
    public List<string> GameVersions { get; set; } = new();
    [JsonPropertyName("mod_version")]
    public string Version { get; set; } = "";
    [JsonPropertyName("mod_releases")]
    public List<ForumModRelease>? Releases { get; set; }
    [JsonPropertyName("mod_allow_direct_download")]
    public bool AllowDirectDownload { get; set; }
    [JsonPropertyName("mod_safe_remove")]
    public bool SafeRemove { get; set; }
    // API 提供名称，不等同于游戏 mod_info.json 中的依赖 ID。
    [JsonPropertyName("mod_dependency_names")]
    public List<string> DependencyNames { get; set; } = new();
    [JsonPropertyName("mod_conflict_names")]
    public List<string> ConflictNames { get; set; } = new();
    [JsonPropertyName("mod_short_description")]
    public string ShortDescription { get; set; } = "";
    [JsonPropertyName("mod_language")]
    public string Language { get; set; } = "";
    // 单位待服务端确认，暂不推断为秒或毫秒。
    [JsonPropertyName("mod_update_date")]
    public long UpdateDate { get; set; }
    [JsonPropertyName("mod_publish_urls")]
    public List<string> PublishUrls { get; set; } = new();
    [JsonPropertyName("admin_notes")]
    public ForumAdminNotes AdminNotes { get; set; } = new();
    [JsonPropertyName("thread_meta")]
    public ForumThreadMeta Thread { get; set; } = new();
}

public sealed class ForumModRelease
{
    [JsonPropertyName("attachment_id")]
    public long AttachmentId { get; set; }
    [JsonPropertyName("game_version_id")]
    public string GameVersionId { get; set; } = "";
    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = "";
    [JsonPropertyName("mod_version")]
    public string ModVersion { get; set; } = "";
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
    [JsonPropertyName("download_count")]
    public long? DownloadCount { get; set; }
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }
    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }
    [JsonPropertyName("uploaded_at")]
    public long? UploadedAt { get; set; }
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }
}

public sealed class ForumAdminNotes
{
    [JsonPropertyName("mod_index_comment")]
    public string? IndexComment { get; set; }
    [JsonPropertyName("thread_comment")]
    public string? ThreadComment { get; set; }
}

public sealed class ForumThreadMeta
{
    [JsonPropertyName("tid")]
    public long ThreadId { get; set; }
    [JsonPropertyName("uid")]
    public long UserId { get; set; }
    [JsonPropertyName("fid")]
    public long ForumId { get; set; }
    [JsonPropertyName("featured_level")]
    public int FeaturedLevel { get; set; }
    [JsonPropertyName("recommend_weight")]
    public int RecommendWeight { get; set; }
    [JsonPropertyName("heats")]
    public long Heats { get; set; }
    [JsonPropertyName("views")]
    public long Views { get; set; }
}

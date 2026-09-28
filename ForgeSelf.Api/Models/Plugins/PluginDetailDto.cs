using ForgeSelf.Api.Plugins.Abstractions;

namespace ForgeSelf.Api.Models.Plugins;

public class PluginDetailDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public PluginState State { get; set; }

    public bool IsEnabled { get; set; }

    public List<string> Dependencies { get; set; } = new();

    public List<string> Permissions { get; set; } = new();

    public List<string> ExtensionPoints { get; set; } = new();

    public PluginUsageStatsDto? UsageStats { get; set; }

    public DateTime? LoadedAt { get; set; }

    public string Category { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public List<string> Screenshots { get; set; } = new();

    public string HomepageUrl { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string License { get; set; } = string.Empty;

    public string ReleaseNotes { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public long InstallCount { get; set; }

    public double Rating { get; set; }

    public bool HasUpdate { get; set; }

    public string? LatestVersion { get; set; }
}

public class PluginUsageStatsDto
{
    public long TotalUsage { get; set; }

    public long TodayUsage { get; set; }

    public double AverageDailyUsage { get; set; }

    public DateTime? LastUsedAt { get; set; }
}

public class PluginCategoryDto
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Count { get; set; }

    public string Icon { get; set; } = string.Empty;
}

public class PluginSearchRequest
{
    public string? Keyword { get; set; }

    public string? Category { get; set; }

    public string? SortBy { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class PluginSearchResult
{
    public List<PluginInfoDto> Items { get; set; } = new();

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public enum PluginSortBy
{
    Name,
    InstallCount,
    UpdatedAt,
    Rating
}

public class PluginVersionInfo
{
    public string Version { get; set; } = string.Empty;

    public DateTime? ReleasedAt { get; set; }

    public string ReleaseNotes { get; set; } = string.Empty;

    public long Size { get; set; }
}

public class PluginUpdateInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool HasUpdate { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>更新来源：staged（versions/ 内已直落未生效版本，2026-09-28 输入31 去 _backups）或 package（插件更新源本地包目录，输入27 新增）。null = 旧数据兼容。</summary>
    public string? Source { get; set; }
}

public class PluginBackupInfo
{
    public string BackupId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long Size { get; set; }
}

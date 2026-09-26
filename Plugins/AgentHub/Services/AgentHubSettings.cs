using System.Text.Json;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>Agent 中枢插件设置（随数据目录持久化的业务配置）。</summary>
public class AgentHubSettings
{
    /// <summary>
    /// 附加扫描目录：探测 CLI agent 时除 PATH 外额外查找的目录。
    /// 解决「agent 装在不进 PATH 的目录（pnpm/bun/scoop/自定义安装）导致扫不到」的误判。
    /// </summary>
    public List<String> SearchDirectories { get; set; } = new();
}

/// <summary>
/// 设置存储：config.json 读写 + 进程内快照（单例）。
/// 文件位于插件数据目录（{数据根}/Plugins/agent-hub/config.json），随数据走、热加载。
/// </summary>
public class AgentHubSettingsStore
{
    private readonly String _path;
    private readonly Object _lock = new();
    private AgentHubSettings _current;

    /// <param name="path">config.json 绝对路径（由插件 Apply 从数据目录构造）</param>
    public AgentHubSettingsStore(String path)
    {
        _path = path;
        _current = LoadFromDisk(path);
    }

    /// <summary>当前设置快照（探测等只读路径读取）</summary>
    public AgentHubSettings Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>保存设置（规范化后落盘并替换内存快照）。</summary>
    public AgentHubSettings Save(AgentHubSettings next)
    {
        lock (_lock)
        {
            var normalized = Normalize(next);
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            var dir = Path.GetDirectoryName(_path);
            if (!dir.IsNullOrEmpty()) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, json);
            _current = normalized;
            XTrace.Log.Info("[AgentHub] 设置已保存，附加扫描目录 {0} 个", normalized.SearchDirectories.Count);
            return _current;
        }
    }

    /// <summary>规范化：去空白、去尾分隔符、去重；空项剔除。</summary>
    private static AgentHubSettings Normalize(AgentHubSettings s)
    {
        var dirs = (s.SearchDirectories ?? new List<String>())
            .Select(d => (d ?? String.Empty).Trim().TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Where(d => !d.IsNullOrEmpty())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new AgentHubSettings { SearchDirectories = dirs };
    }

    /// <summary>从磁盘加载；文件缺失/损坏时退回默认并显式告警（不静默）。</summary>
    private static AgentHubSettings LoadFromDisk(String path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<AgentHubSettings>(json, JsonOptions);
                if (parsed != null) return Normalize(parsed);
            }
        }
        catch (Exception ex)
        {
            // 配置文件坏掉不阻断插件：退回默认并告警，避免插件整体不可用
            XTrace.Log.Warn("[AgentHub] 设置文件解析失败，使用默认值: {0}", ex.Message);
        }
        return new AgentHubSettings();
    }

    /// <summary>JSON 选项（容忍注释/尾随逗号，便于用户手改）。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

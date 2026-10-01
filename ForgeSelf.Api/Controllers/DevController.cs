using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Dev;
using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 插件开发/调试专用端点（dev-only）。
/// <para>
/// 仅当 dev 总闸开启（FORGESELF_DEV_MODE=1 或 --dev）时可用；未开启时所有 action 返回 404，
/// 用户运行中的 Production 宿主行为零变化。鉴权复用宿主 <c>ApiKeyPolicy</c>（铁律 17 同源）。
/// </para>
/// <para>
/// 提供三类能力：
/// ① <c>POST /api/dev/plugin/{id}/reload</c> —— 同版本热重载（shadow-copy 装载，绕开版本严格递增）；
/// ② <c>POST /api/dev/plugin/reload-all</c> —— 全量重载；
/// ③ <c>GET /api/dev/diagnostics</c> —— 插件状态表 / shadow 统计 / 最近错误 / 日志尾。
/// </para>
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly PluginManager _pluginManager;
    private readonly Func<bool> _devGate;

    /// <summary>DI 构造（生产路径：devGate 读 DevMode.Enabled）。</summary>
    public DevController(PluginManager pluginManager) : this(pluginManager, () => DevMode.Enabled)
    {
    }

    /// <summary>测试构造：devGate 可注入，验证 dev-off 404 语义。</summary>
    internal DevController(PluginManager pluginManager, Func<bool> devGate)
    {
        _pluginManager = pluginManager;
        _devGate = devGate;
    }

    /// <summary>
    /// 同版本热重载指定插件：停用旧实例 → 强制回收 → 刷新元数据 → 重新 shadow 装载 → 启用。
    /// 不修改 plugin.json、不产生 versions/、不经过 UpdatePlugin 的版本严格递增校验。
    /// </summary>
    [HttpPost("plugin/{pluginId}/reload")]
    public ActionResult<ApiResponse<DevReloadResultDto>> Reload(string pluginId)
    {
        if (!_devGate())
            return NotFound();

        try
        {
            XTrace.Log.Info("[dev] 热重载请求: {0}", pluginId);

            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
                return NotFound(ApiResponse<DevReloadResultDto>.Error($"插件不存在: {pluginId}", 404));

            var beforeVersion = metadata.Version;
            var success = _pluginManager.ReloadPlugin(pluginId);

            var after = _pluginManager.GetPluginMetadata(pluginId);
            var state = _pluginManager.GetPluginState(pluginId);

            // 已知边界如实告知：宿主级 HostedService 不随 reload 重启（技能 SKILL.md:162-164 实证）。
            var warnings = new List<string>();
            if (success)
                PluginErrorStore.Clear(pluginId);
            else
                warnings.Add("重载失败：插件保持/回到旧实例，详情见插件错误信息与日志");

            warnings.Add("宿主级后台服务（HostedService）不随 reload 重启；如本次改动含 HostedService，请冷启动 dev 宿主");

            var result = new DevReloadResultDto
            {
                PluginId = pluginId,
                Success = success,
                VersionBefore = beforeVersion,
                VersionAfter = after?.Version ?? beforeVersion,
                State = state.ToString(),
                ShadowPath = PluginShadowCopy.GetShadowPath(pluginId),
                Warnings = warnings
            };

            XTrace.Log.Info("[dev] 热重载完成: {0} success={1} state={2}", pluginId, success, result.State);
            return Ok(ApiResponse<DevReloadResultDto>.Ok(result, success ? $"插件 {pluginId} 已同版本重载" : $"插件 {pluginId} 重载失败"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[dev] 热重载异常 [{0}]: {1}", pluginId, ex.Message);
            PluginErrorStore.Set(pluginId, ex);
            return StatusCode(500, ApiResponse<DevReloadResultDto>.Error($"热重载失败: {ex.Message}"));
        }
    }

    /// <summary>对全部已发现插件依次执行同版本热重载，返回逐插件结果。</summary>
    [HttpPost("plugin/reload-all")]
    public ActionResult<ApiResponse<List<DevReloadResultDto>>> ReloadAll()
    {
        if (!_devGate())
            return NotFound();

        try
        {
            var ids = _pluginManager.GetAllMetadatas().Select(m => m.Id).ToList();
            var results = new List<DevReloadResultDto>();

            foreach (var id in ids)
            {
                var success = _pluginManager.ReloadPlugin(id);
                if (success)
                    PluginErrorStore.Clear(id);

                results.Add(new DevReloadResultDto
                {
                    PluginId = id,
                    Success = success,
                    State = _pluginManager.GetPluginState(id).ToString(),
                    ShadowPath = PluginShadowCopy.GetShadowPath(id),
                    Warnings = success ? new List<string>() : new List<string> { "重载失败，详情见日志" }
                });
            }

            var okCount = results.Count(r => r.Success);
            return Ok(ApiResponse<List<DevReloadResultDto>>.Ok(results, $"全量重载完成：成功 {okCount}/{results.Count}"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[dev] 全量重载异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<DevReloadResultDto>>.Error($"全量重载失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// dev 诊断：dev 开关状态 / 逐插件状态与最近错误 / shadow 目录统计 / 当日日志尾 N 行。
    /// </summary>
    [HttpGet("diagnostics")]
    public ActionResult<ApiResponse<DevDiagnosticsDto>> Diagnostics([FromQuery] int logTail = 100)
    {
        if (!_devGate())
            return NotFound();

        try
        {
            logTail = Math.Clamp(logTail, 1, 1000);

            var dto = new DevDiagnosticsDto
            {
                DevMode = new DevModeFlagsDto
                {
                    Enabled = DevMode.Enabled,
                    ShadowCopy = DevMode.ShadowCopyEnabled,
                    WebSrc = DevMode.WebSrcEnabled,
                    WebHmr = DevMode.WebHmrEnabled,
                    BuildConfiguration = DevMode.BuildConfiguration,
                    TargetFramework = DevMode.TargetFramework,
                    PluginsDirectory = _pluginManager.PluginsDirectory
                }
            };

            var errorRecords = PluginErrorStore.GetAll();

            foreach (var m in _pluginManager.GetAllMetadatas().OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                var state = _pluginManager.GetPluginState(m.Id);
                var entryPath = _pluginManager.GetPluginEntryAssemblyPath(m.Id);
                var shadowPath = PluginShadowCopy.GetShadowPath(m.Id);
                errorRecords.TryGetValue(m.Id, out var err);

                dto.Plugins.Add(new DevPluginDiagDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    State = state.ToString(),
                    EntryPath = entryPath,
                    ShadowPath = shadowPath,
                    Error = err == null
                        ? null
                        : new DevPluginErrorDto
                        {
                            Message = err.Message,
                            ExceptionType = err.ExceptionType,
                            StackTrace = err.StackTrace,
                            OccurredAt = err.OccurredAt
                        }
                });
            }

            var (pluginCount, dirCount, totalBytes) = PluginShadowCopy.GetStats();
            dto.Shadow = new DevShadowStatsDto { PluginCount = pluginCount, DirCount = dirCount, TotalBytes = totalBytes };

            dto.LogTail = ReadLogTail(logTail, out var logWarning);
            if (logWarning != null)
                dto.LogTailWarning = logWarning;

            return Ok(ApiResponse<DevDiagnosticsDto>.Ok(dto, "dev 诊断完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[dev] 诊断异常: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DevDiagnosticsDto>.Error($"诊断失败: {ex.Message}"));
        }
    }

    /// <summary>读取当日 XTrace 日志文件尾 N 行；文件缺失/被锁时返回空列表并给 warning。</summary>
    private static List<string> ReadLogTail(int lines, out string? warning)
    {
        warning = null;
        var logPath = Path.Combine(XTrace.LogPath ?? string.Empty, DateTime.Now.ToString("yyyy_MM_dd") + ".log");
        if (!System.IO.File.Exists(logPath))
        {
            warning = $"当日日志文件不存在: {logPath}";
            return new List<string>();
        }

        try
        {
            // FileShare.ReadWrite：日志正被宿主写入，须共享读写句柄
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var all = new List<string>();
            while (reader.ReadLine() is { } line)
                all.Add(line);

            return all.Count <= lines ? all : all.Skip(all.Count - lines).ToList();
        }
        catch (Exception ex)
        {
            warning = $"日志尾读取失败: {ex.Message}";
            return new List<string>();
        }
    }
}

/// <summary>同版本重载结果。</summary>
public class DevReloadResultDto
{
    public string PluginId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string VersionBefore { get; set; } = string.Empty;

    public string VersionAfter { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    /// <summary>shadow 入口路径；未走 shadow（内嵌插件/shadow 失败回落）为 null。</summary>
    public string? ShadowPath { get; set; }

    public List<string> Warnings { get; set; } = new();
}

/// <summary>dev 诊断聚合。</summary>
public class DevDiagnosticsDto
{
    public DevModeFlagsDto DevMode { get; set; } = new();

    public List<DevPluginDiagDto> Plugins { get; set; } = new();

    public DevShadowStatsDto Shadow { get; set; } = new();

    public List<string> LogTail { get; set; } = new();

    public string? LogTailWarning { get; set; }
}

/// <summary>dev 开关状态。</summary>
public class DevModeFlagsDto
{
    public bool Enabled { get; set; }

    public bool ShadowCopy { get; set; }

    public bool WebSrc { get; set; }

    public bool WebHmr { get; set; }

    public string BuildConfiguration { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string PluginsDirectory { get; set; } = string.Empty;
}

/// <summary>单个插件诊断条目。</summary>
public class DevPluginDiagDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string? EntryPath { get; set; }

    public string? ShadowPath { get; set; }

    public DevPluginErrorDto? Error { get; set; }
}

/// <summary>插件最近错误。</summary>
public class DevPluginErrorDto
{
    public string Message { get; set; } = string.Empty;

    public string ExceptionType { get; set; } = string.Empty;

    public string StackTrace { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}

/// <summary>shadow 目录统计。</summary>
public class DevShadowStatsDto
{
    public int PluginCount { get; set; }

    public int DirCount { get; set; }

    public long TotalBytes { get; set; }
}

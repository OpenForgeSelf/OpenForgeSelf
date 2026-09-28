using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.FileTools.Models;
using ForgeSelf.Api.Plugins.FileTools.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools;

/// <summary>
/// 目录大小排行工具（批次C）。一次调用跑完一趟扫描并返回排行。
/// 与 <c>filetools.file_stats</c> 的分界：file_stats 答「这个目录总共多大 / 有哪些类型 / 最大的文件」，
/// 本工具答「这个目录下<strong>哪个子目录</strong>最占地方、各占多少」。
/// <para>
/// ⚠ 已知边界（不是本类的缺陷）：AIAgent 聊天路径的工具白名单
/// <c>AIAgentService.ResolveOwnToolDefinitions()</c> 只含 ai-agent + memory-system，
/// 故 FileTools 的全部工具（含本工具）仅经 <c>aiagent.universal_tool</c> 按名可达。
/// 把 file-tools 整体加进白名单会撑大本地小模型 prompt，属另一项决策（已登记 TODO）。
/// </para>
/// </summary>
public class FolderStatsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "filetools.folder_stats";
    public string Name => "folder_stats";
    public string PluginId { get; }

    public string Description =>
        "统计指定目录下各子目录的大小（含其全部后代），按占用字节降序排行，并给出每个子目录占根目录总量的百分比；" +
        "被 Top 挤掉的子目录合并为「其他」行，保证 Σ排行 + 其他 + 根本级文件 = 根总量。" +
        "符号链接/junction 不跟随；无权限的条目计入 inaccessibleCount 而不中断扫描。" +
        "入参示例 {\"directory\":\"C:\\\\Projects\",\"top\":20}。";

    // ⚠ 必须是逐字字符串 @"..."（内部 "" 还原成 "），不能用原始字符串 """...""""：
    // 后者不做转义，运行时得到 ""type"" → JsonDocument.Parse 直接抛（实测由
    // FileToolsFoldersAuthAndToolTests 抓到）。与 FileToolsPlugin.cs 既有 5 个工具同款写法。
    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""directory"": {
            ""type"": ""string"",
            ""description"": ""要统计的目录绝对路径""
        },
        ""top"": {
            ""type"": ""integer"",
            ""description"": ""返回的排行条数，默认 50，最大 500"",
            ""default"": 50
        }
    },
    ""required"": [""directory""]
}
";

    public FolderStatsToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        var directory = string.Empty;
        try
        {
            XTrace.Log.Info("[FileToolsPlugin] 执行 folder_stats 工具函数");

            var root = JsonDocument.Parse(parameters).RootElement;
            if (root.TryGetProperty("directory", out var dirProp)) directory = dirProp.GetString() ?? string.Empty;
            else if (root.TryGetProperty("path", out var pathProp)) directory = pathProp.GetString() ?? string.Empty;

            var top = 50;
            if (root.TryGetProperty("top", out var topProp) && topProp.TryGetInt32(out var topValue)) top = topValue;

            // 与既有工具函数同款做法：直接构造服务、同步跑完一趟（「受理 + 轮询」那套只服务 HTTP 侧）
            var service = new FolderScanService(new FolderScanJobStore());
            var view = service.ScanAndWait(new FolderScanRequest { Directory = directory, Top = top });

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "folder_stats", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, object>
                {
                    ["directory"] = directory,
                    ["count"] = view.Items.Count
                });

            return JsonSerializer.Serialize(new
            {
                success = view.State == ScanState.Completed,
                state = view.State.ToString(),
                rootPath = view.RootPath,
                rootTotalBytes = view.RootTotalBytes,
                rootTotalFormatted = view.RootTotalFormatted,
                rootOwnBytes = view.RootOwnBytes,
                rootOwnFormatted = view.RootOwnFormatted,
                directoryCount = view.DirectoryCount,
                fileCount = view.FileCount,
                durationMs = view.DurationMs,
                truncated = view.Truncated,
                capNote = view.CapNote,
                inaccessibleCount = view.InaccessibleCount,
                skippedReparseCount = view.SkippedReparseCount,
                error = view.Error,
                ranking = view.Items.Select(r => new
                {
                    relativePath = r.RelativePath,
                    name = r.Name,
                    totalBytes = r.TotalBytes,
                    totalFormatted = r.TotalFormatted,
                    directBytes = r.DirectBytes,
                    fileCount = r.FileCount,
                    dirCount = r.DirCount,
                    percentage = r.Percentage
                }),
                other = view.OtherRow == null
                    ? null
                    : new
                    {
                        name = view.OtherRow.Name,
                        totalBytes = view.OtherRow.TotalBytes,
                        totalFormatted = view.OtherRow.TotalFormatted,
                        percentage = view.OtherRow.Percentage
                    }
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileToolsPlugin] folder_stats 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "folder_stats", stopwatch.ElapsedMilliseconds,
                new Dictionary<string, object> { ["error"] = ex.Message });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

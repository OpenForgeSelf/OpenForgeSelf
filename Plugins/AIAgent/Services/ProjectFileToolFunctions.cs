using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent;

/// <summary>
/// 项目文件 MCP 工具：让 Agent 能对选定的工作目录（项目）执行文件读/写/列表。
/// 所有相对路径都基于 <see cref="IProjectWorkspaceService"/> 选定的项目根目录，天然受路径穿越防护约束。
/// </summary>
/// <remarks>
/// 关键约束：工具函数必须在运行时经 <see cref="IContext"/> 懒解析 <see cref="IProjectWorkspaceService"/>，
/// 不能缓存 <c>Apply</c> 阶段拿到的 <c>IServiceCollection</c> 提供器——后者在运行期解析不出单例（返回 null），
/// 会导致 write_file 抛「项目工作区服务不可用」而不写盘。此模式与 <c>AIAgentService</c> 一致（已验证）。
/// </remarks>
public class ListFilesToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "aiagent.list_files";
    public string Name => "list_files";
    public string PluginId { get; }
    public string Description => "列出项目目录中的文件与子目录。项目根为 Agent 当前选择的工作目录（一个目录视为一个项目）。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""path"": {
            ""type"": ""string"",
            ""description"": ""相对项目根的子目录路径，留空表示项目根本身"",
            ""default"": """"
        }
    },
    ""required"": []
}";

    public ListFilesToolFunction(string pluginId, IContext ctx)
    {
        PluginId = pluginId;
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = _ctx.Get<IProjectWorkspaceService>();
            if (workspace == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "项目工作区服务不可用", hint = "检查插件宿主状态", usedRoot = (string?)null }));
            }

            if (!workspace.IsProjectSet && workspace.DefaultRoot == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录", hint = "set directory via POST api/project/directory 或设置 FORGESELF_DEFAULT_PROJECT_ROOT", usedRoot = (string?)null }));
            }

            var path = string.Empty;
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("path", out var p))
                    path = p.GetString() ?? string.Empty;
            }

            var entries = workspace
                .ListEntries(path)
                .Select(e => new { e.Name, e.IsDirectory, e.RelativePath, e.Size })
                .ToList();

            // usedRoot=当前生效根（ProjectRoot 优先，未选目录时=回退根）。
            var usedRoot = workspace.ProjectRoot ?? workspace.DefaultRoot;
            var response = new
            {
                success = true,
                root = workspace.ProjectRoot,
                usedRoot,
                path = path,
                files = entries
            };
            XTrace.Log.Info("[AIAgentPlugin] list_files 成功：root={0}, usedRoot={1}, path={2}, 数量={3}", workspace.ProjectRoot, usedRoot, path, entries.Count);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] list_files 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(FailEnvelope(_ctx, ex.Message)));
        }
    }

    /// <summary>失败封套（三个文件工具共用）：ResolveSafePath 类错误原样透传 ex.Message，root 可得时补 hint 标注约束根。</summary>
    internal static object FailEnvelope(IContext ctx, string message)
    {
        var root = ctx.Get<IProjectWorkspaceService>() is { } ws ? ws.ProjectRoot ?? ws.DefaultRoot : null;
        return root == null
            ? new { success = false, error = message, usedRoot = (string?)null }
            : new { success = false, error = message, hint = $"路径受当前项目根约束：PROJECT_ROOT={root}", usedRoot = root };
    }
}

public class ReadFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "aiagent.read_file";
    public string Name => "read_file";
    public string PluginId { get; }
    public string Description => "读取项目目录中指定文件（UTF-8 文本）从 start_line 到 end_line 的内容，并同时返回文件总行数 totalLines。参数：path=相对项目根的文件路径（必填）；start_line=起始行号，从 1 开始计数（必填，缺失或非正整数直接报错）；end_line=结束行号（含该行，选填，缺省读到文末；不得小于 start_line，超出总行数自动钳到文末）。示例：{\"path\":\"src/main.py\",\"start_line\":12,\"end_line\":40} 表示读第 12~40 行。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""path"": {
            ""type"": ""string"",
            ""description"": ""相对项目根的文件路径，如 src/main.py""
        },
        ""start_line"": {
            ""type"": ""integer"",
            ""description"": ""起始行号（从 1 开始），必填；超出总行数时返回空 content""
        },
        ""end_line"": {
            ""type"": ""integer"",
            ""description"": ""结束行号（含该行，选填），缺省读到文末；不得小于 start_line""
        }
    },
    ""required"": [""path"", ""start_line""]
}";

    public ReadFileToolFunction(string pluginId, IContext ctx)
    {
        PluginId = pluginId;
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = _ctx.Get<IProjectWorkspaceService>();
            if (workspace == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "项目工作区服务不可用", hint = "检查插件宿主状态", usedRoot = (string?)null }));
            }

            if (!workspace.IsProjectSet && workspace.DefaultRoot == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录", hint = "set directory via POST api/project/directory 或设置 FORGESELF_DEFAULT_PROJECT_ROOT", usedRoot = (string?)null }));
            }

            using var doc = JsonDocument.Parse(parameters);
            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;
            if (!FileToolLineOps.TryReadStartLine(doc.RootElement, out var startLine, out var startLineError))
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, startLineError!)));
            if (!FileToolLineOps.TryReadEndLine(doc.RootElement, startLine, required: false, out var endSpecified, out var endLineError))
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, endLineError!)));

            var content = workspace.ReadFile(path);
            var lines = FileToolLineOps.SplitLines(content);
            var totalLines = lines.Length;
            // 起始行到生效结束行（未指定=文末，指定则钳到文末）；越界返回空段（仍带 totalLines，便于模型自纠）。
            var endLine = Math.Min(endSpecified ?? totalLines, totalLines);
            var segment = startLine > endLine
                ? string.Empty
                : string.Join("\n", lines, startLine - 1, endLine - startLine + 1);
            var usedRoot = workspace.ProjectRoot ?? workspace.DefaultRoot;
            var response = new { success = true, path, startLine, endLine, content = segment, totalLines, usedRoot };
            XTrace.Log.Info("[AIAgentPlugin] read_file 成功：root={0}, usedRoot={1}, path={2}, startLine={3}, endLine={4}, totalLines={5}, 返回长度={6}", workspace.ProjectRoot, usedRoot, path, startLine, endLine, totalLines, segment.Length);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] read_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, ex.Message)));
        }
    }
}

public class WriteFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "aiagent.write_file";
    public string Name => "write_file";
    public string PluginId { get; }
    public string Description => "按行区间替换项目目录中的文件（UTF-8 文本）：用 content 替换 start_line..end_line 区间的行（两端均必填、含端点；必须先用 read_file 读文件拿到行号——先读后写）；区间外原样保留（前 start_line-1 行与 end_line 之后的尾部行）。文件不存在时新建（此时 start_line 必须为 1，end_line 钳到文末）。参数：path=相对项目根的文件路径（必填）；content=新内容（必填）；start_line=起始行号（必填，从 1 开始，缺失或非正整数直接报错）；end_line=结束行号（必填，含该行，不得小于 start_line，超出已有行数自动钳到文末）；start_line 超出已有行数末尾+1 报错。示例：{\"path\":\"src/main.py\",\"start_line\":10,\"end_line\":12,\"content\":\"...\"} 表示用 content 替换第 10~12 行。父目录不存在时自动创建。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""path"": {
            ""type"": ""string"",
            ""description"": ""相对项目根的文件路径，如 src/main.py""
        },
        ""content"": {
            ""type"": ""string"",
            ""description"": ""文件内容（UTF-8）""
        },
        ""start_line"": {
            ""type"": ""integer"",
            ""description"": ""起始行号（从 1 开始）：必填；先 read_file 拿行号，本行起到 end_line 的区间被 content 替换""
        },
        ""end_line"": {
            ""type"": ""integer"",
            ""description"": ""结束行号（含该行）：必填；不得小于 start_line，超出已有行数自动钳到文末""
        }
    },
    ""required"": [""path"", ""content"", ""start_line"", ""end_line""]
}";

    public WriteFileToolFunction(string pluginId, IContext ctx)
    {
        PluginId = pluginId;
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = _ctx.Get<IProjectWorkspaceService>();
            if (workspace == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "项目工作区服务不可用", hint = "检查插件宿主状态", usedRoot = (string?)null }));
            }

            if (!workspace.IsProjectSet && workspace.DefaultRoot == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录", hint = "set directory via POST api/project/directory 或设置 FORGESELF_DEFAULT_PROJECT_ROOT", usedRoot = (string?)null }));
            }

            using var doc = JsonDocument.Parse(parameters);
            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;
            var content = doc.RootElement.TryGetProperty("content", out var c)
                ? c.GetString() ?? string.Empty
                : string.Empty;
            if (!FileToolLineOps.TryReadStartLine(doc.RootElement, out var startLine, out var startLineError))
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, startLineError!)));
            if (!FileToolLineOps.TryReadEndLine(doc.RootElement, startLine, required: true, out var endSpecified, out var endLineError))
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, endLineError!)));

            // 解析绝对路径用于观测记录与存在性判断（与真实写入落点一致，受路径穿越防护约束）。
            var absPath = workspace.ResolveSafePath(path);
            var exists = File.Exists(absPath);
            var existingLines = exists
                ? FileToolLineOps.SplitLines(File.ReadAllText(absPath))
                : Array.Empty<string>();
            if (!exists && startLine != 1)
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, $"文件不存在，无法从第 {startLine} 行覆盖：新建文件时 start_line 必须为 1")));
            if (startLine > existingLines.Length + 1)
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, $"start_line={startLine} 超出文件末尾（现有 {existingLines.Length} 行，最大可写 {existingLines.Length + 1}）")));

            // 保留前 startLine-1 行，用 content 替换 startLine..endLine 区间（未指定=到文末），区间后的尾部行原样保留。
            var endLine = Math.Min(endSpecified ?? existingLines.Length, existingLines.Length);
            var head = existingLines.Take(startLine - 1);
            var tail = endLine < existingLines.Length ? existingLines.Skip(endLine) : Array.Empty<string>();
            var final = string.Join("\n", head.Concat(FileToolLineOps.SplitLines(content)).Concat(tail));
            workspace.WriteFile(path, final);
            var usedRoot = workspace.ProjectRoot ?? workspace.DefaultRoot;
            var response = new { success = true, path, startLine, endLine, writtenLength = final.Length, totalLines = FileToolLineOps.SplitLines(final).Length, usedRoot };
            XTrace.Log.Info("[AIAgentPlugin] write_file 成功：root={0}, usedRoot={1}, 绝对路径={2}, startLine={3}, endLine={4}, 写入大小={5}", workspace.ProjectRoot, usedRoot, absPath, startLine, endLine, final.Length);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] write_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, ex.Message)));
        }
    }
}

/// <summary>read_file / write_file 共用的行处理原语（最小路径：统一按 \n 切分与回写，CRLF/CR 归一）。</summary>
internal static class FileToolLineOps
{
    /// <summary>把文本按行切分（CRLF/CR 归一为 LF）。</summary>
    public static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>从参数 JSON 根节点读取必填的 start_line（正整数，1 基）；数字或数字字符串均可。缺失/非法时返回 false 并给出中文错误。</summary>
    public static bool TryReadStartLine(JsonElement root, out int startLine, out string? error)
    {
        startLine = 0;
        error = null;
        if (!root.TryGetProperty("start_line", out var el))
        {
            error = "缺少必填参数 start_line：起始行号（整数，从 1 开始），read_file 表示从该行读到文末，write_file 表示从该行起直接覆盖";
            return false;
        }
        var ok = el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out startLine),
            JsonValueKind.String => int.TryParse(el.GetString(), out startLine),
            _ => false,
        };
        if (!ok || startLine < 1)
        {
            error = $"start_line 必须为从 1 开始的正整数，收到：{el.GetRawText()}";
            startLine = 0;
            return false;
        }
        return true;
    }

    /// <summary>从参数 JSON 根节点读取 end_line（含，正整数且 ≥ startLine）。required=false（read_file）：缺省返回 null=到文末；required=true（write_file，先读后写行号必知）：缺失即报错。非法时 false + 中文错误。</summary>
    public static bool TryReadEndLine(JsonElement root, int startLine, bool required, out int? endLine, out string? error)
    {
        endLine = null;
        error = null;
        if (!root.TryGetProperty("end_line", out var el))
        {
            if (required)
            {
                error = "缺少必填参数 end_line：结束行号（整数，含该行，从 1 开始）；write_file 必须先 read_file 读文件拿到行号，再用 start_line..end_line 指定要替换的区间";
                return false;
            }
            return true;
        }
        var ok = el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out var v) && (endLine = v) == v,
            JsonValueKind.String => int.TryParse(el.GetString(), out var v) && (endLine = v) == v,
            _ => false,
        };
        if (!ok || endLine < 1)
        {
            error = $"end_line 必须为从 1 开始的正整数，收到：{el.GetRawText()}";
            endLine = null;
            return false;
        }
        if (endLine < startLine)
        {
            error = $"end_line={endLine} 不得小于 start_line={startLine}";
            endLine = null;
            return false;
        }
        return true;
    }
}

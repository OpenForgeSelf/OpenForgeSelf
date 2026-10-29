using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools;

/// <summary>
/// 文件三件套（2026-10-28 归属修订轮迁入 file-tools（用户拍板 2026-10-28 文件域归一，pilot 2026-10-28-plugin-design-boundary D-V2 方案 A）：
/// read_file / write_file / list_files。
/// 绝对路径参数化（FR-5，pilot 2026-10-28-tool-namespace-ownership）：read/write 的 path 必须为绝对路径，
/// 不再隐式接项目根（依赖项目目录的会话状态改为参数传递）。
/// 2026-10-29 用户拍板加严：三个工具 **cwd 必传**（绝对路径且目录已存在，否则必传错误），彻底移除「未选择项目目录/
/// FORGESELF_DEFAULT_PROJECT_ROOT」隐式回退——工作区锚点一律由调用方显式给参；list_files 的 path 可选（缺省=cwd）。
/// 行区间语义不变：read_file start_line 必填 + end_line 选填；write_file 先读后写、区间外原样保留、父目录自动创建。
/// 行处理与路径校验原语见 <see cref="FileToolLineOps"/> / <see cref="FileToolPathOps"/>。
/// </summary>
public class ListFilesToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.list_files";
    public string Name => "list_files";
    public string PluginId => "file-tools";
    public string Description => "列出目录中的文件与子目录（顶层）。参数：cwd=工作目录绝对路径（必填，如 D:\\src\\my-proj\\src）；path=要列出的目录绝对路径（可选，缺省=cwd；传相对路径会被拒）。不再回退当前项目根，cwd 必传。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": {
            ""type"": ""string"",
            ""description"": ""工作目录绝对路径（必填），如 D:\\src\\my-proj\\src；不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT""
        },
        ""path"": {
            ""type"": ""string"",
            ""description"": ""要列出的目录绝对路径（可选），如 D:\\src\\my-proj\\src；缺省=cwd（不再回退当前项目根）"",
            ""default"": """"
        }
    },
    ""required"": [""cwd""]
}";

    public ListFilesToolFunction(IContext ctx)
    {
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            // cwd 必填（2026-10-29 用户拍板）：未传/空 → 直接报必传错误，不再回退项目根/环境变量；path 可选，缺省=cwd。
            string? cwdRaw = null;
            string? pathRaw = null;
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("cwd", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    cwdRaw = c.GetString();
                }
                if (doc.RootElement.TryGetProperty("path", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
                {
                    pathRaw = p.GetString()!.Trim();
                }
            }

            if (!FileToolPathOps.EnsureCwd(cwdRaw, out var cwd, out var cwdError))
            {
                return Task.FromResult(JsonSerializer.Serialize(FailEnvelope(_ctx, cwdError!)));
            }

            // path 可选：传了必须是绝对路径；缺省=cwd（工作区锚点完全来自显式参数）。
            string baseDir;
            if (pathRaw != null)
            {
                if (!Path.IsPathRooted(pathRaw))
                {
                    return Task.FromResult(JsonSerializer.Serialize(FailEnvelope(_ctx,
                        "path 必须为绝对路径（收到相对路径：" + pathRaw + "）。绝对路径如 D:\\src\\my-proj\\src")));
                }
                baseDir = pathRaw;
            }
            else
            {
                baseDir = cwd!;
            }

            if (!Directory.Exists(baseDir))
            {
                return Task.FromResult(JsonSerializer.Serialize(FailEnvelope(_ctx, "目录不存在：" + baseDir)));
            }

            var di = new DirectoryInfo(baseDir);
            var files = di.EnumerateFileSystemInfos()
                .Select(e => new
                {
                    name = e.Name,
                    isDirectory = e is DirectoryInfo,
                    relativePath = e.Name,
                    size = e is FileInfo fi ? fi.Length : 0L,
                    updatedAt = e.LastWriteTime
                })
                .ToList();
            var response = new
            {
                success = true,
                root = baseDir,
                usedRoot = baseDir,
                path = baseDir,
                cwd = cwd,
                files
            };
            XTrace.Log.Info("[FileTools] list_files 成功：root={0}, 数量={1}", baseDir, files.Count);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] list_files 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(FailEnvelope(_ctx, ex.Message)));
        }
    }

    /// <summary>失败封套（三个文件工具共用）：错误原样透传，项目根可得时补 hint（仅 list_files 缺省回退场景有意义）。public：测试项目断言用（mcp-center 原版经 InternalsVisibleTo，file-tools 未配）。</summary>
    public static object FailEnvelope(IContext ctx, string message)
    {
        var root = ctx.Get<IProjectWorkspaceService>() is { } ws ? ws.ProjectRoot ?? ws.DefaultRoot : null;
        return root == null
            ? new { success = false, error = message, usedRoot = (string?)null }
            : new { success = false, error = message, hint = "当前项目根：PROJECT_ROOT=" + root, usedRoot = root };
    }
}

public class ReadFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.read_file";
    public string Name => "read_file";
    public string PluginId => "file-tools";
    public string Description => "读取指定文件（UTF-8 文本）从 start_line 到 end_line 的内容，并返回文件总行数 totalLines。参数：cwd=工作目录绝对路径（必填）；path=文件绝对路径（必填，如 D:\\src\\proj\\main.py）；start_line=起始行号，从 1 开始（必填）；end_line=结束行号（含该行，选填，缺省读到文末，不得小于 start_line，超出总行数自动钳到文末）。不再回退当前项目根，cwd 必传。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""cwd"": {
            ""type"": ""string"",
            ""description"": ""工作目录绝对路径（必填），如 D:\\src\\my-proj\\src；不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT""
        },
        ""path"": {
            ""type"": ""string"",
            ""description"": ""文件绝对路径（必填），如 D:\\src\\my-proj\\src\\main.py；不再隐式接项目根""
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
    ""required"": [""cwd"", ""path"", ""start_line""]
}";

    public ReadFileToolFunction(IContext ctx)
    {
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(parameters);
            // cwd 必填（2026-10-29 用户拍板）：未传/空 → 直接报必传错误，不再回退项目根/FORGESELF_DEFAULT_PROJECT_ROOT。
            var cwd = doc.RootElement.TryGetProperty("cwd", out var cwdEl) && cwdEl.ValueKind == JsonValueKind.String
                ? cwdEl.GetString()
                : null;
            if (!FileToolPathOps.EnsureCwd(cwd, out var absCwd, out var cwdError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, cwdError!)));
            }

            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;
            if (!FileToolPathOps.EnsureAbsolute(path, out var absPath, out var pathError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, pathError!)));
            }
            if (!FileToolPathOps.EnsureFileExists(absPath!, out var existsError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, existsError!)));
            }

            if (!FileToolLineOps.TryReadStartLine(doc.RootElement, out var startLine, out var startLineError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, startLineError!)));
            }
            if (!FileToolLineOps.TryReadEndLine(doc.RootElement, startLine, required: false, out var endSpecified, out var endLineError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, endLineError!)));
            }

            var content = File.ReadAllText(absPath!);
            var lines = FileToolLineOps.SplitLines(content);
            var totalLines = lines.Length;
            // 起始行到生效结束行（未指定=文末，指定则钳到文末）；越界返回空段（仍带 totalLines，便于模型自纠）。
            var endLine = Math.Min(endSpecified ?? totalLines, totalLines);
            var segment = startLine > endLine
                ? string.Empty
                : string.Join("\n", lines, startLine - 1, endLine - startLine + 1);
            var response = new { success = true, path = absPath, cwd = absCwd, startLine, endLine, content = segment, totalLines, usedRoot = absPath };
            XTrace.Log.Info("[FileTools] read_file 成功：path={0}, startLine={1}, endLine={2}, totalLines={3}, 返回长度={4}", absPath, startLine, endLine, totalLines, segment.Length);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] read_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, ex.Message)));
        }
    }
}

public class WriteFileToolFunction : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "file-tools.write_file";
    public string Name => "write_file";
    public string PluginId => "file-tools";
    public string Description => "按行区间替换指定文件（UTF-8 文本）：用 content 替换 start_line..end_line 区间的行（两端均必填、含端点；必须先用 read_file 读文件拿到行号——先读后写）；区间外原样保留。文件不存在时新建（此时 start_line 必须为 1，end_line 钳到文末）。参数：cwd=工作目录绝对路径（必填）；path=文件绝对路径（必填）；content=新内容（必填）；start_line=起始行号（必填）；end_line=结束行号（必填，≥start_line，超出已有行数自动钳到文末）。父目录不存在时自动创建。不再回退当前项目根，cwd 必传。";

    public string ParametersJsonSchema => @"
{
    ""properties"": {
        ""cwd"": {
            ""type"": ""string"",
            ""description"": ""工作目录绝对路径（必填），如 D:\\src\\my-proj\\src；不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT""
        },
        ""path"": {
        ""path"": {
            ""type"": ""string"",
            ""description"": ""文件绝对路径（必填），如 D:\\src\\my-proj\\src\\main.py；不再隐式接项目根""
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
    ""required"": [""cwd"", ""path"", ""content"", ""start_line"", ""end_line""]
}";

    public WriteFileToolFunction(IContext ctx)
    {
        _ctx = ctx;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(parameters);
            // cwd 必填（2026-10-29 用户拍板）：未传/空 → 直接报必传错误，不再回退项目根/FORGESELF_DEFAULT_PROJECT_ROOT。
            var cwd = doc.RootElement.TryGetProperty("cwd", out var cwdEl) && cwdEl.ValueKind == JsonValueKind.String
                ? cwdEl.GetString()
                : null;
            if (!FileToolPathOps.EnsureCwd(cwd, out var absCwd, out var cwdError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, cwdError!)));
            }

            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;
            var content = doc.RootElement.TryGetProperty("content", out var c)
                ? c.GetString() ?? string.Empty
                : string.Empty;
            if (!FileToolPathOps.EnsureAbsolute(path, out var absPath, out var pathError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, pathError!)));
            }

            if (!FileToolLineOps.TryReadStartLine(doc.RootElement, out var startLine, out var startLineError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, startLineError!)));
            }
            if (!FileToolLineOps.TryReadEndLine(doc.RootElement, startLine, required: true, out var endSpecified, out var endLineError))
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, endLineError!)));
            }

            // 存在性判断与行区间拼装（绝对路径直读直写；父目录自动创建）。
            var exists = File.Exists(absPath);
            var existingLines = exists
                ? FileToolLineOps.SplitLines(File.ReadAllText(absPath!))
                : Array.Empty<string>();
            if (!exists && startLine != 1)
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx,
                    "文件不存在，无法从第 " + startLine + " 行覆盖：新建文件时 start_line 必须为 1")));
            }
            if (startLine > existingLines.Length + 1)
            {
                return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx,
                    "start_line=" + startLine + " 超出文件末尾（现有 " + existingLines.Length + " 行，最大可写 " + (existingLines.Length + 1) + "）")));
            }

            // 保留前 startLine-1 行，用 content 替换 startLine..endLine 区间（未指定=到文末），区间后的尾部行原样保留。
            var endLine = Math.Min(endSpecified ?? existingLines.Length, existingLines.Length);
            var head = existingLines.Take(startLine - 1);
            var tail = endLine < existingLines.Length ? existingLines.Skip(endLine) : Array.Empty<string>();
            var final = string.Join("\n", head.Concat(FileToolLineOps.SplitLines(content)).Concat(tail));
            var parentDir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            var response = new { success = true, path = absPath, cwd = absCwd, startLine, endLine, writtenLength = final.Length, totalLines = FileToolLineOps.SplitLines(final).Length, usedRoot = absPath };
            XTrace.Log.Info("[FileTools] write_file 成功：绝对路径={0}, startLine={1}, endLine={2}, 写入大小={3}", absPath, startLine, endLine, final.Length);
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] write_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(ListFilesToolFunction.FailEnvelope(_ctx, ex.Message)));
        }
    }
}

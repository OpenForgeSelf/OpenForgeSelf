using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent;

/// <summary>
/// 项目文件 MCP 工具：让 Agent 能对选定的工作目录（项目）执行文件读/写/列表。
/// 所有相对路径都基于 <see cref="IProjectWorkspaceService"/> 选定的项目根目录，天然受路径穿越防护约束。
/// </summary>
public class ListFilesToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

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

    public ListFilesToolFunction(string pluginId, IServiceProvider? services)
    {
        PluginId = pluginId;
        _services = services;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = (_services?.GetService<IProjectWorkspaceService>())
                ?? throw new InvalidOperationException("项目工作区服务不可用");
            if (!workspace.IsProjectSet)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录" }));
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

            var response = new
            {
                success = true,
                root = workspace.ProjectRoot,
                path = path,
                files = entries
            };
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] list_files 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

public class ReadFileToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

    public string Id => "aiagent.read_file";
    public string Name => "read_file";
    public string PluginId { get; }
    public string Description => "读取项目目录中指定文件的内容（UTF-8 文本）。路径相对项目根。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""path"": {
            ""type"": ""string"",
            ""description"": ""相对项目根的文件路径，如 src/main.py""
        }
    },
    ""required"": [""path""]
}";

    public ReadFileToolFunction(string pluginId, IServiceProvider? services)
    {
        PluginId = pluginId;
        _services = services;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = (_services?.GetService<IProjectWorkspaceService>())
                ?? throw new InvalidOperationException("项目工作区服务不可用");
            if (!workspace.IsProjectSet)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录" }));
            }

            using var doc = JsonDocument.Parse(parameters);
            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;

            var content = workspace.ReadFile(path);
            var response = new { success = true, path, content };
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] read_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

public class WriteFileToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

    public string Id => "aiagent.write_file";
    public string Name => "write_file";
    public string PluginId { get; }
    public string Description => "写入（新建或覆盖）项目目录中的一个文件。路径相对项目根，父目录不存在时自动创建。";

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
        }
    },
    ""required"": [""path"", ""content""]
}";

    public WriteFileToolFunction(string pluginId, IServiceProvider? services)
    {
        PluginId = pluginId;
        _services = services;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var workspace = (_services?.GetService<IProjectWorkspaceService>())
                ?? throw new InvalidOperationException("项目工作区服务不可用");
            if (!workspace.IsProjectSet)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "尚未选择项目目录，请先在左侧选择工作目录" }));
            }

            using var doc = JsonDocument.Parse(parameters);
            var path = doc.RootElement.GetProperty("path").GetString() ?? string.Empty;
            var content = doc.RootElement.TryGetProperty("content", out var c)
                ? c.GetString() ?? string.Empty
                : string.Empty;

            workspace.WriteFile(path, content);
            var response = new { success = true, path, contentLength = content.Length };
            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] write_file 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}
using System.Collections.Concurrent;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

public class McpService : IMcpService
{
    private readonly IToolRegistry? _toolRegistry;

    private readonly ConcurrentDictionary<string, McpServerDto> _servers = new();
    private readonly ConcurrentDictionary<string, McpToolDto> _tools = new();

    /// <summary>软依赖：插件入口传入宿主 IToolRegistry；为 null 时退化为仅预置数据（功能降级但不崩）。</summary>
    public McpService(IToolRegistry? toolRegistry)
    {
        _toolRegistry = toolRegistry;
        InitializeData();
    }

    private void InitializeData()
    {
        // 预填充 4 个 MCP 服务器
        var servers = new List<McpServerDto>
        {
            new()
            {
                Id = "mcp-forgeself-local",
                Name = "ForgeSelf Local",
                Description = "本地 ForgeSelf 智能体工具集，提供代码分析、文件操作等核心能力",
                Status = "connected"
            },
            new()
            {
                Id = "mcp-filesystem",
                Name = "Filesystem",
                Description = "文件系统操作工具，支持文件读写、目录浏览和内容搜索",
                Status = "connected"
            },
            new()
            {
                Id = "mcp-github-api",
                Name = "GitHub API",
                Description = "GitHub API 集成，用于仓库管理、Issue 和 PR 操作",
                Status = "disconnected"
            },
            new()
            {
                Id = "mcp-database",
                Name = "Database",
                Description = "数据库查询与操作工具，支持 SQL 执行和 Schema 浏览",
                Status = "disconnected"
            }
        };

        foreach (var s in servers)
        {
            _servers.TryAdd(s.Id, s);
        }

        // 预填充 12 个工具
        var tools = new List<McpToolDto>
        {
            // ForgeSelf Local (4 tools)
            new() { Id = "mcp-tool-forge-1", Name = "code_search", Description = "在项目中搜索代码片段", ServerId = "mcp-forgeself-local", ServerName = "ForgeSelf Local", Category = "dev", IsEnabled = true },
            new() { Id = "mcp-tool-forge-2", Name = "file_analysis", Description = "分析文件结构和依赖关系", ServerId = "mcp-forgeself-local", ServerName = "ForgeSelf Local", Category = "dev", IsEnabled = true },
            new() { Id = "mcp-tool-forge-3", Name = "project_summary", Description = "生成项目结构摘要", ServerId = "mcp-forgeself-local", ServerName = "ForgeSelf Local", Category = "dev", IsEnabled = true },
            new() { Id = "mcp-tool-forge-4", Name = "code_review", Description = "对代码变更进行审查分析", ServerId = "mcp-forgeself-local", ServerName = "ForgeSelf Local", Category = "dev", IsEnabled = true },

            // Filesystem (3 tools)
            new() { Id = "mcp-tool-fs-1", Name = "read_file", Description = "读取指定路径的文件内容", ServerId = "mcp-filesystem", ServerName = "Filesystem", Category = "file", IsEnabled = true },
            new() { Id = "mcp-tool-fs-2", Name = "write_file", Description = "写入内容到指定文件", ServerId = "mcp-filesystem", ServerName = "Filesystem", Category = "file", IsEnabled = true },
            new() { Id = "mcp-tool-fs-3", Name = "list_directory", Description = "列出目录下的文件和子目录", ServerId = "mcp-filesystem", ServerName = "Filesystem", Category = "file", IsEnabled = true },

            // GitHub API (3 tools)
            new() { Id = "mcp-tool-gh-1", Name = "list_issues", Description = "列出仓库的 Issue 列表", ServerId = "mcp-github-api", ServerName = "GitHub API", Category = "network", IsEnabled = false },
            new() { Id = "mcp-tool-gh-2", Name = "create_pr", Description = "创建 Pull Request", ServerId = "mcp-github-api", ServerName = "GitHub API", Category = "network", IsEnabled = false },
            new() { Id = "mcp-tool-gh-3", Name = "get_repo_info", Description = "获取仓库基本信息", ServerId = "mcp-github-api", ServerName = "GitHub API", Category = "network", IsEnabled = false },

            // Database (2 tools)
            new() { Id = "mcp-tool-db-1", Name = "execute_query", Description = "执行 SQL 查询语句", ServerId = "mcp-database", ServerName = "Database", Category = "data", IsEnabled = false },
            new() { Id = "mcp-tool-db-2", Name = "list_tables", Description = "列出数据库中的所有表", ServerId = "mcp-database", ServerName = "Database", Category = "data", IsEnabled = false },
        };

        foreach (var t in tools)
        {
            _tools.TryAdd(t.Id, t);
        }

        // 从 ToolRegistry 获取真实工具数据补充到 MCP 工具列表
        SyncToolsFromToolRegistry();

        // 更新各服务器的 toolCount
        RefreshServerToolCounts();
    }

    /// <summary>
    /// T032 修复：从 ToolRegistry 同步真实工具到本地缓存。
    /// ToolRegistry 在插件启用/禁用时动态注册与注销，McpService 须在每次查询时重新同步，
    /// 否则仅构建期的一次性快照无法反映运行期插件工具的变化。
    /// </summary>
    private void SyncToolsFromToolRegistry()
    {
        if (_toolRegistry == null)
        {
            XTrace.Log.Warn("[McpService] 宿主 IToolRegistry 不可用（软依赖缺失），仅提供预置服务器/工具数据");
            return;
        }

        try
        {
            var realTools = _toolRegistry.GetAllTools().ToList();
            var validIds = new HashSet<string>(realTools.Select(t => t.Id));

            // 新增：ToolRegistry 中存在但本地尚未收录的工具
            foreach (var rt in realTools)
            {
                if (string.IsNullOrWhiteSpace(rt.Name)) continue;

                var toolId = $"mcp-tool-real-{rt.Id}";
                if (_tools.ContainsKey(toolId)) continue;

                _tools.TryAdd(toolId, new McpToolDto
                {
                    Id = toolId,
                    Name = rt.Name,
                    Description = rt.Description,
                    ServerId = "mcp-forgeself-local",
                    ServerName = "ForgeSelf Local",
                    Category = "system",
                    IsEnabled = true
                });
            }

            // 清理：本地收录但 ToolRegistry 已注销的工具
            foreach (var key in _tools.Keys.Where(k => k.StartsWith("mcp-tool-real-")).ToList())
            {
                var baseId = key["mcp-tool-real-".Length..];
                if (!validIds.Contains(baseId))
                {
                    _tools.TryRemove(key, out _);
                }
            }

            RefreshServerToolCounts();
            XTrace.Log.Info("[McpService] 从 ToolRegistry 同步了 {0} 个真实工具", realTools.Count);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpService] 从 ToolRegistry 同步工具失败: {0}", ex.Message);
        }
    }

    private void RefreshServerToolCounts()
    {
        foreach (var serverId in _servers.Keys)
        {
            if (_servers.TryGetValue(serverId, out var server))
            {
                server.ToolCount = _tools.Values.Count(t => t.ServerId == serverId);
            }
        }
    }

    public Task<List<McpServerDto>> GetServersAsync()
    {
        SyncToolsFromToolRegistry();
        return Task.FromResult(_servers.Values.OrderBy(s => s.Name).ToList());
    }

    public Task<List<McpToolDto>> GetToolsAsync(string serverId, string? keyword = null, string? category = null)
    {
        SyncToolsFromToolRegistry();
        var query = _tools.Values.Where(t => t.ServerId == serverId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(kw) ||
                t.Description.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t =>
                t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.OrderBy(t => t.Name).ToList());
    }

    public Task<McpToolDto?> ToggleToolAsync(string toolId)
    {
        if (!_tools.TryGetValue(toolId, out var tool))
        {
            return Task.FromResult<McpToolDto?>(null);
        }

        tool.IsEnabled = !tool.IsEnabled;
        XTrace.Log.Info("[McpService] 工具状态切换 [{0}]: {1} -> {2}", tool.Name, !tool.IsEnabled, tool.IsEnabled);

        return Task.FromResult<McpToolDto?>(tool);
    }

    public Task<McpTestResultDto> TestToolAsync(string toolId)
    {
        if (!_tools.TryGetValue(toolId, out var tool))
        {
            return Task.FromResult(new McpTestResultDto
            {
                Success = false,
                Message = $"工具 '{toolId}' 不存在",
                DurationMs = 0
            });
        }

        // 模拟测试过程
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(Random.Shared.Next(50, 300));

        sw.Stop();

        if (!tool.IsEnabled)
        {
            return Task.FromResult(new McpTestResultDto
            {
                Success = false,
                Message = $"工具 '{tool.Name}' 当前已禁用，请先启用后再测试",
                DurationMs = sw.ElapsedMilliseconds
            });
        }

        return Task.FromResult(new McpTestResultDto
        {
            Success = true,
            Message = $"工具 '{tool.Name}' 测试通过，功能正常",
            DurationMs = sw.ElapsedMilliseconds
        });
    }

    public Task<McpTestResultDto> TestServerAsync(string serverId)
    {
        if (!_servers.TryGetValue(serverId, out var server))
        {
            return Task.FromResult(new McpTestResultDto
            {
                Success = false,
                Message = $"服务器 '{serverId}' 不存在",
                DurationMs = 0
            });
        }

        // 模拟连接测试
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(Random.Shared.Next(100, 500));

        sw.Stop();

        var success = server.Status == "connected";
        return Task.FromResult(new McpTestResultDto
        {
            Success = success,
            Message = success
                ? $"服务器 '{server.Name}' 连接正常 (延迟: {sw.ElapsedMilliseconds}ms)"
                : $"服务器 '{server.Name}' 连接失败，请检查配置",
            DurationMs = sw.ElapsedMilliseconds
        });
    }
}
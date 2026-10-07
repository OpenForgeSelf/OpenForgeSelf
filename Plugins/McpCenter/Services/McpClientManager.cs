using System.Collections.Concurrent;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 外部 MCP 客户端管理器（v2.1.0）：持有「服务器 id → 会话」字典，负责配置加载、建连、转发与启停。
/// 长连接自管生命周期（铁律 14）：插件 Apply 时 EnsureStarted 按 enabled 建连；StopAll 随 ctx.Effect 逆序释放。
/// 单例注册进插件子 provider（控制器解析）+ 供转发器经构造函数注入。
/// </summary>
public sealed class McpClientManager
{
    /// <summary>外部工具命名空间前缀（tool 参数：mcp.&lt;服务器id&gt;.&lt;工具名&gt;）。</summary>
    public const string ExternalPrefix = "mcp.";

    private readonly ExternalServersStore _store;
    private readonly ConcurrentDictionary<string, McpClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _errors = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _started;

    public McpClientManager(ExternalServersStore store)
    {
        _store = store;
    }

    /// <summary>启动：加载配置并对 enabled 服务器后台建连（失败只记错误，不阻塞）。幂等。</summary>
    public void EnsureStarted()
    {
        if (_started) return;
        _started = true;
        var configs = _store.Load();
        foreach (var cfg in configs.Where(c => c.Enabled))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await TryConnectAsync(cfg.Id, _cts.Token);
                    XTrace.Log.Info("[McpCenter] 外部 MCP 服务器已连接: {0}（{1} 个工具）", cfg.Name, _sessions.TryGetValue(cfg.Id, out var s) ? s.ToolCount : 0);
                }
                catch (Exception ex)
                {
                    _errors[cfg.Id] = ex.Message;
                    XTrace.Log.Warn("[McpCenter] 外部 MCP 服务器连接失败: {0} -> {1}", cfg.Id, ex.Message);
                }
            });
        }
    }

    /// <summary>按 id 建连（已连先断重连）。失败抛 McpClientException 并清理半开会话。</summary>
    public async Task<McpClientSession> TryConnectAsync(string id, CancellationToken ct = default)
    {
        await _connectLock.WaitAsync(ct);
        try
        {
            var cfg = _store.Load().FirstOrDefault(c => c.Id == id)
                ?? throw new McpClientException($"外部服务器 '{id}' 不存在");

            if (_sessions.TryRemove(id, out var old))
            {
                try { await old.DisposeAsync(); } catch (Exception) { }
            }
            _errors.TryRemove(id, out _);

            var session = new McpClientSession(cfg, CreateTransport(cfg));
            try
            {
                await session.ConnectAsync(ct);
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
            _sessions[id] = session;
            return session;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task DisconnectAsync(string id)
    {
        if (_sessions.TryRemove(id, out var s))
        {
            try { await s.DisposeAsync(); } catch (Exception) { }
        }
        _errors.TryRemove(id, out _);
    }

    /// <summary>转发外部工具调用（universal_tool 的 mcp. 前缀路由目标）。</summary>
    public async Task<string> CallExternalAsync(string serverId, string toolName, string argumentsJson, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(serverId, out var session))
            throw new McpClientException($"外部服务器 '{serverId}' 未连接（请先在 MCP 中心连接并拉取工具清单）");
        return await session.CallToolAsync(toolName, argumentsJson, ct);
    }

    /// <summary>
    /// 工具测试台调用（v2.3.0）：按服务器 id + 工具原生名调用外部工具，返回完整结果（文本 + isError + 原文）。
    /// 与 CallExternalAsync 的区别：后者返回拼接文本供 universal_tool 转发，本方法保留结构化信息供界面展示。
    /// </summary>
    public async Task<McpToolCallOutcome> InvokeToolAsync(string serverId, string toolName, string argumentsJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new McpClientException("工具名不能为空");
        if (!_sessions.TryGetValue(serverId, out var session))
            throw new McpClientException($"外部服务器 '{serverId}' 未连接（请先在 MCP 中心连接并拉取工具清单）");
        return await session.CallToolDetailedAsync(toolName, argumentsJson, ct);
    }

    /// <summary>运行状态视图（配置脱敏）。</summary>
    public List<McpExternalServerStateDto> GetStates()
    {
        var result = new List<McpExternalServerStateDto>();
        foreach (var cfg in _store.Load())
        {
            _sessions.TryGetValue(cfg.Id, out var session);
            result.Add(new McpExternalServerStateDto
            {
                Id = cfg.Id,
                Name = cfg.Name,
                Enabled = cfg.Enabled,
                Transport = cfg.Transport,
                Url = cfg.Url,
                HeadersMasked = Mask(cfg.Headers),
                Command = cfg.Command,
                Args = cfg.Args,
                EnvMasked = Mask(cfg.Env),
                Connected = session != null,
                ToolCount = session?.ToolCount ?? 0,
                ProtocolVersion = session?.ProtocolVersion ?? string.Empty,
                ServerInfoName = session?.ServerInfoName ?? string.Empty,
                LastError = _errors.TryGetValue(cfg.Id, out var e) ? e : string.Empty
            });
        }
        return result;
    }

    /// <summary>指定服务器的工具清单（未连接返回空）。</summary>
    public IReadOnlyList<McpExternalToolDto> GetTools(string id) =>
        _sessions.TryGetValue(id, out var session) ? session.GetTools() : Array.Empty<McpExternalToolDto>();

    /// <summary>真实握手测试（initialize + ping；不建会话、不保存）。</summary>
    public async Task<(bool Ok, string Message)> TestAsync(string id, CancellationToken ct)
    {
        var cfg = _store.Load().FirstOrDefault(c => c.Id == id)
            ?? throw new McpClientException($"外部服务器 '{id}' 不存在");
        var session = new McpClientSession(cfg, CreateTransport(cfg));
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await session.ConnectAsync(ct);
            await session.PingAsync(ct);
            sw.Stop();
            return (true, $"连接正常（{session.ServerInfoName}，协议 {session.ProtocolVersion}，{session.ToolCount} 个工具，{sw.ElapsedMilliseconds}ms）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>全部断开（插件卸载/热重载时经 ctx.Effect 调用）。幂等。</summary>
    public async Task StopAllAsync()
    {
        _cts.Cancel();
        foreach (var kv in _sessions)
        {
            if (_sessions.TryRemove(kv.Key, out var s))
            {
                try { await s.DisposeAsync(); } catch (Exception) { }
            }
        }
        _errors.Clear();
    }

    /// <summary>按配置构造传输（stdio / streamable-http / http-sse）。</summary>
    public static IMcpClientTransport CreateTransport(McpExternalServerConfig cfg) =>
        cfg.Transport.ToLowerInvariant() switch
        {
            "stdio" => new StdioMcpTransport(cfg),
            "http-sse" => new LegacySseMcpTransport(cfg.Url, cfg.Headers),
            _ => new StreamableHttpMcpTransport(cfg.Url, cfg.Headers)
        };

    private static Dictionary<string, string> Mask(Dictionary<string, string> map) =>
        map.ToDictionary(kv => kv.Key, kv => MaskValue(kv.Value));

    private static string MaskValue(string v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        return v.Length <= 4 ? "****" : $"****{v[^4..]}";
    }
}

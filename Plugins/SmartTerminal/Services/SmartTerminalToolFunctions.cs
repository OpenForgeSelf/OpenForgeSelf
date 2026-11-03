using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

internal static class ToolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Ok(object data) => JsonSerializer.Serialize(new { success = true, data }, Options);
    public static string Fail(string error) => JsonSerializer.Serialize(new { success = false, error }, Options);
}

/// <summary>列出可用外壳（cmd / powershell / pwsh / WT profiles / POSIX）。</summary>
public sealed class StListShellsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StListShellsToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.list_shells";
    public string Name => "st_list_shells";
    public string PluginId { get; }
    public string Description => "列出智能终端可用的外壳配置（Windows: cmd/powershell/pwsh + Windows Terminal profiles；非 Windows 降级为 bash/sh/pwsh）。";
    public string ParametersJsonSchema => """{"type":"object","properties":{}}""";
    public Task<string> ExecuteAsync(string parameters)
    {
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
        return Task.FromResult(ToolJson.Ok(svc.ListShells()));
    }
}

public sealed class StCreateSessionToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StCreateSessionToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.create_session";
    public string Name => "st_create_session";
    public string PluginId { get; }
    public string Description => "创建智能终端会话。可指定 shellId（cmd/powershell/pwsh/…）、cwd、permissionMode（Auto|AskWrite|AskHighRisk，默认 AskWrite）、title、agentId。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "title": { "type": "string" },
        "shellId": { "type": "string", "description": "外壳 Id，如 pwsh / powershell / cmd" },
        "cwd": { "type": "string", "description": "工作目录绝对路径" },
        "permissionMode": { "type": "string", "enum": ["Auto", "AskWrite", "AskHighRisk"] },
        "agentId": { "type": "string" }
      }
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var req = JsonSerializer.Deserialize<CreateSessionRequest>(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters, ToolJson.Options)
                      ?? new CreateSessionRequest();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
                if (doc.RootElement.TryGetProperty("permissionMode", out var pm) && pm.ValueKind == JsonValueKind.String
                    && Enum.TryParse<PermissionMode>(pm.GetString(), ignoreCase: true, out var mode))
                {
                    req.PermissionMode = mode;
                }
            }
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            return Task.FromResult(ToolJson.Ok(svc.CreateSession(req)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

public sealed class StRunCommandToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StRunCommandToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.run_command";
    public string Name => "st_run_command";
    public string PluginId { get; }
    public string Description =>
        "在指定智能终端会话中提议/执行命令。经权限分类器闸门：Ask 时返回 awaiting_approval + approvalId（不启进程）；Allow 立即执行；Deny 拒绝。" +
        "密钥会在审计与回传中脱敏。与 mcp-center.run_terminal_command（白名单一次性命令）互补：本工具面向会话化、可审批、可审计的终端面。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "sessionId": { "type": "string" },
        "command": { "type": "string" }
      },
      "required": ["sessionId", "command"]
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var sessionId = doc.RootElement.TryGetProperty("sessionId", out var s) ? s.GetString() : null;
            var command = doc.RootElement.TryGetProperty("command", out var c) ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(command))
                return Task.FromResult(ToolJson.Fail("sessionId 与 command 必填"));
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            var result = svc.ProposeOrRun(sessionId!, command!);
            return Task.FromResult(ToolJson.Ok(result));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] st_run_command 失败: {0}", ex.Message);
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

public sealed class StGetOutputToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StGetOutputToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.get_output";
    public string Name => "st_get_output";
    public string PluginId { get; }
    public string Description => "拉取会话增量输出（afterIndex 起）。用于 Agent 轮询命令结果。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "sessionId": { "type": "string" },
        "afterIndex": { "type": "integer", "description": "从此序号起返回，默认 0" }
      },
      "required": ["sessionId"]
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var sessionId = doc.RootElement.TryGetProperty("sessionId", out var s) ? s.GetString() : null;
            var after = doc.RootElement.TryGetProperty("afterIndex", out var a) && a.TryGetInt32(out var n) ? n : 0;
            if (string.IsNullOrWhiteSpace(sessionId)) return Task.FromResult(ToolJson.Fail("sessionId 必填"));
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            return Task.FromResult(ToolJson.Ok(svc.GetOutput(sessionId!, after)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

public sealed class StListSessionsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StListSessionsToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.list_sessions";
    public string Name => "st_list_sessions";
    public string PluginId { get; }
    public string Description => "列出当前内存中的智能终端会话。";
    public string ParametersJsonSchema => """{"type":"object","properties":{}}""";
    public Task<string> ExecuteAsync(string parameters)
    {
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
        return Task.FromResult(ToolJson.Ok(svc.ListSessions()));
    }
}

public sealed class StSetPermissionModeToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StSetPermissionModeToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.set_permission_mode";
    public string Name => "st_set_permission_mode";
    public string PluginId { get; }
    public string Description => "设置全局或指定会话的权限模式：Auto / AskWrite（默认） / AskHighRisk。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "mode": { "type": "string", "enum": ["Auto", "AskWrite", "AskHighRisk"] },
        "sessionId": { "type": "string", "description": "可选；省略则改全局默认" }
      },
      "required": ["mode"]
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            if (!doc.RootElement.TryGetProperty("mode", out var m) || !Enum.TryParse<PermissionMode>(m.GetString(), true, out var mode))
                return Task.FromResult(ToolJson.Fail("mode 必须是 Auto|AskWrite|AskHighRisk"));
            var sessionId = doc.RootElement.TryGetProperty("sessionId", out var s) ? s.GetString() : null;
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            if (string.IsNullOrWhiteSpace(sessionId)) svc.SetGlobalPermissionMode(mode);
            else svc.SetPermissionMode(sessionId!, mode);
            return Task.FromResult(ToolJson.Ok(new { mode = mode.ToString(), sessionId }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

public sealed class StQueryAuditToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StQueryAuditToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.query_audit";
    public string Name => "st_query_audit";
    public string PluginId { get; }
    public string Description => "查询智能终端 JSONL 审计（可按 sessionId/kind/agentId 过滤）。命令已脱敏。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "sessionId": { "type": "string" },
        "kind": { "type": "string" },
        "agentId": { "type": "string" },
        "take": { "type": "integer" },
        "skip": { "type": "integer" }
      }
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            string? Get(string name) => doc.RootElement.TryGetProperty(name, out var e) ? e.GetString() : null;
            var take = doc.RootElement.TryGetProperty("take", out var t) && t.TryGetInt32(out var tn) ? tn : 100;
            var skip = doc.RootElement.TryGetProperty("skip", out var sk) && sk.TryGetInt32(out var sn) ? sn : 0;
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            return Task.FromResult(ToolJson.Ok(svc.QueryAudit(Get("sessionId"), Get("kind"), Get("agentId"), take, skip)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

public sealed class StResolveApprovalToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StResolveApprovalToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.resolve_approval";
    public string Name => "st_resolve_approval";
    public string PluginId { get; }
    public string Description => "批准或拒绝待审批命令。approve=true 开始执行；false 拒绝。通常由 UI 调用，Agent 仅在用户明确授权时使用。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "approvalId": { "type": "string" },
        "approve": { "type": "boolean" },
        "note": { "type": "string" }
      },
      "required": ["approvalId", "approve"]
    }
    """;
    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var approvalId = doc.RootElement.TryGetProperty("approvalId", out var a) ? a.GetString() : null;
            var approve = doc.RootElement.TryGetProperty("approve", out var b) && b.ValueKind == JsonValueKind.True;
            var note = doc.RootElement.TryGetProperty("note", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(approvalId)) return Task.FromResult(ToolJson.Fail("approvalId 必填"));
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITerminalSessionService>();
            return Task.FromResult(ToolJson.Ok(svc.ResolveApproval(approvalId!, approve, note)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.Fail(ex.Message));
        }
    }
}

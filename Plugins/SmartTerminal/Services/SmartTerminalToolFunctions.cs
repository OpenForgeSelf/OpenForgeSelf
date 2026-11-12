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
    public static string FailEx(Exception ex) => ex is SmartTerminalException st
        ? JsonSerializer.Serialize(new { success = false, error = SecretRedactor.Redact(st.Message), code = st.Code }, Options)
        : Fail(SecretRedactor.Redact(ex.Message));
}

/// <summary>列出可用外壳（cmd / powershell / pwsh / WT profiles / POSIX）。</summary>
public sealed class StListShellsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider _sp;
    public StListShellsToolFunction(string pluginId, IServiceProvider sp) { PluginId = pluginId; _sp = sp; }
    public string Id => "smart-terminal.list_shells";
    public string Name => "st_list_shells";
    public string PluginId { get; }
    public string Description => "列出智能终端可用的外壳配置。Windows：cmd/powershell/pwsh + Windows Terminal profiles；Linux/macOS：bash/zsh/sh（及 fish/dash）为一等公民，不可用项仍列出并标记 available=false。";
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
    public string Description => "创建智能终端命令会话（本机或 SSH）。不得传入 password；SSH 需交互密码时返回 password_required，选私钥但缺少 secretId/identityFile 时返回 credential_required，由人在 UI 完成。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "title": { "type": "string" },
        "profileId": { "type": "string" },
        "kind": { "type": "string", "enum": ["local", "ssh"] },
        "shellId": { "type": "string" },
        "cwd": { "type": "string" },
        "host": { "type": "string" },
        "port": { "type": "integer" },
        "user": { "type": "string" },
        "auth": { "type": "string", "enum": ["interactive", "key"] },
        "secretId": { "type": "string" },
        "identityFile": { "type": "string" },
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
            req.Password = null; // Agent 不得携带密码
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
            var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
            return Task.FromResult(ToolJson.Ok(svc.OpenSession(req, "agent", isAgent: true)));
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
        "在指定会话提交命令，对应提示符回车，走同一权限闸。Ask 时返回 awaiting_approval，Agent 不得自批；硬拒绝返回 rejected_hard。命令与回包均脱敏。";
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
            var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
            var result = svc.SubmitCommand(sessionId!, new SubmitCommandRequest { Command = command! }, "agent");
            return Task.FromResult(ToolJson.Ok(result));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] st_run_command 失败: {0}", SecretRedactor.Redact(ex.Message));
            return Task.FromResult(ToolJson.FailEx(ex));
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
    public string Description => "拉取会话块，对应 GET /sessions/{id}/blocks?afterSeq=。afterSeq 优先，afterIndex 作为兼容别名。";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "sessionId": { "type": "string" },
        "afterSeq": { "type": "integer", "description": "返回 seq 大于该值的块，默认 0" },
        "afterIndex": { "type": "integer", "description": "兼容别名，等同 afterSeq" }
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
            var after = 0;
            if (doc.RootElement.TryGetProperty("afterSeq", out var seq) && seq.TryGetInt32(out var seqN)) after = seqN;
            else if (doc.RootElement.TryGetProperty("afterIndex", out var a) && a.TryGetInt32(out var n)) after = n;
            if (string.IsNullOrWhiteSpace(sessionId)) return Task.FromResult(ToolJson.Fail("sessionId 必填"));
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
            return Task.FromResult(ToolJson.Ok(svc.ListBlocks(sessionId!, after)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.FailEx(ex));
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
        var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
        return Task.FromResult(ToolJson.Ok(svc.ListOpenSessions()));
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
            var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
            var pageSize = take <= 0 ? 50 : take;
            var page = skip <= 0 ? 1 : skip / pageSize + 1;
            return Task.FromResult(ToolJson.Ok(svc.QueryAuditV2(Get("sessionId"), Get("kind"), Get("q"), page, pageSize)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.FailEx(ex));
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
    public string Description => "查询或提示待审批项。Agent 不得自行批准或拒绝，必须由人在 UI 调用 POST /approvals/{id}/resolve。";
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
            var svc = scope.ServiceProvider.GetRequiredService<ISmartTerminalV2>();
            var decision = approve ? "approve" : "reject";
            return Task.FromResult(ToolJson.Ok(svc.ResolveApprovalV2(approvalId!, decision, "agent", isAgent: true)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolJson.FailEx(ex));
        }
    }
}

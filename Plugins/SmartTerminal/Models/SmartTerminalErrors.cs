namespace ForgeSelf.Api.Plugins.SmartTerminal.Models;

/// <summary>契约错误（03 §9）。控制器映射为 {code, message} 和对应 HTTP 状态。</summary>
public sealed class SmartTerminalException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object?> Extra { get; }

    public SmartTerminalException(string code, string message, int statusCode = 400, IReadOnlyDictionary<string, object?>? extra = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Extra = extra ?? new Dictionary<string, object?>();
    }

    public static SmartTerminalException Invalid(string message) => new("invalid_request", message, 400);
    public static SmartTerminalException NotFound(string message) => new("not_found", message, 404);
    public static SmartTerminalException Forbidden(string message) => new("forbidden", message, 403);
    public static SmartTerminalException Conflict(string code, string message, IReadOnlyDictionary<string, object?>? extra = null)
        => new(code, message, 409, extra);
    public static SmartTerminalException PasswordRequired(string message = "SSH 交互认证需要密码，且不会创建会话")
        => new("password_required", message, 409, new Dictionary<string, object?> { ["needsInteractivePassword"] = true });
    public static SmartTerminalException HostUnavailable(string message)
        => new("host_module_unavailable", message, 424);
}

public static class StRiskNames
{
    public static string Risk(CommandRiskClass risk) => risk switch
    {
        CommandRiskClass.Read => "read",
        CommandRiskClass.Write => "write",
        CommandRiskClass.HighRisk => "highRisk",
        CommandRiskClass.Deny => "deny",
        _ => "write"
    };

    public static string Decision(ApprovalDecision decision) => decision switch
    {
        ApprovalDecision.Allow => "allow",
        ApprovalDecision.Ask => "ask",
        ApprovalDecision.Deny => "deny",
        _ => "ask"
    };

    public static string Mode(PermissionMode mode) => mode.ToString();
}

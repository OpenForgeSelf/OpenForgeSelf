namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 终端命令工具的入参 DTO（对应 RunTerminalCommandTool 的 ParametersJsonSchema：
/// command / cwd / timeoutSeconds / env）。2026-10-28 自 AIAgent 迁入 mcp-center。
/// 反序列化使用大小写不敏感（模型侧发 camelCase，此处属性 PascalCase），详见 RunTerminalCommandTool。
/// 仅用于工具入参反序列化，不参与持久化。
/// </summary>
public class TerminalCommandDto
{
    /// <summary>要执行的完整命令（如 dotnet build / git status / pnpm --version）。</summary>
    public string? Command { get; set; }

    /// <summary>工作目录（必填，绝对路径且目录须存在；2026-10-29 起不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT，未传直接报必传错误）。</summary>
    public string? Cwd { get; set; }

    /// <summary>超时秒数（1-30，默认 30；超时后进程在后台继续，结果标记 timedOut）。</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>附加环境变量（键值对，可选）。</summary>
    public Dictionary<string, string>? Env { get; set; }
}

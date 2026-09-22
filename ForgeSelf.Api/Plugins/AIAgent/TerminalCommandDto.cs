namespace ForgeSelf.Api.Plugins.AIAgent;

/// <summary>
/// 终端命令工具的入参 DTO（对应 RunTerminalCommandTool 的 ParametersJsonSchema：
/// command / cwd / timeoutSeconds / env）。
/// 反序列化使用大小写不敏感（模型侧发 camelCase，此处属性 PascalCase），详见 RunTerminalCommandTool。
/// 仅用于工具入参反序列化，不参与持久化。
/// </summary>
public class TerminalCommandDto
{
    /// <summary>要执行的完整命令（如 dotnet build / git status / pnpm --version）。</summary>
    public string? Command { get; set; }

    /// <summary>工作目录（当前登记项目根内的相对路径，空=项目根；越界被拒）。</summary>
    public string? Cwd { get; set; }

    /// <summary>超时秒数（1-30，默认 30；超时后进程在后台继续，结果标记 timedOut）。</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>附加环境变量（键值对，可选）。</summary>
    public Dictionary<string, string>? Env { get; set; }
}

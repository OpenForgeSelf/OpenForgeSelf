namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 脚本运行环境探测契约（宿主实现：OpenForgeSelf.Backend.Services.RuntimeDetector）。
/// 供 ScriptRunner 等插件按接口消费，避免直接依赖宿主实现类。
/// </summary>
public interface IRuntimeDetector
{
    Task<List<RuntimeEnvironment>> DetectAllAsync();
    Task<RuntimeEnvironment> DetectAsync(ScriptLanguage language);
    string GetInterpreterPath(ScriptLanguage language);
    string GetVersion(ScriptLanguage language);
}

/// <summary>
/// 脚本运行环境描述（纯 DTO，随接口迁入 Abstractions）。
/// </summary>
public class RuntimeEnvironment
{
    public ScriptLanguage Language { get; set; }
    public bool IsAvailable { get; set; }
    public string InterpreterPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

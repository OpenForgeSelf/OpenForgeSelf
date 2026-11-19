namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// OAR RuntimeWorker 启动与 RPC 选项（P1）。
/// 生产路径发现策略在 P4 固化；本阶段由调用方显式配置。
/// </summary>
public sealed class OarWorkerClientOptions
{
    /// <summary>Node 可执行文件路径或命令名（须 ≥24）。</summary>
    public string NodeExecutable { get; set; } = "node";

    /// <summary>
    /// Worker 入口脚本（优先 <c>dist/index.js</c>；也可 <c>src/index.ts</c> 并配合 <see cref="NodeImportArgs"/>）。
    /// </summary>
    public string WorkerEntryPath { get; set; } = string.Empty;

    /// <summary>
    /// 附加在 Node 与入口之间的参数（例如 <c>--import</c>、<c>tsx</c>）。
    /// 使用已构建的 <c>dist/index.js</c> 时保持空。
    /// </summary>
    public IList<string> NodeImportArgs { get; set; } = new List<string>();

    /// <summary>Worker 工作目录（默认 = 入口所在目录）。</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>单次 JSON-RPC 请求默认超时。</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>启动后等待 <c>worker.hello</c> 成功的超时。</summary>
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>额外环境变量（透传给 Node；不落库明文密钥）。</summary>
    public IDictionary<string, string> Environment { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

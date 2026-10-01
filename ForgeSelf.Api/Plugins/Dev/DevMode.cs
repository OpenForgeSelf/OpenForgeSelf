using System.Reflection;

namespace ForgeSelf.Api.Plugins.Dev;

/// <summary>
/// 插件开发模式总闸（dev-only，默认全关）。
/// <para>
/// 仅当显式启用（环境变量 <c>FORGESELF_DEV_MODE=1</c> 或命令行 <c>--dev</c>）时才开启；
/// <b>不</b>把 <c>ASPNETCORE_ENVIRONMENT=Development</c> 视为开启信号——e2e 隔离实例固定注入该环境变量，
/// 若纳入判定会使 e2e 宿主行为漂移（违背「dev-off 与改动前一致」的兼容性承诺）。
/// </para>
/// <para>
/// 子开关（总闸开启时默认开，显式 <c>=0</c> 可单独关闭）：
/// <list type="bullet">
/// <item><c>FORGESELF_DEV_SHADOWCOPY</c> —— 后端 shadow-copy 装载与同版本热重载</item>
/// <item><c>FORGESELF_DEV_WEB_SRC</c> —— 前端资源 dev 态 no-store（防浏览器陈旧缓存兜底）</item>
/// <item><c>FORGESELF_DEV_DIAG</c> —— /api/dev/diagnostics 诊断端点</item>
/// <item><c>FORGESELF_DEV_WEB_HMR</c> —— 前端真 HMR dev server 对接（opt-in，默认关）</item>
/// </list>
/// </para>
/// <para>
/// 用户运行中的 Production 宿主（如 :51888）不设置任何 FORGESELF_DEV_* 变量，
/// 本类所有判定为 false，全部新代码路径不执行 —— 零行为变化。
/// </para>
/// </summary>
public static class DevMode
{
    /// <summary>总闸环境变量名。</summary>
    public const string EnvName = "FORGESELF_DEV_MODE";

    /// <summary>CLI 开关（--dev）。</summary>
    private const string CliFlag = "--dev";

    /// <summary>插件根目录覆盖的 CLI 参数名（--plugins-dir=&lt;path&gt; 或 --plugins-dir &lt;path&gt;）。</summary>
    private const string CliPluginsDir = "--plugins-dir";

    /// <summary>插件根目录覆盖环境变量。</summary>
    public const string EnvPluginsDir = "FORGESELF_PLUGINS_DIR";

    /// <summary>插件构建配置环境变量（默认 Debug）。</summary>
    public const string EnvConfig = "FORGESELF_DEV_CONFIG";

    /// <summary>插件目标框架环境变量（默认 net10.0）。</summary>
    public const string EnvTargetFramework = "FORGESELF_DEV_TFM";

    /// <summary>总闸是否开启。未调用 <see cref="Initialize"/> 时按环境变量兜底判定（覆盖测试直接构造场景）。</summary>
    public static bool Enabled { get; private set; }

    /// <summary>shadow-copy 装载与同版本热重载。</summary>
    public static bool ShadowCopyEnabled => Enabled && FlagEnabled("FORGESELF_DEV_SHADOWCOPY");

    /// <summary>前端资源 dev 态 no-store。</summary>
    public static bool WebSrcEnabled => Enabled && FlagEnabled("FORGESELF_DEV_WEB_SRC");

    /// <summary>诊断端点。</summary>
    public static bool DiagnosticsEnabled => Enabled && FlagEnabled("FORGESELF_DEV_DIAG");

    /// <summary>前端真 HMR dev server 对接（默认关，需显式 =1）。</summary>
    public static bool WebHmrEnabled => Enabled && FlagEnabled("FORGESELF_DEV_WEB_HMR");

    /// <summary>dev 插件构建配置（bin/&lt;config&gt;/&lt;tfm&gt; 解析用）。</summary>
    public static string BuildConfiguration { get; private set; } = "Debug";

    /// <summary>dev 插件目标框架（bin/&lt;config&gt;/&lt;tfm&gt; 解析用）。</summary>
    public static string TargetFramework { get; private set; } = "net10.0";

    /// <summary>CLI/env 指定的插件根目录覆盖值；未指定为 null（用默认 BaseDirectory/plugins）。</summary>
    public static string? PluginsDirectoryOverride { get; private set; }

    private static bool _initialized;

    /// <summary>
    /// 初始化总闸。在 <c>AppBuilder.CreateWebApplication</c> 入口处调用一次（早于任何插件装载与日志级别设定）；
    /// 幂等可重入（重复调用以后一次为准），便于测试按环境变量矩阵重置。
    /// </summary>
    /// <param name="args">宿主启动命令行参数。</param>
    public static void Initialize(string[]? args)
    {
        _initialized = true;

        var env = Environment.GetEnvironmentVariable(EnvName);
        var cliDev = args != null && args.Any(a => a.Equals(CliFlag, StringComparison.OrdinalIgnoreCase));

        // 仅显式通道：env=1 或 --dev。ASPNETCORE_ENVIRONMENT=Development 不算（e2e 固定注入，须保持 dev-off）。
        Enabled = string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) || cliDev;

        // 子开关：默认随总闸，显式 =0 可单独关闭（FORGESELF_DEV_WEB_HMR 例外：默认关，显式 =1 才开）。
        BuildConfiguration = Environment.GetEnvironmentVariable(EnvConfig) is { Length: > 0 } cfg ? cfg : "Debug";
        TargetFramework = Environment.GetEnvironmentVariable(EnvTargetFramework) is { Length: > 0 } tfm ? tfm : "net10.0";
        PluginsDirectoryOverride = ResolvePluginsDirectoryOverride(args);
    }

    /// <summary>
    /// 解析插件根目录：CLI <c>--plugins-dir=</c> ＞ 环境变量 <see cref="EnvPluginsDir"/> ＞ null（调用方用默认值）。
    /// 语义与 <c>StartupPortResolver</c> 一致：显式覆盖即用，未提供零副作用。
    /// </summary>
    public static string? ResolvePluginsDirectoryOverride(string[]? args)
    {
        if (args != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith(CliPluginsDir + "=", StringComparison.OrdinalIgnoreCase))
                {
                    var v = arg[(CliPluginsDir.Length + 1)..].Trim('"');
                    if (v.Length > 0) return v;
                }
                else if (arg.Equals(CliPluginsDir, StringComparison.OrdinalIgnoreCase)
                         && i + 1 < args.Length)
                {
                    var v = args[i + 1].Trim('"');
                    if (v.Length > 0) return v;
                }
            }
        }

        var env = Environment.GetEnvironmentVariable(EnvPluginsDir);
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    /// <summary>未初始化场景的兜底判定（不依赖 Initialize 的测试/工具场景）。</summary>
    private static bool FlagEnabled(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);

        // HMR 例外：默认关，显式 =1 才开；其余子开关默认随总闸，显式 =0 关。
        if (name == "FORGESELF_DEV_WEB_HMR")
            return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);

        return !string.Equals(v, "0", StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using NewLife.Log;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api;

/// <summary>
/// 启动期端口覆盖解析器（e2e / 多实例并行隔离用）。
/// <list type="bullet">
///   <item>优先级：环境变量 <c>FORGESELF_PORT</c> ＞ 命令行 <c>--server-port</c>。</item>
///   <item>仅当来源端口合法（1024-65535）且与 <see cref="ForgeSetting.Current"/> 不一致时才写盘，
///         保证「env 覆盖 / config 落盘 / ApplicationRestartService 重启读取」三方一致
///         （ApplicationRestartService 重启进程不传环境变量，端口必须落盘才不回退）。</item>
///   <item>未提供任何覆盖时零副作用，保持原有行为（默认 7102 或既有配置文件端口）。</item>
/// </list>
/// </summary>
public static class StartupPortResolver
{
    /// <summary>端口覆盖环境变量名（最优先）。</summary>
    public const string EnvPort = "FORGESELF_PORT";

    /// <summary>端口覆盖命令行参数名（次优先）。</summary>
    public const string CliPort = "--server-port";

    /// <summary>合法端口下界（含）。</summary>
    public const int MinPort = 1024;

    /// <summary>合法端口上界（含）。</summary>
    public const int MaxPort = 65535;

    /// <summary>
    /// 解析来源并（必要时）应用端口覆盖。返回最终生效端口：
    /// 未覆盖时返回 <see cref="ForgeSetting.Current"/> 当前端口（不写盘、不修改）。
    /// </summary>
    /// <param name="args">命令行参数数组。</param>
    public static int ResolveAndApply(string[] args) => ResolveAndApply(args, Environment.GetEnvironmentVariable);

    /// <summary>可注入环境变量查找的重载（测试用，避免测试进程改真实环境变量外泄给并行用例）。</summary>
    public static int ResolveAndApply(string[] args, Func<string, string?> envLookup)
    {
        var requested = ResolveFromSources(args, envLookup);
        if (requested == null)
            return ForgeSetting.Current.PortNumber;

        var current = ForgeSetting.Current.PortNumber;
        if (requested.Value == current)
            return current;

        // 先改内存值确保本进程绑定到目标端口（即使落盘失败也不影响本轮启动）。
        ForgeSetting.Current.PortNumber = requested.Value;
        try
        {
            // 落盘：ApplicationRestartService 重启不传 env，必须靠配置文件保持端口一致。
            ForgeSetting.Current.Save();
        }
        catch (Exception ex)
        {
            // 落盘失败（如目录不可写）不阻断启动：本轮仍按内存值绑定，仅重启后可能回退到旧端口。
            XTrace.Log.Warn("端口覆盖落盘失败（本轮仍生效，重启可能回退）: {0}", ex.Message);
        }

        return requested.Value;
    }

    /// <summary>
    /// 纯解析：按「环境变量 ＞ 命令行」顺序返回覆盖端口；来源缺失或非法返回 <c>null</c>。
    /// 不做任何写盘，供单测断言来源优先级与校验逻辑。
    /// </summary>
    public static int? ResolveFromSources(string[] args) => ResolveFromSources(args, Environment.GetEnvironmentVariable);

    /// <summary>可注入环境变量查找的重载（测试用）。</summary>
    public static int? ResolveFromSources(string[] args, Func<string, string?> envLookup)
    {
        // 1) 环境变量最优先（用户明确要求「优先环境变量设置」）。
        var env = envLookup(EnvPort);
        if (TryParsePort(env, out var envPort))
            return envPort;

        // 2) 命令行 --server-port（与托盘进程独占的 --port 区分，避免冲突）。
        var cli = ParseCliPort(args ?? Array.Empty<string>());
        if (cli != null)
            return cli.Value;

        return null;
    }

    private static int? ParseCliPort(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals(CliPort, StringComparison.OrdinalIgnoreCase))
            {
                // 形式一：--server-port 7105（空格分隔）
                if (i + 1 < args.Length && TryParsePort(args[i + 1], out var v))
                    return v;
                return null;
            }

            if (a.StartsWith(CliPort + "=", StringComparison.OrdinalIgnoreCase))
            {
                // 形式二：--server-port=7105
                var val = a.Substring(CliPort.Length + 1);
                if (TryParsePort(val, out var v))
                    return v;
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析单个端口字符串：支持纯数字或带前导冒号（兼容 <c>--urls</c> 风格）。
    /// 仅 1024-65535 视为合法；空/空白/非数字/越界返回 <c>false</c>。
    /// </summary>
    internal static bool TryParsePort(string? raw, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // 兼容 ":7102" 这类带协议分隔符的写法。
        var trimmed = raw!.Trim().TrimStart(':').Trim();
        if (!int.TryParse(trimmed, out port))
            return false;

        return port is >= MinPort and <= MaxPort;
    }
}

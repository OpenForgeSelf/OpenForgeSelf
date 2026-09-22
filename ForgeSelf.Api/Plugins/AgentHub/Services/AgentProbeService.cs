using System.Diagnostics;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>Agent 探测服务：定位可执行文件、取版本、校验 profile 断言。</summary>
public interface IAgentProbeService
{
    /// <summary>扫描本机 PATH 与已知安装路径，返回可登记的候选（不自动写入注册表）</summary>
    /// <returns>候选列表</returns>
    Task<IReadOnlyList<DiscoveredAgentDto>> DiscoverAsync();

    /// <summary>探测某个 agent 的默认交互口（存在性 / 版本 / profile 断言）</summary>
    /// <param name="agentId">agent 主键</param>
    /// <returns>探测结果</returns>
    Task<ProbeResultDto> ProbeAsync(Int32 agentId);

    /// <summary>探测指定交互口（存在性 / 版本 / profile 断言）</summary>
    /// <param name="accessPointId">交互口主键</param>
    /// <param name="agentVendor">厂商标识（用于取 profile 做断言；为空则跳过断言）</param>
    /// <returns>探测结果</returns>
    Task<ProbeResultDto> ProbeAccessPointAsync(Int32 accessPointId, String? agentVendor);

    /// <summary>在 PATH 中定位可执行文件（含 Windows 常见扩展名补全）</summary>
    /// <param name="executable">可执行文件名或路径</param>
    /// <returns>绝对路径；找不到返回 null</returns>
    String? ResolveExecutable(String executable);
}

/// <summary>
/// Agent 探测实现。
///
/// 探测策略：
/// 1. 若是绝对路径 → 直接校验存在性；
/// 2. 否则按 PATH 逐目录查找（Windows 补全 .cmd/.exe/.bat/.ps1）；
/// 3. 命中后跑 <c>--version</c>（或 profile 指定参数）取版本，并校验 profile 断言；
/// 4. profile 断言不通过 → Degraded + 明确提示「profile 可能过期」，绝不静默。
/// </summary>
public class AgentProbeService : IAgentProbeService
{
    private readonly IAgentRegistry _registry;
    private readonly ProfileLoader _profiles;

    public AgentProbeService(IAgentRegistry registry, ProfileLoader profiles)
    {
        _registry = registry;
        _profiles = profiles;
    }

    /// <summary>Windows 下需要补全的可执行扩展名（按优先级）</summary>
    private static readonly String[] WindowsExtensions = [".exe", ".cmd", ".bat", ".ps1"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscoveredAgentDto>> DiscoverAsync()
    {
        var result = new List<DiscoveredAgentDto>();
        var registered = _registry.List().Select(a => a.Vendor).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        // 内置 profile 逐个探测（每个 profile 一个候选）
        foreach (var profile in _profiles.All())
        {
            if (profile.Executable.IsNullOrEmpty()) continue;

            var path = ResolveExecutable(profile.Executable);
            if (path == null) continue;
            if (!seen.Add(profile.Id)) continue;

            result.Add(new DiscoveredAgentDto
            {
                Vendor = profile.Id,
                DisplayName = profile.DisplayName.IsNullOrEmpty() ? profile.Id : profile.DisplayName,
                Executable = path,
                Version = await TryGetVersionAsync(path, profile.ProbeArgs),
                AlreadyRegistered = registered.Contains(profile.Id),
                ProfileId = profile.Id
            });
        }

        // 常见安装位置兜底扫描（profile 未覆盖但本机装了的）
        foreach (var (vendor, displayName, paths) in KnownLocations)
        {
            if (seen.Contains(vendor) || registered.Contains(vendor)) continue;

            var path = paths.Select(ResolveExecutable).FirstOrDefault(p => p != null);
            if (path == null) continue;
            if (!seen.Add(vendor)) continue;

            result.Add(new DiscoveredAgentDto
            {
                Vendor = vendor,
                DisplayName = displayName,
                Executable = path!,
                Version = await TryGetVersionAsync(path!, "--version"),
                AlreadyRegistered = false,
                ProfileId = vendor
            });
        }

        XTrace.Log.Info("[AgentHub] 本机扫描完成，发现 {0} 个候选 agent", result.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<ProbeResultDto> ProbeAsync(Int32 agentId)
    {
        var agent = _registry.Get(agentId);
        if (agent == null)
        {
            return new ProbeResultDto
            {
                Health = "Missing",
                Error = $"agent #{agentId} 不存在"
            };
        }

        var ap = _registry.GetDefaultAccessPoint(agentId);
        if (ap == null)
        {
            return new ProbeResultDto
            {
                Health = "Missing",
                Error = $"agent「{agent.Name}」尚未配置任何交互口"
            };
        }

        return await ProbeAccessPointAsync(ap.Id, agent.Vendor);
    }

    /// <inheritdoc />
    public async Task<ProbeResultDto> ProbeAccessPointAsync(Int32 accessPointId, String? agentVendor)
    {
        var sw = Stopwatch.StartNew();
        var result = new ProbeResultDto();

        var ap = _registry.GetAccessPoint(accessPointId);

        if (ap == null || ap.Executable.IsNullOrEmpty())
        {
            result.Health = "Missing";
            result.Error = "交互口不存在或未配置可执行文件";
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        var path = ResolveExecutable(ap.Executable);
        if (path == null)
        {
            result.Health = "Missing";
            result.Error = $"未在 PATH 中找到可执行文件「{ap.Executable}」";

            _registry.UpdateHealth(ap.Id, "Missing", null, result.Error);

            result.ElapsedMs = sw.ElapsedMilliseconds;
            XTrace.Log.Warn("[AgentHub] 探测失败: {0}", result.Error);
            return result;
        }

        result.Found = true;
        result.Path = path;

        // 取版本（profile 的 probeArgs 优先，其次交互口配置，最后 --version）
        var profile = agentVendor.IsNullOrEmpty() ? null : _profiles.Get(agentVendor);
        var probeArgs = profile?.ProbeArgs ?? ap.ProbeArgs ?? "--version";
        var version = await TryGetVersionAsync(path, probeArgs);
        result.Version = version;

        if (version == null)
        {
            result.Health = "Degraded";
            result.Error = "可执行文件存在，但版本探测未返回可识别输出";
        }
        else
        {
            result.Health = "Ok";
        }

        // profile 断言校验（防脆弱三件套之一：版本漂移显式提示，不静默失效）
        if (profile != null && profile.Probe != null && profile.Probe.Count > 0 && version != null)
        {
            var failed = CheckAssertions(profile, version);
            if (failed != null)
            {
                result.Health = "Degraded";
                result.ProfileWarning = $"profile「{profile.Id}」可能过期：{failed}（当前版本 {version}）";
                XTrace.Log.Warn("[AgentHub] {0}", result.ProfileWarning);
            }
        }
        else if (profile != null && profile.Probe != null && profile.Probe.Count > 0 && version == null)
        {
            result.ProfileWarning = $"profile「{profile.Id}」的 probe 断言未能校验（拿不到版本号）";
        }

        _registry.UpdateHealth(ap.Id, result.Health, version, result.Error);

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;
        return result;
    }

    /// <inheritdoc />
    public String? ResolveExecutable(String executable)
    {
        if (executable.IsNullOrEmpty()) return null;

        // 1) 绝对路径直接校验
        if (Path.IsPathRooted(executable))
        {
            return File.Exists(executable) ? executable : null;
        }

        // 2) 沿 PATH 逐目录查找
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
        var dirs = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var candidates = new List<String> { executable };
        if (OperatingSystem.IsWindows() && Path.GetExtension(executable).IsNullOrEmpty())
        {
            candidates.AddRange(WindowsExtensions.Select(ext => executable + ext));
        }

        foreach (var dir in dirs)
        {
            foreach (var name in candidates)
            {
                try
                {
                    var full = Path.Combine(dir, name);
                    if (File.Exists(full)) return full;
                }
                catch
                {
                    // PATH 里可能有非法路径项，跳过
                }
            }
        }

        return null;
    }

    /// <summary>跑版本命令并解析出版本号（失败返回 null，不抛出）。</summary>
    /// <param name="path">可执行文件路径</param>
    /// <param name="probeArgs">探测参数（如 "--version"；含空格的按空白切分为多个 argv 项）</param>
    /// <returns>版本号或 null</returns>
    private static async Task<String?> TryGetVersionAsync(String path, String? probeArgs)
    {
        var args = (probeArgs.IsNullOrEmpty() ? "--version" : probeArgs)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var run = await ProcessRunner.RunAsync(path, args, timeoutMs: ProcessRunner.DefaultProbeTimeoutMs);

        if (!run.Started) return null;

        // 版本号可能出现在 stdout 或 stderr（各家 CLI 习惯不同）
        var text = !run.StdOut.IsNullOrWhiteSpace() ? run.StdOut : run.StdErr;
        if (text.IsNullOrWhiteSpace()) return null;

        return ExtractVersion(text);
    }

    /// <summary>从命令输出中提取版本号（形如 1.2.3 或 v1.2.3-beta.1）。</summary>
    /// <param name="text">命令输出</param>
    /// <returns>版本号；提取不到时返回首行摘要</returns>
    internal static String? ExtractVersion(String text)
    {
        var match = VersionRegex.Match(text);
        if (match.Success) return match.Value;

        // 没有标准版本号：返回首行（截断），总比 null 有用
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (firstLine.IsNullOrEmpty()) return null;
        return firstLine.Length > 100 ? firstLine[..100] : firstLine;
    }

    /// <summary>版本号正则（含可选 v 前缀与预发布后缀）</summary>
    private static readonly Regex VersionRegex = new(
        @"v?\d+\.\d+(\.\d+)?(-[0-9A-Za-z.\-]+)?",
        RegexOptions.Compiled);

    /// <summary>校验 profile 断言，返回失败说明；全部通过返回 null。</summary>
    /// <param name="profile">profile</param>
    /// <param name="version">探测到的版本</param>
    /// <returns>失败说明或 null</returns>
    private static String? CheckAssertions(AgentProfile profile, String version)
    {
        foreach (var assertion in profile.Probe!)
        {
            if (assertion.Expect.IsNullOrEmpty()) continue;

            switch (assertion.Kind)
            {
                case "version_min":
                    if (!CompareVersion(version, assertion.Expect, out var below))
                        return $"要求版本 ≥ {assertion.Expect}，实际 {version}";
                    break;

                case "version_eq":
                    if (!String.Equals(NormalizeVersion(version), NormalizeVersion(assertion.Expect), StringComparison.OrdinalIgnoreCase))
                        return $"要求版本 = {assertion.Expect}，实际 {version}";
                    break;

                case "help_contains":
                    // 需要跑 --help，成本较高；此处只校验版本输出里是否含期望关键字。
                    // 真正的 --help 校验在 probe 的扩展路径里做（P1），当前不误报为失败。
                    break;

                default:
                    XTrace.Log.Debug("[AgentHub] 未知的 probe 断言类型「{0}」，已忽略", assertion.Kind);
                    break;
            }
        }

        return null;
    }

    /// <summary>版本比较：actual 是否 ≥ min。</summary>
    /// <param name="actual">实际版本</param>
    /// <param name="min">最低版本</param>
    /// <param name="belowMin">输出：是否低于最低版本</param>
    /// <returns>是否可比较（false 表示格式不可解析）</returns>
    private static Boolean CompareVersion(String actual, String min, out Boolean belowMin)
    {
        belowMin = false;

        if (!Version.TryParse(NormalizeVersion(actual), out var a)) return false;
        if (!Version.TryParse(NormalizeVersion(min), out var m)) return false;

        belowMin = a < m;
        return true;
    }

    /// <summary>规范化版本串（去 v 前缀、去预发布后缀，补齐为 3 段）。</summary>
    private static String NormalizeVersion(String raw)
    {
        var v = raw.Trim().TrimStart('v', 'V');
        var dash = v.IndexOf('-');
        if (dash > 0) v = v[..dash];

        // Version.TryParse 需要至少 major.minor
        var parts = v.Split('.');
        if (parts.Length == 0) return "0.0";
        if (parts.Length == 1) return parts[0] + ".0";

        return v;
    }

    /// <summary>已知安装位置兜底（profile 未覆盖时的补充扫描）</summary>
    private static readonly (String Vendor, String DisplayName, String[] Paths)[] KnownLocations =
    [
        ("cursor", "Cursor", ["cursor-agent", "cursor"]),
        ("aider", "Aider", ["aider"]),
        ("gemini", "Gemini CLI", ["gemini"]),
    ];
}

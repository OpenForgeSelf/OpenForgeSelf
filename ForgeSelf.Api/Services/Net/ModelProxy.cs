using System.Net;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Services.Net;

/// <summary>
/// 模型请求的实时代理决策器（设置·网络设置，2026-10-16-settings-proxy-mode-bypass）。
/// 每次回调实时读 <see cref="ForgeSetting.Current"/> ⇒ 保存即生效、无需重启（BR-3）；
/// 由 AppBuilder 装入 IHttpClientFactory 默认 handler，覆盖全部模型请求出口（M1~M5），
/// AgentChatController 的 OpenAI SDK 亦经同一 factory client（M6）。
/// 决策顺序（BR-1）：① 目标命中旁路条目或回环 ⇒ 直连；② direct ⇒ 直连；
/// ③ manual ⇒ 校验后返回 http://host:port（无效 ⇒ 直连 + Warn 兜底）；
/// ④ system（默认）⇒ 委托 <see cref="WebRequest.GetSystemWebProxy"/>（异常兜底直连 + Warn）。
/// 仅支持 HTTP/HTTPS 代理（BCL WebProxy 限制，SOCKS5 已记 TODO）。
/// </summary>
public class ModelProxy : IWebProxy
{
    public const string ModeSystem = "system";
    public const string ModeManual = "manual";
    public const string ModeDirect = "direct";

    private readonly ISecretEncryptionService _encryption;
    private readonly Func<IWebProxy> _systemProxyProvider;

    public ModelProxy(ISecretEncryptionService encryption, Func<IWebProxy>? systemProxyProvider = null)
    {
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _systemProxyProvider = systemProxyProvider ?? (() => WebRequest.GetSystemWebProxy());
        // 认证凭据按请求实时取配置（407 挑战时由 handler 回调 GetCredential，仅 manual 模式参与）
        Credentials = new DynamicProxyCredentials(_encryption);
    }

    /// <summary>凭据提供器（.NET 10 IWebProxy 契约允许为 null；本实现恒为实时读取器）</summary>
    public ICredentials? Credentials { get; set; }

    /// <summary>目标是否绕过代理：回环永不走代理；命中旁路条目亦视为绕过</summary>
    public bool IsBypassed(Uri destination)
    {
        if (destination == null) return true;
        return IsLoopback(destination) || BypassMatcher.IsMatched(destination.Host, ForgeSetting.Current.ProxyBypass);
    }

    /// <summary>返回目标请求应使用的代理地址；null = 直连（.NET 10 IWebProxy.GetProxy，旧名 GetUri）</summary>
    public Uri? GetProxy(Uri destination)
    {
        if (destination == null) return null;
        var s = ForgeSetting.Current;

        // ① 回环 / 旁路条目命中 ⇒ 直连（BR-1，含 system 模式叠加用户条目）
        if (IsLoopback(destination) || BypassMatcher.IsMatched(destination.Host, s.ProxyBypass)) return null;

        switch (NormalizeMode(s.ProxyMode))
        {
            case ModeDirect:
                return null;

            case ModeManual:
                var host = (s.ProxyHost ?? "").Trim();
                // 运行期兜底：与 API 层 BR-5 同规则校验（带 scheme 的 host 不会被 new Uri 拒绝，而是规整成非法地址，不可依赖 try/catch）
                if (host.Length == 0 || !ProxyHostPattern.IsMatch(host) || s.ProxyPort is <= 0 or > 65535)
                {
                    XTrace.Log.Warn("代理模式为 manual 但地址/端口无效（host={0}, port={1}），本次请求直连", host, s.ProxyPort);
                    return null;
                }
                return TryBuildProxyUri(host, s.ProxyPort);

            default: // system：委托系统代理对象（其自身含 WinINET/WinHTTP 旁路判定）
                try
                {
                    var sp = _systemProxyProvider();
                    if (sp == null || sp.IsBypassed(destination)) return null;
                    var p = sp.GetProxy(destination);
                    // 系统代理对象无配置时通常原样返回 destination，视为直连
                    return SameHost(p, destination) ? null : p;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("读取系统代理失败，本次请求直连：{0}", ex.Message);
                    return null;
                }
        }
    }

    /// <summary>非法/未知模式值 → 按 system 处理 + Warn（运行期兜底，不落盘不改写）</summary>
    public static string NormalizeMode(string? mode)
    {
        var m = (mode ?? "").Trim().ToLowerInvariant();
        if (m is ModeManual or ModeDirect or ModeSystem) return m;
        XTrace.Log.Warn("未知代理模式值 \"{0}\"，按 system 处理", mode);
        return ModeSystem;
    }

    /// <summary>代理地址合法性（域名/IPv4，不含 scheme/端口）——与 SettingsController BR-5 同规则</summary>
    private static readonly Regex ProxyHostPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    /// <summary>拼接代理 Uri；Regex 放行的边界串（如 "."/"..--.."）在 new Uri 处仍可能抛，一律兜底直连不留异常打断请求</summary>
    private static Uri? TryBuildProxyUri(string host, int port)
    {
        try
        {
            return new Uri($"http://{host}:{port}");
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("代理地址无法构成有效 Uri（host={0}, port={1}），本次请求直连：{2}", host, port, ex.Message);
            return null;
        }
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool SameHost(Uri? a, Uri b) =>
        a == null || (a.Host.Equals(b.Host, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port);

    /// <summary>按请求实时读取 ForgeSetting 的代理认证凭据；仅 manual 模式参与；解密失败降级为无密码并留痕（BC-2）</summary>
    private sealed class DynamicProxyCredentials : ICredentials
    {
        private readonly ISecretEncryptionService _encryption;

        public DynamicProxyCredentials(ISecretEncryptionService encryption) => _encryption = encryption;

        public NetworkCredential? GetCredential(Uri uri, string authType)
        {
            var s = ForgeSetting.Current;
            if (NormalizeMode(s.ProxyMode) != ModeManual) return null;
            var user = (s.ProxyUsername ?? "").Trim();
            if (user.Length == 0) return null;

            string? password = null;
            if (!string.IsNullOrEmpty(s.ProxyPasswordCipher))
            {
                try
                {
                    password = _encryption.Decrypt(s.ProxyPasswordCipher);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("代理密码解密失败，按无密码处理：{0}", ex.Message);
                }
            }
            return new NetworkCredential(user, password ?? "");
        }
    }
}

/// <summary>
/// 「不走代理的条目」解析与匹配（BR-2，参考系统代理 ProxyOverride / NO_PROXY 惯例）：
/// - <c>*.example.com</c> → 后缀匹配（含 example.com 本身）；
/// - 裸域名（含前导点）→ 同样后缀匹配；
/// - IPv4 精确 → host 相等；
/// - <c>10.*</c> / <c>192.168.*</c> 段前缀 → host 以 <c>10.</c> 开头即匹配。
/// 大小写不敏感、trim、空行忽略。
/// </summary>
public static class BypassMatcher
{
    /// <summary>条目字符集白名单（BR-5）</summary>
    private static readonly Regex EntryPattern = new(@"^[A-Za-z0-9.*_:-]+$", RegexOptions.Compiled);
    private static readonly Regex Ipv4Pattern = new(@"^\d{1,3}(\.\d{1,3}){3}$", RegexOptions.Compiled);

    /// <summary>解析原始文本为条目列表（\n 分隔，trim，忽略空行，转小写去重）</summary>
    public static List<string> Parse(string? raw) =>
        (raw ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();

    /// <summary>目标 host 是否命中任一条目</summary>
    public static bool IsMatched(string? host, string? raw)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(raw)) return false;
        var h = host.ToLowerInvariant();
        foreach (var entry in Parse(raw))
            if (Matches(h, entry)) return true;
        return false;
    }

    private static bool Matches(string host, string entry)
    {
        if (entry.StartsWith("*."))
        {
            var suffix = entry.Substring(2);
            return host == suffix || host.EndsWith("." + suffix);
        }
        if (entry.EndsWith(".*"))
        {
            // 10.* → 前缀 "10."；192.168.* → 前缀 "192.168."
            var prefix = entry[..^1];
            return host.StartsWith(prefix);
        }
        if (Ipv4Pattern.IsMatch(entry)) return host == entry;
        var bare = entry.StartsWith(".") ? entry.Substring(1) : entry;
        return host == bare || host.EndsWith("." + bare);
    }

    /// <summary>单条条目是否合法（长度与字符集，BR-5；空串视为合法=占位行，保存前过滤）</summary>
    public static bool IsValidEntry(string? entry) =>
        string.IsNullOrWhiteSpace(entry) || (entry.Trim().Length <= 128 && EntryPattern.IsMatch(entry.Trim()));
}

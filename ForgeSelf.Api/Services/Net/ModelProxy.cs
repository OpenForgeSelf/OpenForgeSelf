using System.Net;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Services.Net;

/// <summary>
/// 模型请求的实时代理决策器（设置·网络设置，2026-10-16-settings-network-proxy）。
/// 每次回调实时读 <see cref="ForgeSetting.Current"/> ⇒ 保存即生效、无需重启（Spec BR-3）；
/// 由 AppBuilder 装入 IHttpClientFactory 默认 handler，覆盖全部模型请求出口（M1~M5），
/// AgentChatController 的 OpenAI SDK 亦经同一 factory client（M6）。
/// 语义（Spec FR-2/BR-1/BR-2/BC-2/BC-3）：
/// - 未开启 ⇒ GetProxy 返回 null = 显式直连（同时屏蔽默认的「跟随系统代理」行为）；
/// - 回环/localhost 目标 ⇒ 永不走代理；
/// - 开启但运行期地址无效 ⇒ 直连 + Warn（API 层已按 BR-5 阻止无效配置落盘，此处为兜底）；
/// - 仅支持 HTTP/HTTPS 代理（BCL WebProxy 限制，SOCKS5 已记 TODO）。
/// </summary>
public class ModelProxy : IWebProxy
{
    private readonly ISecretEncryptionService _encryption;

    public ModelProxy(ISecretEncryptionService encryption)
    {
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        // 认证凭据按请求实时取配置（407 挑战时由 handler 回调 GetCredential）
        Credentials = new DynamicProxyCredentials(_encryption);
    }

    /// <summary>凭据提供器（.NET 10 IWebProxy 契约允许为 null；本实现恒为实时读取器）</summary>
    public ICredentials? Credentials { get; set; }

    /// <summary>目标是否绕过代理：回环地址永不走代理（BR-2）</summary>
    public bool IsBypassed(Uri destination) => IsLoopback(destination);

    /// <summary>返回目标请求应使用的代理地址；null = 直连（.NET 10 IWebProxy.GetProxy，旧名 GetUri）</summary>
    public Uri? GetProxy(Uri destination)
    {
        if (destination == null) return null;
        var s = ForgeSetting.Current;
        if (!s.ProxyEnabled) return null;
        if (IsLoopback(destination)) return null;

        var host = (s.ProxyHost ?? "").Trim();
        // 运行期兜底：与 API 层 BR-5 同规则校验（带 scheme 的 host 不会被 new Uri 拒绝，而是规整成非法地址，不可依赖 try/catch）
        if (host.Length == 0 || !ProxyHostPattern.IsMatch(host) || s.ProxyPort is <= 0 or > 65535)
        {
            XTrace.Log.Warn("代理已开启但地址/端口无效（host={0}, port={1}），本次请求直连", host, s.ProxyPort);
            return null;
        }

        // Bugbot 审计（2026-10-16）：Regex 放行的极端串（如纯 "-"/"."）仍可能在 Uri 构造处抛异常，
        // 兜底必须覆盖构造，否则每个模型请求带异常而非承诺的「直连 + Warn」
        try
        {
            return new Uri($"http://{host}:{s.ProxyPort}");
        }
        catch (UriFormatException)
        {
            XTrace.Log.Warn("代理地址无法解析（host={0}, port={1}），本次请求直连", host, s.ProxyPort);
            return null;
        }
    }

    /// <summary>代理地址合法性（域名/IPv4，不含 scheme/端口）——与 SettingsController BR-5 同规则</summary>
    private static readonly Regex ProxyHostPattern = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>按请求实时读取 ForgeSetting 的代理认证凭据；解密失败降级为无密码并留痕（BC-2）</summary>
    private sealed class DynamicProxyCredentials : ICredentials
    {
        private readonly ISecretEncryptionService _encryption;

        public DynamicProxyCredentials(ISecretEncryptionService encryption) => _encryption = encryption;

        public NetworkCredential? GetCredential(Uri uri, string authType)
        {
            var s = ForgeSetting.Current;
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

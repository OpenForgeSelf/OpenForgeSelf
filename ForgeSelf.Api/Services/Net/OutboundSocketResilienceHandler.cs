using System.Net.Sockets;

namespace ForgeSelf.Api.Services.Net;

/// <summary>
/// 出站 Socket 10013 自愈层（2026-11-21 输入15）：包在 SocketsHttpHandler 外的 primary handler。
/// 根因（pilot 2026-11-21-update-restart-socket-10013）：Windows 全机动态端口段（实测
/// 10000-64999）与 WinNAT/Hyper-V 保留段重叠时，connect 阶段报 WSAEACCES（SocketException
/// 10013「以一种访问权限不允许的方式做了一个访问套接字的尝试」）；保留段可长期驻留，
/// 手动重开连接「自愈」只是扫过了保留段。本 handler 对 AccessDenied 做有界重试
/// （MaxAttempts=6，退避 150ms×attempt），耗尽后抛带处置指引的中文诊断，不吞异常。
/// 不改变代理与回环语义（InnerHandler 链上的 SocketsHttpHandler 仍持有 ModelProxy）。
/// </summary>
public sealed class OutboundSocketResilienceHandler : DelegatingHandler
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(150);

    private readonly ILogger<OutboundSocketResilienceHandler>? _logger;

    public OutboundSocketResilienceHandler(ILogger<OutboundSocketResilienceHandler>? logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (IsAccessDenied(ex) && attempt < MaxAttempts)
            {
                var delay = BaseDelay * attempt;
                _logger?.LogWarning(
                    "出站连接被拒（WSAEACCES/10013，疑似动态端口段撞 WinNAT 保留段），第 {Attempt}/{Max} 次有界重试，退避 {Delay}ms：{Target}",
                    attempt, MaxAttempts, (int)delay.TotalMilliseconds, request.RequestUri?.Host ?? "(unknown)");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (IsAccessDenied(ex))
            {
                _logger?.LogError(
                    "出站连接持续被拒（WSAEACCES/10013）{Attempt} 次，已耗尽重试：{Target}",
                    MaxAttempts, request.RequestUri?.Host ?? "(unknown)");
                throw new InvalidOperationException(
                    $"出站连接被系统拒绝（SocketException 10013，WSAEACCES）已重试 {MaxAttempts} 次：本机动态端口段与 WinNAT/Hyper-V 保留端口段重叠。" +
                    "临时处置（管理员）：net stop winnat && net start winnat；" +
                    "反复发生则永久挪开重叠段：netsh int ipv4 set dynamicport tcp start=40000 num=9900。详见 docs/11-troubleshooting/index.md 10013 行。", ex);
            }
        }
    }

    /// <summary>判定 HttpRequestException 链上是否有 connect 阶段的 SocketError.AccessDenied（WSAEACCES=10013）。</summary>
    private static bool IsAccessDenied(HttpRequestException ex)
    {
        for (var cur = (Exception?)ex; cur != null; cur = cur.InnerException)
        {
            if (cur is SocketException se && se.SocketErrorCode == SocketError.AccessDenied)
                return true;
        }
        return false;
    }
}

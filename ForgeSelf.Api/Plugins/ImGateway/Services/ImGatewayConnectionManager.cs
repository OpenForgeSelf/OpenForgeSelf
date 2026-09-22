using ForgeSelf.Api.Plugins.ImGateway.Channels;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 长连接通道生命周期管理器。
/// 背景：回调型通道是"平台推过来"，无需本端维持任何后台任务；而长连接型通道必须
/// 由本端主动外连并保活，因此需要一个地方负责「启动时拉起 / 改配置后重启 / 停止时断开」。
/// 只对 <see cref="ImTransportMode.WebSocket"/> 通道生效，回调型通道被完全跳过（零影响）。
/// 生命周期自管：插件构造本 Manager 时即自初始化（不依赖宿主级 HostedService——
/// 插件热重载时 HostedService 不会重启，会导致新实例永远建不了连）。
/// </summary>
public class ImGatewayConnectionManager
{
    private readonly IEnumerable<IImChannel> _channels;
    private readonly ImGatewayRouter _router;

    private Func<InboundMessage, CancellationToken, Task>? _onMessage;
    private CancellationToken _hostToken;
    private CancellationTokenSource? _cts;

    public ImGatewayConnectionManager(IEnumerable<IImChannel> channels, ImGatewayRouter router)
    {
        _channels = channels;
        _router = router;
        // 插件自管生命周期：构造即自初始化（不依赖宿主级 HostedService——热重载时 HostedService 不会重启）。
        try { EnsureStarted(); } catch (Exception ex) { XTrace.Log.Error("[IM网关] 构造期自启动失败（将在首次 Apply/重连时重试）: {0}", ex.Message); }
    }

    /// <summary>确保已初始化（幂等）：未初始化则自起 CTS + 注册消息回调 + 拉起所有长连接。</summary>
    private void EnsureStarted()
    {
        if (_onMessage != null) return;
        _cts = new CancellationTokenSource();
        _hostToken = _cts.Token;
        _onMessage = (msg, ct) => _router.ProcessInboundAsync(msg, ct);
        Apply();
    }

    private IEnumerable<IImChannel> LongLived =>
        _channels.Where(c => c.TransportMode == ImTransportMode.WebSocket);

    /// <summary>宿主启动：拉起所有长连接通道（未启用/凭据不全的通道内部会自行跳过）。</summary>
    public void StartAll(CancellationToken hostToken = default)
    {
        // 兼容旧调用方：已自初始化则直接 Apply 校正；未初始化则接管外部 token。
        if (_onMessage == null)
        {
            _hostToken = hostToken;
            _onMessage = (msg, ct) => _router.ProcessInboundAsync(msg, ct);
        }
        Apply();
    }

    /// <summary>
    /// 按当前配置校正所有长连接：启用→确保运行（凭据变更则重建）；禁用→停止。
    /// 保存配置后调用，使改密钥/开关立即生效，无需重启宿主。
    /// 依赖 StartAsync 的幂等性——重复调用不会造成重复抢连。
    /// </summary>
    public void Apply()
    {
        if (_onMessage == null)
        {
            EnsureStarted();
        }
        foreach (var ch in LongLived)
        {
            try
            {
                ch.StartAsync(_onMessage!, _hostToken);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[IM网关] 启动长连接失败 Channel={0}: {1}", ch.ChannelType, ex.Message);
            }
        }
    }

    /// <summary>宿主停止：断开所有长连接。</summary>
    public void StopAll()
    {
        foreach (var ch in LongLived)
        {
            try { ch.StopAsync(); }
            catch (Exception ex)
            {
                XTrace.Log.Error("[IM网关] 停止长连接失败 Channel={0}: {1}", ch.ChannelType, ex.Message);
            }
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _onMessage = null;
    }

    /// <summary>
    /// 手动重连指定长连接通道（D2）：复位被踢标志并强制重建连接。
    /// 返回是否找到并触发了该通道；找不到 / 非长连接形态返回 false。
    /// </summary>
    public bool Reconnect(string channelType)
    {
        var ch = LongLived.FirstOrDefault(c => c.ChannelType == channelType);
        if (ch == null || ch is not WeComChannel wecom)
        {
            XTrace.Log.Warn("[IM网关] 重连请求的目标通道不存在或非长连接形态: {0}", channelType);
            return false;
        }
        try
        {
            EnsureStarted();
            wecom.ReconnectAsync(_hostToken);
            XTrace.Log.Info("[IM网关] 已触发手动重连 Channel={0}", channelType);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[IM网关] 手动重连失败 Channel={0}: {1}", channelType, ex.Message);
            return false;
        }
    }
}
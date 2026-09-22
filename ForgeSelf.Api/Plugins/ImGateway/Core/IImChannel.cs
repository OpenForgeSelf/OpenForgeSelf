namespace ForgeSelf.Api.Plugins.ImGateway.Core;

/// <summary>
/// IM 通道适配器抽象。当前仅有企业微信「智能机器人」WebSocket 长连接一种形态（v2.0.0 起移除回调形态）。
/// 网关内核只依赖本接口：收（长连接推送 → onMessage）与发（SendAsync）。
/// </summary>
public interface IImChannel
{
    /// <summary>通道类型标识（与 ImChannelTypes 常量一致）。</summary>
    string ChannelType { get; }

    /// <summary>显示名（配置页用）。</summary>
    string DisplayName { get; }

    /// <summary>该通道是否启用（随运行时配置）。</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 传输模式：v2.0.0 起仅 WebSocket（长连接，客户端主动外连，无需公网 IP、无需加解密）。
    /// </summary>
    ImTransportMode TransportMode => ImTransportMode.WebSocket;

    /// <summary>长连接状态快照（未启动时默认 Disconnected）。</summary>
    ImConnectionStatus Status => new ImConnectionStatus();

    /// <summary>
    /// 启动长连接。实现方应在后台维持连接、心跳与自动重连，并把收到的每条入站消息交给 <paramref name="onMessage"/>。
    /// </summary>
    /// <param name="onMessage">入站消息处理器（网关路由入口）。</param>
    /// <param name="cancellationToken">宿主停止令牌。</param>
    Task StartAsync(Func<InboundMessage, CancellationToken, Task> onMessage, CancellationToken cancellationToken = default);

    /// <summary>停止长连接并释放资源。</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>出站发送（把 AI 回复推回平台）。</summary>
    Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}

using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Channels;

/// <summary>
/// 企业微信「智能机器人」通道适配器（WebSocket 长连接形态）。
/// 与旧的「自建应用回调」模型（CorpId+AgentId+Token+EncodingAESKey+access_token）彻底不同：
/// - 收：不是被动等 HTTP 回调，而是本端主动外连 wss://openws.work.weixin.qq.com 并订阅，
///       平台通过长连接推送 aibot_msg_callback，无需公网 URL、无需消息加解密。
/// - 发：直接经长连接发 aibot_respond_msg（透传入站 req_id），不再走 access_token + message/send。
/// 凭据只有两项：BotId + Secret（长连接专用密钥），在企微后台开启「API 模式-长连接」后获得。
/// 同一机器人同时只允许一个长连接，若别处已连，本连接会被踢下线（disconnected_event）并停止重连。
/// </summary>
public class WeComChannel : IImChannel
{
    private readonly IConfigStore _store;
    private readonly object _sync = new();

    private Func<InboundMessage, CancellationToken, Task>? _onMessage;
    private CancellationTokenSource? _pumpCts;
    private Task? _pumpTask;
    private WeComAiBotClient? _client;

    /// <summary>当前正在运行的凭据指纹（botId+secret），用于判断配置是否被改动、是否需要重连。</summary>
    private string _runningKey = string.Empty;

    public string ChannelType => ImChannelTypes.WeCom;
    public string DisplayName => "企业微信（智能机器人）";

    public ImTransportMode TransportMode => ImTransportMode.WebSocket;

    private WeComConfig Cfg => _store.Load().WeCom;

    public bool IsEnabled
    {
        get
        {
            var c = Cfg;
            return c.Enabled && !string.IsNullOrWhiteSpace(c.BotId) && !string.IsNullOrWhiteSpace(c.Secret);
        }
    }

    public ImConnectionStatus Status => _client?.Status ?? new ImConnectionStatus();

    public WeComChannel(IConfigStore store)
    {
        _store = store;
    }

    /// <summary>
    /// 启动长连接。幂等：若已在运行且凭据未变化，则不重复建连（企微对订阅有频率保护，
    /// 且同一机器人重复抢连会把既有连接踢掉）。
    /// </summary>
    public Task StartAsync(Func<InboundMessage, CancellationToken, Task> onMessage, CancellationToken ct = default)
    {
        _onMessage = onMessage;
        EnsureRunning(ct);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 按当前配置校正连接状态：启用且凭据完整→确保在运行（凭据变了则重启）；否则→停止。
    /// 配置保存后由连接管理器调用，使改配置即时生效。
    /// </summary>
    public void EnsureRunning(CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_onMessage == null) return; // 尚未注册处理器（宿主未启动）

            var c = Cfg;
            var key = $"{c.BotId}|{c.Secret}";

            if (!IsEnabled)
            {
                if (_pumpTask != null)
                {
                    XTrace.Log.Info("[企微] 通道未启用或凭据不全，停止长连接");
                    StopInternal();
                }
                _runningKey = string.Empty;
                return;
            }

            // 已在运行且凭据未变 → 保持现状
            if (_pumpTask != null && !_pumpTask.IsCompleted && key == _runningKey) return;

            StopInternal();

            _runningKey = key;
            var cts = new CancellationTokenSource();
            _pumpCts = cts;

            // 客户端每次重连都重新读配置拿最新凭据（支持"改密钥后不重启宿主"）
            var client = new WeComAiBotClient(() =>
            {
                var cc = Cfg;
                return (cc.BotId, cc.Secret);
            }, _onMessage!);
            _client = client;

            var linked = ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token)
                : cts;

            _pumpTask = Task.Run(() => client.RunAsync(linked.Token), CancellationToken.None);
            XTrace.Log.Info("[企微] 长连接已启动 botId={0}", c.BotId);
        }
    }

    /// <summary>停止长连接（宿主停止 / 通道禁用 / 凭据变更重建前）。</summary>
    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            StopInternal();
            _runningKey = string.Empty;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 手动重连（D2）：复位被踢标志并强制重建长连接，不等自动退避。
    /// 场景：被其它实例抢占后已停止重连（kicked），用户点「重连」重新抢占。
    /// </summary>
    public Task ReconnectAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            _client?.ResetKicked();
            StopInternal();
            _runningKey = string.Empty;
            EnsureRunning(ct);
        }
        return Task.CompletedTask;
    }

    private void StopInternal()
    {
        try { _pumpCts?.Cancel(); } catch { }
        try { _pumpTask?.Wait(TimeSpan.FromSeconds(3)); } catch { }
        _pumpCts?.Dispose();
        _pumpCts = null;
        _pumpTask = null;
    }

    /// <summary>
    /// 出站发送。长连接下有两条路径：
    /// 1. 带 ReplyReqId → 被动回复 aibot_respond_msg（透传回调 req_id，服务端据此关联到那次提问）。
    /// 2. 不带 → 主动推送 aibot_send_msg（需 chatType 指明单聊/群聊，否则服务端默认按群聊解析）。
    /// </summary>
    public async Task SendAsync(OutboundMessage msg, CancellationToken ct = default)
    {
        var client = _client;
        if (client == null)
        {
            XTrace.Log.Warn("[企微] 未建立长连接，跳过发送");
            return;
        }

        if (!string.IsNullOrEmpty(msg.ReplyReqId))
        {
            var ok = await client.ReplyAsync(msg.ReplyReqId!, msg.Content, ct).ConfigureAwait(false);
            if (!ok) XTrace.Log.Error("[企微] 回复失败（长连接不可用？）会话={0}", msg.ConversationId);
            return;
        }

        // 主动推送：单聊时 chatid 必须传 userid（1），群聊传 chatid（2）
        var isGroup = string.Equals(msg.ChatType, "group", StringComparison.OrdinalIgnoreCase);
        var chatId = isGroup ? msg.ConversationId : msg.PlatformUserId;
        if (string.IsNullOrEmpty(chatId))
        {
            XTrace.Log.Warn("[企微] 主动推送缺少目标会话标识，跳过");
            return;
        }
        var okPush = await client.SendAsync(chatId!, isGroup ? 2 : 1, msg.Content, ct).ConfigureAwait(false);
        if (!okPush) XTrace.Log.Error("[企微] 主动推送失败 目标={0}", chatId);
    }
}

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 企业微信「智能机器人」长连接客户端（WebSocket 形态）。
/// 协议依据官方文档《智能机器人长连接》(developer.work.weixin.qq.com/document/path/101463)：
/// - 端点：wss://openws.work.weixin.qq.com
/// - 订阅：连上后发 aibot_subscribe{bot_id, secret}，errcode=0 才算可用
/// - 收：aibot_msg_callback（消息）/ aibot_event_callback（事件）
/// - 回：aibot_respond_msg，长连接下普通文本回复也必须用 msgtype=stream + stream.finish
/// - 心跳：定期发 ping 命令帧（官方建议 30s）
/// - 限制：同一机器人同时只允许一个长连接，新连接会踢掉旧连接并给旧连接推 disconnected_event
/// 与回调形态相比：不需要公网 URL、不需要 access_token 中控、不需要消息加解密。
/// </summary>
public sealed class WeComAiBotClient
{
    /// <summary>官方长连接端点（私有化部署时可通过配置覆盖）。</summary>
    public const string DefaultEndpoint = "wss://openws.work.weixin.qq.com";

    private readonly Func<(string BotId, string Secret)> _credProvider;
    private readonly Func<InboundMessage, CancellationToken, Task> _onMessage;
    private readonly string _endpoint;

    // 发送侧必须串行：ClientWebSocket 不支持并发 Send。
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // 入站消息处理并发闸（D1 修复）：接收泵只入队，处理放独立 worker，最多同时处理 4 条，
    // 避免单条 AI 回复慢阻塞接收泵（收不到心跳/新消息/被踢事件）。
    private const int ProcessingConcurrency = 4;
    private readonly SemaphoreSlim _procGate = new(ProcessingConcurrency, ProcessingConcurrency);

    private ClientWebSocket? _ws;
    private DateTime _lastRecv = DateTime.Now;
    private bool _kicked; // 收到 disconnected_event：本连接被新实例抢占，停止重连避免互踢

    /// <summary>当前连接状态快照（供配置页展示）。</summary>
    public ImConnectionStatus Status { get; } = new();

    public WeComAiBotClient(
        Func<(string BotId, string Secret)> credProvider,
        Func<InboundMessage, CancellationToken, Task> onMessage,
        string? endpoint = null)
    {
        _credProvider = credProvider;
        _onMessage = onMessage;
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
    }

    private static void SetStatus(ImConnectionStatus s, ImConnectionState state, string? msg)
    {
        s.State = state;
        s.Message = msg;
        if (state == ImConnectionState.Connected) s.ConnectedAt = DateTime.Now;
    }

    /// <summary>
    /// 运行长连接主循环：建连 → 订阅 → 收帧 + 心跳 → 断线按指数退避重连。
    /// 收到 disconnected_event（被同机器人新连接踢下线）后主动退出，不再重连。
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(3);   // 首次重连间隔
        var maxDelay = TimeSpan.FromSeconds(30); // 退避上限

        while (!ct.IsCancellationRequested && !_kicked)
        {
            try
            {
                await ConnectAndPumpAsync(ct).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(3); // 正常断开（未抛异常）后重置退避
            }
            catch (OperationCanceledException)
            {
                break; // 宿主停止
            }
            catch (Exception ex)
            {
                SetStatus(Status, ImConnectionState.Failed, $"连接异常：{ex.Message}");
                XTrace.Log.Error("[企微WS] 连接异常，{0}s 后重连：{1}", (int)delay.TotalSeconds, ex.Message);
            }
            finally
            {
                await CloseQuietlyAsync().ConfigureAwait(false);
            }

            if (_kicked)
            {
                SetStatus(Status, ImConnectionState.Kicked, "该机器人的长连接已被其它实例抢占，已停止重连");
                XTrace.Log.Warn("[企微WS] 收到 disconnected_event，停止重连（避免多实例互踢）");
                break;
            }

            if (ct.IsCancellationRequested) break;

            SetStatus(Status, ImConnectionState.Failed, $"连接断开，{(int)delay.TotalSeconds}s 后重连");
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            // 指数退避，封顶 30s
            delay = delay >= maxDelay ? maxDelay : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
        }

        if (Status.State != ImConnectionState.Kicked)
            SetStatus(Status, ImConnectionState.Disconnected, "已停止");
    }

    /// <summary>建连 + 订阅 + 收帧循环 + 心跳（一次连接生命周期）。</summary>
    private async Task ConnectAndPumpAsync(CancellationToken ct)
    {
        var (botId, secret) = _credProvider();
        if (string.IsNullOrWhiteSpace(botId) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("BotId 或 Secret 缺失，无法建立长连接");

        SetStatus(Status, ImConnectionState.Connecting, "正在建立长连接");
        _lastRecv = DateTime.Now;

        var ws = new ClientWebSocket();
        _ws = ws;
        await ws.ConnectAsync(new Uri(_endpoint), ct).ConfigureAwait(false);
        XTrace.Log.Info("[企微WS] WebSocket 已连接，开始订阅 botId={0}", botId);

        // 订阅（身份校验）。有频率保护，订阅成功后不得反复请求。
        var subOk = await SubscribeAsync(ws, botId, secret, ct).ConfigureAwait(false);
        if (!subOk)
        {
            SetStatus(Status, ImConnectionState.Failed, "订阅失败：BotID 或 Secret 不正确（errcode=853000 类错误）");
            throw new InvalidOperationException("企微长连接订阅失败");
        }

        SetStatus(Status, ImConnectionState.Connected, null);
        XTrace.Log.Info("[企微WS] 订阅成功，长连接已就绪");

        // 心跳：官方建议 30s；判定失效用「多久没收到任何下行帧」而不是等 pong 帧，
        // 因为官方未承诺 pong 的命令名，任何下行帧都证明链路活着（更宽容、不易误判断线）。
        using var heartbeat = new Timer(_ => _ = SendPingAsync(CancellationToken.None), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // D1：消息帧入队异步处理（独立 worker，最多 4 并发），接收泵只做入队；
        // 事件帧（含被踢判定）必须同步及时处理，不进队列。
        var queue = Channel.CreateBounded<string>(new BoundedChannelOptions(512)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var consumeTask = ConsumeMessagesAsync(queue.Reader, ct);

        var buffer = new byte[64 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested && !_kicked)
            {
                if (DateTime.Now - _lastRecv > TimeSpan.FromSeconds(95))
                {
                    // 连续 3 个心跳周期无任何下行 → 判定链路失效，交给外层重连
                    throw new InvalidOperationException("心跳超时：95s 未收到任何下行数据");
                }

                // 接收（处理分片：累积到 EndOfMessage 才是完整一帧）
                string frameText;
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult res;
                    do
                    {
                        res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            XTrace.Log.Warn("[企微WS] 服务端主动关闭：{0} {1}", res.CloseStatus, res.CloseStatusDescription);
                            return;
                        }
                        ms.Write(buffer, 0, res.Count);
                    } while (!res.EndOfMessage);

                    frameText = Encoding.UTF8.GetString(ms.ToArray());
                }

                _lastRecv = DateTime.Now;
                if (IsMessageFrame(frameText))
                {
                    // 消息处理放 worker，接收泵不阻塞
                    await queue.Writer.WriteAsync(frameText, ct).ConfigureAwait(false);
                }
                else
                {
                    // 事件/心跳/未知帧同步轻量处理（disconnected_event 判定必须及时）
                    await HandleFrameAsync(frameText, ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            queue.Writer.TryComplete();
            try { await consumeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { XTrace.Log.Debug("[企微WS] 消息消费任务退出：{0}", ex.Message); }
        }
    }

    /// <summary>判断是否为业务消息帧（aibot_msg_callback）——只有它需要入队异步处理。</summary>
    private static bool IsMessageFrame(string text)
        => text.Contains("\"aibot_msg_callback\"", StringComparison.Ordinal);

    /// <summary>独立消费 worker：读队列，最多 <see cref="ProcessingConcurrency"/> 并发处理入站消息。</summary>
    private async Task ConsumeMessagesAsync(ChannelReader<string> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var text in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await _procGate.WaitAsync(ct).ConfigureAwait(false);
                _ = ProcessMessageFrameAsync(text, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>处理一条入站消息帧（并发扇出任务；任何异常都不得影响其它消息/长连接）。</summary>
    private async Task ProcessMessageFrameAsync(string text, CancellationToken ct)
    {
        try
        {
            await HandleFrameAsync(text, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[企微WS] 异步处理消息帧异常：{0}", ex.Message);
        }
        finally
        {
            _procGate.Release();
        }
    }

    /// <summary>发送订阅帧并校验结果。</summary>
    private async Task<bool> SubscribeAsync(ClientWebSocket ws, string botId, string secret, CancellationToken ct)
    {
        // 订阅帧（身份校验）
        await SendRawAsync(ws, WeComFrameParser.BuildSubscribeFrame(botId, secret), ct).ConfigureAwait(false);

        // 等订阅响应（服务端也可能先推业务帧，故循环过滤直到拿到 errcode）
        var deadline = DateTime.Now.AddSeconds(10);
        var buffer = new byte[64 * 1024];
        while (DateTime.Now < deadline && ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            string text;
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult res;
                do
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                    res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token).ConfigureAwait(false);
                    if (res.MessageType == WebSocketMessageType.Close) return false;
                    ms.Write(buffer, 0, res.Count);
                } while (!res.EndOfMessage);
                text = Encoding.UTF8.GetString(ms.ToArray());
            }

            _lastRecv = DateTime.Now;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("errcode", out var ec))
            {
                // 没有 errcode：可能是业务帧（理论上订阅前不会），继续等
                continue;
            }
            var code = ec.GetInt32();
            var errmsg = root.TryGetProperty("errmsg", out var em) ? em.GetString() : null;
            if (code == 0)
            {
                XTrace.Log.Info("[企微WS] 订阅成功");
                return true;
            }
            XTrace.Log.Error("[企微WS] 订阅失败 errcode={0} errmsg={1}", code, errmsg);
            SetStatus(Status, ImConnectionState.Failed, $"订阅失败 errcode={code} {errmsg}");
            return false;
        }
        return false;
    }

    /// <summary>处理一帧下行数据。</summary>
    private async Task HandleFrameAsync(string text, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var cmd = root.TryGetProperty("cmd", out var c) ? c.GetString() : null;

        switch (cmd)
        {
            case "pong":
                return; // 心跳应答，无需处理（_lastRecv 已更新）

            case "aibot_msg_callback":
                await HandleMessageAsync(root, text, ct).ConfigureAwait(false);
                return;

            case "aibot_event_callback":
                HandleEvent(root);
                return;

            default:
                // 未知帧（含订阅响应之外的 ACK 等）只记 debug，避免噪音
                XTrace.Log.Debug("[企微WS] 忽略未知帧 cmd={0}", cmd);
                return;
        }
    }

    /// <summary>解析消息回调并交给网关路由（协议细节在 <see cref="WeComFrameParser"/>）。</summary>
    private async Task HandleMessageAsync(JsonElement root, string rawText, CancellationToken ct)
    {
        var inbound = WeComFrameParser.ParseMessage(root, rawText);
        if (inbound == null) return;
        try
        {
            await _onMessage(inbound, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 单条消息处理失败绝不能拖垮长连接
            XTrace.Log.Error("[企微WS] 处理入站消息异常：{0}", ex.Message);
        }
    }

    /// <summary>处理事件回调（进入会话/卡片/反馈/被踢下线）。</summary>
    private void HandleEvent(JsonElement root)
    {
        var eventType = WeComFrameParser.ParseEventType(root);
        XTrace.Log.Info("[企微WS] 收到事件 eventtype={0}", eventType);

        // 同一机器人只允许一个长连接：被新连接抢占后必须停止重连，否则两个实例会互相踢。
        if (eventType == "disconnected_event") _kicked = true;
    }

    /// <summary>
    /// 被动回复一条消息（aibot_respond_msg）。
    /// 长连接下普通文本回复也必须用 msgtype=stream（官方 Node SDK 对齐），finish=true 表示结束流式。
    /// </summary>
    public async Task<bool> ReplyAsync(string reqId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(reqId)) return false;
        var json = WeComFrameParser.BuildRespondFrame(reqId, content);
        return await SendFrameAsync(json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 主动推送消息（aibot_send_msg）：无回调触发时使用（定时提醒/告警等）。
    /// chatType：1=单聊（chatId 传 userid），2=群聊（chatId 传 chatid）。缺省服务端按群聊解析，单聊会发不出去。
    /// </summary>
    public async Task<bool> SendAsync(string chatId, int chatType, string content, CancellationToken ct = default)
    {
        var json = WeComFrameParser.BuildSendFrame(chatId, chatType, content);
        return await SendFrameAsync(json, ct).ConfigureAwait(false);
    }

    /// <summary>发送心跳 ping 命令帧（官方：开发者需定期发送 ping 保活）。</summary>
    private async Task SendPingAsync(CancellationToken ct)
    {
        await SendFrameAsync(WeComFrameParser.BuildPingFrame(), ct).ConfigureAwait(false);
    }

    /// <summary>串行发送一帧（带当前 socket 引用）。</summary>
    private async Task<bool> SendFrameAsync(string json, CancellationToken ct)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open) return false;
        try
        {
            await SendRawAsync(ws, json, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[企微WS] 发送帧失败：{0}", ex.Message);
            return false;
        }
    }

    private async Task SendRawAsync(ClientWebSocket ws, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task CloseQuietlyAsync()
    {
        var ws = _ws;
        _ws = null;
        if (ws == null) return;
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
        }
        catch { /* 关闭失败无需抛出，外层会重连 */ }
        finally
        {
            ws.Dispose();
        }
    }

    /// <summary>
    /// 复位「被踢」标志并强制重建连接（D2：用户手动重连）。
    /// 场景：本连接因被同机器人新实例抢占而停止重连（_kicked=true），用户确认不再有别的实例
    /// 后点「重连」，本端重新抢占长连接。
    /// </summary>
    public void ResetKicked() => _kicked = false;

    /// <summary>生成 req_id / stream id（本地唯一即可，用于关联请求与响应）。</summary>
    public static string NewReqId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}

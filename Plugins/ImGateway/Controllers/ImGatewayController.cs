using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.ImGateway.Controllers;

/// <summary>
/// IM 网关控制器：配置读写 + 长连接状态查询 + 扫码授权 + 重连控制 + 会话/消息查询（v2.1.0）。
/// v2.0.0 起仅企微「智能机器人」长连接形态，回调形态已整体移除，不再有 /{channel}/callback 路由。
/// v2.1.0（PILOT-078）：类级补 [Authorize("ApiKeyPolicy")]（铁律 17，宿主无全局鉴权中间件，
/// 插件控制器经子 provider 注册进 Kestrel 不会自动被保护）；新增会话列表与消息线程只读查询端点。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/im-gateway")]
public class ImGatewayController : ControllerBase
{
    private readonly IEnumerable<IImChannel> _channels;
    private readonly ImGatewayRouter _router;
    private readonly IConfigStore _configStore;
    private readonly ImGatewayConnectionManager _connMgr;
    private readonly WeComScanAuthService _scanAuth;

    public ImGatewayController(IEnumerable<IImChannel> channels, ImGatewayRouter router,
        IConfigStore configStore, ImGatewayConnectionManager connMgr, WeComScanAuthService scanAuth)
    {
        _channels = channels;
        _router = router;
        _configStore = configStore;
        _connMgr = connMgr;
        _scanAuth = scanAuth;
    }

    /// <summary>统一 camelCase 序列化（对齐前端 DTO 约定；否则默认 PascalCase 会让前端读不到 channelType/type 等字段）。</summary>
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>读取当前配置（含已保存的通道凭证）。</summary>
    [HttpGet("config")]
    // 禁止客户端缓存：避免浏览器对 GET 做启发式缓存导致配置页读到陈旧数据（已发生 e2e reload 命中早期空响应）。
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetConfig() => new JsonResult(_configStore.Load(), JsonOpts);

    /// <summary>保存配置（前端配置页提交）。</summary>
    [HttpPost("config")]
    public IActionResult SaveConfig([FromBody] ImGatewayConfig config)
    {
        if (config == null) return BadRequest(new { error = "配置为空" });
        _configStore.Save(config);
        // 立即按新配置校正长连接（启用/禁用/改密钥都会即时生效，无需重启宿主）
        _connMgr.Apply();
        return new JsonResult(new { ok = true }, JsonOpts);
    }

    /// <summary>通道状态（是否启用 + 显示名 + 传输形态 + 长连接状态），供配置页展示。</summary>
    [HttpGet("status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Status()
    {
        var list = _channels.Select(c =>
        {
            var st = c.Status;
            return new
            {
                type = c.ChannelType,
                name = c.DisplayName,
                enabled = c.IsEnabled,
                // callback=回调形态；websocket=长连接形态（前端据此决定是否展示连接状态灯）
                transport = c.TransportMode.ToString().ToLowerInvariant(),
                connection = new
                {
                    state = st.State.ToString().ToLowerInvariant(),
                    message = st.Message,
                    connectedAt = st.ConnectedAt,
                },
            };
        });
        return new JsonResult(list, JsonOpts);
    }

    /// <summary>
    /// 手动重连企微长连接（D2）。场景：本连接被其它实例抢占后已停止重连（kicked），
    /// 用户确认不再有别的实例后点「重连」重新抢占；也可用于不想等自动退避时立即重连。
    /// </summary>
    [HttpPost("wecom/reconnect")]
    public IActionResult ReconnectWeCom()
    {
        var ok = _connMgr.Reconnect(ImChannelTypes.WeCom);
        return ok
            ? new JsonResult(new { ok = true }, JsonOpts)
            : StatusCode(StatusCodes.Status409Conflict, new { error = "企微通道未运行或非长连接形态，无法重连" });
    }

    /// <summary>
    /// 发起扫码授权（方案 A）：拉起企微 CLI（隔离目录）返回扫码链接/二维码，
    /// 用户手机扫码确认后服务端自动解密凭据、回填 BotId+Secret 并重建长连接（无需前端提交）。
    /// </summary>
    [HttpPost("wecom/scan-auth")]
    public IActionResult StartScanAuth()
    {
        try
        {
            var session = _scanAuth.Start();
            return new JsonResult(new
            {
                id = session.Id,
                state = session.State.ToString().ToLowerInvariant(),
                qrLink = session.QrLink,
                qrText = session.QrText,
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { error = ex.Message });
        }
    }

    /// <summary>轮询扫码授权状态（前端每 2s 拉一次；成功后凭据已自动保存，不回传 Secret）。</summary>
    [HttpGet("wecom/scan-auth/{id}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetScanAuth(string id)
    {
        var session = _scanAuth.Get(id);
        if (session == null) return NotFound(new { error = "扫码会话不存在或已过期" });
        return new JsonResult(new
        {
            id = session.Id,
            state = session.State.ToString().ToLowerInvariant(),
            qrLink = session.QrLink,
            qrText = session.QrText,
            botId = session.BotId,
            error = session.Error,
        }, JsonOpts);
    }

    /// <summary>
    /// 联系人列表（PILOT-078 FR-2，只读）：ImGatewaySession 每行 = 1 个联系人。
    /// displayName = ChannelKey 末段（PlatformUserId）；lastMessage 为最新一条（Content 截 60 字符预览），
    /// 零消息时为 null（历史库升级后「映射存在但零消息」形态，前端显示「暂无消息记录」）；
    /// lastActiveAt = 最后消息时间，无消息用会话 UpdatedAt。排序：有消息按最后消息时间倒序在前，无消息按 UpdatedAt 倒序排后。
    /// </summary>
    [HttpGet("conversations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Conversations()
    {
        // 唯一性/列表判断直查数据库，不复用实体缓存（plugin-development 铁律 11）
        var sessions = ImGatewaySession.FindAll();
        var rows = new List<(ImGatewaySession Session, ImGatewayMessage? Latest, long Count)>(sessions.Count);
        foreach (var s in sessions)
        {
            var latest = ImGatewayMessage.FindLatestByChannelKey(s.ChannelKey);
            var count = ImGatewayMessage.FindCount(ImGatewayMessage._.ChannelKey == s.ChannelKey);
            rows.Add((s, latest, count));
        }

        var payload = rows
            .OrderByDescending(r => r.Latest != null)
            .ThenByDescending(r => r.Latest?.CreatedAt ?? r.Session.UpdatedAt)
            .Select(r => new
            {
                channelKey = r.Session.ChannelKey,
                channelType = ImChannelKeys.ChannelTypeOf(r.Session.ChannelKey),
                displayName = ImChannelKeys.DisplayNameOf(r.Session.ChannelKey),
                sessionId = r.Session.SessionId,
                lastMessage = r.Latest == null
                    ? null
                    : new
                    {
                        direction = r.Latest.Direction,
                        status = r.Latest.Status,
                        preview = Truncate(r.Latest.Content, 60),
                        createdAt = r.Latest.CreatedAt,
                    },
                lastActiveAt = r.Latest?.CreatedAt ?? r.Session.UpdatedAt,
                messageCount = r.Count,
            });
        return new JsonResult(payload, JsonOpts);
    }

    /// <summary>
    /// 消息线程（PILOT-078 FR-3，只读）：指定联系人的消息，取 beforeId（不含）之前的最新 limit 条，
    /// 按 Id 升序输出（便于直接渲染）+ hasMore（是否还有更早）。limit 钳制 1..200，缺省 50；
    /// channelKey 走 query 参数（避开路由编码），缺省 400；beforeId 不存在/越界 → 空数组 + hasMore=false（不报错）。
    /// </summary>
    [HttpGet("messages")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Messages([FromQuery] string channelKey, [FromQuery] int limit = 50, [FromQuery] long beforeId = 0)
    {
        if (string.IsNullOrWhiteSpace(channelKey)) return BadRequest(new { error = "channelKey 不能为空" });
        if (limit < 1) limit = 1;
        if (limit > ImGatewayMessage.MaxPageSize) limit = ImGatewayMessage.MaxPageSize;

        var (rows, hasMore) = ImGatewayMessage.FindPageByChannelKey(channelKey, limit, beforeId);
        var items = rows.Select(m => new
        {
            id = m.Id,
            channelKey = m.ChannelKey,
            channelType = m.ChannelType,
            direction = m.Direction,
            content = m.Content,
            sessionId = m.SessionId,
            platformMsgId = m.PlatformMsgId,
            status = m.Status,
            errorText = m.ErrorText,
            createdAt = m.CreatedAt,
        }).ToList();
        return new JsonResult(new { messages = items, hasMore }, JsonOpts);
    }

    /// <summary>文本截断（null 安全）：preview 截 60 字符等场景。</summary>
    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

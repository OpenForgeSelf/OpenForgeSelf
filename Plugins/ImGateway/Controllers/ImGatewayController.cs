using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.ImGateway.Controllers;

/// <summary>
/// IM 网关控制器：配置读写 + 长连接状态查询 + 扫码授权 + 重连控制。
/// v2.0.0 起仅企微「智能机器人」长连接形态，回调形态已整体移除，不再有 /{channel}/callback 路由。
/// </summary>
[ApiController]
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
}

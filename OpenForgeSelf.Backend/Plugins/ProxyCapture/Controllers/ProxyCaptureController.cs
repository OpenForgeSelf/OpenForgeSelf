using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewLife.Log;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Models;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Controllers;

[ApiController]
[Route("api/capture")]
public class ProxyCaptureController : ControllerBase
{
    private ListenerConfigDto ToDto(ListenerConfig c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ListenAddress = c.ListenAddress,
        ListenPort = c.ListenPort,
        TargetHost = c.TargetHost,
        TargetPort = c.TargetPort,
        Enabled = c.Enabled,
        Description = c.Description,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        IsRunning = CaptureEngine.Instance.IsRunning(c.Id)
    };

    [HttpGet("listeners")]
    public async Task<ActionResult<ApiResponse<List<ListenerConfigDto>>>> GetListeners()
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var list = await db.ListenerConfigs.OrderBy(x => x.Id).ToListAsync();
            return Ok(ApiResponse<List<ListenerConfigDto>>.Ok(list.Select(ToDto).ToList()));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 获取监听器列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ListenerConfigDto>>.Error(ex.Message));
        }
    }

    [HttpPost("listeners")]
    public async Task<ActionResult<ApiResponse<ListenerConfigDto>>> CreateListener([FromBody] CreateListenerRequest req)
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(ApiResponse<ListenerConfigDto>.Error("名称不能为空", 400));
            if (req.ListenPort <= 0 || req.ListenPort > 65535)
                return BadRequest(ApiResponse<ListenerConfigDto>.Error("监听端口不合法", 400));
            if (!string.IsNullOrWhiteSpace(req.TargetHost) && (!req.TargetPort.HasValue || req.TargetPort <= 0))
                return BadRequest(ApiResponse<ListenerConfigDto>.Error("配置了目标主机时必须填写目标端口", 400));

            var cfg = new ListenerConfig
            {
                Name = req.Name,
                ListenAddress = req.ListenAddress ?? "0.0.0.0",
                ListenPort = req.ListenPort,
                TargetHost = req.TargetHost,
                TargetPort = req.TargetPort,
                Enabled = req.Enabled,
                Description = req.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            using (var db = ProxyCaptureDbContext.Create())
            {
                db.ListenerConfigs.Add(cfg);
                await db.SaveChangesAsync();
            }

            if (cfg.Enabled) CaptureEngine.Instance.StartListener(cfg);
            return StatusCode(201, ApiResponse<ListenerConfigDto>.Ok(ToDto(cfg), "创建成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 创建监听器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ListenerConfigDto>.Error(ex.Message));
        }
    }

    [HttpPut("listeners/{id}")]
    public async Task<ActionResult<ApiResponse<ListenerConfigDto>>> UpdateListener(int id, [FromBody] UpdateListenerRequest req)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var cfg = await db.ListenerConfigs.FindAsync(id);
            if (cfg == null) return NotFound(ApiResponse<ListenerConfigDto>.Error("监听器不存在", 404));
            if (!string.IsNullOrWhiteSpace(req.TargetHost) && (!req.TargetPort.HasValue || req.TargetPort <= 0))
                return BadRequest(ApiResponse<ListenerConfigDto>.Error("配置了目标主机时必须填写目标端口", 400));

            cfg.Name = req.Name;
            cfg.ListenAddress = req.ListenAddress ?? "0.0.0.0";
            cfg.ListenPort = req.ListenPort;
            cfg.TargetHost = req.TargetHost;
            cfg.TargetPort = req.TargetPort;
            cfg.Enabled = req.Enabled;
            cfg.Description = req.Description;
            cfg.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // 重启监听以套用变更
            CaptureEngine.Instance.StopListener(id);
            if (cfg.Enabled) CaptureEngine.Instance.StartListener(cfg);

            return Ok(ApiResponse<ListenerConfigDto>.Ok(ToDto(cfg), "更新成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 更新监听器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ListenerConfigDto>.Error(ex.Message));
        }
    }

    [HttpDelete("listeners/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteListener(int id)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var cfg = await db.ListenerConfigs.FindAsync(id);
            if (cfg == null) return NotFound(ApiResponse.Error("监听器不存在", 404));

            CaptureEngine.Instance.StopListener(id);
            db.ListenerConfigs.Remove(cfg);
            await db.SaveChangesAsync();
            return Ok(ApiResponse.Ok("删除成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 删除监听器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse.Error(ex.Message));
        }
    }

    [HttpPost("listeners/{id}/start")]
    public ActionResult<ApiResponse> StartListener(int id)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var cfg = db.ListenerConfigs.Find(id);
            if (cfg == null) return NotFound(ApiResponse.Error("监听器不存在", 404));

            cfg.Enabled = true;
            cfg.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            CaptureEngine.Instance.StartListener(cfg);
            return Ok(ApiResponse.Ok("已启动"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 启动监听器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse.Error(ex.Message));
        }
    }

    [HttpPost("listeners/{id}/stop")]
    public ActionResult<ApiResponse> StopListener(int id)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var cfg = db.ListenerConfigs.Find(id);
            if (cfg == null) return NotFound(ApiResponse.Error("监听器不存在", 404));

            cfg.Enabled = false;
            cfg.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            CaptureEngine.Instance.StopListener(id);
            return Ok(ApiResponse.Ok("已停止"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 停止监听器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse.Error(ex.Message));
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<PagedResult<CaptureSessionSummaryDto>>>> GetSessions(
        [FromQuery] int? listenerId = null,
        [FromQuery] string? protocol = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            using var db = ProxyCaptureDbContext.Create();
            var q = db.CaptureSessions.AsQueryable();
            if (listenerId.HasValue) q = q.Where(s => s.ListenerId == listenerId.Value);
            if (!string.IsNullOrWhiteSpace(protocol)) q = q.Where(s => s.Protocol == protocol);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new CaptureSessionSummaryDto
                {
                    Id = s.Id,
                    ListenerId = s.ListenerId,
                    Timestamp = s.Timestamp,
                    Protocol = s.Protocol,
                    ClientIp = s.ClientIp,
                    Target = s.Target,
                    Method = s.Method,
                    Url = s.Url,
                    StatusCode = s.StatusCode,
                    RequestBytes = s.RequestBytes,
                    ResponseBytes = s.ResponseBytes,
                    DurationMs = s.DurationMs,
                    Forwarded = s.Forwarded
                })
                .ToListAsync();

            return Ok(ApiResponse<PagedResult<CaptureSessionSummaryDto>>.Ok(new PagedResult<CaptureSessionSummaryDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 获取抓包记录失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PagedResult<CaptureSessionSummaryDto>>.Error(ex.Message));
        }
    }

    [HttpGet("sessions/{id}")]
    public async Task<ActionResult<ApiResponse<CaptureSessionDetailDto>>> GetSession(long id)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var s = await db.CaptureSessions.FindAsync(id);
            if (s == null) return NotFound(ApiResponse<CaptureSessionDetailDto>.Error("记录不存在", 404));

            var dto = new CaptureSessionDetailDto
            {
                Id = s.Id,
                ListenerId = s.ListenerId,
                Timestamp = s.Timestamp,
                Protocol = s.Protocol,
                ClientIp = s.ClientIp,
                LocalEndpoint = s.LocalEndpoint,
                Target = s.Target,
                Method = s.Method,
                Url = s.Url,
                HttpVersion = s.HttpVersion,
                RequestHeaders = s.RequestHeaders,
                RequestBody = s.RequestBody,
                StatusCode = s.StatusCode,
                ResponseHeaders = s.ResponseHeaders,
                ResponseBody = s.ResponseBody,
                RequestBytes = s.RequestBytes,
                ResponseBytes = s.ResponseBytes,
                DurationMs = s.DurationMs,
                Forwarded = s.Forwarded,
                RawPreview = s.RawPreview,
                ErrorMessage = s.ErrorMessage
            };
            return Ok(ApiResponse<CaptureSessionDetailDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<CaptureSessionDetailDto>.Error(ex.Message));
        }
    }

    [HttpDelete("sessions")]
    public async Task<ActionResult<ApiResponse>> ClearSessions([FromQuery] int? listenerId = null)
    {
        try
        {
            using var db = ProxyCaptureDbContext.Create();
            var q = db.CaptureSessions.AsQueryable();
            if (listenerId.HasValue) q = q.Where(s => s.ListenerId == listenerId.Value);
            db.CaptureSessions.RemoveRange(q);
            await db.SaveChangesAsync();
            return Ok(ApiResponse.Ok("已清空抓包记录"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse.Error(ex.Message));
        }
    }

    [HttpGet("ca-cert")]
    public ActionResult<ApiResponse<object>> GetCaCert()
    {
        try
        {
            var path = CaptureEngine.Instance.CaCertificatePem;
            if (!System.IO.File.Exists(path))
                return StatusCode(500, ApiResponse<object>.Error("CA 证书未生成"));

            var pem = "-----BEGIN CERTIFICATE-----\n" +
                      Convert.ToBase64String(System.IO.File.ReadAllBytes(path), Base64FormattingOptions.InsertLineBreaks) +
                      "\n-----END CERTIFICATE-----";
            return Ok(ApiResponse<object>.Ok(new
            {
                pem,
                installHint = "将根证书安装到系统/浏览器的「受信任的根证书颁发机构」后，本插件即可解密并显示 HTTPS 明文。",
                certPath = path
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }
}

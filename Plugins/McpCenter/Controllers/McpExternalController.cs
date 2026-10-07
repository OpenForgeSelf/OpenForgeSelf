using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Controllers;

/// <summary>
/// 外部 MCP 服务器管理（v2.1.0 新增）：外部服务器清单 CRUD + 连接/断开/工具清单/真实握手测试。
/// 配置持久化于 {插件数据根}/external-servers.json；GET 返回脱敏视图（headers/env 值掩码）。
/// 路由前缀 api/mcp-center/servers 与既有 api/mcp、api/mcp-center/config 无冲突。
/// 管理面鉴权（验收标准）：类级 [Authorize("ApiKeyPolicy")]，未带宿主令牌一律 401。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/mcp-center/servers")]
public class McpExternalController : ControllerBase
{
    private readonly ExternalServersStore _store;
    private readonly McpClientManager _manager;

    public McpExternalController(ExternalServersStore store, McpClientManager manager)
    {
        _store = store;
        _manager = manager;
    }

    /// <summary>外部服务器列表（含连接状态/工具数/最近错误，配置脱敏）。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<McpExternalServerStateDto>>> Get()
    {
        return Ok(ApiResponse<List<McpExternalServerStateDto>>.Ok(_manager.GetStates(), "获取外部服务器列表成功"));
    }

    /// <summary>新增外部服务器：校验 → 持久化 → 若 enabled 自动建连（连接失败不阻塞保存，LastError 说明）。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<McpExternalServerStateDto>>> Post([FromBody] McpExternalServerUpsertDto dto)
    {
        try
        {
            var config = FromUpsert(dto, existing: null);
            var configs = _store.Load();
            if (configs.Any(c => c.Id == config.Id))
                return StatusCode(400, ApiResponse<McpExternalServerStateDto>.Error($"外部服务器 '{config.Id}' 已存在", 400));

            configs.Add(config);
            _store.Save(configs);
            XTrace.Log.Info("[McpCenter] 新增外部服务器: {0}（{1}）", config.Name, config.Transport);

            if (config.Enabled)
            {
                try { await _manager.TryConnectAsync(config.Id); }
                catch (Exception ex) { XTrace.Log.Warn("[McpCenter] 新增后建连失败（可稍后手动连接）: {0}", ex.Message); }
            }
            return Ok(ApiResponse<McpExternalServerStateDto>.Ok(GetState(config.Id), "外部服务器已新增"));
        }
        catch (McpClientException ex)
        {
            return StatusCode(400, ApiResponse<McpExternalServerStateDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("新增外部服务器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<McpExternalServerStateDto>.Error("新增外部服务器失败: " + ex.Message));
        }
    }

    /// <summary>更新外部服务器：校验 → 持久化 → 若 enabled 重连。</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<McpExternalServerStateDto>>> Put(string id, [FromBody] McpExternalServerUpsertDto dto)
    {
        try
        {
            var configs = _store.Load();
            var index = configs.FindIndex(c => c.Id == id);
            if (index < 0)
                return StatusCode(404, ApiResponse<McpExternalServerStateDto>.Error($"外部服务器 '{id}' 不存在", 404));

            var config = FromUpsert(dto, existing: configs[index]);
            configs[index] = config;
            _store.Save(configs);

            await _manager.DisconnectAsync(config.Id);
            if (config.Enabled)
            {
                try { await _manager.TryConnectAsync(config.Id); }
                catch (Exception ex) { XTrace.Log.Warn("[McpCenter] 更新后建连失败（可稍后手动连接）: {0}", ex.Message); }
            }
            return Ok(ApiResponse<McpExternalServerStateDto>.Ok(GetState(config.Id), "外部服务器已更新"));
        }
        catch (McpClientException ex)
        {
            return StatusCode(400, ApiResponse<McpExternalServerStateDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新外部服务器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<McpExternalServerStateDto>.Error("更新外部服务器失败: " + ex.Message));
        }
    }

    /// <summary>删除外部服务器（断开连接 + 移除清单）。</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string id)
    {
        try
        {
            var configs = _store.Load();
            var removed = configs.RemoveAll(c => c.Id == id);
            if (removed == 0)
                return StatusCode(404, ApiResponse<string>.Error($"外部服务器 '{id}' 不存在", 404));
            _store.Save(configs);
            await _manager.DisconnectAsync(id);
            return Ok(ApiResponse<string>.Ok(id, "外部服务器已删除"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.Error("删除外部服务器失败: " + ex.Message));
        }
    }

    /// <summary>连接外部服务器（幂等：已连先断重连）+ initialize 握手 + tools/list 拉取工具清单。</summary>
    [HttpPost("{id}/connect")]
    public async Task<ActionResult<ApiResponse<McpExternalServerStateDto>>> Connect(string id)
    {
        try
        {
            var session = await _manager.TryConnectAsync(id);
            return Ok(ApiResponse<McpExternalServerStateDto>.Ok(GetState(id),
                $"连接成功（{session.ServerInfoName}，协议 {session.ProtocolVersion}，{session.ToolCount} 个工具）"));
        }
        catch (McpClientException ex)
        {
            return StatusCode(400, ApiResponse<McpExternalServerStateDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("连接外部服务器失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<McpExternalServerStateDto>.Error("连接失败: " + ex.Message));
        }
    }

    /// <summary>断开外部服务器连接。</summary>
    [HttpPost("{id}/disconnect")]
    public async Task<ActionResult<ApiResponse<string>>> Disconnect(string id)
    {
        await _manager.DisconnectAsync(id);
        return Ok(ApiResponse<string>.Ok(id, "已断开"));
    }

    /// <summary>已拉取的外部工具清单（含完整转发名 mcp.&lt;id&gt;.&lt;name&gt;）。</summary>
    [HttpGet("{id}/tools")]
    public ActionResult<ApiResponse<List<McpExternalToolDto>>> Tools(string id)
    {
        var tools = _manager.GetTools(id).ToList();
        return Ok(ApiResponse<List<McpExternalToolDto>>.Ok(tools, tools.Count == 0 ? "该服务器未连接或无工具" : "获取工具清单成功"));
    }

    /// <summary>
    /// 工具测试台（v2.3.0）：按工具原生名真实调用该服务器的外部工具（tools/call）。
    /// 返回文本 + isError + 原始 JSON + 耗时；远端声明 isError 时 Ok=false 但 HTTP 仍 200（调用本身成功）。
    /// </summary>
    [HttpPost("{id}/tools/invoke")]
    public async Task<ActionResult<ApiResponse<McpToolInvokeResult>>> InvokeTool(string id, [FromBody] McpToolInvokeRequest request)
    {
        try
        {
            var tool = request?.Tool?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(tool))
                return StatusCode(400, ApiResponse<McpToolInvokeResult>.Error("工具名不能为空", 400));

            var argsJson = string.IsNullOrWhiteSpace(request?.ArgumentsJson) ? "{}" : request!.ArgumentsJson!;
            try
            {
                using var _ = System.Text.Json.JsonDocument.Parse(argsJson);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return StatusCode(400, ApiResponse<McpToolInvokeResult>.Error($"参数 JSON 解析失败: {ex.Message}", 400));
            }

            var known = _manager.GetTools(id);
            if (known.Count > 0 && known.All(t => t.Name != tool))
            {
                var hint = string.Join("、", known.Take(10).Select(t => t.Name));
                return StatusCode(400, ApiResponse<McpToolInvokeResult>.Error(
                    $"工具 '{tool}' 不在该服务器的工具清单中（可用：{hint}）", 400));
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var outcome = await _manager.InvokeToolAsync(id, tool, argsJson, HttpContext.RequestAborted);
            sw.Stop();

            var result = new McpToolInvokeResult
            {
                ServerId = id,
                Tool = tool,
                Ok = !outcome.IsError,
                IsError = outcome.IsError,
                Text = outcome.Text,
                RawJson = outcome.RawJson,
                ElapsedMs = sw.ElapsedMilliseconds
            };
            return Ok(ApiResponse<McpToolInvokeResult>.Ok(result,
                outcome.IsError ? $"工具已调用，但远端返回 isError（{sw.ElapsedMilliseconds}ms）" : $"调用成功（{sw.ElapsedMilliseconds}ms）"));
        }
        catch (McpClientException ex)
        {
            return StatusCode(400, ApiResponse<McpToolInvokeResult>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("调用外部工具失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<McpToolInvokeResult>.Error("调用外部工具失败: " + ex.Message));
        }
    }

    /// <summary>真实握手测试（initialize + ping，非模拟；不建会话、不保存）。</summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<ApiResponse<object>>> Test(string id)
    {
        try
        {
            var (ok, message) = await _manager.TestAsync(id, default);
            return ok
                ? Ok(ApiResponse<object>.Ok(new { id, ok }, message))
                : StatusCode(400, ApiResponse<object>.Error(message, 400));
        }
        catch (Exception ex)
        {
            return StatusCode(400, ApiResponse<object>.Error(ex.Message, 400));
        }
    }

    private McpExternalServerConfig FromUpsert(McpExternalServerUpsertDto dto, McpExternalServerConfig? existing)
    {
        if (dto == null) throw new McpClientException("请求体不能为空");
        var baseConfig = existing ?? new McpExternalServerConfig();
        var merged = new McpExternalServerConfig
        {
            Id = dto.Id ?? baseConfig.Id,
            Name = dto.Name ?? baseConfig.Name,
            Enabled = dto.Enabled ?? baseConfig.Enabled,
            Transport = dto.Transport ?? baseConfig.Transport,
            Url = dto.Url ?? baseConfig.Url,
            Headers = dto.Headers ?? baseConfig.Headers,
            Command = dto.Command ?? baseConfig.Command,
            Args = dto.Args ?? baseConfig.Args,
            Env = dto.Env ?? baseConfig.Env
        };
        return ExternalServersStore.ValidateAndNormalize(merged);
    }

    private McpExternalServerStateDto GetState(string id) =>
        _manager.GetStates().FirstOrDefault(s => s.Id == id)
        ?? new McpExternalServerStateDto { Id = id, LastError = "状态暂不可用" };
}

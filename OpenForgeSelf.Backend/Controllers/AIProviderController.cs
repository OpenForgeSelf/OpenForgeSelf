using System.Net.Http;
using OpenForgeSelf.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Http;
using NewLife.Log;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Security;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>
/// AI 提供方配置管理 API（数据库化）。
/// 任意写操作成功后调用 <see cref="AIProviderRegistry.ReloadAsync"/> 实现"保存即生效、无需重启"。
/// 列表/详情仅返回掩码后的 ApiKey，绝不返回明文。
/// </summary>
[ApiController]
[Route("api/ai-providers")]
[Authorize("ApiKeyPolicy")]
public class AIProviderController : ControllerBase
{
    private readonly IAIProviderService _service;
    private readonly ISecretEncryptionService _encryption;
    private readonly AIProviderRegistry _registry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogService _logService;
    private readonly IAIModelService _modelService;

    public AIProviderController(
        IAIProviderService service,
        ISecretEncryptionService encryption,
        AIProviderRegistry registry,
        IHttpClientFactory httpClientFactory,
        ILogService logService,
        IAIModelService modelService)
    {
        _service = service;
        _encryption = encryption;
        _registry = registry;
        _httpClientFactory = httpClientFactory;
        _logService = logService;
        _modelService = modelService;
    }

    /// <summary>获取全部提供方（ApiKey 仅返回掩码）</summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        try
        {
            var list = _service.GetAll().Select(MapToResponse).ToList();
            return Ok(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logService.Error("获取 AI 提供方列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>获取指定提供方详情（ApiKey 仅返回掩码）</summary>
    [HttpGet("{id}")]
    public IActionResult GetById(long id)
    {
        try
        {
            var entity = _service.GetById(id);
            if (entity == null) return NotFound(new { success = false, message = "Provider not found" });

            return Ok(new { success = true, data = MapToResponse(entity) });
        }
        catch (Exception ex)
        {
            _logService.Error("获取 AI 提供方详情失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>新增提供方；成功后重载网关注册表</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AIProviderRequest request)
    {
        try
        {
            if (request == null) return BadRequest(new { success = false, message = "请求体不能为空" });
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return BadRequest(new { success = false, message = "ApiKey 不能为空" });

            var config = ToConfig(request);
            var saved = _service.Create(config);

            // 写后副作用：重建网关提供方集合，保存即生效
            await _registry.ReloadAsync(_httpClientFactory, _service.GetAllConfigs());

            return CreatedAtAction(nameof(GetById), new { id = saved.Id },
                new { success = true, data = MapToResponse(saved) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logService.Error("新增 AI 提供方失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>更新指定提供方；apiKey 留空或省略则保留原密钥。成功后重载网关注册表</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] AIProviderRequest request)
    {
        try
        {
            if (request == null) return BadRequest(new { success = false, message = "请求体不能为空" });

            var existing = _service.GetById(id);
            if (existing == null) return NotFound(new { success = false, message = "Provider not found" });

            var config = ToConfig(request);
            var saved = _service.Update(id, config);
            if (saved == null) return NotFound(new { success = false, message = "Provider not found" });

            await _registry.ReloadAsync(_httpClientFactory, _service.GetAllConfigs());

            return Ok(new { success = true, data = MapToResponse(saved) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logService.Error("更新 AI 提供方失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>删除指定提供方；成功后重载网关注册表</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var existing = _service.GetById(id);
            if (existing == null) return NotFound(new { success = false, message = "Provider not found" });

            _service.Delete(id);

            await _registry.ReloadAsync(_httpClientFactory, _service.GetAllConfigs());

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logService.Error("删除 AI 提供方失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>测试指定提供方连通性（解密 ApiKey 后发一次最小探测请求）</summary>
    [HttpPost("{id}/test")]
    public async Task<IActionResult> TestConnection(long id)
    {
        try
        {
            var result = await _service.TestConnectionAsync(id);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logService.Error("测试 AI 提供方连接失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 对指定提供方触发上游模型列表拉取：解密 ApiKey → 请求上游 /models → 按 (供应商, 模型名) upsert 持久化。
    /// 上游鉴权失败(401)/超时/错误时返回错误且不写入（保留既有列表）；供应商不存在返回 404。
    /// </summary>
    [HttpPost("{id}/fetch-models")]
    public async Task<IActionResult> FetchModels(long id)
    {
        try
        {
            var result = await _modelService.FetchForProviderAsync(id);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { success = false, message = "上游模型接口请求超时" });
        }
        catch (HttpRequestException ex)
        {
            var status = (int?)ex.StatusCode;
            var msg = status switch
            {
                401 or 403 => "上游鉴权失败(401)",
                _ => $"上游请求失败：{ex.Message}"
            };
            return StatusCode(status == 401 || status == 403 ? 502 : 504, new { success = false, message = msg });
        }
        catch (Exception ex)
        {
            _logService.Error("拉取供应商模型失败: {0}", ex.Message);
            return StatusCode(502, new { success = false, message = $"拉取失败：{ex.Message}" });
        }
    }

    #region 映射

    private AIProviderResponse MapToResponse(AIProvider e)
    {
        return new AIProviderResponse(
            Id: e.Id,
            Name: e.Name,
            ProviderType: e.ProviderType,
            Endpoint: e.Endpoint,
            ApiKeyMasked: _encryption.Mask(_encryption.Decrypt(e.ApiKey)),
            SupportedModels: string.IsNullOrEmpty(e.SupportedModels)
                ? new List<string>()
                : e.SupportedModels
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
            IsDefault: e.IsDefault,
            TimeoutSeconds: e.TimeoutSeconds,
            VisionModel: string.IsNullOrEmpty(e.VisionModel) ? null : e.VisionModel,
            EnableMultimodal: e.EnableMultimodal,
            VisionPromptTemplate: string.IsNullOrEmpty(e.VisionPromptTemplate) ? null : e.VisionPromptTemplate,
            CreateTime: e.CreateTime,
            UpdateTime: e.UpdateTime);
    }

    private static AIProviderConfig ToConfig(AIProviderRequest request)
    {
        return new AIProviderConfig
        {
            Name = request.Name,
            ProviderType = Enum.TryParse<AIProviderType>(request.ProviderType, true, out var t)
                ? t
                : AIProviderType.OpenAI,
            Endpoint = request.Endpoint,
            ApiKey = request.ApiKey ?? string.Empty,
            SupportedModels = request.SupportedModels ?? new List<string>(),
            IsDefault = request.IsDefault,
            TimeoutSeconds = request.TimeoutSeconds,
            VisionModel = request.VisionModel,
            EnableMultimodal = request.EnableMultimodal,
            VisionPromptTemplate = request.VisionPromptTemplate
        };
    }

    #endregion
}

/// <summary>AI 提供方响应（ApiKey 仅返回掩码）</summary>
public record AIProviderResponse(
    long Id,
    string Name,
    string ProviderType,
    string Endpoint,
    string ApiKeyMasked,
    List<string> SupportedModels,
    bool IsDefault,
    int TimeoutSeconds,
    string? VisionModel,
    bool EnableMultimodal,
    string? VisionPromptTemplate,
    DateTime CreateTime,
    DateTime UpdateTime);

/// <summary>AI 提供方请求。apiKey 为明文入参；编辑时留空或省略 = 保留原密钥</summary>
public record AIProviderRequest(
    string Name,
    string ProviderType,
    string Endpoint,
    string? ApiKey,
    List<string>? SupportedModels,
    bool IsDefault,
    int TimeoutSeconds,
    string? VisionModel,
    bool EnableMultimodal,
    string? VisionPromptTemplate);

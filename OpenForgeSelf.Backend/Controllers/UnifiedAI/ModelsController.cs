using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenForgeSelf.Backend.Services.AI;

namespace OpenForgeSelf.Backend.Controllers.UnifiedAI;

/// <summary>
/// 对齐 OpenAI 规范的模型列表接口（/v1/models）。
/// 聚合所有已注册 AI 提供方的模型，owned_by 填提供方名。
/// </summary>
[ApiController]
[Route("v1")]
[Authorize("ApiKeyPolicy")]
public class ModelsController : ControllerBase
{
    private readonly AIProviderRegistry _registry;

    public ModelsController(AIProviderRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 获取全部模型列表（GET /v1/models）
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> ListModels(CancellationToken cancellationToken)
    {
        var allModels = await _registry.GetAllModelsAsync(cancellationToken);
        var response = new
        {
            @object = "list",
            data = allModels.Select(m => new
            {
                id = m.Id,
                @object = "model",
                created = m.Created,
                owned_by = m.Owner
            }).ToList()
        };
        return Ok(response);
    }

    /// <summary>
    /// 获取单个模型信息（GET /v1/models/{model}）
    /// </summary>
    [HttpGet("models/{model}")]
    public IActionResult GetModel(string model)
    {
        var modelInfo = _registry.GetModelById(model);
        if (modelInfo == null)
            return NotFound(new { error = new { message = $"Model '{model}' not found", type = "not_found" } });

        return Ok(new
        {
            id = modelInfo.Id,
            @object = "model",
            created = modelInfo.Created,
            owned_by = modelInfo.Owner
        });
    }
}

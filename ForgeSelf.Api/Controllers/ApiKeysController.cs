using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// API 子密钥管理面（<c>api/api-keys</c>）。
/// 全部端点均需 <c>ApiKeyPolicy</c> 鉴权（主密钥或任一有效子密钥均可调用）。
/// </summary>
/// <remarks>
/// 响应外壳沿用 <see cref="ApiServerController"/>：成功 <c>{ success: true, data: … }</c>，
/// 业务失败 <c>{ success: false, error: "…" }</c>（HTTP 200）；鉴权失败由策略返回 401；
/// 库异常等未预期故障统一返回 HTTP 500 且 body 仍是 <c>{ success: false, error }</c> 外壳，
/// 绝不把原始异常堆栈透出。
/// <b>明文红线</b>：列表永不返回明文；创建 / 轮换的明文只出现在当次响应体；
/// 日志只记录 <c>ex.Message</c>，绝不记录请求体或明文。
/// </remarks>
[ApiController]
[Route("api/api-keys")]
[Authorize("ApiKeyPolicy")]
public class ApiKeysController : ControllerBase
{
    private readonly ApiKeyService _service;

    public ApiKeysController(ApiKeyService service)
    {
        _service = service;
    }

    /// <summary>
    /// 列出全部子密钥（仅掩码）。
    /// </summary>
    [HttpGet]
    public ActionResult<object> List()
    {
        try
        {
            return Ok(new { success = true, data = _service.List() });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取子密钥列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"获取子密钥列表失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 创建子密钥。<b>唯一一次</b>返回明文。
    /// </summary>
    [HttpPost]
    public ActionResult<object> Create([FromBody] CreateApiKeyRequest req)
    {
        try
        {
            var result = _service.Create(req ?? new CreateApiKeyRequest());
            if (result == null)
            {
                return Ok(new { success = false, error = $"子密钥数量已达上限（{ApiKeyService.MaxKeyCount}）" });
            }

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建子密钥失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"创建子密钥失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 更新子密钥（重命名 / 备注 / 过期时间）。
    /// </summary>
    [HttpPut("{id:long}")]
    public ActionResult<object> Update(long id, [FromBody] UpdateApiKeyRequest req)
    {
        try
        {
            var item = _service.Update(id, req ?? new UpdateApiKeyRequest());
            if (item == null)
            {
                return NotFound(new { success = false, error = "密钥不存在" });
            }

            return Ok(new { success = true, data = item });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新子密钥失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"更新子密钥失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 启停子密钥。停用立即 401（不会再回退到主密钥）；可再次启用。
    /// </summary>
    [HttpPost("{id:long}/toggle")]
    public ActionResult<object> Toggle(long id, [FromBody] ToggleApiKeyRequest req)
    {
        try
        {
            var item = _service.Toggle(id, (req ?? new ToggleApiKeyRequest()).Enabled);
            if (item == null)
            {
                return NotFound(new { success = false, error = "密钥不存在" });
            }

            return Ok(new { success = true, data = item });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启停子密钥失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"启停子密钥失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 轮换子密钥：换新明文，旧值立即失效。<b>唯一一次</b>返回新明文。
    /// </summary>
    [HttpPost("{id:long}/roll")]
    public ActionResult<object> Roll(long id)
    {
        try
        {
            var result = _service.Roll(id);
            if (result == null)
            {
                return NotFound(new { success = false, error = "密钥不存在" });
            }

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("轮换子密钥失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"轮换子密钥失败: {ex.Message}" });
        }
    }

    /// <summary>
    /// 删除子密钥（硬删除，立即失效）。
    /// </summary>
    [HttpDelete("{id:long}")]
    public ActionResult<object> Delete(long id)
    {
        try
        {
            if (!_service.Delete(id))
            {
                return NotFound(new { success = false, error = "密钥不存在" });
            }

            return Ok(new { success = true, data = new { deleted = true } });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除子密钥失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"删除子密钥失败: {ex.Message}" });
        }
    }
}

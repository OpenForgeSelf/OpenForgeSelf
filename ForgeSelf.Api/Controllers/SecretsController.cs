using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using ForgeSelf.Api.Services.Vault;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 凭据保险库管理面（<c>api/secrets</c>，PILOT-086）。
/// 全部端点均需 <c>ApiKeyPolicy</c> 鉴权（类级，PC-002 守卫）。
/// </summary>
/// <remarks>
/// 响应外壳沿用 <see cref="ApiKeysController"/>：成功 <c>{ success: true, data: … }</c>，
/// 业务失败 <c>{ success: false, error: "…" }</c>（HTTP 200）；目标不存在 404（同外壳）；
/// 未预期故障 HTTP 500 且 body 仍是外壳，绝不透堆栈。
/// <b>明文红线</b>（方案 §7/§9）：任何端点不返回明文与密文材料（信封字段）；
/// 无「显示原文」「测试连接」端点——明文唯一出口是宿主可信代码经授权的内部通道
/// <see cref="ISecretStore.GetValueForHost"/>；日志只记 ex.Message。
/// </remarks>
[ApiController]
[Route("api/secrets")]
[Authorize("ApiKeyPolicy")]
public class SecretsController : ControllerBase
{
    private readonly ISecretStore _store;

    public SecretsController(ISecretStore store)
    {
        _store = store;
    }

    /// <summary>列出全部凭据（仅元数据）。</summary>
    [HttpGet]
    public ActionResult<object> List()
    {
        try
        {
            return Ok(new { success = true, data = _store.List() });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取凭据列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"获取凭据列表失败: {ex.Message}" });
        }
    }

    /// <summary>创建凭据。明文只在请求当次出现，响应不含 value。</summary>
    [HttpPost]
    public ActionResult<object> Create([FromBody] CreateSecretRequest req)
    {
        try
        {
            var meta = _store.Create(req ?? new CreateSecretRequest(), Actor);
            return Ok(new { success = true, data = meta });
        }
        catch (VaultOperationException ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建凭据失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"创建凭据失败: {ex.Message}" });
        }
    }

    /// <summary>查询审计事件（脱敏字段；limit 默认 100，上限 500）。</summary>
    [HttpGet("audit")]
    public ActionResult<object> Audit([FromQuery] long? secretId, [FromQuery] int limit = 100)
    {
        try
        {
            return Ok(new { success = true, data = _store.GetAudit(secretId, limit) });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("查询凭据审计失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"查询凭据审计失败: {ex.Message}" });
        }
    }

    /// <summary>凭据详情（仅元数据）。<c>{id}</c> 接受数字 Id 或 sec_ 引用。</summary>
    [HttpGet("{id}")]
    public ActionResult<object> GetOne(string id)
    {
        try
        {
            var meta = _store.Get(id);
            return meta == null ? NotFound(NotFoundShell(id)) : Ok(new { success = true, data = meta });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取凭据详情失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"获取凭据详情失败: {ex.Message}" });
        }
    }

    /// <summary>更新凭据元数据（名称/提供方/元数据/过期时间）。</summary>
    [HttpPatch("{id}")]
    public ActionResult<object> Patch(string id, [FromBody] UpdateSecretRequest req)
    {
        try
        {
            var meta = _store.Update(id, req ?? new UpdateSecretRequest(), Actor);
            return meta == null ? NotFound(NotFoundShell(id)) : Ok(new { success = true, data = meta });
        }
        catch (VaultOperationException ex) when (ex.ReasonCode == "secret_not_found")
        {
            return NotFound(NotFoundShell(id));
        }
        catch (VaultOperationException ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新凭据失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"更新凭据失败: {ex.Message}" });
        }
    }

    /// <summary>轮换凭据值（新 DEK + 轮值版本 +1）。响应<b>不回显</b>任何值。</summary>
    [HttpPut("{id}/value")]
    public ActionResult<object> SetValue(string id, [FromBody] SetSecretValueRequest req)
    {
        try
        {
            var meta = _store.SetSecretValue(id, req?.Value ?? "", Actor);
            return meta == null ? NotFound(NotFoundShell(id)) : Ok(new { success = true, data = meta });
        }
        catch (VaultOperationException ex) when (ex.ReasonCode == "secret_not_found")
        {
            return NotFound(NotFoundShell(id));
        }
        catch (VaultOperationException ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("轮换凭据值失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"轮换凭据值失败: {ex.Message}" });
        }
    }

    /// <summary>删除凭据（先吊销全部授权，审计保留）。</summary>
    [HttpDelete("{id}")]
    public ActionResult<object> Remove(string id)
    {
        try
        {
            return _store.Delete(id, Actor)
                ? Ok(new { success = true, data = new { deleted = true, id } })
                : NotFound(NotFoundShell(id));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除凭据失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"删除凭据失败: {ex.Message}" });
        }
    }

    /// <summary>列出某凭据的授权。</summary>
    [HttpGet("{id}/grants")]
    public ActionResult<object> Grants(string id)
    {
        try
        {
            if (_store.Get(id) == null) return NotFound(NotFoundShell(id));
            return Ok(new { success = true, data = _store.ListGrants(id) });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取凭据授权失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"获取凭据授权失败: {ex.Message}" });
        }
    }

    /// <summary>创建授权（默认拒绝模型的显式例外面）。</summary>
    [HttpPost("{id}/grants")]
    public ActionResult<object> CreateGrant(string id, [FromBody] CreateGrantRequest req)
    {
        try
        {
            var grant = _store.CreateGrant(id, req ?? new CreateGrantRequest(), Actor);
            return grant == null ? NotFound(NotFoundShell(id)) : Ok(new { success = true, data = grant });
        }
        catch (VaultOperationException ex) when (ex.ReasonCode == "secret_not_found")
        {
            return NotFound(NotFoundShell(id));
        }
        catch (VaultOperationException ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建凭据授权失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"创建凭据授权失败: {ex.Message}" });
        }
    }

    /// <summary>撤销授权（立即生效）。</summary>
    [HttpDelete("{id}/grants/{grantId:long}")]
    public ActionResult<object> RevokeGrant(string id, long grantId)
    {
        try
        {
            return _store.RevokeGrant(id, grantId, Actor)
                ? Ok(new { success = true, data = new { revoked = true, grantId } })
                : NotFound(NotFoundShell(id));
        }
        catch (VaultOperationException ex) when (ex.ReasonCode == "secret_not_found")
        {
            return NotFound(NotFoundShell(id));
        }
        catch (VaultOperationException ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("撤销凭据授权失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = $"撤销凭据授权失败: {ex.Message}" });
        }
    }

    /// <summary>统一 404 外壳（不区分「数字 Id 不存在」与「引用不存在」）</summary>
    private static object NotFoundShell(string id) => new { success = false, error = $"凭据不存在: {id}" };

    /// <summary>审计发起方（P1 无请求级身份上下文，固定 api:admin；后续接操作人身份时更新）</summary>
    private string Actor => "api:admin";
}

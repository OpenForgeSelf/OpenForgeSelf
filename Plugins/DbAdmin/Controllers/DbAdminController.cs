using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.DbAdmin.Controllers;

/// <summary>
/// DbAdmin 管理面 HTTP（前缀 /api/db-admin，类级 ApiKeyPolicy，铁律17）。
/// 只注入本插件自有单例服务，不构造注入宿主契约（铁律12c）。
/// 错误体统一 {code, message}；状态码见 <see cref="StatusOf"/>。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/db-admin")]
public class DbAdminController(DbConnectionService connections, DbQueryService queries) : ControllerBase
{
    /// <summary>错误码 → HTTP 状态</summary>
    public static Int32 StatusOf(String code) => code switch
    {
        DbErrorCodes.NotFound => 404,
        DbErrorCodes.RejectedHard => 403,
        DbErrorCodes.ReadonlyConnection => 403,
        DbErrorCodes.ConfirmationRequired => 409,
        DbErrorCodes.DriverMissing => 424,
        DbErrorCodes.Unauthorized => 401,
        DbErrorCodes.SqlError => 400,
        _ => 400,
    };

    private IActionResult Fail(DbAdminException ex) => StatusCode(StatusOf(ex.Code), new DbError(ex.Code, ex.Message));

    private IActionResult Run(Func<Object?> fn)
    {
        try
        {
            var r = fn();
            return r == null ? NoContent() : Ok(r);
        }
        catch (DbAdminException ex) { return Fail(ex); }
    }

    [HttpGet("providers")]
    public IActionResult Providers() => Run(() => connections.ListProviders());

    [HttpGet("connections")]
    public IActionResult ListConnections() => Run(() => connections.List());

    [HttpGet("connections/{id}")]
    public IActionResult GetConnection(String id) => Run(() => connections.Get(id));

    [HttpPost("connections")]
    public IActionResult CreateConnection([FromBody] ConnectionInput input) => Run(() => connections.Create(input));

    [HttpPut("connections/{id}")]
    public IActionResult UpdateConnection(String id, [FromBody] ConnectionInput input) => Run(() => connections.Update(id, input));

    /// <summary>仅删配置，不删库文件</summary>
    [HttpDelete("connections/{id}")]
    public IActionResult DeleteConnection(String id) => Run(() => { connections.Delete(id); return null; });

    /// <summary>测连：恒 200，结果体 {ok, code, message, serverVersion, elapsedMs}</summary>
    [HttpPost("connections/test")]
    public IActionResult TestConnection([FromBody] ConnectionInput input) => Ok(connections.Test(input));

    [HttpGet("connections/{id}/tables")]
    public IActionResult Tables(String id) => Run(() => connections.ListTables(id));

    [HttpGet("connections/{id}/tables/{table}")]
    public IActionResult Table(String id, String table) => Run(() => connections.DescribeTable(id, table));

    /// <summary>
    /// 执行 SQL。200 = ok / rejected（业务拒绝，看 code）；202 = awaiting_approval；error 按 code 映射状态码，
    /// 响应体始终是 QueryOutcome（含 code/message），便于结果格统一渲染。
    /// </summary>
    [HttpPost("query")]
    public IActionResult Query([FromBody] QueryRequest req)
    {
        var r = queries.Execute(req, QueryContext.Ui);
        return r.Status switch
        {
            "ok" => Ok(r),
            "awaiting_approval" => StatusCode(202, r),
            "rejected" => Ok(r), // 业务拒绝（rejected_hard/readonly_connection）也是正常结果，前端按 status 渲染
            _ => StatusCode(StatusOf(r.Code ?? DbErrorCodes.SqlError), r),
        };
    }

    [HttpGet("approvals")]
    public IActionResult Approvals([FromQuery] String? status) => Run(() => queries.ListApprovals(status));

    [HttpPost("approvals/{id:long}/resolve")]
    public IActionResult ResolveApproval(Int64 id, [FromBody] ApprovalResolveInput input)
    {
        var d = input.Decision?.Trim().ToLowerInvariant();
        if (d != "approve" && d != "reject") return BadRequest(new DbError(DbErrorCodes.InvalidRequest, "decision 只能是 approve 或 reject"));
        try
        {
            var r = queries.Resolve(id, d == "approve", input.ConfirmDestructive, QueryContext.Ui);
            return r.Status is "ok" or "rejected" ? Ok(r) : StatusCode(StatusOf(r.Code ?? DbErrorCodes.SqlError), r);
        }
        catch (DbAdminException ex) { return Fail(ex); }
    }

    [HttpGet("history")]
    public IActionResult History([FromQuery] LogFilter f) => Run(() => queries.History(f));

    [HttpGet("audit")]
    public IActionResult Audit([FromQuery] LogFilter f) => Run(() => queries.Audit(f));

    [HttpGet("permission-mode")]
    public IActionResult GetMode() => Run(() => queries.GetMode(QueryContext.Ui));

    [HttpPut("permission-mode")]
    public IActionResult SetMode([FromBody] PermissionModeInput input) => Run(() => queries.SetMode(input.Mode, QueryContext.Ui));
}

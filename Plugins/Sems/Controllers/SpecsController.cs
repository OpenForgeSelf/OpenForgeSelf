using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.Sems.Controllers;

/// <summary>
/// sems 规格控制器：项目规格的新建、追加版本、读取、列表、外部标识关联与验收标准用例关联（只追加，旧版本不被覆盖）。
/// 业务一律经 <see cref="ISpecService"/>，与对外 MCP 工具（sems_*_spec）共用同一实现。
/// 全部端点挂 ApiKeyPolicy 鉴权；失败封套与既有控制器一致（success=false + message，状态码取 StatusCode）。
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/specs")]
[Authorize("ApiKeyPolicy")]
public class SpecsController : ControllerBase
{
    private readonly ISpecService _specs;

    public SpecsController(ISpecService specs) => _specs = specs;

    public record CriterionDto(string? Id, string? Text);

    public record CreateSpecRequest(string? Title, string? Content, string? Author, string? Note,
        List<CriterionDto>? Criteria = null);

    public record AddVersionRequest(string? Content, string? Author, string? Note, List<CriterionDto>? Criteria = null);

    public record LinkSpecRequest(int Version, string? Kind, string? Ref);

    public record LinkCaseRequest(int Version, string? CriterionId, string? Case);

    private ObjectResult Failed<T>(SemsResult<T> r) =>
        StatusCode(r.StatusCode, new { success = false, message = r.Error });

    private static IReadOnlyList<SpecCriterion>? ToCriteria(List<CriterionDto>? dto) =>
        dto?.Select(c => new SpecCriterion(c?.Id ?? "", c?.Text ?? "")).ToList();

    /// <summary>项目下全部规格（各取最新版）。GET api/projects/{projectId}/specs</summary>
    [HttpGet]
    public ActionResult<object> List(int projectId)
    {
        var r = _specs.List(projectId);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, specs = r.Data });
    }

    /// <summary>新建规格（自动为 v1），可带结构化验收标准。POST api/projects/{projectId}/specs</summary>
    [HttpPost]
    public ActionResult<object> Create(int projectId, [FromBody] CreateSpecRequest body)
    {
        var r = _specs.Create(projectId, body.Title, body.Content, body.Author, body.Note, ToCriteria(body.Criteria));
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, spec = r.Data });
    }

    /// <summary>读取规格；version 省略取最新版。GET api/projects/{projectId}/specs/{specId}</summary>
    [HttpGet("{specId}")]
    public ActionResult<object> Get(int projectId, string specId, [FromQuery] int? version)
    {
        var r = _specs.Get(projectId, specId, version);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, spec = r.Data });
    }

    /// <summary>追加新版本；criteria 省略则沿用上一版。POST api/projects/{projectId}/specs/{specId}/versions</summary>
    [HttpPost("{specId}/versions")]
    public ActionResult<object> AddVersion(int projectId, string specId, [FromBody] AddVersionRequest body)
    {
        var r = _specs.AddVersion(projectId, specId, body.Content, body.Author, body.Note, ToCriteria(body.Criteria));
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, spec = r.Data });
    }

    /// <summary>关联外部标识（commit / PR / Issue）。POST api/projects/{projectId}/specs/{specId}/links</summary>
    [HttpPost("{specId}/links")]
    public ActionResult<object> Link(int projectId, string specId, [FromBody] LinkSpecRequest body)
    {
        var r = _specs.Link(projectId, specId, body.Version, body.Kind, body.Ref);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, link = r.Data });
    }

    /// <summary>列出规格的全部关联。GET api/projects/{projectId}/specs/{specId}/links</summary>
    [HttpGet("{specId}/links")]
    public ActionResult<object> ListLinks(int projectId, string specId)
    {
        var r = _specs.ListLinks(projectId, specId);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, links = r.Data });
    }

    /// <summary>按外部标识反查所依据的规格及版本。GET api/projects/{projectId}/specs/by-ref?kind=...&amp;ref=...</summary>
    [HttpGet("by-ref")]
    public ActionResult<object> FindByRef(int projectId, [FromQuery] string? kind, [FromQuery(Name = "ref")] string? reference)
    {
        var r = _specs.FindByRef(projectId, kind, reference);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, links = r.Data });
    }

    /// <summary>为某版本的某条验收标准登记用例（手工，只追加）。POST api/projects/{projectId}/specs/{specId}/cases</summary>
    [HttpPost("{specId}/cases")]
    public ActionResult<object> LinkCase(int projectId, string specId, [FromBody] LinkCaseRequest body)
    {
        var r = _specs.LinkCase(projectId, specId, body.Version, body.CriterionId, body.Case);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, @case = r.Data });
    }

    /// <summary>某版本每条验收标准是否已登记用例；version 省略取最新版。GET api/projects/{projectId}/specs/{specId}/coverage</summary>
    [HttpGet("{specId}/coverage")]
    public ActionResult<object> Coverage(int projectId, string specId, [FromQuery] int? version)
    {
        var r = _specs.GetCoverage(projectId, specId, version);
        if (!r.Success) return Failed(r);
        return Ok(new { success = true, coverage = r.Data });
    }
}
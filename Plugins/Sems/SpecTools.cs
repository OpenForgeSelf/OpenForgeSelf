using System.Text.Json;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Plugins.Sems;

/// <summary>规格类工具公共基类：复用 SemsToolFunctionBase 的封套、异常兜底与用量上报。</summary>
public abstract class SemsSpecToolBase : SemsToolFunctionBase
{
    protected SemsSpecToolBase(string pluginId, string toolName, string description, string schema,
        IServiceProvider? services)
        : base(pluginId, toolName, description, schema, services) { }

    protected static ISpecService? Specs(IServiceProvider s) => s.GetService<ISpecService>();

    protected static object ShapeSpec(SpecVersion v) => new
    {
        specId = v.SpecId,
        version = v.Version,
        projectId = v.ProjectId,
        title = v.Title,
        content = v.Content,
        author = v.Author,
        changeNote = v.ChangeNote,
        createdAt = v.CreatedAt,
        criteria = v.Criteria?.Select(c => new { id = c.Id, text = c.Text }).ToList()
    };

    private static string StrOf(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? ""
            : "";

    /// <summary>解析入参 criteria（数组，每条 id 与 text）；未传或不是数组返回 null。</summary>
    protected static IReadOnlyList<SpecCriterion>? ParseCriteria(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty("criteria", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
            return null;
        var list = new List<SpecCriterion>();
        foreach (var e in arr.EnumerateArray())
            list.Add(new SpecCriterion(StrOf(e, "id"), StrOf(e, "text")));
        return list;
    }

    protected static string One(SemsResult<SpecVersion> r) =>
        r.Success ? Ok(ShapeSpec(r.Data!)) : Fail(r.Error ?? "操作失败");
}

/// <summary>sems_create_spec</summary>
public class SemsCreateSpecToolFunction : SemsSpecToolBase
{
    public SemsCreateSpecToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_create_spec",
            "为项目新建一份规格（意图/需求/验收标准），自动成为 v1。projectId 来自 sems_list_projects，title 与 content 必填。" +
            "可选 criteria 是结构化验收标准数组（每条 id 与 text），有了它才能用 sems_link_spec_case 登记用例。" +
            "返回 specId（后续追加版本、读取、关联都用它）。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id（sems_list_projects）" },
              "title": { "type": "string", "description": "规格标题" },
              "content": { "type": "string", "description": "规格正文（Markdown）" },
              "author": { "type": "string", "description": "提交者（人或 Agent 标识，可选）" },
              "note": { "type": "string", "description": "变更说明（可选）" },
              "criteria": { "type": "array", "description": "结构化验收标准（可选）。每条含 id（如 AC1，同一版本内唯一）与 text，例如 id=AC1，text=登录失败返回 401",
                "items": { "type": "object", "properties": {
                  "id": { "type": "string", "description": "标准编号，如 AC1" },
                  "text": { "type": "string", "description": "标准内容" } },
                  "required": ["id", "text"] } } },
              "required": ["projectId", "title", "content"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        return One(svc.Create(pid, GetStr(args, "title"), GetStr(args, "content"),
            GetStr(args, "author"), GetStr(args, "note"), ParseCriteria(args)));
    }
}

/// <summary>sems_add_spec_version</summary>
public class SemsAddSpecVersionToolFunction : SemsSpecToolBase
{
    public SemsAddSpecVersionToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_add_spec_version",
            "给已有规格追加新版本（只追加，旧版本永不被覆盖）。projectId、specId、content 必填。" +
            "criteria 省略则沿用上一版的验收标准；已登记的用例按版本固定，新版本从未关联开始。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "specId": { "type": "string", "description": "规格 id（sems_create_spec / sems_list_specs 返回）" },
              "content": { "type": "string", "description": "新版本正文" },
              "author": { "type": "string", "description": "提交者（可选）" },
              "note": { "type": "string", "description": "变更说明（可选）" },
              "criteria": { "type": "array", "description": "新版本的验收标准（可选，省略沿用上一版）。每条含 id 与 text",
                "items": { "type": "object", "properties": {
                  "id": { "type": "string", "description": "标准编号，如 AC1" },
                  "text": { "type": "string", "description": "标准内容" } },
                  "required": ["id", "text"] } } },
              "required": ["projectId", "specId", "content"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        return One(svc.AddVersion(pid, GetStr(args, "specId") ?? "", GetStr(args, "content"),
            GetStr(args, "author"), GetStr(args, "note"), ParseCriteria(args)));
    }
}

/// <summary>sems_get_spec</summary>
public class SemsGetSpecToolFunction : SemsSpecToolBase
{
    public SemsGetSpecToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_get_spec",
            "读取规格的指定版本；version 省略时取最新版。projectId、specId 必填。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "specId": { "type": "string", "description": "规格 id" },
              "version": { "type": "integer", "description": "版本号（可选，默认最新）" } },
              "required": ["projectId", "specId"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        int? ver = TryGetInt(args, "version", out var v) ? v : null;
        return One(svc.Get(pid, GetStr(args, "specId") ?? "", ver));
    }
}

/// <summary>sems_list_specs</summary>
public class SemsListSpecsToolFunction : SemsSpecToolBase
{
    public SemsListSpecsToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_list_specs",
            "列出项目下全部规格的最新版本。projectId 必填（sems_list_projects）。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" } },
              "required": ["projectId"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        var r = svc.List(pid);
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        return Ok(new { total = r.Data!.Count, specs = r.Data.Select(ShapeSpec).ToList() });
    }
}

/// <summary>sems_link_spec</summary>
public class SemsLinkSpecToolFunction : SemsSpecToolBase
{
    public SemsLinkSpecToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_link_spec",
            "把规格某个版本关联到一个外部标识（如 commit、PR、Issue 编号），手工登记，只追加。" +
            "projectId、specId、version、kind、ref 必填。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "specId": { "type": "string", "description": "规格 id" },
              "version": { "type": "integer", "description": "规格版本号" },
              "kind": { "type": "string", "description": "关联类型，如 commit / pr / issue" },
              "ref": { "type": "string", "description": "关联标识，如提交哈希或编号" } },
              "required": ["projectId", "specId", "version", "kind", "ref"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        if (!TryGetInt(args, "version", out var ver)) return Fail("参数 version（整数）必填");
        var r = svc.Link(pid, GetStr(args, "specId") ?? "", ver, GetStr(args, "kind"), GetStr(args, "ref"));
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        return Ok(new { specId = r.Data!.SpecId, version = r.Data.Version, kind = r.Data.Kind, @ref = r.Data.Ref, at = r.Data.At });
    }
}

/// <summary>关联记录的对外形状（工具层共用）。</summary>
internal static class SemsSpecLinkShape
{
    public static object Of(SpecLink l) => new { specId = l.SpecId, version = l.Version, kind = l.Kind, @ref = l.Ref, at = l.At };
}

/// <summary>sems_list_spec_links</summary>
public class SemsListSpecLinksToolFunction : SemsSpecToolBase
{
    public SemsListSpecLinksToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_list_spec_links",
            "列出一份规格已登记的全部外部关联（commit / PR / Issue），每条带它关联的规格版本。projectId、specId 必填。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id（sems_list_projects）" },
              "specId": { "type": "string", "description": "规格 id（sems_create_spec / sems_list_specs 返回）" } },
              "required": ["projectId", "specId"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        var r = svc.ListLinks(pid, GetStr(args, "specId") ?? "");
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        return Ok(new { total = r.Data!.Count, links = r.Data.Select(SemsSpecLinkShape.Of).ToList() });
    }
}

/// <summary>sems_find_spec_by_ref</summary>
public class SemsFindSpecByRefToolFunction : SemsSpecToolBase
{
    public SemsFindSpecByRefToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_find_spec_by_ref",
            "按外部标识反查所依据的规格及版本，例如 kind=commit、ref=abc123。同一标识可对应多份规格。projectId、kind、ref 必填。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "kind": { "type": "string", "description": "关联类型，如 commit / pr / issue" },
              "ref": { "type": "string", "description": "关联标识，如提交哈希或编号，例如 abc123" } },
              "required": ["projectId", "kind", "ref"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        var r = svc.FindByRef(pid, GetStr(args, "kind"), GetStr(args, "ref"));
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        return Ok(new { total = r.Data!.Count, links = r.Data.Select(SemsSpecLinkShape.Of).ToList() });
    }
}

/// <summary>sems_link_spec_case</summary>
public class SemsLinkSpecCaseToolFunction : SemsSpecToolBase
{
    public SemsLinkSpecCaseToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_link_spec_case",
            "为规格某个版本的某条验收标准登记一个测试用例（手工登记，只追加）。case 约定写成「测试文件路径::用例名」，" +
            "只校验非空，不检查文件是否存在或用例是否通过，所以「已关联」只表示有人登记了，不是验证结果。" +
            "projectId、specId、version、criterionId、case 必填；criterionId 必须属于该版本的 criteria。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "specId": { "type": "string", "description": "规格 id" },
              "version": { "type": "integer", "description": "规格版本号" },
              "criterionId": { "type": "string", "description": "验收标准编号，如 AC1（须属于该版本）" },
              "case": { "type": "string", "description": "用例标识，约定「测试文件路径::用例名」，例如 SpecServiceTests.cs::Coverage_Shows_Linked" } },
              "required": ["projectId", "specId", "version", "criterionId", "case"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        if (!TryGetInt(args, "version", out var ver)) return Fail("参数 version（整数）必填");
        var r = svc.LinkCase(pid, GetStr(args, "specId") ?? "", ver, GetStr(args, "criterionId"), GetStr(args, "case"));
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        return Ok(new { specId = r.Data!.SpecId, version = r.Data.Version, criterionId = r.Data.CriterionId, @case = r.Data.Case, at = r.Data.At });
    }
}

/// <summary>sems_get_spec_coverage</summary>
public class SemsGetSpecCoverageToolFunction : SemsSpecToolBase
{
    public SemsGetSpecCoverageToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_get_spec_coverage",
            "查看规格某个版本的每条验收标准是否已登记用例，返回 total、linked 与逐条 items（含已登记的用例列表）。" +
            "version 省略取最新版；规格没有验收标准时 total 为 0。linked 只表示有人登记过用例标识，不代表用例存在或已通过。" +
            "projectId、specId 必填。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            """
            { "type": "object", "properties": {
              "projectId": { "type": "integer", "description": "项目 id" },
              "specId": { "type": "string", "description": "规格 id" },
              "version": { "type": "integer", "description": "版本号（可选，默认最新）" } },
              "required": ["projectId", "specId"] }
            """, services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Specs(services);
        if (svc == null) return ServiceMissing(nameof(ISpecService));
        if (!TryGetInt(args, "projectId", out var pid)) return MissingInt("projectId", "sems_list_projects");
        int? ver = TryGetInt(args, "version", out var v) ? v : null;
        var r = svc.GetCoverage(pid, GetStr(args, "specId") ?? "", ver);
        if (!r.Success) return Fail(r.Error ?? "操作失败");
        var c = r.Data!;
        return Ok(new
        {
            specId = c.SpecId,
            version = c.Version,
            total = c.Total,
            linked = c.Linked,
            items = c.Items.Select(i => new { id = i.Id, text = i.Text, linked = i.Linked, cases = i.Cases }).ToList()
        });
    }
}
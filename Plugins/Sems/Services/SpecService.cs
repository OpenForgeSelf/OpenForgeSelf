using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>某条验收标准的用例覆盖。Linked 只表示有人登记了用例标识，不表示用例存在或已通过。</summary>
public sealed record CriterionCoverage(string Id, string Text, IReadOnlyList<string> Cases, bool Linked);

/// <summary>规格某个版本的验收标准覆盖汇总（B1-AC2）。</summary>
public sealed record SpecCoverage(string SpecId, int Version, int Total, int Linked, IReadOnlyList<CriterionCoverage> Items);

/// <summary>sems 规格服务：规格的新建、追加版本、读取、列表、外部标识关联与验收标准用例关联。HTTP 控制器与 MCP 工具共用本服务。</summary>
public interface ISpecService
{
    SemsResult<SpecVersion> Create(int projectId, string? title, string? content, string? author, string? note,
        IReadOnlyList<SpecCriterion>? criteria = null);
    SemsResult<SpecVersion> AddVersion(int projectId, string specId, string? content, string? author, string? note,
        IReadOnlyList<SpecCriterion>? criteria = null);
    SemsResult<SpecVersion> Get(int projectId, string specId, int? version);
    SemsResult<IReadOnlyList<SpecVersion>> List(int projectId);
    SemsResult<SpecLink> Link(int projectId, string specId, int version, string? kind, string? reference);
    SemsResult<IReadOnlyList<SpecLink>> ListLinks(int projectId, string specId);
    SemsResult<IReadOnlyList<SpecLink>> FindByRef(int projectId, string? kind, string? reference);
    SemsResult<SpecCase> LinkCase(int projectId, string specId, int version, string? criterionId, string? caseRef);
    SemsResult<SpecCoverage> GetCoverage(int projectId, string specId, int? version);
}

/// <summary>
/// 规格服务实现。构造只保存委托，不解析服务；项目接缝每次调用经 ctx 重新解析，不缓存为字段。
/// 规格元数据记录 projectId 与项目 Root：项目被移除后 id 若被别的目录复用，Root 不一致的规格视为孤儿，不列出。
/// </summary>
public sealed class SpecService : ISpecService
{
    private readonly SpecFileStore _store;
    private readonly Func<IProjectRegistry?> _registry;

    public SpecService(IContext ctx)
    {
        _store = new SpecFileStore(() => Path.Combine(ctx.GetPluginDataDirectory(), "specs"));
        _registry = () => ctx.Get<IProjectRegistry>();
    }

    private SemsResult<ProjectInfo> Project(int projectId)
    {
        var reg = _registry();
        if (reg == null) return SemsResult<ProjectInfo>.Fail(SemsOpStatus.Unavailable, "项目注册表接缝不可用");
        var p = reg.Get(projectId);
        return p == null
            ? SemsResult<ProjectInfo>.Fail(SemsOpStatus.NotFound, $"项目 {projectId} 不存在")
            : SemsResult<ProjectInfo>.Ok(p);
    }

    /// <summary>校验并规整验收标准：id 与 text 必填，同一版本内 id 不区分大小写唯一。返回错误文案，null 表示通过。</summary>
    private static string? CleanCriteria(IReadOnlyList<SpecCriterion>? input, out IReadOnlyList<SpecCriterion>? cleaned)
    {
        cleaned = null;
        if (input == null) return null;
        var list = new List<SpecCriterion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in input)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Text))
                return "criteria 每条的 id 与 text 必填";
            var id = c.Id.Trim();
            if (!seen.Add(id)) return $"criteria 的 id 重复：{id}";
            list.Add(new SpecCriterion(id, c.Text.Trim()));
        }

        cleaned = list;
        return null;
    }

    public SemsResult<SpecVersion> Create(int projectId, string? title, string? content, string? author, string? note,
        IReadOnlyList<SpecCriterion>? criteria = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, "title 必填");
        if (string.IsNullOrWhiteSpace(content)) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, "content 必填");
        var err = CleanCriteria(criteria, out var crit);
        if (err != null) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, err);
        var p = Project(projectId);
        if (!p.Success) return SemsResult<SpecVersion>.Fail(p.Status, p.Error);
        var specId = SpecFileStore.NewId();
        var rec = _store.Append(projectId, specId, v => new SpecVersion(
            specId, v, projectId, p.Data!.Root, title.Trim(), content, author, note, DateTime.UtcNow, crit));
        return SemsResult<SpecVersion>.Ok(rec);
    }

    public SemsResult<SpecVersion> AddVersion(int projectId, string specId, string? content, string? author, string? note,
        IReadOnlyList<SpecCriterion>? criteria = null)
    {
        if (!SpecFileStore.IsValidId(specId)) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, "specId 非法");
        if (string.IsNullOrWhiteSpace(content)) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, "content 必填");
        var err = CleanCriteria(criteria, out var crit);
        if (err != null) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, err);
        var p = Project(projectId);
        if (!p.Success) return SemsResult<SpecVersion>.Fail(p.Status, p.Error);
        var latestNo = _store.LatestVersion(projectId, specId);
        var latest = latestNo > 0 ? _store.Read(projectId, specId, latestNo) : null;
        if (latest == null) return SemsResult<SpecVersion>.Fail(SemsOpStatus.NotFound, "规格不存在");
        // 未传 criteria 时沿用上一版的验收标准；用例登记按版本固定，新版本从未关联开始
        var useCrit = crit ?? latest.Criteria;
        var rec = _store.Append(projectId, specId, v => new SpecVersion(
            specId, v, projectId, p.Data!.Root, latest.Title, content, author, note, DateTime.UtcNow, useCrit));
        return SemsResult<SpecVersion>.Ok(rec);
    }

    public SemsResult<SpecVersion> Get(int projectId, string specId, int? version)
    {
        if (!SpecFileStore.IsValidId(specId)) return SemsResult<SpecVersion>.Fail(SemsOpStatus.Invalid, "specId 非法");
        var p = Project(projectId);
        if (!p.Success) return SemsResult<SpecVersion>.Fail(p.Status, p.Error);
        var v = version ?? _store.LatestVersion(projectId, specId);
        var rec = v > 0 ? _store.Read(projectId, specId, v) : null;
        return rec == null
            ? SemsResult<SpecVersion>.Fail(SemsOpStatus.NotFound, "规格或版本不存在")
            : SemsResult<SpecVersion>.Ok(rec);
    }

    public SemsResult<IReadOnlyList<SpecVersion>> List(int projectId)
    {
        var p = Project(projectId);
        if (!p.Success) return SemsResult<IReadOnlyList<SpecVersion>>.Fail(p.Status, p.Error);
        var list = new List<SpecVersion>();
        foreach (var id in _store.ListSpecIds(projectId))
        {
            var latestNo = _store.LatestVersion(projectId, id);
            if (latestNo <= 0) continue;
            var rec = _store.Read(projectId, id, latestNo);
            // Root 与当前项目不一致 = 孤儿规格（id 被复用），不列出
            if (rec != null && string.Equals(rec.ProjectRoot, p.Data!.Root, StringComparison.OrdinalIgnoreCase))
                list.Add(rec);
        }
        return SemsResult<IReadOnlyList<SpecVersion>>.Ok(list);
    }

    public SemsResult<SpecLink> Link(int projectId, string specId, int version, string? kind, string? reference)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(reference))
            return SemsResult<SpecLink>.Fail(SemsOpStatus.Invalid, "kind 与 ref 必填");
        var g = Get(projectId, specId, version);
        if (!g.Success) return SemsResult<SpecLink>.Fail(g.Status, g.Error);
        var link = new SpecLink(specId, version, kind.Trim(), reference.Trim(), DateTime.UtcNow);
        _store.AppendLink(projectId, specId, link);
        return SemsResult<SpecLink>.Ok(link);
    }

    public SemsResult<IReadOnlyList<SpecLink>> ListLinks(int projectId, string specId)
    {
        if (!SpecFileStore.IsValidId(specId))
            return SemsResult<IReadOnlyList<SpecLink>>.Fail(SemsOpStatus.Invalid, "specId 非法");
        var p = Project(projectId);
        if (!p.Success) return SemsResult<IReadOnlyList<SpecLink>>.Fail(p.Status, p.Error);
        if (_store.LatestVersion(projectId, specId) <= 0)
            return SemsResult<IReadOnlyList<SpecLink>>.Fail(SemsOpStatus.NotFound, "规格不存在");
        return SemsResult<IReadOnlyList<SpecLink>>.Ok(_store.ReadLinks(projectId, specId));
    }

    public SemsResult<IReadOnlyList<SpecLink>> FindByRef(int projectId, string? kind, string? reference)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(reference))
            return SemsResult<IReadOnlyList<SpecLink>>.Fail(SemsOpStatus.Invalid, "kind 与 ref 必填");
        var p = Project(projectId);
        if (!p.Success) return SemsResult<IReadOnlyList<SpecLink>>.Fail(p.Status, p.Error);
        var k = kind.Trim();
        var r = reference.Trim();
        var hits = new List<SpecLink>();
        foreach (var id in _store.ListSpecIds(projectId))
        {
            var latestNo = _store.LatestVersion(projectId, id);
            if (latestNo <= 0) continue;
            var latest = _store.Read(projectId, id, latestNo);
            // Root 与当前项目不一致 = 孤儿规格，不参与反查
            if (latest == null || !string.Equals(latest.ProjectRoot, p.Data!.Root, StringComparison.OrdinalIgnoreCase))
                continue;
            hits.AddRange(_store.ReadLinks(projectId, id).Where(l =>
                string.Equals(l.Kind, k, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(l.Ref, r, StringComparison.OrdinalIgnoreCase)));
        }

        return SemsResult<IReadOnlyList<SpecLink>>.Ok(hits);
    }

    public SemsResult<SpecCase> LinkCase(int projectId, string specId, int version, string? criterionId, string? caseRef)
    {
        if (string.IsNullOrWhiteSpace(criterionId) || string.IsNullOrWhiteSpace(caseRef))
            return SemsResult<SpecCase>.Fail(SemsOpStatus.Invalid, "criterionId 与 case 必填");
        var g = Get(projectId, specId, version);
        if (!g.Success) return SemsResult<SpecCase>.Fail(g.Status, g.Error);
        var cid = criterionId.Trim();
        var crit = g.Data!.Criteria?.FirstOrDefault(c => string.Equals(c.Id, cid, StringComparison.OrdinalIgnoreCase));
        if (crit == null)
            return SemsResult<SpecCase>.Fail(SemsOpStatus.Invalid, $"规格 v{version} 没有验收标准 {cid}");
        var rec = new SpecCase(specId, version, crit.Id, caseRef.Trim(), DateTime.UtcNow);
        _store.AppendCase(projectId, specId, rec);
        return SemsResult<SpecCase>.Ok(rec);
    }

    public SemsResult<SpecCoverage> GetCoverage(int projectId, string specId, int? version)
    {
        var g = Get(projectId, specId, version);
        if (!g.Success) return SemsResult<SpecCoverage>.Fail(g.Status, g.Error);
        var v = g.Data!;
        var cases = _store.ReadCases(projectId, specId).Where(c => c.Version == v.Version).ToList();
        var items = (v.Criteria ?? Array.Empty<SpecCriterion>()).Select(c =>
        {
            var cs = cases
                .Where(x => string.Equals(x.CriterionId, c.Id, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Case)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return new CriterionCoverage(c.Id, c.Text, cs, cs.Count > 0);
        }).ToList();
        return SemsResult<SpecCoverage>.Ok(
            new SpecCoverage(v.SpecId, v.Version, items.Count, items.Count(i => i.Linked), items));
    }
}
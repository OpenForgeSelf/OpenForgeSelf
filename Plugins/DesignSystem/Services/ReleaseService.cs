using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>快照里的一条有效令牌（已解析值，按主题展开）</summary>
public sealed record ReleaseToken(String Theme, String Path, String Tier, String Type, String Value, String? AliasPath, String? Hex, String? Group);

/// <summary>
/// 快照里的一条**非令牌规格**（组件目录 / 变体格子 / 资产 / 起手屏 / 字体登记）。
///
/// 为什么要进快照：这些正是界面上"看得见、会被改"的东西 —— 换了 logo、改了字体许可证、
/// 加删一个起手屏，如果版本里没有它们，"可版本化"就只对令牌成立，同版本号重发还会被当成"内容一致"幂等放行。
/// 形状用 `Kind + Key + 字段表`，这样 diff 能报到"哪一条的哪个字段"，而不是只说"有变化"。
/// </summary>
public sealed record ReleaseSpec(String Kind, String Key, IReadOnlyDictionary<String, String?> Fields);

/// <summary>单条规格字段的变更（Field 为空表示整条新增/删除）</summary>
public sealed record SpecChange(String Kind, String Key, String? Field, String? From, String? To);

/// <summary>建发布请求体</summary>
public sealed record ReleaseRequest(String Version, String? Notes, Int64 SourceReleaseId = 0);

/// <summary>
/// 不可变发布快照。
/// `Specs` 可空是为了读得动 **schema 1 的旧快照文件**（那时只有令牌）：
/// 旧文件不能凭空补一节，比对时必须如实报"不可比"而不是"新增 N 条"。
/// schema 2→3 只多了一类规格（`kind="guideline"`，M3 UX 规范），所以按**类**判可比性，见 <see cref="Schema3SpecKinds"/>。
/// </summary>
public sealed record ReleaseSnapshot(Int32 SchemaVersion, String Project, String Version, DateTime GeneratedAt,
    IReadOnlyList<ReleaseToken> Tokens, IReadOnlyList<ReleaseSpec>? Specs)
{
    public const Int32 CurrentSchema = 3;

    /// <summary>schema 3 才出现的规格类：与 schema≤2 的快照互比时这类**不可比**（旧文件里从来没记过，报"新增 14 条"是假的）</summary>
    public static readonly String[] Schema3SpecKinds = ["guideline"];

    public static Boolean KindComparable(Int32 schemaA, Int32 schemaB, String kind) =>
        !(Schema3SpecKinds.Contains(kind, StringComparer.Ordinal) && (schemaA < 3 || schemaB < 3));
}

/// <summary>两个快照的差异（令牌 + 规格两节）</summary>
public sealed record ReleaseDiff(String From, String To, IReadOnlyList<ReleaseToken> Added, IReadOnlyList<ReleaseToken> Removed,
    IReadOnlyList<TokenChange> Changed, IReadOnlyList<String> MissingThemes,
    IReadOnlyList<ReleaseSpec> SpecsAdded, IReadOnlyList<ReleaseSpec> SpecsRemoved, IReadOnlyList<SpecChange> SpecsChanged,
    Boolean SpecsComparable,
    /// <summary>整节能比但某一类不可比（schema 2↔3 的 guideline）：界面必须写"这一类无法比较"，而不是"没有变化"</summary>
    IReadOnlyList<String>? NotComparableKinds = null)
{
    /// <summary>不可比时不算"有变更"：否则老快照一比就凭空冒出整节新增</summary>
    public Boolean IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0
                              && SpecsAdded.Count == 0 && SpecsRemoved.Count == 0 && SpecsChanged.Count == 0;
    public Int32 Total => Added.Count + Removed.Count + Changed.Count
                          + SpecsAdded.Count + SpecsRemoved.Count + SpecsChanged.Count;
}

/// <summary>单个令牌字段的变更</summary>
public sealed record TokenChange(String Theme, String Path, String Field, String? From, String? To);

/// <summary>
/// 设计系统自身的版本化：不可变快照 + 令牌级 diff。
///
/// 为什么快照落文件而不是落库字段：一个项目几千令牌，行存大 JSON 会把表撑爆也难 diff；
/// 库里只存索引（版本/哈希/文件路径/审计摘要），快照文件永不删除、永不覆写（铁律 10）。
/// </summary>
public sealed class ReleaseService
{
    readonly TokenRepository _tokens;
    readonly DesignProjectService _projects;
    readonly AuditRepository _audits;
    readonly AuditEngine _audit;
    readonly DesignSystemPaths _paths;
    readonly CatalogRepository _catalog;
    readonly GuidelineRepository? _guidelines;

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="guidelines">M3 规范仓储：不给时快照不含 guideline 类（旧装配照跑），给了就把规范一起钉进版本</param>
    public ReleaseService(TokenRepository tokens, DesignProjectService projects, AuditRepository audits, AuditEngine audit,
        DesignSystemPaths paths, CatalogRepository catalog, GuidelineRepository? guidelines = null)
    {
        _tokens = tokens;
        _projects = projects;
        _audits = audits;
        _audit = audit;
        _paths = paths;
        _catalog = catalog;
        _guidelines = guidelines;
    }

    public IList<DesignRelease> List(Int64 projectId) =>
        DesignRelease.QueryAll(DesignRelease._.ProjectId == projectId).OrderByDescending(r => r.CreatedAt).ToList();

    public DesignRelease? Find(Int64 releaseId) => DesignRelease.QueryAll(DesignRelease._.Id == releaseId).FirstOrDefault();

    /// <summary>
    /// 同一项目的发布串行化。并发或重复投递的两个 POST /releases 会各跑一遍审计写库，
    /// 在 SQLite 上互相撞锁（实测 SQLITE_BUSY → 500，两边都没落成）。串行后第二个请求自然走
    /// "同版本同内容 = 幂等返回"那条路。仅单进程内有效（宿主即单进程）；多进程部署需换库级锁。
    /// </summary>
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Int64, SemaphoreSlim> ProjectGates = new();

    public DesignRelease Create(Int64 projectId, String version, String? notes, Int64 sourceReleaseId = 0)
    {
        var gate = ProjectGates.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try
        {
            return CreateCore(projectId, version, notes, sourceReleaseId);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 建发布快照。顺序按"错误信息最有指向性"排：空内容 → 版本号占用 → 审计门禁 → 落盘。
    /// 同版本重复创建：内容一致则幂等返回，不一致则拒绝（快照不可变）。
    /// </summary>
    DesignRelease CreateCore(Int64 projectId, String version, String? notes, Int64 sourceReleaseId)
    {
        version = version.Trim();
        if (version.IsNullOrEmpty()) throw new ArgumentException("版本号不能为空", nameof(version));
        var project = _projects.Find(projectId) ?? throw new KeyNotFoundException($"项目 {projectId} 不存在");

        var snapshot = BuildSnapshot(projectId, version);
        if (snapshot.Tokens.Count == 0) throw new DesignConflictException("项目还没有任何令牌，无内容可发布（先生成或导入令牌）");
        var hash = Hash(snapshot);

        var existing = DesignRelease.QueryAll(DesignRelease._.ProjectId == projectId & DesignRelease._.Version == version).FirstOrDefault();
        if (existing != null)
        {
            if (string.Equals(existing.TokensHash, hash, StringComparison.OrdinalIgnoreCase)) return existing;
            throw new DesignConflictException($"版本 {version} 已存在且内容不同；快照不可变，请递增版本号");
        }

        var blocking = _audits.HasBlocking(projectId) || _audit.Run(projectId).Blocking;
        if (blocking)
        {
            var detail = _audits.List(projectId, 0, null, false).Where(a => !a.Passed && a.Severity == "critical").Take(3)
                .Select(a => $"{a.TargetPath}：{a.Message}");
            throw new DesignConflictException($"存在未通过的 critical 审计，禁止发布。例：{string.Join(" / ", detail)}");
        }

        var file = Path.Combine(_paths.ReleasesDirectory, $"{projectId}-{Sanitize(version)}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(snapshot, JsonOpts));

        var now = DateTime.Now;
        var summary = _audits.Summarize(projectId);
        var release = new DesignRelease
        {
            ProjectId = projectId,
            Version = version,
            Status = "published",
            TokensHash = hash,
            SnapshotFile = file,
            TokenCount = snapshot.Tokens.Count,
            AuditSummary = $"{summary.Total} 项（critical {summary.Critical} / warning {summary.Warning} / info {summary.Info}）",
            AuditPassed = !summary.Blocking,
            SourceReleaseId = sourceReleaseId,
            ReleaseNotes = notes ?? "",
            CreatedBy = "design-system-plugin",
            CreatedAt = now,
            UpdatedAt = now,
        };
        release.Insert();

        project.Version = version;
        project.Status = ProjectStatus.Published;
        project.PublishedAt = now;
        project.Save();
        return release;
    }

    /// <summary>读快照文件</summary>
    public ReleaseSnapshot Load(Int64 releaseId)
    {
        var release = Find(releaseId) ?? throw new KeyNotFoundException($"发布 {releaseId} 不存在");
        if (release.SnapshotFile.IsNullOrEmpty() || !File.Exists(release.SnapshotFile))
            throw new FileNotFoundException($"快照文件缺失：{release.SnapshotFile}");
        return JsonSerializer.Deserialize<ReleaseSnapshot>(File.ReadAllText(release.SnapshotFile), JsonOpts)
               ?? throw new InvalidDataException("快照文件无法解析");
    }

    /// <summary>两个快照的令牌级差异（新增/删除/改值/改别名/改类型 + 主题缺失）</summary>
    public ReleaseDiff Diff(Int64 fromId, Int64 toId)
    {
        var a = Load(fromId);
        var b = Load(toId);
        return DiffOf(a, b);
    }

    public static ReleaseDiff DiffOf(ReleaseSnapshot a, ReleaseSnapshot b)
    {
        var left = a.Tokens.ToDictionary(Key);
        var right = b.Tokens.ToDictionary(Key);

        var added = right.Where(kv => !left.ContainsKey(kv.Key)).Select(kv => kv.Value).OrderBy(t => t.Theme).ThenBy(t => t.Path).ToList();
        var removed = left.Where(kv => !right.ContainsKey(kv.Key)).Select(kv => kv.Value).OrderBy(t => t.Theme).ThenBy(t => t.Path).ToList();

        var changed = new List<TokenChange>();
        foreach (var kv in left)
        {
            if (!right.TryGetValue(kv.Key, out var now)) continue;
            var old = kv.Value;
            Add(old.Value, now.Value, "value");
            Add(old.AliasPath, now.AliasPath, "aliasPath");
            Add(old.Type, now.Type, "type");
            Add(old.Tier, now.Tier, "tier");
            Add(old.Hex, now.Hex, "hex");
            void Add(String? from, String? to, String field)
            {
                if (string.Equals(from, to, StringComparison.Ordinal)) return;
                changed.Add(new TokenChange(old.Theme, old.Path, field, from, to));
            }
        }

        var themesA = a.Tokens.Select(t => t.Theme).ToHashSet(StringComparer.Ordinal);
        var themesB = b.Tokens.Select(t => t.Theme).ToHashSet(StringComparer.Ordinal);
        var missing = themesB.Except(themesA).Concat(themesA.Except(themesB)).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // 规格节：任何一边没有这一节（schema 1 的旧快照）就报"不可比"，绝不把整节凭空报成新增
        var comparable = a.Specs is not null && b.Specs is not null;
        // 类级不可比（M3）：schema≤2 的快照里从来没记过 guideline，这一类必须报"无法比较"而不是"新增 14 条"
        Boolean KindOk(String kind) => ReleaseSnapshot.KindComparable(a.SchemaVersion, b.SchemaVersion, kind);
        var specA = (a.Specs ?? []).Where(s => KindOk(s.Kind)).ToDictionary(SpecKey);
        var specB = (b.Specs ?? []).Where(s => KindOk(s.Kind)).ToDictionary(SpecKey);
        var specAdded = comparable ? specB.Where(kv => !specA.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList() : [];
        var specRemoved = comparable ? specA.Where(kv => !specB.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList() : [];
        var specChanged = new List<SpecChange>();
        if (comparable)
        {
            foreach (var kv in specA)
            {
                if (!specB.TryGetValue(kv.Key, out var now)) continue;
                var old = kv.Value;
                foreach (var field in old.Fields.Keys.Union(now.Fields.Keys).OrderBy(x => x, StringComparer.Ordinal))
                {
                    old.Fields.TryGetValue(field, out var from);
                    now.Fields.TryGetValue(field, out var to);
                    if (string.Equals(from, to, StringComparison.Ordinal)) continue;
                    specChanged.Add(new SpecChange(old.Kind, old.Key, field, from, to));
                }
            }
        }

        return new ReleaseDiff(a.Version, b.Version,
            added, removed, changed.OrderBy(c => c.Theme).ThenBy(c => c.Path).ToList(), missing,
            specAdded.OrderBy(s => s.Kind).ThenBy(s => s.Key).ToList(),
            specRemoved.OrderBy(s => s.Kind).ThenBy(s => s.Key).ToList(),
            specChanged.OrderBy(c => c.Kind).ThenBy(c => c.Key).ThenBy(c => c.Field, StringComparer.Ordinal).ToList(),
            comparable,
            // 由 schema 版本号判定，不依赖数据里是否恰好出现过这类（两边都是空的也可能是"旧侧根本没记"）
            ReleaseSnapshot.Schema3SpecKinds.Where(k => !KindOk(k)).ToList());
    }

    static String Key(ReleaseToken t) => t.Theme + "\u0000" + t.Path;

    ReleaseSnapshot BuildSnapshot(Int64 projectId, String version)
    {
        var tokens = new List<ReleaseToken>();
        var shared = _tokens.LoadGraph(projectId, null, "shared");
        tokens.AddRange(FromGraph(shared, "shared"));

        foreach (var theme in _projects.ListThemes(projectId))
            tokens.AddRange(FromGraph(_tokens.LoadGraph(projectId, theme.Id == DesignSystemConstants.SharedThemeId ? null : theme.Id, theme.Code), theme.Code));

        var ordered = tokens.OrderBy(t => t.Theme, StringComparer.Ordinal).ThenBy(t => t.Path, StringComparer.Ordinal).ToList();
        return new ReleaseSnapshot(ReleaseSnapshot.CurrentSchema, projectId.ToString(), version, DateTime.UtcNow, ordered, BuildSpecs(projectId));

        static ReleaseToken ToToken(TokenGraph g, String theme, TokenNode n)
        {
            var r = g.Resolve(n.Path);
            String? hex = null;
            if (n.Type == TokenTypes.Color && g.ResolveColor(n.Path) is { } color) hex = Oklch.ToRgb8(color).ToHex();
            // 描述/分组等展示字段不进快照：diff 要稳，只留会影响产物的字段
            return new ReleaseToken(theme, n.Path, n.Tier, n.Type, r.IsOk ? r.Value : "", n.AliasPath, hex, n.Path.Split('.')[0]);
        }

        static IEnumerable<ReleaseToken> FromGraph(TokenGraph g, String theme) =>
            // 已下架（removed）的令牌不进快照：下一版 diff 才能把它报成"删除"
            g.All().Where(n => n.Lifecycle != TokenLifecycles.Removed).Select(n => ToToken(g, theme, n));
    }

    /// <summary>
    /// 项目里的**非令牌规格**：组件目录、变体格子、资产、起手屏、字体登记。
    /// 内置图标库（ProjectId=0）不进来 —— 它随插件版本走，不是这个项目的一次发布内容。
    /// </summary>
    List<ReleaseSpec> BuildSpecs(Int64 projectId)
    {
        var specs = new List<ReleaseSpec>();
        // 变体一次批量取（与导出同一处 N+1 收敛；行序与逐组件取逐位一致，见 VariantsByComponent）
        var components = _catalog.ListComponents(projectId, null).ToList();
        var variants = _catalog.VariantsByComponent(components.Select(c => c.Id).ToList());

        foreach (var c in components)
        {
            specs.Add(new ReleaseSpec("component", c.Code, Dict(
                ("category", c.Category), ("interactive", c.Interactive ? "1" : "0"),
                ("tokenRefs", c.TokenRefsJson), ("a11y", c.A11yNotes))));
            foreach (var v in variants[c.Id])
                specs.Add(new ReleaseSpec("variant", $"{c.Code}/{v.Code}", Dict(
                    ("state", v.State), ("theme", v.ThemeId.ToString()),
                    ("axes", v.VariantJson), ("tokenRefs", v.TokenRefsJson))));
        }

        foreach (var a in _catalog.ListAssets(projectId, null))
            specs.Add(new ReleaseSpec("asset", a.Code, Dict(
                ("kind", a.Kind), ("svgBody", a.SvgBody), ("license", a.License), ("tokenRefs", a.TokenRefsJson))));

        foreach (var s in _catalog.ListScreens(projectId))
            specs.Add(new ReleaseSpec("screen", s.Code, Dict(
                ("title", s.Title), ("route", s.Route), ("icon", s.IconCode))));

        foreach (var f in _catalog.ListFonts(projectId).Where(f => f.ProjectId == projectId))
            specs.Add(new ReleaseSpec("font", $"{f.Family}|{f.Weight}|{f.Style}", Dict(
                ("role", f.Role), ("fileName", f.FileName), ("fileRef", f.FileRef),
                ("display", f.Display), ("license", f.License))));

        // UX 规范（M3，schema 3 新增类）：口径与交付一致 —— 不含 archived（归档就是"这一版不再要求它"，diff 报删除是对的）。
        // 只存**原文**（RulesJson/TokenRefsJson 是库里那份），括注值不进快照：值由令牌层自己进快照，避免同一数字存两处。
        List<DesignGuideline> guidelineRows = _guidelines?.List(projectId).ToList() ?? [];
        foreach (var g in guidelineRows)
            specs.Add(new ReleaseSpec("guideline", g.Code, Dict(
                ("category", g.Category), ("status", g.Status), ("title", g.Title), ("summary", g.Summary),
                ("body", g.Body), ("rules", g.RulesJson), ("tokenRefs", g.TokenRefsJson),
                ("appliesTo", g.AppliesToJson), ("source", g.Source),
                ("generatorVersion", g.GeneratorVersion), ("generatorSeed", g.GeneratorSeed))));

        return specs.OrderBy(s => s.Kind, StringComparer.Ordinal).ThenBy(s => s.Key, StringComparer.Ordinal).ToList();

        static Dictionary<String, String?> Dict(params (String Name, String? Value)[] items) =>
            items.ToDictionary(i => i.Name, i => i.Value, StringComparer.Ordinal);
    }

    static String SpecKey(ReleaseSpec s) => s.Kind + "\u0000" + s.Key;

    static String Hash(ReleaseSnapshot snapshot)
    {
        var canon = new StringBuilder();
        foreach (var t in snapshot.Tokens)
            canon.Append(t.Theme).Append('|').Append(t.Path).Append('|').Append(t.Tier).Append('|').Append(t.Type).Append('|')
                 .Append(t.Value).Append('|').Append(t.AliasPath).Append('\n');
        // 规格也进哈希：否则"只换了 logo"会被判定为与上一版内容一致，同版本号重发直接幂等放行
        foreach (var s in snapshot.Specs ?? [])
            foreach (var f in s.Fields.OrderBy(x => x.Key, StringComparer.Ordinal))
                canon.Append(s.Kind).Append('|').Append(s.Key).Append('|').Append(f.Key).Append('|').Append(f.Value).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canon.ToString()))).ToLowerInvariant();
    }

    static String Sanitize(String version) => new(version.Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_').ToArray());

    /// <summary>把快照再投影成 DTCG（给 release 详情页/离线归档用）</summary>
    public String SnapshotToDtcg(ReleaseSnapshot snapshot, String theme)
    {
        var root = new JsonObject();
        foreach (var t in snapshot.Tokens.Where(t => t.Theme == theme || (theme == "shared" && t.Theme == "shared")))
        {
            var leaf = new JsonObject { ["$value"] = JsonValue.Create(t.AliasPath.IsNullOrEmpty() ? t.Value : "{" + t.AliasPath + "}"), ["$type"] = t.Type };
            var segs = t.Path.Split('.');
            var cur = root;
            for (var i = 0; i < segs.Length - 1; i++)
            {
                if (cur[segs[i]] is not JsonObject next) { next = new JsonObject(); cur[segs[i]] = next; }
                cur = next;
            }
            cur[segs[^1]] = leaf;
        }
        return root.ToJsonString(JsonOpts);
    }
}

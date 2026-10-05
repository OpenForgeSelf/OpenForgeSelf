using System.Security.Cryptography;
using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Agent;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DesignSystem.Controllers;

/// <summary>
/// 设计系统底座控制器：项目 / 主题 / 三层令牌 / 组件与变体 / 图标 / 资产 / 页面 / 字体 / 审计。
///
/// 鉴权：宿主没有全局鉴权中间件，鉴权逐控制器显式 —— 本控制器管理类 CRUD，
/// 必须类级 <c>[Authorize("ApiKeyPolicy")]</c>（plugin-development 铁律 17）。
/// 响应：统一 camelCase（插件自带 http.ts 与本契约同源，宿主不解析这层）。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/design-system")]
public class DesignSystemController : ControllerBase
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly DesignProjectService _projects;
    private readonly TokenRepository _tokens;
    private readonly CatalogRepository _catalog;
    private readonly AuditRepository _audits;
    private readonly AuditEngine _auditEngine;
    private readonly ExportService _export;
    private readonly ReleaseService _releases;
    private readonly GenerationService _generation;
    private readonly AgentAccess _agentAccess;
    private readonly DesignReviewService _review;
    private readonly QuickCreateService _quickCreate;
    private readonly DesignBriefBuilder _brief;
    private readonly PreviewCssService _previewCss;
    private readonly GuidelineService _guidelines;

    public DesignSystemController(DesignProjectService projects, TokenRepository tokens, CatalogRepository catalog,
        AuditRepository audits, AuditEngine auditEngine, ExportService export, ReleaseService releases,
        GenerationService generation, AgentAccess agentAccess, DesignReviewService review,
        QuickCreateService quickCreate, DesignBriefBuilder brief, PreviewCssService previewCss, GuidelineService guidelines)
    {
        _projects = projects;
        _tokens = tokens;
        _catalog = catalog;
        _audits = audits;
        _auditEngine = auditEngine;
        _export = export;
        _releases = releases;
        _generation = generation;
        _agentAccess = agentAccess;
        _review = review;
        _quickCreate = quickCreate;
        _brief = brief;
        _previewCss = previewCss;
        _guidelines = guidelines;
    }

    /// <summary>插件自描述：版本三元组 + 能力面清单。前端据 capabilities 对不支持项显式降级。</summary>
    [HttpGet("meta")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Meta() => Data(new
    {
        pluginId = DesignSystemConstants.PluginId,
        modelVersion = DesignSystemConstants.ModelVersion,
        generatorVersion = DesignSystemConstants.GeneratorVersion,
        projectionVersion = DesignSystemConstants.ProjectionVersion,
        tiers = TokenTiers.All,
        tokenTypes = TokenTypes.All,
        lifecycles = TokenLifecycles.All,
        capabilities = new[] { "projects", "themes", "tokens", "effective", "generate", "audit.read", "audit.run", "export", "import", "releases", "releases.diff", "components", "variants", "icons", "assets", "screens", "fonts", "brief", "review", "presets", "quick-create", "preview-css", "agent", "guidelines" },
        agentTools = DesignToolIndex.All.Select(t => t.Name).ToArray(),
        exportFormats = ExportFormats.All,
        // 导入面：格式清单与上限都由后端出，前端据此决定入口是否可用与怎么提示（不另抄一份数字）
        importFormats = ImportFormats.All,
        importLimits = new { maxEntries = ImportLimits.MaxEntries, maxBytes = ImportLimits.MaxBytes },
        // 词表与档位序由后端供给：界面按它排状态/尺寸/角色、并按它做"先选轴再选值"的表单 ——
        // 一份清单（轴 → 档位序），不是三条并列字段：加第四条轴只改 VariantAxes.Orders 一处
        variantAxes = VariantAxes.Vocabulary(),
        // 尺度档位序直接取自生成器那三张表（含 pill/full 这类绝对值档），前端不许再镜像一份档名
        scaleOrders = new
        {
            space = ScaleGenerators.SpaceOrder,
            radius = ScaleGenerators.RadiusOrder,
            duration = ScaleGenerators.DurationOrder,
        },
        // 风格轴词表（M3）：轴/取值/默认值/范围都由 StyleAxes 单点供给，界面按它渲染控件，不许在 TS 里抄
        styleAxes = StyleAxes.Vocabulary(),
        // 规范分类词表（M3）：界面的分组与筛选按它排，新增分类只改后端 GuidelineCategories 一处
        guidelineCategories = GuidelineCategories.All,
        guidelineLevels = GuidelineCategories.Levels,
        // 色族序 = 生成器逐族产阶用的那张表（同一处消费：界面排色阶条带不许自己抄一份）
        colorFamilies = ColorFamilies.All,
        // 审计类别序 = 后端 AuditKinds 的展示序；界面据此排筛选下拉，新增一类不必改前端
        auditKinds = AuditKinds.All,
        // 十类逻辑实体（Stardust 兼容只读投影）：界面「导出交付」按它列真实 url，不另抄一份
        entities = ExportService.StardustEntities,
    });

    #region 项目 / 主题

    [HttpGet("projects")]
    public IActionResult ListProjects([FromQuery] String? status, [FromQuery] String? keyword) =>
        Data(_projects.List(status, keyword).Select(ProjectDto));

    [HttpPost("projects")]
    public IActionResult CreateProject([FromBody] ProjectInput input) => Guard(() => ProjectDto(_projects.Create(input)));

    [HttpGet("projects/{id:long}")]
    public IActionResult GetProject(Int64 id) => Guard(() => ProjectDto(RequireProject(id)));

    [HttpPut("projects/{id:long}")]
    public IActionResult UpdateProject(Int64 id, [FromBody] ProjectPatch patch)
    {
        RequireProject(id);
        return Guard(() => ProjectDto(_projects.Update(id, patch)));
    }

    /// <summary>归档（软删）。数据行与库文件一律保留。</summary>
    [HttpPost("projects/{id:long}/archive")]
    public IActionResult ArchiveProject(Int64 id)
    {
        RequireProject(id);
        return Guard(() => ProjectDto(_projects.Archive(id)));
    }

    [HttpGet("projects/{id:long}/themes")]
    public IActionResult ListThemes(Int64 id)
    {
        RequireProject(id);
        return Data(_projects.ListThemes(id).Select(DesignMapper.ToDto));
    }

    [HttpPost("projects/{id:long}/themes")]
    public IActionResult AddTheme(Int64 id, [FromBody] ThemeInput input)
    {
        RequireProject(id);
        return Guard(() => _projects.AddTheme(id, input));
    }

    #endregion

    #region 令牌

    /// <summary>分页读取令牌原行（未解析别名）</summary>
    [HttpGet("projects/{id:long}/tokens")]
    public IActionResult ListTokens(Int64 id, [FromQuery] Int64? themeId = null, [FromQuery] String? tier = null,
        [FromQuery] String? group = null, [FromQuery] String? q = null, [FromQuery] Int32 page = 1, [FromQuery] Int32 pageSize = 200)
    {
        RequireProject(id);
        var result = _tokens.List(id, themeId, tier, group, q, page, pageSize);
        return Data(new { items = result.Items.Select(DesignMapper.ToDto).ToList(), total = result.Total, page = result.Page, pageSize = result.PageSize });
    }

    /// <summary>有效令牌视图：共享层 + 主题覆盖合并后逐条解析别名，附诊断（design G4）</summary>
    [HttpGet("projects/{id:long}/tokens/effective")]
    public IActionResult EffectiveTokens(Int64 id, [FromQuery] String? theme)
    {
        RequireProject(id);
        var themeId = _projects.ResolveThemeId(id, theme);
        var graph = _tokens.LoadGraph(id, themeId > 0 ? themeId : null, theme);

        var items = new List<EffectiveToken>();
        var problems = new List<TokenDiagnostic>();
        // 正文色的判级基准 = 本主题的背景令牌；它自己就是背景时无意义，判成 Decorative（不报警）
        var bg = graph.ResolveColor("semantic.surface-bg");
        foreach (var node in graph.All().OrderBy(n => n.Path, StringComparer.Ordinal))
        {
            var r = graph.Resolve(node.Path);
            if (!r.IsOk) problems.Add(new TokenDiagnostic(node.Path, r.Status, r.Status.ToString()));

            String? hex = null;
            var ratio = -1d;
            String? level = null;
            if (node.Type == TokenTypes.Color)
            {
                var color = graph.ResolveColor(node.Path);
                if (color != null)
                {
                    hex = Oklch.ToRgb8(color.Value).ToHex();
                    if (bg != null && node.Path != "semantic.surface-bg")
                    {
                        ratio = ContrastMath.Ratio(color.Value, bg.Value);
                        level = ContrastMath.LevelTag(ContrastMath.Judge(ratio, ContrastMath.Usage.TextNormal));
                    }
                }
            }

            items.Add(new EffectiveToken(node.Path, node.Tier, node.Type, r.IsOk ? r.Value : node.Value ?? "", r.SourcePath,
                node.AliasPath, r.IsOk, r.IsOk ? null : r.Status.ToString(), hex, ratio, level,
                node.Description, node.ValueJson, node.Extensions));
        }

        return Data(new { theme, themeId, count = items.Count, items, diagnostics = problems });
    }

    /// <summary>单条写入（点即保存 + 部分更新）</summary>
    [HttpPost("projects/{id:long}/tokens")]
    public IActionResult UpsertToken(Int64 id, [FromBody] TokenPatch patch)
    {
        RequireProject(id);
        return Write(() => _tokens.UpsertOne(id, patch));
    }

    /// <summary>批量写入：校验失败整批回滚并返回明细（AC4）</summary>
    [HttpPost("projects/{id:long}/tokens/batch")]
    public IActionResult UpsertTokens(Int64 id, [FromBody] TokenBatchInput input)
    {
        RequireProject(id);
        return Write(() => _tokens.UpsertBatch(id, input.Items ?? [], input.Overwrite));
    }

    /// <summary>
    /// 导入预览（FR-I2）：与 <c>import</c> 共用同一份解析，但**一条都不写**。
    /// 返回"将写入 / 冲突（受手改保护）/ 被拒"三张清单 —— 用户先看清差异再决定；
    /// 库没有回滚，把"真的导入"当确认手段就是拿数据当试验品。
    /// </summary>
    [HttpPost("projects/{id:long}/import/preview")]
    public Task<IActionResult> ImportPreview(Int64 id, [FromQuery] String format = ImportFormats.Dtcg,
                                             [FromQuery] String? theme = null, [FromQuery] Boolean overwrite = false) =>
        WithImport(id, format, theme, overwrite, plan => Task.FromResult<object?>(DescribePlan(plan)));

    /// <summary>
    /// 导入落库（FR-I1）：解析后**只经 `TokenRepository.UpsertBatch`**（整批事务，图校验不过零行落库）。
    /// 不代跑审计：审计结论必须是"用户自己看到的当前状态"，静默重跑会让人把导入前的门禁结果当成本批结果。
    /// </summary>
    [HttpPost("projects/{id:long}/import")]
    public Task<IActionResult> Import(Int64 id, [FromQuery] String format = ImportFormats.Dtcg,
                                      [FromQuery] String? theme = null, [FromQuery] Boolean overwrite = false) =>
        WithImport(id, format, theme, overwrite, plan =>
        {
            var result = _tokens.UpsertBatch(id, plan.Patches, overwrite);
            return Task.FromResult<object?>(new
            {
                created = result.Created,
                updated = result.Updated,
                skippedProtected = result.SkippedProtected,
                conflicts = result.Conflicts,
                diagnostics = result.Diagnostics,
                rejected = plan.Rejected,
                counts = plan.Counts,
                auditHint = result.Succeeded
                    ? "已落库。请重跑审计：新令牌的对比度/分层/命名结论要重新算（本端点不代跑）"
                    : "整批已回滚（零行落库）：按 diagnostics 修文件后重来",
            });
        });

    /// <summary>
    /// 导入的两个端点共用这条链：读原文 → 校验格式/主题/上限 → 解析 → 交给调用方。
    ///
    /// 为什么自己读 body 而不是 <c>[FromBody] JsonElement</c>：FR-I6 要把**上传字节的 sha256** 记进
    /// <c>GeneratorSeed</c>（"这一批是从哪份文件来的"），经过模型绑定就再也拿不回原文了。
    /// </summary>
    async Task<IActionResult> WithImport(Int64 id, String format, String? theme, Boolean overwrite,
                                         Func<ImportPlan, Task<object?>> apply)
    {
        RequireProject(id);
        try
        {
            using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            var raw = await reader.ReadToEndAsync();
            if (raw.IsNullOrWhiteSpace()) return BadRequest(ErrorBody("请求体是空的：导入需要一份 DTCG JSON"));

            var bytes = System.Text.Encoding.UTF8.GetByteCount(raw);
            if (bytes > ImportLimits.MaxBytes)
                return BadRequest(ErrorBody($"内容 {bytes} 字节，超过单次上限 {ImportLimits.MaxBytes} 字节"));

            if (!ImportFormats.IsKnown(format))
                return BadRequest(ErrorBody($"未实现的导入格式：{format}（当前只支持 {String.Join('/', ImportFormats.All)}）"));

            if (!theme.IsNullOrEmpty() && theme != "shared" &&
                !_projects.ListThemes(id).Any(t => string.Equals(t.Code, theme, StringComparison.Ordinal)))
                return BadRequest(ErrorBody($"主题 {theme} 不在项目 {id} 的主题清单里（导入不新建主题）"));

            JsonElement doc;
            try
            {
                doc = JsonDocument.Parse(raw).RootElement;
            }
            catch (JsonException ex)
            {
                return BadRequest(ErrorBody($"不是合法 JSON：{ex.Message}"));
            }

            var seed = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            var themeId = _projects.ResolveThemeId(id, theme);
            var (themeByPath, protectedKeys) = _tokens.ImportLookups(id, themeId);
            var plan = DtcgImporter.Parse(doc, themeId, themeByPath, protectedKeys, seed, overwrite);
            return Data(await apply(plan));
        }
        catch (ImportException ex)
        {
            return BadRequest(ErrorBody(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorBody(ex.Message));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DesignSystem] 导入失败: {0}", ex);
            return StatusCode(500, ErrorBody($"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>preview 的响应形状：三张清单 + 计数，全部来自解析器，界面不另算一遍差异。</summary>
    static object DescribePlan(ImportPlan plan) => new
    {
        format = ImportFormats.Dtcg,
        documentProject = plan.DocumentProject,
        counts = plan.Counts,
        willWrite = plan.Patches.Select(p => new
        {
            path = p.Path,
            tier = p.Tier,
            type = p.Type,
            themeId = p.ThemeId,
            alias = p.AliasPath,
            value = p.ValueJson ?? p.Value,
        }).ToList(),
        rejected = plan.Rejected,
        conflicts = plan.ConflictPaths,
    };

    /// <summary>退役令牌（软删：Lifecycle=removed）</summary>
    [HttpPost("projects/{id:long}/tokens/retire")]
    public IActionResult RetireToken(Int64 id, [FromQuery] Int64 themeId = 0, [FromQuery] String path = "", [FromQuery] String? replacedBy = null)
    {
        RequireProject(id);
        if (String.IsNullOrWhiteSpace(path)) return BadRequest(ErrorBody("path 不能为空"));
        var done = _tokens.Retire(id, themeId, path, replacedBy);
        return done ? Data(new { retired = path }) : NotFound(ErrorBody($"令牌 {path}（主题 {themeId}）不存在"));
    }

    /// <summary>写 shadow 复合令牌的展开层（真源仍是令牌的 valueJson，本表为查询投影）</summary>
    [HttpPost("projects/{id:long}/tokens/{tokenPath}/shadow-layers")]
    public IActionResult ReplaceShadowLayers(Int64 id, String tokenPath, [FromBody] ShadowLayerBatchInput input)
    {
        RequireProject(id);
        var themeId = _projects.ResolveThemeId(id, input.Theme is { Length: > 0 } ? input.Theme : null);
        var token = _tokens.Find(id, themeId, tokenPath);
        if (token == null) return NotFound(ErrorBody($"令牌 {tokenPath} 不存在"));
        _tokens.ReplaceShadowLayers(token, input.Layers ?? []);
        return Data(new { tokenPath, layers = _tokens.FindShadowLayers(token.Id).Count });
    }

    #endregion

    #region 生成与审计

    /// <summary>
    /// 由种子色/参数（可选 brief）确定性生成三层令牌 + 多主题语义层 + 组件层，整批落库后立即跑审计。
    /// 同参数必得同一 seed 与同一令牌集（AC5）；手改过的行默认不被覆盖（AC18）。
    /// </summary>
    [HttpPost("projects/{id:long}/generate")]
    public IActionResult Generate(Int64 id, [FromBody] GenerationRequest? request, [FromQuery] Boolean overwrite = false)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var outcome = _generation.Run(id, request ?? new GenerationRequest(), overwrite);
            var result = outcome.Result;
            var seed = outcome.Components;
            var brand = outcome.Brand;
            return new
            {
                seed = result.Seed,
                industry = result.Industry,
                hue = result.Hue,
                tokens = result.Total,
                components = seed.Components,
                variants = seed.Variants,
                fonts = brand.Fonts,
                screens = brand.Screens,
                assets = brand.Assets,
                themes = result.Themed.Keys.ToList(),
                notes = result.Notes,
                skippedProtected = result.SkippedProtected,
                conflicts = result.Conflicts,
                audit = outcome.Audit,
                // 规范条数由后端种子流程给（只补空，故是"现存"），界面据此判断"生成后规范到底有没有"
                guidelines = outcome.Guidelines,
            };
        });
    }

    /// <summary>生成参数预览（不落库）：用于界面上的"先看效果再写入"</summary>
    [HttpPost("generate/preview")]
    public IActionResult GeneratePreview([FromBody] GenerationRequest? request) =>
        Guard(() =>
        {
            var result = DesignGenerator.Generate(request ?? new GenerationRequest());
            return new
            {
                seed = result.Seed,
                industry = result.Industry,
                hue = result.Hue,
                notes = result.Notes,
                shared = result.Shared.Count,
                themes = result.Themed.ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                sample = result.Shared.Where(p => p.Type == TokenTypes.Color).Take(12)
                    .Select(p => new { p.Path, p.Value }).ToList(),
            };
        });

    /// <summary>
    /// 内存预览 CSS（不落库）：展厅滑动生成参数时"立刻看到的那份换肤 CSS"。
    /// 与落库导出同源（共用 ExportService 的构造与 ToCss 投影），故预览所见即交付所得。
    /// </summary>
    [HttpPost("generate/preview-css")]
    public IActionResult GeneratePreviewCss([FromBody] PreviewCssInput? input) =>
        Guard(() =>
        {
            var r = _previewCss.Preview(input?.ToRequest(), input?.Theme);
            return new { theme = r.Theme, css = r.Css, seed = r.Seed, industry = r.Industry, hue = r.Hue, notes = r.Notes };
        });

    /// <summary>跑审计并落库（Critical 未清会在这里现形，发布端点据此拒绝）</summary>
    [HttpPost("projects/{id:long}/audit")]
    public IActionResult RunAudit(Int64 id, [FromQuery] Int64 releaseId = 0)
    {
        RequireProject(id);
        return Guard(() => _auditEngine.Run(id, releaseId));
    }

    #endregion

    #region UX 规范（M3）

    /// <summary>
    /// 规范清单。默认**不含 archived**（归档是软删，仍可按 <c>status=all|archived</c> 读到并可恢复）。
    /// 每条附 <c>tokenValues</c> 与 <c>brokenRefs</c>：引用令牌在取值主题里取不到值时逐条列出，不静默（§G4）。
    /// </summary>
    [HttpGet("projects/{id:long}/guidelines")]
    public IActionResult ListGuidelines(Int64 id, [FromQuery] String? status, [FromQuery] String? category, [FromQuery] String? theme)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var (valueOf, valueTheme) = GuidelineValues(id, theme);
            return _guidelines.List(id, status, category).Select(g => GuidelineDto(g, valueOf, valueTheme)).ToList();
        });
    }

    [HttpGet("projects/{id:long}/guidelines/{code}")]
    public IActionResult GetGuideline(Int64 id, String code, [FromQuery] String? theme)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var (valueOf, valueTheme) = GuidelineValues(id, theme);
            var g = _guidelines.Find(id, code) ?? throw new KeyNotFoundException($"规范 {code} 不存在");
            return GuidelineDto(g, valueOf, valueTheme);
        });
    }

    /// <summary>
    /// 新增或更新一条规范（upsert）。写入即置 <c>Source=manual</c>：用户改过的东西不该被下次重新生成悄悄覆盖。
    /// 带 <c>expectUpdatedAt</c> 时做乐观并发，不一致回 409（不写）。引用的令牌不存在 → 400 并逐条列出。
    /// </summary>
    [HttpPut("projects/{id:long}/guidelines/{code}")]
    public IActionResult SaveGuideline(Int64 id, String code, [FromBody] GuidelinePatch patch)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var (valueOf, valueTheme) = GuidelineValues(id, null);
            return GuidelineDto(_guidelines.Save(id, code, patch), valueOf, valueTheme);
        });
    }

    /// <summary>生成/补齐默认规范。只补空：已存在的不动，手改行更不动（在 skippedProtected 里点名）</summary>
    [HttpPost("projects/{id:long}/guidelines/generate")]
    public IActionResult GenerateGuidelines(Int64 id, [FromQuery] Boolean overwrite = false)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var r = _guidelines.Generate(id, overwrite);
            return new { created = r.Created, skipped = r.Skipped, skippedProtected = r.SkippedProtected, overwritten = r.Overwritten, total = r.Total };
        });
    }

    /// <summary>归档（软删）。本插件**不提供任何删除端点**，恢复 = PUT 回来带 status=adopted。</summary>
    [HttpPost("projects/{id:long}/guidelines/{code}/archive")]
    public IActionResult ArchiveGuideline(Int64 id, String code)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var (valueOf, valueTheme) = GuidelineValues(id, null);
            var g = _guidelines.Archive(id, code) ?? throw new KeyNotFoundException($"规范 {code} 不存在");
            return GuidelineDto(g, valueOf, valueTheme);
        });
    }

    /// <summary>
    /// 规范出参的取值口径：直接借用导出侧那一份视图与取值函数（<see cref="ExportService.GuidelineView"/> +
    /// <see cref="ExportService.ValueOf"/>），REST / brief / design-md / bundle / 界面 chip 从此是同一个函数的调用。
    /// 主题口径：请求主题 → 项目默认色彩主题 → 首个非密度主题（规范引用的语义/阴影令牌只在主题层，
    /// 共享层视图会把它们整片判成断链 —— M3 实测踩过，由 GuidelineRestTests 钉住）。
    /// 第二项是实际取值的主题编码，出参里作为 <c>valueTheme</c> 交代清楚，界面不许假装它是"当前主题"。
    /// </summary>
    private (Func<String, String?> ValueOf, String? Theme) GuidelineValues(Int64 projectId, String? theme)
    {
        var view = _export.GuidelineView(_export.Load(projectId, theme));
        return (ExportService.ValueOf(view), view.ThemeCode);
    }

    static Object GuidelineDto(Entities.DesignGuideline g, Func<String, String?> valueOf, String? valueTheme)
    {
        var rules = GuidelineRepository.ReadRules(g.RulesJson);
        var refs = GuidelineRepository.ReadPaths(g.TokenRefsJson);
        var broken = new List<String>();
        foreach (var r in rules)
            foreach (var p in GuidelineRenderer.ConcreteRefs(r.Text))
                if (valueOf(p).IsNullOrEmpty() && !refs.Contains(p)) broken.Add(p);
        foreach (var p in refs.Where(p => valueOf(p).IsNullOrEmpty())) broken.Add(p);

        return new
        {
            code = g.Code,
            category = g.Category,
            categoryLabel = GuidelineCategories.Display(g.Category),
            title = g.Title,
            // 摘要与正文走同一个标注函数：界面上"一句话摘要"里出现的间距档必须和令牌页当前值一致
            summary = GuidelineRenderer.Annotate(g.Summary, valueOf),
            // 正文按纯文本给（界面与导出都不做 HTML 渲染），但当前值括注由同一个渲染函数加
            body = GuidelineRenderer.Annotate(g.Body, valueOf),
            bodyRaw = g.Body,
            rules = rules.Select(r => new { id = r.Id, level = r.Level, text = GuidelineRenderer.Annotate(r.Text, valueOf), textRaw = r.Text }).ToList(),
            tokenRefs = refs,
            // 界面 chip 直接读这份值：前端自己再查一次 effective 就等于给"这条规范说多少"写第二份真相
            tokenValues = refs.ToDictionary(p => p, p => valueOf(p), StringComparer.Ordinal),
            valueTheme,
            brokenRefs = broken.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            appliesTo = GuidelineRepository.ReadPaths(g.AppliesToJson),
            source = g.Source,
            status = g.Status,
            generatorVersion = g.GeneratorVersion,
            generatorSeed = g.GeneratorSeed,
            sortOrder = g.SortOrder,
            updatedAt = g.UpdatedAt,
        };
    }

    #endregion

    #region 导出

    /// <summary>单格式导出（text 类直接返回文件下载；json 同理）</summary>
    [HttpGet("projects/{id:long}/export")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Export(Int64 id, [FromQuery] String format = "dtcg", [FromQuery] String? theme = null)
    {
        RequireProject(id);
        if (!ExportFormats.IsKnown(format))
            return BadRequest(ErrorBody($"未知导出格式 {format}，可用：{String.Join(",", ExportFormats.All)}"));

        try
        {
            var file = _export.Produce(id, format, theme);
            return new FileContentResult(file.Bytes, file.ContentType) { FileDownloadName = file.Name };
        }
        catch (KeyNotFoundException ex) { return NotFound(ErrorBody(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ErrorBody(ex.Message)); }
    }

    /// <summary>导出能力自描述：格式清单 + 每种格式的文件名规则（前端导出台据此渲染）</summary>
    [HttpGet("projects/{id:long}/export/formats")]
    public IActionResult ListExportFormats(Int64 id)
    {
        RequireProject(id);
        return Data(new
        {
            projectionVersion = DesignSystemConstants.ProjectionVersion,
            formats = ExportFormats.All.Select(f => new { name = f, bundle = f == ExportFormats.Bundle }),
        });
    }

    #endregion

    #region Agent 接入（M1：设计系统对外部智能体的桥）

    /// <summary>设计说明书（BRIEF）：REST 与 design_lookup export=brief 同源（同 DesignBriefBuilder）。</summary>
    [HttpGet("projects/{id:long}/brief")]
    public IActionResult Brief(Int64 id, [FromQuery] String? theme = null, [FromQuery] String? sections = null,
        [FromQuery] Int32 maxChars = 60000, [FromQuery] String format = "markdown")
    {
        RequireProject(id);
        return Guard(() =>
        {
            var outcome = _brief.Build(id, theme, sections?.Split(',').Where(s => !s.IsNullOrWhiteSpace()).ToArray(),
                Math.Clamp(maxChars, 2000, 60000), format);
            return new
            {
                theme = outcome.Theme, contentHash = outcome.ContentHash, sections = outcome.Sections,
                omitted = outcome.Omitted, truncated = outcome.Truncated, markdown = outcome.Markdown, notes = outcome.Notes,
            };
        });
    }

    /// <summary>设计审查（代码/样式反例检查）：REST 与 design_review 同源。</summary>
    [HttpPost("projects/{id:long}/review")]
    public IActionResult Review(Int64 id, [FromBody] ReviewRequest? body)
    {
        RequireProject(id);
        return Guard(() =>
        {
            _review.LoadIndex(id, body?.Theme, out var theme, out var notesList);
            var note = notesList.FirstOrDefault();
            var files = body?.Files?.Select(f => new ReviewInput(f.Path ?? "", f.Content ?? "", f.Language)).ToList() ?? [];
            if (!body?.Code.IsNullOrEmpty() ?? true)
                files.Add(new ReviewInput("input.css", body!.Code!, "css"));
            var outcome = _review.Review(id, theme, files, body?.Strict ?? false, body?.MaxFindings ?? 50);
            return new
            {
                error = outcome.Error, summary = outcome.Summary, findings = outcome.Findings,
                skipped = outcome.Skipped, truncated = outcome.Truncated, notes = string.Join("\n", outcome.Notes),
                theme, themeNote = note,
            };
        });
    }

    /// <summary>内置风格预设清单（design_presets action=list 的 REST 同源）。</summary>
    [HttpGet("presets")]
    public IActionResult ListPresets() => Data(StylePresets.All.Select(p => new
    {
        p.Id, p.Name, p.Tagline, p.Tones, p.Kinds, p.Industries, p.Keywords,
    }));

    /// <summary>按简述推荐预设（design_presets action=recommend 的 REST 同源）。</summary>
    [HttpPost("presets/recommend")]
    public IActionResult RecommendPreset([FromBody] PresetRecommendRequest? body)
    {
        var matches = PresetRecommender.Recommend(body?.Brief, body?.Kind, body?.Industry,
            body?.Tone == null ? null : [body.Tone], body?.Density, body?.BrandColor, body?.Limit ?? 3);
        return Data(matches.Select(m => new
        {
            m.Id, m.Name, m.Tagline, m.Score, m.Reasons, Request = m.Request,
        }));
    }

    /// <summary>一键创建项目（design_create 的 REST 同源）。dryRun=false 落库。</summary>
    [HttpPost("projects/quick-create")]
    public IActionResult QuickCreate([FromBody] QuickCreateRequest? body)
    {
        var dryRun = body?.DryRun ?? false;
        return Guard(() =>
        {
            _quickCreate.Create(
                body?.Name ?? "", body?.Code, body?.Kind, body?.Description, body?.Preset,
                body?.Request ?? new GenerationRequest(), !dryRun,
                out var uiRoute, out var applied, out var error);
            if (error != null) throw new ArgumentException(error);
            return new
            {
                uiRoute, applied, dryRun,
                project = applied?.Project == null ? null : new { applied.Project.Code, applied.Project.Name, applied.Project.Status },
                warnings = applied?.Warnings,
            };
        });
    }

    /// <summary>写入开关（design_* 工具的写动作门禁）。</summary>
    [HttpGet("agent-access")]
    public IActionResult GetAgentAccess()
    {
        var (allow, source, corrupt, updatedAt) = _agentAccess.Get();
        return Data(new { allowWrite = allow, source, corrupt, updatedAt });
    }

    [HttpPut("agent-access")]
    public IActionResult SetAgentAccess([FromBody] AgentAccessPatch? body)
    {
        if (body == null || !body.Enabled.HasValue)
            return BadRequest(ErrorBody("请提供 { enabled: true|false }"));
        return Guard(() =>
        {
            _agentAccess.Set(body.Enabled.Value);
            var (allow, source, corrupt, updatedAt) = _agentAccess.Get();
            return new { allowWrite = allow, source, corrupt, updatedAt };
        });
    }

    /// <summary>agent 工具清单（design_guide / meta.agentTools 同源，供外部枚举）。</summary>
    [HttpGet("agent/tools")]
    public IActionResult ListAgentTools() => Data(DesignToolIndex.All.Select(t => new
    {
        t.Name, t.Summary, ReadOnly = t.Kind == "read", Parameters = t.Schema,
    }));

    #endregion

    #region Stardust 兼容实体明细（FR13）

    /// <summary>
    /// 逻辑实体明细：<c>{entity, source, generated, total, data[]}</c> 裸 JSON（不走 success/data 信封 —— 参考物的查看器直连这个形状）。
    /// <c>export?format=stardust-json</c> 清单里每行的 url 就指这里；插座没人实现就是假声明，所以这条路由与那份清单一同交付。
    /// </summary>
    [HttpGet("{id:long}/{entity}.json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Entity(Int64 id, String entity, [FromQuery] String? theme = null)
    {
        RequireProject(id);
        if (!ExportService.IsKnownEntity(entity))
            return BadRequest(ErrorBody($"未知逻辑实体 {entity}，可用：{String.Join(",", ExportService.StardustEntities)}"));

        try
        {
            return Content(_export.ToStardustEntity(_export.Load(id, theme), entity), "application/json");
        }
        catch (KeyNotFoundException ex) { return NotFound(ErrorBody(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ErrorBody(ex.Message)); }
    }

    #endregion

    #region 组件 / 变体

    [HttpGet("projects/{id:long}/components")]
    public IActionResult ListComponents(Int64 id, [FromQuery] String? category)
    {
        RequireProject(id);
        return Data(_catalog.ListComponents(id, category).Select(DesignMapper.ToDto));
    }

    [HttpPost("projects/{id:long}/components")]
    public IActionResult SaveComponent(Int64 id, [FromBody] ComponentInput input)
    {
        RequireProject(id);
        return Guard(() => _catalog.SaveComponent(id, input));
    }

    [HttpGet("projects/{id:long}/components/{code}/variants")]
    public IActionResult ListVariants(Int64 id, String code)
    {
        RequireProject(id);
        var c = _catalog.FindComponent(id, code);
        if (c == null) return NotFound(ErrorBody($"组件 {code} 不存在"));
        return Data(_catalog.ListVariants(c.Id).Select(DesignMapper.ToDto));
    }

    [HttpPost("projects/{id:long}/components/{code}/variants")]
    public IActionResult SaveVariant(Int64 id, String code, [FromBody] VariantInput input)
    {
        RequireProject(id);
        return Guard(() =>
        {
            var c = _catalog.FindComponent(id, code) ?? throw new KeyNotFoundException($"组件 {code} 不存在");
            return _catalog.SaveVariant(id, c, input);
        });
    }

    #endregion

    #region 图标 / 资产 / 页面 / 字体

    [HttpGet("icons")]
    public IActionResult ListIcons([FromQuery] Int64 projectId, [FromQuery] String? collection, [FromQuery] String? q) =>
        Data(_catalog.ListIcons(projectId, collection, q).Select(DesignMapper.ToDto));

    [HttpPost("projects/{id:long}/icons")]
    public IActionResult SaveIcon(Int64 id, [FromBody] IconInput input)
    {
        RequireProject(id);
        return Guard(() => _catalog.SaveIcon(id, input));
    }

    [HttpGet("projects/{id:long}/assets")]
    public IActionResult ListAssets(Int64 id, [FromQuery] String? kind)
    {
        RequireProject(id);
        return Data(_catalog.ListAssets(id, kind).Select(DesignMapper.ToDto));
    }

    [HttpGet("projects/{id:long}/screens")]
    public IActionResult ListScreens(Int64 id)
    {
        RequireProject(id);
        return Data(_catalog.ListScreens(id).Select(DesignMapper.ToDto));
    }

    [HttpGet("projects/{id:long}/fonts")]
    public IActionResult ListFonts(Int64 id)
    {
        RequireProject(id);
        return Data(_catalog.ListFonts(id).Select(DesignMapper.ToDto));
    }

    // 三张表过去只有 GET：capabilities 声明了 assets/screens/fonts，却没有写入口也没人种数据，
    // 界面只能永远显示"无…"。声明了就必须给得出（要么可写、要么撤下声明）。
    [HttpPost("projects/{id:long}/assets")]
    public IActionResult SaveAsset(Int64 id, [FromBody] AssetRequest request) =>
        Guard(() => _catalog.SaveAsset(id, request.Code, request.Name, request.Kind, request.SvgBody,
            request.FileRef, request.TokenRefsJson, request.Description, request.License));

    [HttpPost("projects/{id:long}/screens")]
    public IActionResult SaveScreen(Int64 id, [FromBody] ScreenRequest request) =>
        Guard(() => _catalog.SaveScreen(id, request.Code, request.Title, request.IconCode, request.Route,
            request.ComponentIdsJson, request.Description, request.Notes, request.ThemeId, request.SortOrder));

    [HttpPost("projects/{id:long}/fonts")]
    public IActionResult SaveFont(Int64 id, [FromBody] FontRequest request) =>
        Guard(() => _catalog.SaveFont(id, request.Family, request.Weight, request.Style, request.FileName,
            request.FileRef, request.Display, request.Role, request.SourceUrl, request.License, request.MetricsJson));

    #endregion

    #region 审计（只读；审计引擎与写入口在 M2）

    [HttpGet("projects/{id:long}/audit")]
    public IActionResult ListAudit(Int64 id, [FromQuery] String? kind = null, [FromQuery] Boolean? passed = null, [FromQuery] Int64 releaseId = 0)
    {
        RequireProject(id);
        return Data(new
        {
            items = _audits.List(id, releaseId, kind, passed).Select(DesignMapper.ToDto),
            summary = _audits.Summarize(id, releaseId),
            blocking = _audits.HasBlocking(id, releaseId),
        });
    }

    #endregion

    #region 版本快照（发布与 diff）

    /// <summary>建不可变发布快照。存在未通过的 critical 审计 → 409；同版本号内容不同 → 409。</summary>
    [HttpPost("projects/{id:long}/releases")]
    public IActionResult CreateRelease(Int64 id, [FromBody] ReleaseRequest request) =>
        Guard(() => DesignMapper.ToDto(_releases.Create(id, request.Version, request.Notes, request.SourceReleaseId)));

    [HttpGet("projects/{id:long}/releases")]
    public IActionResult ListReleases(Int64 id)
    {
        RequireProject(id);
        return Data(_releases.List(id).Select(DesignMapper.ToDto));
    }

    /// <summary>两个快照之间的令牌级差异（新增/删除/改值/改别名/改类型/改档 + 主题集合变化）。</summary>
    [HttpGet("projects/{id:long}/releases/diff")]
    public IActionResult DiffReleases(Int64 id, [FromQuery] Int64 from, [FromQuery] Int64 to)
    {
        RequireProject(id);
        var diff = _releases.Diff(from, to);
        return Data(new
        {
            from = diff.From,
            to = diff.To,
            added = diff.Added,
            removed = diff.Removed,
            changed = diff.Changed,
            missingThemes = diff.MissingThemes,
            specsAdded = diff.SpecsAdded,
            specsRemoved = diff.SpecsRemoved,
            specsChanged = diff.SpecsChanged,
            // 旧快照（schema 1）没有这一节：界面必须显示"不可比"，而不是把整节报成新增
            specsComparable = diff.SpecsComparable,
            // M3：整节能比但某一类（guideline）在旧 schema 里从来没记过 → 界面写"这一类无法比较"，不写"没有变化"
            notComparableKinds = diff.NotComparableKinds ?? [],
            total = diff.Total,
            isEmpty = diff.IsEmpty,
        });
    }

    /// <summary>快照详情；<c>format=dtcg</c> 时直接回投影文本（离线归档/对比用）。</summary>
    [HttpGet("projects/{id:long}/releases/{releaseId:long}")]
    public IActionResult GetRelease(Int64 id, Int64 releaseId, [FromQuery] String? format = null, [FromQuery] String theme = "light")
    {
        RequireProject(id);
        var release = _releases.Find(releaseId) ?? throw new KeyNotFoundException($"发布 {releaseId} 不存在");
        if (release.ProjectId != id) throw new KeyNotFoundException($"发布 {releaseId} 不属于项目 {id}");
        if (string.IsNullOrWhiteSpace(format)) return Data(DesignMapper.ToDto(release));

        var snapshot = _releases.Load(releaseId);
        if (string.Equals(format, "dtcg", StringComparison.OrdinalIgnoreCase))
            return Data(new { version = snapshot.Version, theme, content = _releases.SnapshotToDtcg(snapshot, theme) });
        return Data(new { version = snapshot.Version, generatedAt = snapshot.GeneratedAt, tokens = snapshot.Tokens });
    }

    #endregion

    #region 内部

    /// <summary>统一异常→状态码映射：冲突 409、缺失 404、入参 400，其余 500 并把原因写日志（操作失败必须可查）。</summary>
    private IActionResult Guard(Func<object?> action)
    {
        try
        {
            return Data(action());
        }
        catch (DesignConflictException ex)
        {
            return Conflict(ErrorBody(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorBody(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorBody(ex.Message));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DesignSystem] 请求失败: {0}", ex);
            return StatusCode(500, ErrorBody($"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private DesignProject RequireProject(Int64 id) =>
        _projects.Find(id) ?? throw new KeyNotFoundException($"项目 {id} 不存在");

    /// <summary>
    /// 项目出参统一走这里：计数现算、且绝不把 XCode 实体直接序列化出去。
    /// 表里的 TokenCount/ComponentCount 缓存列没人维护，直接回给界面就是"有效令牌 250 / 令牌数 0"这种假数字。
    /// </summary>
    private Object ProjectDto(DesignProject p) =>
        DesignMapper.ToDto(p, _projects.CountTokens(p.Id), _projects.CountComponents(p.Id));

    private IActionResult Data(object? data) => new JsonResult(new { success = true, data }, JsonOpts);

    private static Object ErrorBody(String message) => new { success = false, error = message };

    private IActionResult Write(Func<UpsertResult> action)
    {
        try
        {
            var r = action();
            return r.Succeeded
                ? Data(new { created = r.Created, updated = r.Updated, skippedProtected = r.SkippedProtected, conflicts = r.Conflicts })
                : BadRequest(new { success = false, error = "令牌校验未通过，本批已全部回滚（零行落库）", diagnostics = r.Diagnostics, conflicts = r.Conflicts });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DesignSystem] 令牌写入失败: {0}", ex);
            return StatusCode(500, ErrorBody($"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    #endregion
}

/// <summary>批量令牌写入输入</summary>
public sealed class TokenBatchInput
{
    public List<TokenPatch>? Items { get; set; }
    public Boolean Overwrite { get; set; }
}

/// <summary>阴影层批量输入</summary>
public sealed class ShadowLayerBatchInput
{
    public String? Theme { get; set; }
    public List<ShadowLayerInput>? Layers { get; set; }
}

// ---- Agent 接入 REST 请求体（M1）----

/// <summary>POST projects/{id}/review 输入</summary>
public sealed class ReviewRequest
{
    public List<ReviewFileInput>? Files { get; set; }
    public String? Code { get; set; }
    public String? Theme { get; set; }
    public Boolean Strict { get; set; }
    public Int32 MaxFindings { get; set; } = 50;
}

public sealed class ReviewFileInput
{
    public String? Path { get; set; }
    public String? Content { get; set; }
    public String? Language { get; set; }
}

/// <summary>POST presets/recommend 输入</summary>
public sealed class PresetRecommendRequest
{
    public String? Brief { get; set; }
    public String? Kind { get; set; }
    public String? Industry { get; set; }
    public String? Tone { get; set; }
    public String? Density { get; set; }
    public String? BrandColor { get; set; }
    public Int32 Limit { get; set; } = 3;
}

/// <summary>POST projects/quick-create 输入（dryRun=false 落库；与工具 design_create 的 apply 方向相反，见接口注释）</summary>
public sealed class QuickCreateRequest
{
    public String? Name { get; set; }
    public String? Code { get; set; }
    public String? Kind { get; set; }
    public String? Description { get; set; }
    public String? Preset { get; set; }
    public GenerationRequest? Request { get; set; }
    public Boolean DryRun { get; set; }
}

/// <summary>PUT agent-access 输入</summary>
public sealed class AgentAccessPatch
{
    public Boolean? Enabled { get; set; }
}

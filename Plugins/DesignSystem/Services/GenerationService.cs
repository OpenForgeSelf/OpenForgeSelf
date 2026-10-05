using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>生成编排结果：写令牌 → 组件目录 → 品牌目录 → 审计，一次 Run 全部完成</summary>
public sealed record GenerationOutcome(
    GenerationResult Result,
    ComponentSeed Components,
    BrandSeed Brand,
    AuditSummary Audit,
    /// <summary>本次生成后库里的 UX 规范条数（只补空，所以这是"现存"而不是"新增"）；未接规范服务时为 null</summary>
    Int32? Guidelines = null);

/// <summary>
/// 生成编排：把控制器 Generate 内联的「ApplyToProject → SeedComponentCatalog → SeedBrandCatalog → AuditEngine.Run」
/// 收成一处。控制器 Generate 与工具 design_create / design_edit 共用同一路径 —— REST 与 Agent 行为同源，
/// 不存在第二套编排。响应形状由调用方组装（GenerateShapeTests 钉死键序）。
/// </summary>
public sealed class GenerationService
{
    readonly TokenRepository _tokens;
    readonly DesignProjectService _projects;
    readonly CatalogRepository _catalog;
    readonly AuditEngine _auditEngine;

    /// <summary>
    /// UX 规范服务（M3）。用属性注入而不是构造参数：规范服务依赖项目/令牌仓储，
    /// 而本类在插件注册表里比它们更早构造 —— 塞进构造函数会把注册顺序变成隐藏契约。
    /// 未设置时生成链路照旧跑（只是不种规范），不会把生成打死。
    /// </summary>
    public GuidelineService? Guidelines { get; set; }

    public GenerationService(TokenRepository tokens, DesignProjectService projects, CatalogRepository catalog, AuditEngine auditEngine)
    {
        _tokens = tokens;
        _projects = projects;
        _catalog = catalog;
        _auditEngine = auditEngine;
    }

    /// <summary>完整生成一趟并落库（令牌 + 组件目录 + 品牌目录 + 审计），返回各部分结果。</summary>
    public GenerationOutcome Run(Int64 projectId, GenerationRequest req, Boolean overwrite)
    {
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, projectId, req, overwrite);
        // 组件层令牌要落成可浏览的组件规格 + 变体矩阵，否则"组件库"对刚生成的项目永远是 0 条
        var seed = DesignGenerator.SeedComponentCatalog(_catalog, projectId, result);
        var brand = DesignGenerator.SeedBrandCatalog(_catalog, projectId, result);
        // 规范播种放在令牌/目录之后、审计之前：它只补空、不改令牌，所以审计口径不受影响
        Int32? guidelines = null;
        if (Guidelines != null)
        {
            try { guidelines = Guidelines.SeedGuidelines(projectId); }
            catch (Exception ex)
            {
                // 规范失败不该把"令牌已生成成功"整体判死，但必须可见：进 Notes 由响应带回
                result.Notes.Add($"UX 规范播种失败（令牌与组件目录已生成）：{ex.GetType().Name}: {ex.Message}");
            }
        }
        var summary = _auditEngine.Run(projectId);
        return new GenerationOutcome(result, seed, brand, summary, guidelines);
    }
}

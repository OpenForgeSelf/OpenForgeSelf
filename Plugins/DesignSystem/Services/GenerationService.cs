using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>生成编排结果：写令牌 → 组件目录 → 品牌目录 → 审计，一次 Run 全部完成</summary>
public sealed record GenerationOutcome(
    GenerationResult Result,
    ComponentSeed Components,
    BrandSeed Brand,
    AuditSummary Audit);

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
        var summary = _auditEngine.Run(projectId);
        return new GenerationOutcome(result, seed, brand, summary);
    }
}

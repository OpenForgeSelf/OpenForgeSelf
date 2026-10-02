using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Plugins.DesignSystem.Agent;

/// <summary>设计系统工具的服务束：由 DesignSystemPlugin.Apply 一次性装配，8 个工具共用同一批服务实例（与控制器同源）。</summary>
public sealed class DesignToolKit(
    DesignProjectService projects,
    TokenRepository tokens,
    CatalogRepository catalog,
    AuditRepository audits,
    AuditEngine auditEngine,
    ExportService export,
    ReleaseService releases,
    GenerationService generation,
    AgentAccess agentAccess,
    DesignReviewService review,
    QuickCreateService quickCreate,
    DesignBriefBuilder brief)
{
    public DesignProjectService Projects { get; } = projects;
    public TokenRepository Tokens { get; } = tokens;
    public CatalogRepository Catalog { get; } = catalog;
    public AuditRepository Audits { get; } = audits;
    public AuditEngine AuditEngine { get; } = auditEngine;
    public ExportService Export { get; } = export;
    public ReleaseService Releases { get; } = releases;
    public GenerationService Generation { get; } = generation;
    public AgentAccess AgentAccess { get; } = agentAccess;
    public DesignReviewService Review { get; } = review;
    public QuickCreateService QuickCreate { get; } = quickCreate;
    public DesignBriefBuilder Brief { get; } = brief;
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DesignSystem.Agent;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DesignSystem;

/// <summary>
/// 设计系统插件：宿主的设计语言底座。
///
/// v2.0.0 起从「一次性生成器演示件」升级为**有库、有校验、有版本、有标准投影**的设计系统：
/// 项目 / 主题（mode 轴）/ 三层令牌（primitive→semantic→component + 别名图）/ 组件与变体矩阵 /
/// 图标与资产 / 页面清单 / 字体登记 / 可达性审计 / 版本快照，全部落插件自建库
/// （<c>~/.forgeself/Plugins/design-system/DesignSystem.db</c>）。
///
/// 前端贡献仍由 plugin.json 的 frontend 块驱动（菜单/路由/视图），后端在此注册服务与建表。
/// </summary>
public class DesignSystemPlugin : IPlugin
{
    /// <summary>对外暴露的 design_* 工具（宿主 ToolRegistry 自动收集的约定入口）。</summary>
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        XTrace.Log.Info("初始化设计系统插件（设计语言底座）");

        // 库与表必须先就绪：插件 Apply 早于宿主建表，宿主不会替插件建表（plugin-development 铁律 12）
        if (!DesignSystemTables.EnsureCreated())
            XTrace.Log.Warn("设计系统库初始化未成功，设计系统端点将不可用（详见上一条 Error 日志）");
        else
            SeedBuiltinIcons();

        var services = ctx.Get<IServiceCollection>();
        if (services != null)
        {
            // 随数据走的文件（版本快照等）落插件数据目录，发布覆盖不影响
            var dataDir = ctx.EnsurePluginDataDirectory();
            var paths = new DesignSystemPaths(dataDir);
            var projects = new DesignProjectService();
            var tokens = new TokenRepository();
            var catalog = new CatalogRepository();
            var audits = new AuditRepository();
            var auditEngine = new AuditEngine(tokens, projects, audits);
            var export = new ExportService(tokens, projects, catalog);
            var releases = new ReleaseService(tokens, projects, audits, auditEngine, paths, catalog);
            var generation = new GenerationService(tokens, projects, catalog, auditEngine);
            var agentAccess = new AgentAccess(paths);
            var review = new DesignReviewService(export, tokens, projects);
            var quickCreate = new QuickCreateService(projects, generation);
            var brief = new DesignBriefBuilder(export, projects, catalog, review);
            export.BriefBuilder = brief;
            var previewCss = new PreviewCssService(export);

            // 同一实例注册：控制器与工具由此用同一批对象（§G）
            services.AddSingleton(paths);
            services.AddSingleton(projects);
            services.AddSingleton(tokens);
            services.AddSingleton(catalog);
            services.AddSingleton(audits);
            services.AddSingleton(auditEngine);
            services.AddSingleton(export);
            services.AddSingleton(releases);
            services.AddSingleton(generation);
            services.AddSingleton(agentAccess);
            services.AddSingleton(review);
            services.AddSingleton(quickCreate);
            services.AddSingleton(brief);
            services.AddSingleton(previewCss);

            var kit = new DesignToolKit(projects, tokens, catalog, audits, auditEngine, export, releases,
                generation, agentAccess, review, quickCreate, brief);
            services.AddSingleton(kit);

            var pluginId = ctx.Get<PluginMetadata>()?.Id ?? DesignSystemConstants.PluginId;
            ToolExtensions.Add(new DesignGuideTool(kit));
            ToolExtensions.Add(new DesignContextTool(kit));
            ToolExtensions.Add(new DesignLookupTool(kit));
            ToolExtensions.Add(new DesignReviewTool(kit));
            ToolExtensions.Add(new DesignAuditTool(kit));
            ToolExtensions.Add(new DesignPresetsTool(kit));
            ToolExtensions.Add(new DesignCreateTool(kit));
            ToolExtensions.Add(new DesignEditTool(kit));
            XTrace.Log.Info("设计系统插件已注册 {0} 个工具函数", ToolExtensions.Count);
        }

        XTrace.Log.Info("设计系统插件初始化完成（tools={0}）", ToolExtensions.Count);
    }

    /// <summary>
    /// 首植内置图标库 forge（用户拍板：插件必须自带一套零许可证负担、且好看的图标，用户可再自行导入）。
    /// 失败只记 Error 不抛出：图标缺失不该让整批设计系统端点不可用。
    /// </summary>
    static void SeedBuiltinIcons()
    {
        try
        {
            var written = BuiltinIcons.SeedIfMissing(new CatalogRepository());
            if (written > 0) XTrace.Log.Info("设计系统：首植内置图标 {0} 枚（collection=forge，License=Owned）", written);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("设计系统：内置图标首植失败（图标页可能为空，其余功能不受影响）: {0}", ex);
        }
    }
}

/// <summary>插件数据目录（由 Apply 一次性注入，避免各服务自行猜路径）</summary>
public sealed record DesignSystemPaths(String DataDirectory)
{
    /// <summary>版本快照目录（按需创建；永不删除其中文件）</summary>
    public String ReleasesDirectory
    {
        get
        {
            var dir = Path.Combine(DataDirectory, "releases");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Agent 写开关文件（只写不删；文件损坏按只读处理）</summary>
    public String AgentAccessFile => Path.Combine(DataDirectory, "agent-access.json");
}

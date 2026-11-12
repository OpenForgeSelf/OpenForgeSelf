using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DbAdmin;

/// <summary>
/// DbAdmin 插件入口（Id db-admin，工具前缀 db，自有库 ConnName DbAdmin）。
/// Agent 工具（IToolFunctionExtension，db_* 七件，见 Services/DbAdminToolFunctions.cs 与 docs/07）：
/// 构造只存 pluginId + 上下文，执行期 CreateHostScope 取 <see cref="DbAdminAgentService"/>。
/// </summary>
public class DbAdminPlugin : IPlugin
{
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        XTrace.Log.Info("[DbAdminPlugin] 初始化数据库管理插件");

        // F1（2026-11-05）：不在 Apply（builder.Build 阶段）建表——插件跑在宿主 XCodeConfig 登记/建表之前，
        // 此时 InitConnection 会触发 XCode 自动兜底 `Data Source=Data\<ConnName>.db`（相对 CWD=版本目录），
        // 库落版本层，宿主升级换版本目录即丢用户数据（packaging-upgrade-backup §1.7 红线）。
        // 对齐 AgentHub 先例：插件侧不建表，宿主 InitializeXCodeDatabase 在 app.Build 后按 DbFiles 登记并统一建表。
        // EnsureCreated 方法本体保留（XCodeTestFixture / 测试夹具仍用）。

        String dataDir;
        try { dataDir = ctx.EnsurePluginDataDirectory(); }
        catch (Exception ex)
        {
            // 无数据位置服务（极端/测试宿主）时退回程序目录下插件子目录；仍不入库
            dataDir = Path.Combine(AppContext.BaseDirectory, "Data", "db-admin");
            XTrace.Log.Warn("[DbAdminPlugin] 取插件数据目录失败，改用 {0}: {1}", dataDir, ex.Message);
        }

        var services = ctx.Get<IServiceCollection>();
        var audit = new DbAuditWriter();
        var protector = new FileKeySecretProtector(dataDir);
        var connections = new DbConnectionService(protector, new DefaultDriverProbe(dataDir), audit);
        var queries = new DbQueryService(connections, new PermissionModeStore(dataDir), audit);
        // F1 收尾（0.1.2）：Apply 阶段零实体访问（对齐 AgentHub 先例）——过期清理已由
        // DbQueryService.ResolveCore/ListApprovals 首行惰性触发，此处预热在宿主登记缺失时会触发 XCode 兜底。

        services?.AddSingleton<ISecretProtector>(protector);
        services?.AddSingleton(audit);
        services?.AddSingleton(connections);
        services?.AddSingleton(queries);
        services?.AddSingleton(new DbAdminAgentService(connections, queries));

        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "db-admin";
        RegisterTools(pluginId, ctx);

        XTrace.Log.Info("[DbAdminPlugin] 数据库管理插件初始化完成");
    }

    private void RegisterTools(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Clear();
        ToolExtensions.AddRange(DbAdminTools.Create(pluginId, services));
        XTrace.Log.Debug("[DbAdminPlugin] 已注册 {0} 个 db_* 工具", ToolExtensions.Count);
    }
}

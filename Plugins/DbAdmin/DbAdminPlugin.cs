using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DbAdmin;

/// <summary>
/// DbAdmin 插件入口（Id db-admin，工具前缀 db，自有库 ConnName DbAdmin）。
/// Agent 工具（IToolFunctionExtension）由 AI 工程师后续接入：执行期取 <see cref="DbAdminAgentService"/>。
/// </summary>
public class DbAdminPlugin : IPlugin
{
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        XTrace.Log.Info("[DbAdminPlugin] 初始化数据库管理插件");

        if (!Data.DbAdminTables.EnsureCreated())
            XTrace.Log.Warn("[DbAdminPlugin] 插件库未就绪，连接/历史/审计将不可用（详见 DbAdminTables 的 Error 日志）");

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
        try { queries.ExpireStale(); } catch (Exception ex) { XTrace.Log.Warn("[DbAdminPlugin] 过期审批清理失败: {0}", ex.Message); }

        services?.AddSingleton<ISecretProtector>(protector);
        services?.AddSingleton(audit);
        services?.AddSingleton(connections);
        services?.AddSingleton(queries);
        services?.AddSingleton(new DbAdminAgentService(connections, queries));

        XTrace.Log.Info("[DbAdminPlugin] 数据库管理插件初始化完成");
    }
}

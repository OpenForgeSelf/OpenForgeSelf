using ForgeSelf.Api.Plugins.DbAdmin.Models;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>Agent 可见的连接摘要（绝无密码 / 连接串字段）</summary>
public record AgentConnectionInfo(String Id, String Name, String Provider, String Kind, Boolean Readonly, Boolean Tested);

/// <summary>
/// Agent 工具服务层（docs/04 §4）。后续 <c>IToolFunctionExtension</c>（db_list_connections 等）只需：
/// 构造收 IContext → 执行期取本服务 → 调对应方法 → JSON 序列化返回。本类不抛业务异常以外的异常；
/// 业务异常 <see cref="DbAdminException"/> 的 Code 即工具失败语义（rejected_hard / readonly_connection / driver_missing / not_found …）。
/// </summary>
public sealed class DbAdminAgentService(DbConnectionService connections, DbQueryService queries)
{
    /// <summary>db_list_connections</summary>
    public IReadOnlyList<AgentConnectionInfo> ListConnections() =>
        connections.List().Select(c => new AgentConnectionInfo(c.Id, c.Name, c.Provider, c.Kind, c.Readonly, c.Tested)).ToList();

    /// <summary>db_list_tables</summary>
    public IReadOnlyList<TableSummary> ListTables(String connectionId) => connections.ListTables(connectionId);

    /// <summary>db_describe_table</summary>
    public TableDetail DescribeTable(String connectionId, String table) => connections.DescribeTable(connectionId, table);

    /// <summary>db_query_readonly：仅只读语句；强制 ≤1000 行 / ≤256KB，返回 truncated</summary>
    public QueryOutcome QueryReadonly(String connectionId, String sql, Dictionary<String, Object?>? ps, String? sessionId)
    {
        var cls = SqlGuard.Classify(sql);
        if (!cls.IsRead)
        {
            // 非只读不进执行链；硬拒语句仍走 Execute 以留审计
            if (cls.IsRejected) return queries.Execute(new QueryRequest { ConnectionId = connectionId, Sql = sql, Params = ps }, QueryContext.Agent(sessionId), "db_query_readonly");
            var o = DbQueryService.Normalize(new QueryOutcome { Status = "rejected", Code = DbErrorCodes.RejectedHard, Kind = cls.Kind, Message = "db_query_readonly 只接受 SELECT / WITH 等只读语句，写操作请用 db_execute_write" });
            queries.AuditWriter.WriteAgent("db_query_readonly", connectionId, sql, sessionId, o);
            return o;
        }
        return queries.Execute(new QueryRequest { ConnectionId = connectionId, Sql = sql, Params = ps }, QueryContext.Agent(sessionId), "db_query_readonly");
    }

    /// <summary>db_execute_write：AskWrite → awaiting_approval + approvalId；硬拒 → rejected</summary>
    public QueryOutcome ExecuteWrite(String connectionId, String sql, Dictionary<String, Object?>? ps, String? sessionId) =>
        queries.Execute(new QueryRequest { ConnectionId = connectionId, Sql = sql, Params = ps }, QueryContext.Agent(sessionId), "db_execute_write");

    /// <summary>db_set_permission_mode：只改会话级</summary>
    public PermissionModeView SetPermissionMode(String mode, String sessionId) => queries.SetMode(mode, QueryContext.Agent(sessionId));

    /// <summary>db_resolve_approval：破坏性语句 Agent 无法批准（confirmation_required）</summary>
    public QueryOutcome ResolveApproval(Int64 approvalId, Boolean approve, String? sessionId) =>
        queries.Resolve(approvalId, approve, confirmDestructive: false, QueryContext.Agent(sessionId));
}

using ForgeSelf.Api.Plugins.DbAdmin.Models;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>Agent 工具层补充错误码（与 <see cref="DbErrorCodes"/> 并列，仅出现在 db_* 工具回包与 kind=agent 审计里）</summary>
public static class DbAgentCodes
{
    /// <summary>db_query_readonly 收到非 SELECT/WITH 语句（写语句、PRAGMA/SHOW/EXPLAIN 等）</summary>
    public const String NotReadonlySql = "not_readonly_sql";
    /// <summary>db_execute_write 收到只读语句，应改用 db_query_readonly</summary>
    public const String UseQueryReadonly = "use_query_readonly";
    /// <summary>工具参数缺失或类型不对</summary>
    public const String InvalidArguments = "invalid_arguments";
    /// <summary>db_resolve_approval 批准时未提供用户原话确认</summary>
    public const String UserConfirmationRequired = "user_confirmation_required";
    /// <summary>未预期异常（消息已脱敏）</summary>
    public const String InternalError = "internal_error";
}

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

    /// <summary>
    /// db_query_readonly（工具用严格版）：只接受以 SELECT / WITH 开头且经 SqlGuard 判为只读的单条语句；
    /// 服务端子查询包装强制取数上限（≤1000 行 / ≤256KB），并按 <paramref name="maxRows"/>（1..1000）再裁剪，超出即 truncated=true。
    /// 写语句 / PRAGMA / SHOW / EXPLAIN 返回 rejected + <see cref="DbAgentCodes.NotReadonlySql"/>；硬拒清单仍为 rejected_hard。
    /// </summary>
    public QueryOutcome QueryReadonlyStrict(String connectionId, String sql, Dictionary<String, Object?>? ps, String? sessionId, Int32 maxRows)
    {
        const String tool = "db_query_readonly";
        var cls = SqlGuard.Classify(sql);
        if (cls.IsRejected) return queries.Execute(new QueryRequest { ConnectionId = connectionId, Sql = sql, Params = ps }, QueryContext.Agent(sessionId), tool);
        if (!cls.IsRead || !StartsWithSelectOrWith(sql))
        {
            var msg = cls.IsRead
                ? "db_query_readonly 只接受 SELECT / WITH 查询；PRAGMA / SHOW / EXPLAIN 等元数据语句请改用 db_list_tables / db_describe_table"
                : $"db_query_readonly 只接受 SELECT / WITH 查询，该语句被识别为{(cls.Category == SqlCategory.Destructive ? "破坏性写" : "写")}操作（{cls.Reason}）";
            var o = DbQueryService.Normalize(new QueryOutcome { Status = "rejected", Code = DbAgentCodes.NotReadonlySql, Kind = cls.Kind, Destructive = cls.Category == SqlCategory.Destructive, Message = msg });
            queries.AuditWriter.WriteAgent(tool, connectionId, sql, sessionId, o);
            return o;
        }
        var r = queries.Execute(new QueryRequest { ConnectionId = connectionId, Sql = sql, Params = ps }, QueryContext.Agent(sessionId), tool);
        var cap = Math.Clamp(maxRows, 1, ResultLimiter.AgentMaxRows);
        if (r.Rows != null && r.Rows.Count > cap)
        {
            r.Rows = r.Rows.Take(cap).ToList();
            r.Truncated = true;
        }
        return r;
    }

    /// <summary>db_execute_write（工具用严格版）：只读语句不代执行，返回 rejected + <see cref="DbAgentCodes.UseQueryReadonly"/>；其余同 <see cref="ExecuteWrite"/>。</summary>
    public QueryOutcome ExecuteWriteStrict(String connectionId, String sql, Dictionary<String, Object?>? ps, String? sessionId)
    {
        var cls = SqlGuard.Classify(sql);
        if (cls.IsRead)
        {
            var o = new QueryOutcome { Status = "rejected", Code = DbAgentCodes.UseQueryReadonly, Kind = "read", Message = "这是只读语句，db_execute_write 不执行查询；请改用 db_query_readonly" };
            queries.AuditWriter.WriteAgent("db_execute_write", connectionId, sql, sessionId, o);
            return o;
        }
        return ExecuteWrite(connectionId, sql, ps, sessionId);
    }

    /// <summary>当前会话生效的权限模式（无会话覆盖时为全局默认）</summary>
    public String EffectiveMode(String? sessionId) => queries.Modes.Effective(QueryContext.Agent(sessionId));

    /// <summary>全局默认权限模式（只能由 UI 修改）</summary>
    public String GlobalMode => queries.Modes.Global;

    /// <summary>审批记录（只读查看；原始 SQL 不下发，仅 sqlRedacted）</summary>
    public ApprovalView? FindApproval(Int64 approvalId) => queries.ListApprovals(null).FirstOrDefault(a => a.Id == approvalId);

    /// <summary>工具回包兜底擦除用的明文密钥（仅内存）</summary>
    public IReadOnlyCollection<String> SecretsForScrub() => connections.CollectSecretsForScrub();

    internal static Boolean StartsWithSelectOrWith(String? sql)
    {
        if (String.IsNullOrWhiteSpace(sql)) return false;
        String s;
        try { s = SqlGuard.Strip(sql); } catch (FormatException) { return false; }
        s = s.TrimStart('(', ' ', '\r', '\n', '\t');
        return System.Text.RegularExpressions.Regex.IsMatch(s, @"^(SELECT|WITH)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

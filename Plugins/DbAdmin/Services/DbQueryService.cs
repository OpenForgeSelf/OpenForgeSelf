using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using NewLife.Data;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>
/// SQL 执行服务（UI 控制器与后续 Agent 工具共用的唯一入口）。
/// 链路：SqlGuard → 只读硬拒 → 权限模式 / 审批 → DAL.Query/Execute（参数化）→ ResultLimiter → 审计/历史（脱敏）。
/// </summary>
public sealed class DbQueryService
{
    /// <summary>审批有效期（原始 SQL 仅内存持有，过期即丢弃）</summary>
    public static TimeSpan ApprovalTtl { get; set; } = TimeSpan.FromMinutes(30);

    private readonly DbConnectionService _connections;
    private readonly PermissionModeStore _modes;
    private readonly DbAuditWriter _audit;
    private readonly ConcurrentDictionary<Int64, PendingWrite> _pending = new();

    private sealed record PendingWrite(String ConnectionId, String Sql, Dictionary<String, Object?>? Params, Boolean Destructive, String Source, DateTime CreateTime);

    public DbQueryService(DbConnectionService connections, PermissionModeStore modes, DbAuditWriter audit)
    {
        _connections = connections;
        _modes = modes;
        _audit = audit;
    }

    public PermissionModeStore Modes => _modes;

    #region 执行
    /// <summary>UI / Agent 通用执行入口。不抛业务异常：错误以 Status=rejected/error + Code 返回。</summary>
    public QueryOutcome Execute(QueryRequest req, QueryContext ctx)
    {
        var sql = req.Sql ?? "";
        var cid = req.ConnectionId;
        var cls = SqlGuard.Classify(sql);

        if (cls.IsRejected) return Reject(cid, sql, cls.Reason, DbErrorCodes.RejectedHard, ctx, cls.Kind);

        ResolvedConnection rc;
        try { rc = _connections.Resolve(cid); }
        catch (DbAdminException ex) { return Error(cid, sql, ex.Code, ex.Message, cls.Kind, ctx); }

        if (!cls.IsRead && rc.Readonly)
        {
            var why = rc.IsSystem ? "系统连接（宿主/插件库）只读，任何写操作均被拒绝" : "该连接已设为只读";
            return Reject(rc.Id, sql, why, DbErrorCodes.ReadonlyConnection, ctx, cls.Kind);
        }

        if (cls.IsRead) return RunRead(rc, sql, req, ctx);

        var destructive = cls.Category == SqlCategory.Destructive;
        var mode = _modes.Effective(ctx);
        _audit.Write("classify", rc.Id, sql, cls.Kind, $"mode={mode} source={ctx.Source} {cls.Reason} {SqlRedactor.DescribeParams(req.Params)}");

        // AskWrite：所有写都询问；Auto：仅破坏性询问
        if (mode == PermissionModes.AskWrite || destructive)
            return Enqueue(rc.Id, sql, req.Params, destructive, ctx, cls.Kind);

        return RunWrite(rc, sql, req.Params, ctx, cls.Kind);
    }

    private QueryOutcome RunRead(ResolvedConnection rc, String sql, QueryRequest req, QueryContext ctx)
    {
        var sw = Stopwatch.StartNew();
        var agent = ctx.Source == QuerySources.Agent;
        var page = Math.Max(1, req.Page);
        var pageSize = req.PageSize <= 0 ? ResultLimiter.UiDefaultPageSize : Math.Min(req.PageSize, ResultLimiter.UiMaxPageSize);
        try
        {
            var dal = DAL.Create(rc.ConnName);
            var ps = req.Params?.ToDictionary(k => k.Key, v => v.Value);
            var body = TrimTrailingSemicolon(sql);
            var wrappable = IsWrappable(body);

            DbTable dt;
            Int64? total = null;
            if (wrappable)
            {
                var sb = new SelectBuilder { Table = $"({body}) dbadmin_t", Column = "*" };
                if (ps != null && ps.Count > 0) sb.Parameters.AddRange(dal.Db.CreateParameters(ps!));
                if (agent)
                {
                    dt = DbConnectionService.Wrap(rc, () => dal.Query(sb, 0, ResultLimiter.AgentMaxRows + 1));
                }
                else
                {
                    total = DbConnectionService.Wrap(rc, () => (Int64)dal.SelectCount(sb));
                    dt = DbConnectionService.Wrap(rc, () => dal.Query(sb, (page - 1L) * pageSize, pageSize));
                }
            }
            else
            {
                // PRAGMA / SHOW / EXPLAIN 等不能作子查询：原样执行后内存分页（这类结果天然很小）
                var all = DbConnectionService.Wrap(rc, () => dal.Query(body, ps!));
                total = all.Rows?.Count ?? 0;
                if (!agent)
                {
                    all.Rows = (all.Rows ?? []).Skip((page - 1) * pageSize).Take(pageSize).ToList();
                }
                dt = all;
            }

            var cols = (dt.Columns ?? []).ToList();
            var types = (dt.Types ?? []).Select(t => t?.Name ?? "").ToList();
            var rows = (dt.Rows ?? []).Select(r => r.Select(ResultLimiter.NormalizeCell).ToArray());
            var outcome = new QueryOutcome { Status = "ok", Kind = "read", Columns = cols, ColumnTypes = types };
            if (agent)
            {
                var (limited, truncated) = ResultLimiter.Limit(rows, cols, ResultLimiter.AgentMaxRows, ResultLimiter.AgentMaxBytes);
                outcome.Rows = limited;
                outcome.Truncated = truncated;
                outcome.Total = wrappable ? null : total;
            }
            else
            {
                outcome.Rows = rows.ToList();
                outcome.Total = total;
                outcome.Page = page;
                outcome.PageSize = pageSize;
            }
            outcome.ElapsedMs = sw.ElapsedMilliseconds;
            _audit.Write("query", rc.Id, sql, "allow", $"source={ctx.Source} rows={outcome.Rows.Count} truncated={outcome.Truncated} {SqlRedactor.DescribeParams(req.Params)}");
            _audit.WriteHistory(rc.Id, sql, "read", outcome.Rows.Count, outcome.ElapsedMs, true, null, ctx.Source);
            return outcome;
        }
        catch (DbAdminException ex)
        {
            _audit.WriteHistory(rc.Id, sql, "read", 0, sw.ElapsedMilliseconds, false, ex.Message, ctx.Source);
            _audit.Write("query", rc.Id, sql, ex.Code, ex.Message);
            return new QueryOutcome { Status = "error", Code = ex.Code, Message = SqlRedactor.RedactText(ex.Message), Kind = "read", ElapsedMs = sw.ElapsedMilliseconds };
        }
    }

    private QueryOutcome RunWrite(ResolvedConnection rc, String sql, Dictionary<String, Object?>? ps, QueryContext ctx, String kind)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var dal = DAL.Create(rc.ConnName);
            var body = TrimTrailingSemicolon(sql);
            var affected = DbConnectionService.Wrap(rc, () => ps == null || ps.Count == 0
                ? dal.Execute(body)
                : dal.Execute(body, CommandType.Text, ps.ToDictionary(k => k.Key, v => v.Value)!));
            _audit.Write("write_result", rc.Id, sql, "ok", $"source={ctx.Source} affected={affected}");
            _audit.WriteHistory(rc.Id, sql, "write", affected, sw.ElapsedMilliseconds, true, null, ctx.Source);
            return new QueryOutcome { Status = "ok", Kind = kind, AffectedRows = affected, ElapsedMs = sw.ElapsedMilliseconds, Destructive = kind == "destructive" };
        }
        catch (DbAdminException ex)
        {
            _audit.Write("write_result", rc.Id, sql, ex.Code, ex.Message);
            _audit.WriteHistory(rc.Id, sql, "write", 0, sw.ElapsedMilliseconds, false, ex.Message, ctx.Source);
            return new QueryOutcome { Status = "error", Code = ex.Code, Message = SqlRedactor.RedactText(ex.Message), Kind = kind, ElapsedMs = sw.ElapsedMilliseconds };
        }
    }

    private QueryOutcome Reject(String? cid, String sql, String reason, String code, QueryContext ctx, String kind)
    {
        _audit.Write("rejected", cid, sql, code, $"source={ctx.Source} {reason}");
        _audit.WriteHistory(cid, sql, kind == "read" ? "read" : "write", 0, 0, false, reason, ctx.Source);
        return new QueryOutcome { Status = "rejected", Code = code, Message = reason, Kind = kind };
    }

    private QueryOutcome Error(String? cid, String sql, String code, String message, String kind, QueryContext ctx)
    {
        _audit.Write("query", cid, sql, code, message);
        return new QueryOutcome { Status = "error", Code = code, Message = SqlRedactor.RedactText(message), Kind = kind };
    }

    internal static String TrimTrailingSemicolon(String sql) => sql.Trim().TrimEnd(';').TrimEnd();

    /// <summary>仅 SELECT / WITH / VALUES 可包成子查询做服务端分页</summary>
    private static Boolean IsWrappable(String sql)
    {
        var s = SqlGuard.Strip(sql).TrimStart('(', ' ', '\r', '\n', '\t');
        return s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("VALUES", StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region 审批
    private QueryOutcome Enqueue(String cid, String sql, Dictionary<String, Object?>? ps, Boolean destructive, QueryContext ctx, String kind)
    {
        var a = new DbApproval
        {
            ConnectionId = cid,
            SqlRedacted = DbAuditWriter.Cut(SqlRedactor.RedactSql(sql), 8000)!,
            Kind = kind,
            Destructive = destructive,
            Status = "pending",
            Source = ctx.Source,
            CreateTime = DateTime.Now,
        };
        a.Insert();
        _pending[a.Id] = new PendingWrite(cid, sql, ps, destructive, ctx.Source, a.CreateTime);
        _audit.Write("approval_pending", cid, sql, "ask", $"approvalId={a.Id} destructive={destructive} source={ctx.Source}");
        return new QueryOutcome
        {
            Status = "awaiting_approval",
            Kind = kind,
            ApprovalId = a.Id,
            Destructive = destructive,
            Message = destructive ? "破坏性语句：需在界面批准并二次确认" : "写操作等待批准",
        };
    }

    /// <summary>
    /// 处理审批。approve：重新分类 + 只读校验后执行；破坏性需 <paramref name="confirmDestructive"/>=true 且来源为 UI。
    /// </summary>
    public QueryOutcome Resolve(Int64 approvalId, Boolean approve, Boolean confirmDestructive, QueryContext ctx)
    {
        ExpireStale();
        var a = DbApproval.Find(DbApproval._.Id == approvalId) ?? throw new DbAdminException(DbErrorCodes.NotFound, $"审批不存在：{approvalId}");
        if (a.Status != "pending") throw new DbAdminException(DbErrorCodes.InvalidRequest, $"审批已处理（{a.Status}）");
        if (!_pending.TryGetValue(approvalId, out var p))
        {
            MarkStatus(a, "expired");
            throw new DbAdminException(DbErrorCodes.NotFound, "审批已过期（原始 SQL 不再持有，请重新执行）");
        }

        if (!approve)
        {
            _pending.TryRemove(approvalId, out _);
            MarkStatus(a, "rejected");
            _audit.Write("approval_rejected", a.ConnectionId, p.Sql, "rejected", $"approvalId={approvalId} by={ctx.Source}");
            return new QueryOutcome { Status = "rejected", Code = "approval_rejected", Message = "已拒绝", ApprovalId = approvalId, Kind = a.Kind, Destructive = a.Destructive };
        }

        if (p.Destructive && (ctx.Source != QuerySources.Ui || !confirmDestructive))
            throw new DbAdminException(DbErrorCodes.ConfirmationRequired, "破坏性语句需在界面批准并二次确认（confirmDestructive=true）");

        _pending.TryRemove(approvalId, out _);
        MarkStatus(a, "approved");
        _audit.Write("approval_approved", a.ConnectionId, p.Sql, "approved", $"approvalId={approvalId} by={ctx.Source} confirm={confirmDestructive}");

        // 防御：执行前再分类 + 再解析连接（期间连接可能被改为只读 / 删除）
        var cls = SqlGuard.Classify(p.Sql);
        if (cls.IsRejected) return Reject(p.ConnectionId, p.Sql, cls.Reason, DbErrorCodes.RejectedHard, ctx, cls.Kind);
        ResolvedConnection rc;
        try { rc = _connections.Resolve(p.ConnectionId); }
        catch (DbAdminException ex) { return Error(p.ConnectionId, p.Sql, ex.Code, ex.Message, cls.Kind, ctx); }
        if (rc.Readonly) return Reject(rc.Id, p.Sql, "连接只读", DbErrorCodes.ReadonlyConnection, ctx, cls.Kind);

        var outcome = RunWrite(rc, p.Sql, p.Params, new QueryContext(p.Source, ctx.SessionId), cls.Kind);
        outcome.ApprovalId = approvalId;
        return outcome;
    }

    private static void MarkStatus(DbApproval a, String status)
    {
        a.Status = status;
        a.ResolveTime = DateTime.Now;
        a.Update();
    }

    /// <summary>过期内存中的待审批；并把库里已无原始 SQL 的 pending 记为 expired</summary>
    public Int32 ExpireStale()
    {
        var n = 0;
        var now = DateTime.Now;
        foreach (var kv in _pending)
        {
            if (now - kv.Value.CreateTime > ApprovalTtl && _pending.TryRemove(kv.Key, out _))
            {
                var a = DbApproval.Find(DbApproval._.Id == kv.Key);
                if (a != null && a.Status == "pending") { MarkStatus(a, "expired"); n++; }
            }
        }
        foreach (var a in DbApproval.FindAll(DbApproval._.Status == "pending", null, null, 0, 0))
        {
            if (!_pending.ContainsKey(a.Id)) { MarkStatus(a, "expired"); n++; }
        }
        return n;
    }

    public IReadOnlyList<ApprovalView> ListApprovals(String? status)
    {
        ExpireStale();
        var exp = String.IsNullOrEmpty(status) ? null : DbApproval._.Status == status;
        return DbApproval.FindAll(exp, DbApproval._.Id.Desc(), null, 0, 200)
            .Select(a => new ApprovalView(a.Id, a.ConnectionId, a.SqlRedacted, a.Kind, a.Destructive, a.Status, a.Source, a.CreateTime, a.ResolveTime == DateTime.MinValue ? null : a.ResolveTime))
            .ToList();
    }
    #endregion

    #region 历史 / 审计 / 模式
    public PagedResult<HistoryView> History(LogFilter f)
    {
        var (page, size) = Norm(f);
        var exp = new WhereExpression();
        if (!String.IsNullOrEmpty(f.ConnectionId)) exp &= DbQueryHistory._.ConnectionId == f.ConnectionId;
        if (!String.IsNullOrEmpty(f.Kind)) exp &= DbQueryHistory._.Kind == f.Kind;
        if (!String.IsNullOrEmpty(f.Source)) exp &= DbQueryHistory._.Source == f.Source;
        if (f.From != null) exp &= DbQueryHistory._.CreateTime >= f.From.Value;
        if (f.To != null) exp &= DbQueryHistory._.CreateTime < f.To.Value;
        var total = DbQueryHistory.FindCount(exp, null, null, 0, 0);
        var items = DbQueryHistory.FindAll(exp, DbQueryHistory._.Id.Desc(), null, (page - 1) * size, size)
            .Select(h => new HistoryView(h.Id, h.ConnectionId, h.SqlRedacted, h.Kind, h.RowCount, h.ElapsedMs, h.Success, h.Error, h.Source, h.CreateTime)).ToList();
        return new PagedResult<HistoryView>(items, total, page, size);
    }

    public PagedResult<AuditView> Audit(LogFilter f)
    {
        var (page, size) = Norm(f);
        var exp = new WhereExpression();
        if (!String.IsNullOrEmpty(f.ConnectionId)) exp &= DbAuditLog._.ConnectionId == f.ConnectionId;
        if (!String.IsNullOrEmpty(f.Kind)) exp &= DbAuditLog._.Kind == f.Kind;
        if (f.From != null) exp &= DbAuditLog._.CreateTime >= f.From.Value;
        if (f.To != null) exp &= DbAuditLog._.CreateTime < f.To.Value;
        var total = DbAuditLog.FindCount(exp, null, null, 0, 0);
        var items = DbAuditLog.FindAll(exp, DbAuditLog._.Id.Desc(), null, (page - 1) * size, size)
            .Select(a => new AuditView(a.Id, a.Kind, a.ConnectionId, a.SqlRedacted, a.Decision, a.Detail, a.CreateTime)).ToList();
        return new PagedResult<AuditView>(items, total, page, size);
    }

    private static (Int32, Int32) Norm(LogFilter f) => (Math.Max(1, f.Page), f.PageSize <= 0 ? 50 : Math.Min(f.PageSize, 500));

    public PermissionModeView GetMode(QueryContext ctx) => new(_modes.Effective(ctx), _modes.Global);

    /// <summary>UI：改全局默认；Agent（带 SessionId）：只改会话级</summary>
    public PermissionModeView SetMode(String? mode, QueryContext ctx)
    {
        var m = PermissionModes.Normalize(mode) ?? throw new DbAdminException(DbErrorCodes.InvalidRequest, "mode 只能是 AskWrite 或 Auto");
        if (ctx.Source == QuerySources.Agent)
        {
            if (String.IsNullOrEmpty(ctx.SessionId)) throw new DbAdminException(DbErrorCodes.InvalidRequest, "Agent 只能修改会话级模式（需 sessionId）");
            _modes.SetSession(ctx.SessionId, m);
        }
        else _modes.SetGlobal(m);
        _audit.Write("permission_mode", null, null, m, $"source={ctx.Source} scope={(ctx.Source == QuerySources.Agent ? "session" : "global")}");
        return GetMode(ctx);
    }
    #endregion
}

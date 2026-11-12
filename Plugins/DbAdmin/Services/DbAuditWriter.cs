using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>审计写入（所有字段写入前统一脱敏 + 截断；写失败只告警不影响主流程）</summary>
public sealed class DbAuditWriter
{
    public void Write(String kind, String? connectionId, String? sql, String? decision, String? detail)
    {
        try
        {
            var log = new DbAuditLog
            {
                Kind = kind,
                ConnectionId = connectionId!,
                SqlRedacted = Cut(sql == null ? null : SqlRedactor.RedactSql(sql), 8000)!,
                Decision = decision!,
                Detail = Cut(SqlRedactor.RedactText(detail), 2000)!,
                CreateTime = DateTime.Now,
            };
            log.Insert();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DbAdmin] 写审计失败: {0}", SqlRedactor.RedactText(ex.Message));
        }
    }

    /// <summary>Agent 发起的一次调用结果摘要（kind=agent），供前端 S8 回显：GET /audit?kind=agent。</summary>
    public void WriteAgent(String tool, String? connectionId, String? sql, String? sessionId, Models.QueryOutcome o)
    {
        try
        {
            new DbAuditLog
            {
                Kind = "agent",
                ConnectionId = connectionId!,
                SqlRedacted = Cut(sql == null ? null : SqlRedactor.RedactSql(sql), 8000)!,
                Decision = tool,
                Detail = Cut(SqlRedactor.RedactText(o.Message), 2000)!,
                Source = Models.QuerySources.Agent,
                SessionId = Cut(sessionId, 100)!,
                Status = o.Status,
                Code = o.Code!,
                SqlKind = o.Kind!,
                RowCount = o.Rows?.Count ?? o.AffectedRows ?? 0,
                Truncated = o.Truncated,
                ApprovalId = o.ApprovalId ?? 0,
                CreateTime = DateTime.Now,
            }.Insert();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DbAdmin] 写 Agent 审计失败: {0}", SqlRedactor.RedactText(ex.Message));
        }
    }

    public void WriteHistory(String? connectionId, String? sql, String kind, Int64 rowCount, Int64 elapsedMs, Boolean success, String? error, String source)
    {
        try
        {
            new DbQueryHistory
            {
                ConnectionId = connectionId!,
                SqlRedacted = Cut(SqlRedactor.RedactSql(sql), 8000)!,
                Kind = kind,
                RowCount = rowCount,
                ElapsedMs = elapsedMs,
                Success = success,
                Error = Cut(SqlRedactor.RedactText(error), 2000)!,
                Source = source,
                CreateTime = DateTime.Now,
            }.Insert();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DbAdmin] 写历史失败: {0}", SqlRedactor.RedactText(ex.Message));
        }
    }

    internal static String? Cut(String? s, Int32 max) => s == null || s.Length <= max ? s : s[..max];
}

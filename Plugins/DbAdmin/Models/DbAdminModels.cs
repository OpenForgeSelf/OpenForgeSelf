namespace ForgeSelf.Api.Plugins.DbAdmin.Models;

/// <summary>错误码（与 docs/03 §3 一致；confirmation_required / invalid_request 为补充）</summary>
public static class DbErrorCodes
{
    public const String RejectedHard = "rejected_hard";
    public const String ReadonlyConnection = "readonly_connection";
    public const String DriverMissing = "driver_missing";
    public const String SqlError = "sql_error";
    public const String NotFound = "not_found";
    public const String Unauthorized = "unauthorized";
    /// <summary>破坏性语句批准时缺少二次确认（或 Agent 试图批准破坏性语句）</summary>
    public const String ConfirmationRequired = "confirmation_required";
    /// <summary>请求参数不合法（名称重复、缺字段等）</summary>
    public const String InvalidRequest = "invalid_request";
}

/// <summary>统一错误体 <c>{code, message}</c></summary>
public record DbError(String Code, String Message);

/// <summary>业务异常：携带错误码，由控制器映射为 HTTP 状态 + {code,message}</summary>
public class DbAdminException(String code, String message) : Exception(message)
{
    public String Code { get; } = code;
}

/// <summary>权限模式</summary>
public static class PermissionModes
{
    public const String AskWrite = "AskWrite";
    public const String Auto = "Auto";

    public static Boolean IsValid(String? m) => m == AskWrite || m == Auto;

    /// <summary>大小写不敏感归一；非法返回 null</summary>
    public static String? Normalize(String? m)
    {
        if (String.Equals(m, AskWrite, StringComparison.OrdinalIgnoreCase)) return AskWrite;
        if (String.Equals(m, Auto, StringComparison.OrdinalIgnoreCase)) return Auto;
        return null;
    }
}

/// <summary>调用来源</summary>
public static class QuerySources
{
    public const String Ui = "ui";
    public const String Agent = "agent";
}

/// <summary>provider 描述</summary>
public record ProviderInfo(String Id, String DisplayName, Boolean DriverAvailable, Boolean Tested, String? DriverPackage, Int32? DefaultPort);

/// <summary>连接视图（GET 返回；密码一律掩码）</summary>
public class ConnectionView
{
    /// <summary>连接标识：用户连接为数字字符串；系统连接为 <c>sys:{ConnName}</c></summary>
    public String Id { get; set; } = "";
    public String Name { get; set; } = "";
    public String Provider { get; set; } = "";
    /// <summary>system / user</summary>
    public String Kind { get; set; } = "user";
    /// <summary>form / connstr（系统连接为 system）</summary>
    public String Mode { get; set; } = "form";
    public String? Host { get; set; }
    public Int32? Port { get; set; }
    public String? Database { get; set; }
    public String? UserName { get; set; }
    /// <summary>有密码时为 <c>****</c>，否则 null</summary>
    public String? Password { get; set; }
    /// <summary>connstr 模式的掩码连接串</summary>
    public String? ConnStr { get; set; }
    /// <summary>最终是否只读（系统连接恒 true）</summary>
    public Boolean Readonly { get; set; }
    /// <summary>非 SQLite 标记「未实测」</summary>
    public Boolean Tested { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>新建 / 编辑 / 测连请求</summary>
public class ConnectionInput
{
    public String? Name { get; set; }
    public String? Provider { get; set; }
    /// <summary>form / connstr</summary>
    public String? Mode { get; set; }
    public String? Host { get; set; }
    public Int32? Port { get; set; }
    public String? Database { get; set; }
    public String? UserName { get; set; }
    /// <summary>编辑时为空 = 不改；<c>****</c> 也视为不改</summary>
    public String? Password { get; set; }
    /// <summary>connstr 模式原始连接串；编辑时为空 = 不改</summary>
    public String? ConnStr { get; set; }
    public Boolean? Readonly { get; set; }
    /// <summary>测连时可带：基于已保存连接测试（表单中密码为空则沿用已存密码）</summary>
    public Int32? Id { get; set; }
}

/// <summary>测连结果</summary>
public record ConnectionTestResult(Boolean Ok, String? Code, String Message, String? ServerVersion, Int64 ElapsedMs);

/// <summary>表/视图条目</summary>
public record TableSummary(String Name, Boolean IsView, String? Description, Int32 ColumnCount);

public record ColumnInfo(String Name, String? DataType, String? RawType, Boolean Nullable, Boolean PrimaryKey, Boolean Identity, Int32 Length, String? DefaultValue, String? Description);

public record IndexInfo(String? Name, String[] Columns, Boolean Unique, Boolean PrimaryKey);

public record TableDetail(String Name, Boolean IsView, String? Description, IReadOnlyList<ColumnInfo> Columns, IReadOnlyList<String> PrimaryKeys, IReadOnlyList<IndexInfo> Indexes);

/// <summary>SQL 执行请求</summary>
public class QueryRequest
{
    public String? ConnectionId { get; set; }
    public String? Sql { get; set; }
    /// <summary>命名参数（不带前缀，如 {"id":1} 对应 SQL 中 @id / :id）</summary>
    public Dictionary<String, Object?>? Params { get; set; }
    /// <summary>页码，从 1 开始（UI）</summary>
    public Int32 Page { get; set; } = 1;
    /// <summary>每页条数（UI，50/100/500，上限 500）</summary>
    public Int32 PageSize { get; set; } = 50;
}

/// <summary>调用上下文（来源 + 会话，用于会话级权限模式）</summary>
public record QueryContext(String Source, String? SessionId = null)
{
    public static QueryContext Ui { get; } = new(QuerySources.Ui);
    public static QueryContext Agent(String? sessionId) => new(QuerySources.Agent, sessionId);
}

/// <summary>SQL 执行结果</summary>
public class QueryOutcome
{
    /// <summary>ok / awaiting_approval / rejected / error</summary>
    public String Status { get; set; } = "ok";
    public String? Code { get; set; }
    public String? Message { get; set; }
    /// <summary>read / write / destructive / rejected</summary>
    public String? Kind { get; set; }
    public List<String>? Columns { get; set; }
    public List<String>? ColumnTypes { get; set; }
    public List<Object?[]>? Rows { get; set; }
    /// <summary>总行数（UI 分页；Agent 不计算时为 null）</summary>
    public Int64? Total { get; set; }
    public Int32? Page { get; set; }
    public Int32? PageSize { get; set; }
    /// <summary>Agent 上限截断标记</summary>
    public Boolean Truncated { get; set; }
    public Int64? AffectedRows { get; set; }
    public Int64 ElapsedMs { get; set; }
    public Int64? ApprovalId { get; set; }
    public Boolean Destructive { get; set; }
}

/// <summary>审批视图</summary>
public record ApprovalView(Int64 Id, String? ConnectionId, String? SqlRedacted, String? Kind, Boolean Destructive, String Status, String? Source, DateTime CreateTime, DateTime? ResolveTime);

/// <summary>审批处理请求</summary>
public class ApprovalResolveInput
{
    /// <summary>approve / reject</summary>
    public String? Decision { get; set; }
    /// <summary>破坏性语句的二次确认（仅 UI 可置 true）</summary>
    public Boolean ConfirmDestructive { get; set; }
}

/// <summary>分页结果</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, Int64 Total, Int32 Page, Int32 PageSize);

public record HistoryView(Int64 Id, String? ConnectionId, String? SqlRedacted, String? Kind, Int64 RowCount, Int64 ElapsedMs, Boolean Success, String? Error, String? Source, DateTime CreateTime);

public record AuditView(Int64 Id, String? Kind, String? ConnectionId, String? SqlRedacted, String? Decision, String? Detail, DateTime CreateTime);

/// <summary>历史/审计查询过滤</summary>
public class LogFilter
{
    public String? ConnectionId { get; set; }
    public String? Kind { get; set; }
    public String? Source { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Int32 Page { get; set; } = 1;
    public Int32 PageSize { get; set; } = 50;
}

public class PermissionModeInput
{
    public String? Mode { get; set; }
}

public record PermissionModeView(String Mode, String Default);

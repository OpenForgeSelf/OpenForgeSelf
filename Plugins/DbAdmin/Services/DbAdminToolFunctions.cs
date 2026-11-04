using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>
/// DbAdmin Agent 工具（docs/04 §4、docs/07）。七件：db_list_connections / db_list_tables / db_describe_table /
/// db_query_readonly / db_execute_write / db_set_permission_mode / db_resolve_approval。
/// <para>统一回包：成功 <c>{success:true, tool, status, data, hint?}</c>；失败 <c>{success:false, tool, status, code, message, hint}</c>。
/// hint 永远给“下一步该做什么”，便于模型自我纠正。</para>
/// <para>安全：回包出口统一做两道擦除——<see cref="SqlRedactor.RedactText"/> + 明文密钥清单逐字替换为 ****；
/// 构造只存 pluginId / IServiceProvider（铁律 26），执行期 CreateHostScope 取 <see cref="DbAdminAgentService"/>。</para>
/// </summary>
public abstract class DbToolBase : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

    protected DbToolBase(string pluginId, IServiceProvider? services) { PluginId = pluginId; _services = services; }

    public string Id => "db-admin." + Name[3..];
    public abstract string Name { get; }
    public string PluginId { get; }
    public abstract string Description { get; }
    public abstract string ParametersJsonSchema { get; }

    /// <summary>工具主体；返回的对象即回包（由 <see cref="DbToolJson"/> 构造）。业务异常抛 <see cref="DbAdminException"/>。</summary>
    protected abstract Object Invoke(DbAdminAgentService svc, JsonElement args);

    public Task<string> ExecuteAsync(string parameters)
    {
        DbAdminAgentService? svc = null;
        Object result;
        try
        {
            using var doc = JsonDocument.Parse(String.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                result = DbToolJson.Fail(Name, DbAgentCodes.InvalidArguments, "参数必须是 JSON 对象");
            else
            {
                using var scope = _services.CreateHostScope();
                svc = scope.ServiceProvider.GetService<DbAdminAgentService>();
                result = svc == null
                    ? DbToolJson.Fail(Name, DbAgentCodes.InternalError, "DbAdmin 插件服务未就绪（插件未启用或未完成加载）")
                    : Invoke(svc, doc.RootElement.Clone());
            }
        }
        catch (JsonException)
        {
            result = DbToolJson.Fail(Name, DbAgentCodes.InvalidArguments, "参数不是合法 JSON");
        }
        catch (DbToolArgumentException ex)
        {
            result = DbToolJson.Fail(Name, DbAgentCodes.InvalidArguments, ex.Message);
        }
        catch (DbAdminException ex)
        {
            result = DbToolJson.Fail(Name, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            result = DbToolJson.Fail(Name, DbAgentCodes.InternalError, ex.GetType().Name + ": " + ex.Message);
        }
        return Task.FromResult(DbToolJson.Serialize(result, svc));
    }

    #region 参数读取
    protected static String RequireString(JsonElement a, String name)
    {
        var v = OptString(a, name);
        if (String.IsNullOrWhiteSpace(v)) throw new DbToolArgumentException($"缺少必填参数 {name}（字符串）");
        return v;
    }

    protected static String? OptString(JsonElement a, String name) =>
        a.TryGetProperty(name, out var p) ? p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new DbToolArgumentException($"参数 {name} 应为字符串"),
        } : null;

    protected static Int64 RequireInt64(JsonElement a, String name)
    {
        if (a.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && Int64.TryParse(p.GetString(), out n)) return n;
        }
        throw new DbToolArgumentException($"缺少必填参数 {name}（整数）");
    }

    protected static Int32? OptInt32(JsonElement a, String name)
    {
        if (!a.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        if (p.ValueKind == JsonValueKind.String && Int32.TryParse(p.GetString(), out n)) return n;
        throw new DbToolArgumentException($"参数 {name} 应为整数");
    }

    protected static Boolean RequireBool(JsonElement a, String name)
    {
        if (a.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
            if (p.ValueKind == JsonValueKind.String && Boolean.TryParse(p.GetString(), out var b)) return b;
        }
        throw new DbToolArgumentException($"缺少必填参数 {name}（true/false）");
    }

    /// <summary>params：对象，值只收标量（字符串/数字/布尔/null）</summary>
    protected static Dictionary<String, Object?>? OptParams(JsonElement a)
    {
        if (!a.TryGetProperty("params", out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind != JsonValueKind.Object) throw new DbToolArgumentException("参数 params 应为对象，如 {\"id\": 1}（SQL 中写 @id）");
        var d = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in p.EnumerateObject())
        {
            d[kv.Name.TrimStart('@', ':', '$')] = kv.Value.ValueKind switch
            {
                JsonValueKind.String => kv.Value.GetString(),
                JsonValueKind.Number => kv.Value.TryGetInt64(out var l) ? l : kv.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => throw new DbToolArgumentException($"params.{kv.Name} 只能是字符串/数字/布尔/null"),
            };
        }
        return d;
    }

    protected static String SessionOrDefault(JsonElement a) => OptString(a, "sessionId") is { Length: > 0 } s ? s : "default";
    #endregion
}

/// <summary>参数错误（映射为 invalid_arguments）</summary>
public sealed class DbToolArgumentException(String message) : Exception(message);

/// <summary>DbAdmin 工具回包构造与出口擦除</summary>
public static class DbToolJson
{
    /// <summary>Agent 回包字节上限（与 <see cref="ResultLimiter.AgentMaxBytes"/> 一致）</summary>
    public const Int32 MaxOutputBytes = ResultLimiter.AgentMaxBytes;

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Object Ok(String tool, Object? data, String? hint = null, String status = "ok") =>
        Compact(new Dictionary<String, Object?> { ["success"] = true, ["tool"] = tool, ["status"] = status, ["data"] = data is Dictionary<String, Object?> d ? Compact(d) : data, ["hint"] = hint });

    private static Dictionary<String, Object?> Compact(Dictionary<String, Object?> d)
    {
        foreach (var k in d.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList()) d.Remove(k);
        return d;
    }

    private static readonly Regex SqlEchoRx = new(@"\s*\[SQL:[\s\S]*\]\s*$", RegexOptions.Compiled);

    public static Object Fail(String tool, String? code, String? message, String status = "error", String? hint = null)
    {
        code ??= DbAgentCodes.InternalError;
        return new Dictionary<String, Object?>
        {
            ["success"] = false,
            ["tool"] = tool,
            ["status"] = status,
            ["code"] = code,
            ["message"] = SqlRedactor.RedactText(message == null ? null : SqlEchoRx.Replace(message, "")),
            ["hint"] = hint ?? HintFor(code, message),
        };
    }

    /// <summary>按错误码给出下一步建议（面向 LLM）</summary>
    public static String HintFor(String code, String? message) => code switch
    {
        DbErrorCodes.RejectedHard => "硬拒：该语句命中安全硬拒清单，在任何权限模式、任何审批下都不会执行；不要改写措辞绕过。"
            + (message?.Contains("多条") == true ? "请拆成单条语句逐条提交。" : "")
            + (message?.Contains("WHERE") == true ? "确需更新/删除时请加上精确的 WHERE 条件，再用 db_execute_write 提交。" : "")
            + "如确属必要，请向用户说明原因，由用户决定如何处理。",
        DbErrorCodes.ReadonlyConnection => "该连接只读（系统库 sys:* 恒为只读，或用户把连接设为只读），任何写操作都会被拒，审批也无法放行。只读查询请用 db_query_readonly；"
            + "确需写入请让用户在 DbAdmin 界面新建一个可写的用户连接（Agent 不能创建连接）。",
        DbErrorCodes.DriverMissing => "目标数据库驱动未安装，无法连接。请告知用户按提示安装驱动包后重试；当前只能操作 SQLite 连接。",
        DbErrorCodes.SqlError when message?.Contains("文件不存在") == true => "该连接指向的库文件不存在（DbAdmin 不会自动建库）。请让用户在 DbAdmin 界面检查或修改该连接；不要尝试创建或删除文件。",
        DbErrorCodes.SqlError => "SQL 执行出错。请先用 db_list_tables / db_describe_table 核对表名、列名与类型，修正后重试；不要反复盲试。",
        DbErrorCodes.NotFound => "对象不存在（连接或审批）。连接请先调用 db_list_connections 取 id（系统库形如 \"sys:Name\"，用户连接为数字串）；审批可能已处理或已过期（30 分钟），需要时重新提交。",
        DbErrorCodes.ConfirmationRequired => "破坏性语句（DROP / TRUNCATE / ALTER…DROP）只能由用户在 DbAdmin 界面「审批」中批准并二次确认，Agent 无权批准。请把 approvalId 告诉用户并等待。",
        DbErrorCodes.InvalidRequest => "请求不合法（如审批已处理过、模式取值错误）。请按 message 修正参数后重试。",
        DbErrorCodes.Unauthorized => "无权限。请让用户在界面操作。",
        "approval_rejected" => "用户已驳回该写操作，不要重复提交同一语句；如有需要先询问用户意图。",
        DbAgentCodes.NotReadonlySql => "db_query_readonly 只接受 SELECT / WITH 查询。写操作请改用 db_execute_write（会进入审批）；查看表结构请用 db_list_tables / db_describe_table。",
        DbAgentCodes.UseQueryReadonly => "这是只读查询，请改用 db_query_readonly。",
        DbAgentCodes.InvalidArguments => "参数不合法，请对照工具参数说明修正后重试。",
        DbAgentCodes.UserConfirmationRequired => "批准写操作前必须先在对话中征得用户明确同意，并把用户的原话填入 userConfirmationQuote。不要自行批准；破坏性语句即使有原话也只能由用户在界面确认。",
        _ => "内部错误。请稍后重试一次；仍失败请告知用户查看 DbAdmin 审计日志。",
    };

    /// <summary>QueryOutcome → 回包（read 结果 / 写执行 / 待审批 / 拒绝 / 错误）</summary>
    public static Object FromOutcome(String tool, QueryOutcome o, IReadOnlyCollection<String>? maskedColumns = null, String? extraHint = null)
    {
        switch (o.Status)
        {
            case "ok" when o.Rows != null || o.Columns != null:
                return Ok(tool, new Dictionary<String, Object?>
                {
                    ["columns"] = o.Columns,
                    ["columnTypes"] = o.ColumnTypes,
                    ["rows"] = o.Rows,
                    ["rowCount"] = o.Rows?.Count ?? 0,
                    ["truncated"] = o.Truncated,
                    ["maskedColumns"] = maskedColumns is { Count: > 0 } ? maskedColumns : null,
                    ["elapsedMs"] = o.ElapsedMs,
                }, o.Truncated
                    ? $"结果已截断（返回 {o.Rows?.Count ?? 0} 行；上限 maxRows / {ResultLimiter.AgentMaxRows} 行 / 256KB）。如需更多，请加 WHERE 条件、只选需要的列，或用 COUNT/GROUP BY 聚合、ORDER BY…LIMIT 分批。"
                    : extraHint);
            case "ok":
                return Ok(tool, new Dictionary<String, Object?>
                {
                    ["affectedRows"] = o.AffectedRows,
                    ["kind"] = o.Kind,
                    ["destructive"] = o.Destructive,
                    ["approvalId"] = o.ApprovalId,
                    ["elapsedMs"] = o.ElapsedMs,
                }, extraHint ?? "写操作已执行。可用 db_query_readonly 复核结果。");
            case "awaiting_approval":
                return Ok(tool, new Dictionary<String, Object?>
                {
                    ["approvalId"] = o.ApprovalId,
                    ["kind"] = o.Kind,
                    ["destructive"] = o.Destructive,
                    ["message"] = SqlRedactor.RedactText(o.Message),
                }, o.Destructive
                    ? $"破坏性语句已进入审批队列（approvalId={o.ApprovalId}）。只有用户本人能在 DbAdmin 界面「审批」中批准并二次确认，Agent 不能批准。请告知用户并等待；30 分钟未处理自动过期。"
                    : $"写操作未执行，已进入审批队列（approvalId={o.ApprovalId}）。请告知用户去 DbAdmin 界面「审批」中批准；Agent 不能自己批准。只有用户在对话里明确同意时，才可调用 db_resolve_approval 并附上用户原话。30 分钟未处理自动过期。",
                    "awaiting_approval");
            default:
                return Fail(tool, o.Code, o.Message, o.Status == "rejected" ? "rejected" : "error");
        }
    }

    /// <summary>序列化 + 出口擦除：RedactText 已在 message 上做过；这里再按明文密钥清单逐字替换（含 JSON 转义形态）。</summary>
    public static String Serialize(Object result, DbAdminAgentService? svc)
    {
        var json = JsonSerializer.Serialize(result, Options);
        return Scrub(json, svc);
    }

    /// <summary>
    /// 逐个字符串值替换明文密钥为 ****（只动 JSON 字符串值，不动数字/结构，保证回包仍是合法 JSON）。
    /// 密钥清单见 <see cref="DbConnectionService.CollectSecretsForScrub"/>。
    /// </summary>
    public static String Scrub(String json, DbAdminAgentService? svc)
    {
        if (svc == null) return json;
        List<String> secrets;
        try { secrets = svc.SecretsForScrub().OrderByDescending(x => x.Length).ToList(); } catch { return json; }
        if (secrets.Count == 0 || !secrets.Any(x => json.Contains(x, StringComparison.Ordinal) || json.Contains(JsonEncodedText.Encode(x, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString(), StringComparison.Ordinal)))
            return json;
        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
        var changed = false;
        String? Fix(System.Text.Json.Nodes.JsonNode? n)
        {
            if (n is not System.Text.Json.Nodes.JsonValue v || v.GetValueKind() != JsonValueKind.String) return null;
            var str = v.GetValue<String>();
            var orig = str;
            foreach (var sec in secrets) str = str.Replace(sec, "****", StringComparison.Ordinal);
            return str == orig ? null : str;
        }
        void Walk(System.Text.Json.Nodes.JsonNode? n)
        {
            if (n is System.Text.Json.Nodes.JsonObject o)
            {
                foreach (var key in o.Select(kv => kv.Key).ToList())
                {
                    if (Fix(o[key]) is { } r) { o[key] = r; changed = true; }
                    else Walk(o[key]);
                }
            }
            else if (n is System.Text.Json.Nodes.JsonArray a)
            {
                for (var i = 0; i < a.Count; i++)
                {
                    if (Fix(a[i]) is { } r) { a[i] = r; changed = true; }
                    else Walk(a[i]);
                }
            }
        }
        Walk(node);
        var outNode = node;
        return changed ? outNode!.ToJsonString(Options) : json;
    }

    /// <summary>列名像密钥（password / connstr / token / secret …）的列，值一律掩码</summary>
    public static readonly Regex SecretColumnRx = new(
        @"(pass(word)?|passwd|pwd|secret|api_?key|access_?token|refresh_?token|private_?key|conn(ection)?_?str(ing)?|credential)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>掩码密钥列并返回被掩码的列名</summary>
    public static List<String> MaskSecretColumns(QueryOutcome o)
    {
        var masked = new List<String>();
        if (o.Columns == null || o.Rows == null) return masked;
        var idx = new List<Int32>();
        for (var i = 0; i < o.Columns.Count; i++)
            if (SecretColumnRx.IsMatch(o.Columns[i] ?? "")) { idx.Add(i); masked.Add(o.Columns[i]); }
        if (idx.Count == 0) return masked;
        foreach (var row in o.Rows)
            foreach (var i in idx)
                if (i < row.Length && row[i] != null) row[i] = "****";
        return masked;
    }

    /// <summary>按序列化字节数兜底：超过 <see cref="MaxOutputBytes"/> 就从尾部丢行并置 truncated</summary>
    public static void EnforceByteLimit(QueryOutcome o)
    {
        if (o.Rows == null || o.Rows.Count == 0) return;
        Int32 Size() => System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(o.Rows, Options)) + 2048;
        var size = Size();
        while (size > MaxOutputBytes && o.Rows.Count > 0)
        {
            var keep = Math.Max(0, Math.Min(o.Rows.Count - 1, (Int32)((Int64)o.Rows.Count * MaxOutputBytes / size) - 1));
            o.Rows = o.Rows.Take(keep).ToList();
            o.Truncated = true;
            size = Size();
        }
    }
}

/// <summary>db_list_connections</summary>
public sealed class DbListConnectionsTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_list_connections";
    public override string Description =>
        "列出可用的数据库连接（只读，无副作用）。返回每个连接的 id、name、provider（sqlite/mysql/postgresql/sqlserver）、kind（system=宿主/插件系统库，恒只读；user=用户在界面创建的连接）、readonly、writePolicy，以及当前会话的权限模式。" +
        "其它 db_* 工具的 connectionId 都从这里取。不返回主机、库路径、账号、密码或连接串。Agent 不能创建或修改连接，需要新连接时请让用户在 DbAdmin 界面创建。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "sessionId": { "type": "string", "description": "当前对话会话 Id（可选，用于显示本会话生效的权限模式；同一对话请保持一致）" }
      }
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var sid = SessionOrDefault(args);
        var mode = svc.EffectiveMode(sid);
        var list = svc.ListConnections().Select(c => new Dictionary<String, Object?>
        {
            ["id"] = c.Id,
            ["name"] = c.Name,
            ["provider"] = c.Provider,
            ["kind"] = c.Kind,
            ["readonly"] = c.Readonly,
            ["tested"] = c.Tested,
            ["writePolicy"] = c.Readonly
                ? (c.Kind == "system" ? "readonly_system（系统库，任何写都拒绝）" : "readonly（用户设为只读，任何写都拒绝）")
                : (mode == PermissionModes.Auto ? "auto（普通写直接执行，破坏性仍需界面审批）" : "ask_write（写操作进入审批队列）"),
        }).ToList();
        return DbToolJson.Ok(Name, new Dictionary<String, Object?>
        {
            ["connections"] = list,
            ["permissionMode"] = mode,
            ["globalMode"] = svc.GlobalMode,
        }, list.Count == 0
            ? "当前没有可用连接。请让用户在 DbAdmin 界面新建连接（Agent 不能创建连接）。"
            : "下一步：db_list_tables(connectionId) 看有哪些表；系统库（kind=system）只能查询。");
    }
}

/// <summary>db_list_tables</summary>
public sealed class DbListTablesTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_list_tables";
    public override string Description =>
        "列出某个连接下的表和视图（只读，无副作用）。返回表名、类型（table/view）、注释。connectionId 来自 db_list_connections（系统库形如 \"sys:Name\"，用户连接为数字串）。看列结构请再调用 db_describe_table。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "connectionId": { "type": "string", "description": "连接 id，来自 db_list_connections，如 \"sys:Membership\" 或 \"3\"" }
      },
      "required": ["connectionId"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var cid = RequireString(args, "connectionId");
        var tables = svc.ListTables(cid);
        return DbToolJson.Ok(Name, new Dictionary<String, Object?> { ["connectionId"] = cid, ["tables"] = tables, ["count"] = tables.Count },
            tables.Count == 0 ? "该连接下没有表。" : "下一步：db_describe_table(connectionId, table) 看列与索引，然后用 db_query_readonly 查询。");
    }
}

/// <summary>db_describe_table</summary>
public sealed class DbDescribeTableTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_describe_table";
    public override string Description =>
        "查看一张表的结构（只读，无副作用）：列名、类型、是否可空、主键、默认值、索引。写 SQL 前先用它核对列名，避免 sql_error。表名来自 db_list_tables。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "connectionId": { "type": "string", "description": "连接 id，来自 db_list_connections" },
        "table": { "type": "string", "description": "表名或视图名，来自 db_list_tables" }
      },
      "required": ["connectionId", "table"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var cid = RequireString(args, "connectionId");
        var table = RequireString(args, "table");
        return DbToolJson.Ok(Name, svc.DescribeTable(cid, table), "下一步：用 db_query_readonly 查询，例如 SELECT 需要的列 FROM 表 WHERE … LIMIT 50。");
    }
}

/// <summary>db_query_readonly</summary>
public sealed class DbQueryReadonlyTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public const Int32 DefaultMaxRows = 200;

    public override string Name => "db_query_readonly";
    public override string Description =>
        "执行一条只读查询（SELECT 或 WITH…SELECT），系统库和用户库都可用。" +
        "强制上限：最多返回 1000 行且回包不超过 256KB，超出时截断并返回 truncated=true（请缩小范围，不要试图绕过）。" +
        "只接受单条 SELECT / WITH；INSERT/UPDATE/DELETE/DDL 会被拒（code=not_readonly_sql，写操作请用 db_execute_write），PRAGMA/SHOW 也会被拒（请用 db_describe_table）。" +
        "多语句、DROP DATABASE 等命中硬拒清单（code=rejected_hard），任何模式都不会执行。" +
        "值请用 params 传入并在 SQL 中写 @name，不要拼接字符串。列名像 password/secret/token/connstr 的列值会被掩码为 ****。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "connectionId": { "type": "string", "description": "连接 id，来自 db_list_connections" },
        "sql": { "type": "string", "description": "单条 SELECT 或 WITH…SELECT 语句，参数写 @name" },
        "params": { "type": "object", "description": "参数值，如 {\"id\": 1, \"name\": \"abc\"}；只收字符串/数字/布尔/null", "additionalProperties": true },
        "maxRows": { "type": "integer", "minimum": 1, "maximum": 1000, "description": "最多返回行数，默认 200，硬上限 1000" },
        "sessionId": { "type": "string", "description": "当前对话会话 Id（可选，用于审计回显）" }
      },
      "required": ["connectionId", "sql"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var cid = RequireString(args, "connectionId");
        var sql = RequireString(args, "sql");
        var ps = OptParams(args);
        var maxRows = Math.Clamp(OptInt32(args, "maxRows") ?? DefaultMaxRows, 1, ResultLimiter.AgentMaxRows);
        var o = svc.QueryReadonlyStrict(cid, sql, ps, SessionOrDefault(args), maxRows);
        if (o.Status != "ok") return DbToolJson.FromOutcome(Name, o);
        var masked = DbToolJson.MaskSecretColumns(o);
        DbToolJson.EnforceByteLimit(o);
        return DbToolJson.FromOutcome(Name, o, masked);
    }
}

/// <summary>db_execute_write</summary>
public sealed class DbExecuteWriteTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_execute_write";
    public override string Description =>
        "提交一条写语句（INSERT / UPDATE…WHERE / DELETE…WHERE / CREATE / ALTER 等），仅限用户连接；系统库（kind=system）与只读连接一律拒绝（code=readonly_connection）。" +
        "默认 AskWrite 模式下不会立即执行，而是返回 status=awaiting_approval 和 approvalId，必须由用户在 DbAdmin 界面批准——Agent 不能自己批准。" +
        "破坏性语句（DROP / TRUNCATE / ALTER…DROP）永远需要用户在界面二次确认。" +
        "硬拒（code=rejected_hard，任何模式/审批都不放行）：多语句、无 WHERE 的 UPDATE/DELETE、DROP DATABASE 等。只读查询请用 db_query_readonly。值请用 params 传入。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "connectionId": { "type": "string", "description": "用户连接 id（数字串），来自 db_list_connections；sys:* 系统库只读" },
        "sql": { "type": "string", "description": "单条写语句；UPDATE/DELETE 必须带 WHERE；参数写 @name" },
        "params": { "type": "object", "description": "参数值，如 {\"id\": 1}", "additionalProperties": true },
        "sessionId": { "type": "string", "description": "当前对话会话 Id（决定生效的会话级权限模式，并用于审计回显）" }
      },
      "required": ["connectionId", "sql"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var cid = RequireString(args, "connectionId");
        var sql = RequireString(args, "sql");
        var o = svc.ExecuteWriteStrict(cid, sql, OptParams(args), SessionOrDefault(args));
        return DbToolJson.FromOutcome(Name, o);
    }
}

/// <summary>db_set_permission_mode</summary>
public sealed class DbSetPermissionModeTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_set_permission_mode";
    public override string Description =>
        "切换本会话的写操作权限模式（只影响本会话，不改全局默认；全局默认只能由用户在界面修改）。" +
        "AskWrite：写操作进入审批队列（默认、推荐）；Auto：普通写直接执行。任何模式下：系统库仍只读，硬拒仍拒绝，破坏性语句仍需用户在界面二次确认。" +
        "只有用户在对话中明确要求切换时才调用。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "mode": { "type": "string", "enum": ["AskWrite", "Auto"], "description": "目标模式" },
        "sessionId": { "type": "string", "description": "当前对话会话 Id（必填，模式按会话生效）" }
      },
      "required": ["mode", "sessionId"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var mode = RequireString(args, "mode");
        var sid = RequireString(args, "sessionId");
        var v = svc.SetPermissionMode(mode, sid);
        return DbToolJson.Ok(Name, new Dictionary<String, Object?> { ["mode"] = v.Mode, ["globalMode"] = v.Default, ["scope"] = "session", ["sessionId"] = sid },
            v.Mode == PermissionModes.Auto
                ? "本会话已切到 Auto：普通写会直接执行，请谨慎；破坏性语句仍需用户在界面二次确认，系统库仍只读。"
                : "本会话已切到 AskWrite：写操作会进入审批队列。");
    }
}

/// <summary>db_resolve_approval</summary>
public sealed class DbResolveApprovalTool(string pluginId, IServiceProvider? services) : DbToolBase(pluginId, services)
{
    public override string Name => "db_resolve_approval";
    public override string Description =>
        "按用户的明确指示处理一条待审批的写操作。approve=false（撤回/驳回）随时可用；" +
        "approve=true 只能在用户本人在对话中明确同意后调用，并必须把用户原话填入 userConfirmationQuote——不得自行批准、不得替用户编造同意。" +
        "破坏性语句（destructive=true：DROP/TRUNCATE/ALTER…DROP）Agent 永远无法批准，会返回 confirmation_required，只能由用户在 DbAdmin 界面二次确认。";
    public override string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "approvalId": { "type": "integer", "description": "db_execute_write 返回的 approvalId" },
        "approve": { "type": "boolean", "description": "true=按用户同意执行；false=撤回/驳回" },
        "userConfirmationQuote": { "type": "string", "description": "approve=true 时必填：用户在对话中表示同意的原话" },
        "sessionId": { "type": "string", "description": "当前对话会话 Id（用于审计回显）" }
      },
      "required": ["approvalId", "approve"]
    }
    """;

    protected override Object Invoke(DbAdminAgentService svc, JsonElement args)
    {
        var id = RequireInt64(args, "approvalId");
        var approve = RequireBool(args, "approve");
        var sid = SessionOrDefault(args);
        if (approve)
        {
            var a = svc.FindApproval(id);
            if (a != null && a.Destructive)
                return DbToolJson.Fail(Name, DbErrorCodes.ConfirmationRequired, $"审批 {id} 是破坏性语句，Agent 无权批准", "rejected");
            if (String.IsNullOrWhiteSpace(OptString(args, "userConfirmationQuote")))
                return DbToolJson.Fail(Name, DbAgentCodes.UserConfirmationRequired, "approve=true 需要 userConfirmationQuote（用户同意的原话）", "rejected");
        }
        var o = svc.ResolveApproval(id, approve, sid);
        if (!approve && o.Status == "rejected")
            return DbToolJson.Ok(Name, new Dictionary<String, Object?> { ["approvalId"] = id, ["resolved"] = "rejected" }, "已驳回该写操作，语句未执行。");
        return DbToolJson.FromOutcome(Name, o, extraHint: "已按用户同意执行。可用 db_query_readonly 复核。");
    }
}

/// <summary>工具清单（插件注册与测试共用）</summary>
public static class DbAdminTools
{
    public static IReadOnlyList<IToolFunctionExtension> Create(string pluginId, IServiceProvider? services) =>
    [
        new DbListConnectionsTool(pluginId, services),
        new DbListTablesTool(pluginId, services),
        new DbDescribeTableTool(pluginId, services),
        new DbQueryReadonlyTool(pluginId, services),
        new DbExecuteWriteTool(pluginId, services),
        new DbSetPermissionModeTool(pluginId, services),
        new DbResolveApprovalTool(pluginId, services),
    ];
}

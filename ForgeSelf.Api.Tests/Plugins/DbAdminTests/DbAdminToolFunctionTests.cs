using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Microsoft.Extensions.DependencyInjection;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>
/// db_* Agent 工具（IToolFunctionExtension）单测：真 MS 容器 + 临时 SQLite（夹具随机目录，只建不删）。
/// 覆盖：命名/前缀、上限截断、非 SELECT 拒绝、AskWrite 待审批、Agent 无法自批、系统库只读、密码/连接串零泄漏。
/// </summary>
[Collection(DbAdminCollection.Name)]
public class DbAdminToolFunctionTests(DbAdminTestFixture fx)
{
    internal const String Secret = "S3cr3t-Pa55";

    internal static Dictionary<String, IToolFunctionExtension> Tools(DbAdminTestFixture fx)
    {
        var sp = new ServiceCollection().AddSingleton(new DbAdminAgentService(fx.Connections, fx.Queries)).BuildServiceProvider();
        return DbAdminTools.Create("db-admin", sp).ToDictionary(t => t.Name);
    }

    internal static JsonElement Call(Dictionary<String, IToolFunctionExtension> tools, String name, Object? args = null, List<String>? sink = null)
    {
        var raw = tools[name].ExecuteAsync(args == null ? "{}" : JsonSerializer.Serialize(args)).GetAwaiter().GetResult();
        sink?.Add(raw);
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    /// <summary>用户连接：items(1500 行可选) + accounts(password 列)</summary>
    internal static String NewUserConn(DbAdminTestFixture fx, Int32 rows = 3)
    {
        var file = fx.CreateTargetDb("tool");
        var v = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("tool"), Provider = "sqlite", Database = file });
        var dal = DAL.Create(fx.Connections.Resolve(v.Id).ConnName);
        if (rows > 0)
            dal.Execute($"WITH RECURSIVE c(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM c WHERE x < {rows}) INSERT INTO items (name, note) SELECT 'row' || x, 'n' FROM c");
        dal.Execute("CREATE TABLE IF NOT EXISTS accounts (id INTEGER PRIMARY KEY, username TEXT, password TEXT)");
        dal.Execute("INSERT INTO accounts (username, password) VALUES ('alice', 'hunter2-plain')");
        return v.Id;
    }

    /// <summary>connstr 模式、含密码、指向不存在文件的用户连接（错误路径不得泄漏）</summary>
    internal static String NewSecretConn(DbAdminTestFixture fx)
    {
        var missing = Path.Combine(fx.Dir, "missing_" + Guid.NewGuid().ToString("N")[..6] + ".db");
        return fx.Connections.Create(new ConnectionInput
        {
            Name = DbAdminTestFixture.UniqueName("sec"), Provider = "sqlite", Mode = "connstr",
            ConnStr = $"Data Source={missing};Password={Secret}",
        }).Id;
    }

    private static String Sid() => "tool-" + Guid.NewGuid().ToString("N")[..8];
    private const String SysId = DbConnectionService.SystemIdPrefix + DbAdminTestFixture.SystemConnName;

    [Fact]
    public void Tools_SevenWithDbPrefix_IdsAndSchemas()
    {
        var tools = DbAdminTools.Create("db-admin", null);
        Assert.Equal(7, tools.Count);
        Assert.Equal("db", ToolNamespace.PrefixByPluginId["db-admin"]);
        foreach (var t in tools)
        {
            Assert.StartsWith("db_", t.Name);
            Assert.Null(ToolNamespace.Validate("db-admin", t.Name));
            Assert.Equal("db-admin." + t.Name[3..], t.Id);
            Assert.Equal("db-admin", t.PluginId);
            Assert.False(String.IsNullOrWhiteSpace(t.Description));
            using var schema = JsonDocument.Parse(t.ParametersJsonSchema);
            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        }
        Assert.Equal(new[] { "db_list_connections", "db_list_tables", "db_describe_table", "db_query_readonly", "db_execute_write", "db_set_permission_mode", "db_resolve_approval" },
            tools.Select(t => t.Name));
    }

    [Fact]
    public void NoServiceProvider_ReturnsStructuredError_NotThrow()
    {
        var t = new DbListConnectionsTool("db-admin", null);
        using var doc = JsonDocument.Parse(t.ExecuteAsync("{}").GetAwaiter().GetResult());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(DbAgentCodes.InternalError, doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void ListConnections_NoSecretsFields_HasWritePolicy()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx, 0);
        var r = Call(tools, "db_list_connections", new { sessionId = Sid() });
        Assert.True(r.GetProperty("success").GetBoolean());
        var conns = r.GetProperty("data").GetProperty("connections").EnumerateArray().ToList();
        var sys = conns.Single(c => c.GetProperty("id").GetString() == SysId);
        Assert.True(sys.GetProperty("readonly").GetBoolean());
        Assert.Equal("system", sys.GetProperty("kind").GetString());
        Assert.StartsWith("readonly_system", sys.GetProperty("writePolicy").GetString());
        var user = conns.Single(c => c.GetProperty("id").GetString() == cid);
        Assert.False(user.GetProperty("readonly").GetBoolean());
        foreach (var c in conns)
            foreach (var p in c.EnumerateObject())
                Assert.DoesNotMatch("(?i)pass|connstr|host|database|user", p.Name);
    }

    [Fact]
    public void ListTables_DescribeTable_Work_AndUnknownConnGivesHint()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx, 0);
        var t = Call(tools, "db_list_tables", new { connectionId = cid });
        Assert.True(t.GetProperty("success").GetBoolean());
        Assert.Contains("items", t.GetRawText());
        var d = Call(tools, "db_describe_table", new { connectionId = cid, table = "items" });
        Assert.True(d.GetProperty("success").GetBoolean(), d.GetRawText());
        Assert.Contains("note", d.GetRawText());

        var nf = Call(tools, "db_list_tables", new { connectionId = "999999" });
        Assert.False(nf.GetProperty("success").GetBoolean());
        Assert.Equal(DbErrorCodes.NotFound, nf.GetProperty("code").GetString());
        Assert.Contains("db_list_connections", nf.GetProperty("hint").GetString());

        var miss = Call(tools, "db_describe_table", new { connectionId = cid });
        Assert.Equal(DbAgentCodes.InvalidArguments, miss.GetProperty("code").GetString());
        var bad = tools["db_list_tables"].ExecuteAsync("not json").GetAwaiter().GetResult();
        Assert.Contains(DbAgentCodes.InvalidArguments, bad);
    }

    [Fact]
    public void QueryReadonly_DefaultLimit_MaxRows_HardCap_Truncated()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx, 1500);
        var sid = Sid();
        var def = Call(tools, "db_query_readonly", new { connectionId = cid, sql = "SELECT id, name FROM items", sessionId = sid });
        Assert.True(def.GetProperty("success").GetBoolean(), def.GetRawText());
        Assert.Equal(DbQueryReadonlyTool.DefaultMaxRows, def.GetProperty("data").GetProperty("rowCount").GetInt32());
        Assert.True(def.GetProperty("data").GetProperty("truncated").GetBoolean());
        Assert.Contains("截断", def.GetProperty("hint").GetString());

        var ten = Call(tools, "db_query_readonly", new { connectionId = cid, sql = "SELECT id FROM items", maxRows = 10, sessionId = sid });
        Assert.Equal(10, ten.GetProperty("data").GetProperty("rows").GetArrayLength());

        var cap = Call(tools, "db_query_readonly", new { connectionId = cid, sql = "SELECT id FROM items", maxRows = 5000, sessionId = sid });
        Assert.Equal(ResultLimiter.AgentMaxRows, cap.GetProperty("data").GetProperty("rowCount").GetInt32());
        Assert.True(cap.GetProperty("data").GetProperty("truncated").GetBoolean());

        var small = Call(tools, "db_query_readonly", new { connectionId = cid, sql = "WITH t AS (SELECT id FROM items WHERE id <= @n) SELECT COUNT(*) AS c FROM t", @params = new { n = 7 }, sessionId = sid });
        Assert.True(small.GetProperty("success").GetBoolean(), small.GetRawText());
        Assert.False(small.GetProperty("data").GetProperty("truncated").GetBoolean());
        Assert.Equal(7, small.GetProperty("data").GetProperty("rows")[0][0].GetInt64());
    }

    [Fact]
    public void QueryReadonly_ByteCap_256KB()
    {
        var tools = Tools(fx);
        var file = fx.CreateTargetDb("big");
        var cid = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("big"), Provider = "sqlite", Database = file }).Id;
        var dal = DAL.Create(fx.Connections.Resolve(cid).ConnName);
        var note = new String('x', 2000);
        dal.Execute($"WITH RECURSIVE c(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM c WHERE x < 600) INSERT INTO items (name, note) SELECT 'r' || x, '{note}' FROM c");
        var raw = tools["db_query_readonly"].ExecuteAsync(JsonSerializer.Serialize(new { connectionId = cid, sql = "SELECT id, note FROM items", maxRows = 1000, sessionId = Sid() })).GetAwaiter().GetResult();
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(raw) <= ResultLimiter.AgentMaxBytes, $"回包 {raw.Length} 字节超上限");
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("truncated").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("rowCount").GetInt32() < 600);
    }

    [Theory]
    [InlineData("INSERT INTO items (name) VALUES ('x')", "not_readonly_sql")]
    [InlineData("UPDATE items SET name='x' WHERE id=1", "not_readonly_sql")]
    [InlineData("DROP TABLE items", "not_readonly_sql")]
    [InlineData("PRAGMA table_info(items)", "not_readonly_sql")]
    [InlineData("WITH t AS (SELECT 1) DELETE FROM items WHERE id = 1", "not_readonly_sql")]
    [InlineData("SELECT 1; DROP TABLE items", "rejected_hard")]
    [InlineData("DELETE FROM items", "rejected_hard")]
    [InlineData("ATTACH DATABASE 'x.db' AS x", "rejected_hard")]
    public void QueryReadonly_RejectsNonSelect(String sql, String code)
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var r = Call(tools, "db_query_readonly", new { connectionId = cid, sql, sessionId = Sid() });
        Assert.False(r.GetProperty("success").GetBoolean());
        Assert.Equal("rejected", r.GetProperty("status").GetString());
        Assert.Equal(code, r.GetProperty("code").GetString());
        Assert.False(String.IsNullOrWhiteSpace(r.GetProperty("hint").GetString()));
        // 语句未被执行
        Assert.Equal(3L, DAL.Create(fx.Connections.Resolve(cid).ConnName).ExecuteScalar<Int64>("SELECT COUNT(*) FROM items"));
    }

    [Fact]
    public void ExecuteWrite_AskWrite_AwaitingApproval_NotExecuted()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var sid = Sid();
        fx.Modes.SetSession(sid, PermissionModes.AskWrite);
        var r = Call(tools, "db_execute_write", new { connectionId = cid, sql = "UPDATE items SET name = @n WHERE id = 1", @params = new { n = "foo" }, sessionId = sid });
        Assert.True(r.GetProperty("success").GetBoolean(), r.GetRawText());
        Assert.Equal("awaiting_approval", r.GetProperty("status").GetString());
        Assert.True(r.GetProperty("data").GetProperty("approvalId").GetInt64() > 0);
        Assert.False(r.GetProperty("data").GetProperty("destructive").GetBoolean());
        Assert.Contains("不能自己批准", r.GetProperty("hint").GetString());
        var dal = DAL.Create(fx.Connections.Resolve(cid).ConnName);
        Assert.Equal("row1", dal.ExecuteScalar<String>("SELECT name FROM items WHERE id=1"));

        var read = Call(tools, "db_execute_write", new { connectionId = cid, sql = "SELECT * FROM items", sessionId = sid });
        Assert.Equal(DbAgentCodes.UseQueryReadonly, read.GetProperty("code").GetString());
    }

    [Fact]
    public void ResolveApproval_NeedsUserQuote_ThenExecutes_RejectAlwaysAllowed()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var sid = Sid();
        fx.Modes.SetSession(sid, PermissionModes.AskWrite);
        var w = Call(tools, "db_execute_write", new { connectionId = cid, sql = "UPDATE items SET name = 'foo' WHERE id = 1", sessionId = sid });
        var id = w.GetProperty("data").GetProperty("approvalId").GetInt64();

        var noQuote = Call(tools, "db_resolve_approval", new { approvalId = id, approve = true, sessionId = sid });
        Assert.False(noQuote.GetProperty("success").GetBoolean());
        Assert.Equal(DbAgentCodes.UserConfirmationRequired, noQuote.GetProperty("code").GetString());
        Assert.Equal("pending", fx.Queries.ListApprovals(null).Single(a => a.Id == id).Status);

        var ok = Call(tools, "db_resolve_approval", new { approvalId = id, approve = true, userConfirmationQuote = "可以，执行", sessionId = sid });
        Assert.True(ok.GetProperty("success").GetBoolean(), ok.GetRawText());
        Assert.Equal(1, ok.GetProperty("data").GetProperty("affectedRows").GetInt64());

        var again = Call(tools, "db_resolve_approval", new { approvalId = id, approve = true, userConfirmationQuote = "再来", sessionId = sid });
        Assert.Equal(DbErrorCodes.InvalidRequest, again.GetProperty("code").GetString());

        var w2 = Call(tools, "db_execute_write", new { connectionId = cid, sql = "UPDATE items SET name = 'bar' WHERE id = 2", sessionId = sid });
        var rej = Call(tools, "db_resolve_approval", new { approvalId = w2.GetProperty("data").GetProperty("approvalId").GetInt64(), approve = false, sessionId = sid });
        Assert.True(rej.GetProperty("success").GetBoolean(), rej.GetRawText());
        Assert.Equal("rejected", rej.GetProperty("data").GetProperty("resolved").GetString());
    }

    [Fact]
    public void Destructive_AgentCannotApprove_EvenInAutoAndWithQuote()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var sid = Sid();
        Call(tools, "db_set_permission_mode", new { mode = "Auto", sessionId = sid });
        var w = Call(tools, "db_execute_write", new { connectionId = cid, sql = "DROP TABLE items", sessionId = sid });
        Assert.Equal("awaiting_approval", w.GetProperty("status").GetString());
        Assert.True(w.GetProperty("data").GetProperty("destructive").GetBoolean());
        Assert.Contains("二次确认", w.GetProperty("hint").GetString());
        var id = w.GetProperty("data").GetProperty("approvalId").GetInt64();

        var r = Call(tools, "db_resolve_approval", new { approvalId = id, approve = true, userConfirmationQuote = "我同意删除", sessionId = sid });
        Assert.False(r.GetProperty("success").GetBoolean());
        Assert.Equal(DbErrorCodes.ConfirmationRequired, r.GetProperty("code").GetString());
        Assert.Contains("界面", r.GetProperty("hint").GetString());
        Assert.Equal("pending", fx.Queries.ListApprovals(null).Single(a => a.Id == id).Status);
        // 表仍在
        Assert.Contains("items", Call(tools, "db_list_tables", new { connectionId = cid }).GetRawText());
    }

    [Fact]
    public void SetPermissionMode_SessionOnly_AutoWritesDirectly()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var sid = Sid();
        var globalBefore = fx.Modes.Global;
        var noSid = Call(tools, "db_set_permission_mode", new { mode = "Auto" });
        Assert.Equal(DbAgentCodes.InvalidArguments, noSid.GetProperty("code").GetString());
        var badMode = Call(tools, "db_set_permission_mode", new { mode = "God", sessionId = sid });
        Assert.Equal(DbErrorCodes.InvalidRequest, badMode.GetProperty("code").GetString());

        var m = Call(tools, "db_set_permission_mode", new { mode = "Auto", sessionId = sid });
        Assert.True(m.GetProperty("success").GetBoolean(), m.GetRawText());
        Assert.Equal("session", m.GetProperty("data").GetProperty("scope").GetString());
        Assert.Equal(globalBefore, fx.Modes.Global);

        var w = Call(tools, "db_execute_write", new { connectionId = cid, sql = "INSERT INTO items (name) VALUES ('auto')", sessionId = sid });
        Assert.Equal("ok", w.GetProperty("status").GetString());
        Assert.Equal(1, w.GetProperty("data").GetProperty("affectedRows").GetInt64());
    }

    [Fact]
    public void SystemDb_ReadOk_WriteReadonlyConnection_EvenInAuto()
    {
        var tools = Tools(fx);
        var sid = Sid();
        Call(tools, "db_set_permission_mode", new { mode = "Auto", sessionId = sid });
        var q = Call(tools, "db_query_readonly", new { connectionId = SysId, sql = "SELECT name FROM host_items ORDER BY id", sessionId = sid });
        Assert.True(q.GetProperty("success").GetBoolean(), q.GetRawText());
        var w = Call(tools, "db_execute_write", new { connectionId = SysId, sql = "INSERT INTO host_items (name) VALUES ('x')", sessionId = sid });
        Assert.False(w.GetProperty("success").GetBoolean());
        Assert.Equal(DbErrorCodes.ReadonlyConnection, w.GetProperty("code").GetString());
        Assert.Contains("系统库", w.GetProperty("hint").GetString());
        var d = Call(tools, "db_execute_write", new { connectionId = SysId, sql = "DROP TABLE host_items", sessionId = sid });
        Assert.Equal(DbErrorCodes.ReadonlyConnection, d.GetProperty("code").GetString());
    }

    [Fact]
    public void SecretColumns_Masked()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var raw = new List<String>();
        var r = Call(tools, "db_query_readonly", new { connectionId = cid, sql = "SELECT username, password AS Password FROM accounts", sessionId = Sid() }, raw);
        Assert.True(r.GetProperty("success").GetBoolean(), r.GetRawText());
        Assert.Equal("****", r.GetProperty("data").GetProperty("rows")[0][1].GetString());
        Assert.Contains("Password", r.GetProperty("data").GetProperty("maskedColumns").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain("hunter2-plain", raw[0]);
    }

    /// <summary>密码 / 连接串绝不出现在任何工具输出（含错误信息）里</summary>
    [Fact]
    public void Secrets_NeverInAnyToolOutput_IncludingErrors()
    {
        var tools = Tools(fx);
        var outputs = new List<String>();
        var sid = Sid();
        fx.Modes.SetSession(sid, PermissionModes.AskWrite);

        // ① connstr 模式 + 密码 + 不存在的文件 → 各工具都走错误路径
        var secretConn = NewSecretConn(fx);
        // ② 表单模式带密码（sqlite 忽略密码，但密文仍存库）
        var formFile = fx.CreateTargetDb("pw");
        var formConn = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("pw"), Provider = "sqlite", Database = formFile, UserName = "admin", Password = Secret }).Id;
        // ③ 驱动缺失（mysql）带密码
        var myConn = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("my"), Provider = "mysql", Host = "db.internal", Port = 3306, Database = "shop", UserName = "root", Password = Secret }).Id;

        Call(tools, "db_list_connections", new { sessionId = sid }, outputs);
        foreach (var c in new[] { secretConn, formConn, myConn })
        {
            Call(tools, "db_list_tables", new { connectionId = c }, outputs);
            Call(tools, "db_describe_table", new { connectionId = c, table = "items" }, outputs);
            Call(tools, "db_query_readonly", new { connectionId = c, sql = "SELECT * FROM items", sessionId = sid }, outputs);
            Call(tools, "db_query_readonly", new { connectionId = c, sql = $"SELECT '{Secret}' AS x", sessionId = sid }, outputs);
            Call(tools, "db_query_readonly", new { connectionId = c, sql = "SELECT * FROM no_such_table", sessionId = sid }, outputs);
            Call(tools, "db_execute_write", new { connectionId = c, sql = "INSERT INTO items (name) VALUES ('x')", sessionId = sid }, outputs);
            Call(tools, "db_execute_write", new { connectionId = c, sql = $"UPDATE items SET note = '{Secret}' WHERE id = 1", sessionId = sid }, outputs);
        }
        // 批准一个会失败的写（目标库不可达）→ 错误路径
        var pend = Call(tools, "db_execute_write", new { connectionId = secretConn, sql = "INSERT INTO items (name) VALUES ('y')", sessionId = sid }, outputs);
        if (pend.TryGetProperty("data", out var pd) && pd.TryGetProperty("approvalId", out var aid))
            Call(tools, "db_resolve_approval", new { approvalId = aid.GetInt64(), approve = true, userConfirmationQuote = "同意", sessionId = sid }, outputs);

        Assert.True(outputs.Count >= 20);
        Assert.Contains(outputs, o => o.Contains("\"success\":false"));
        foreach (var o in outputs)
        {
            Assert.DoesNotContain(Secret, o);
            Assert.DoesNotContain("Password=", o, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Data Source=", o, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("db.internal", o);
            using var _ = JsonDocument.Parse(o); // 擦除后仍是合法 JSON
        }
    }

    [Fact]
    public void Scrub_OnlyTouchesStringValues_KeepsJsonValid()
    {
        var svc = new DbAdminAgentService(fx.Connections, fx.Queries);
        fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("num"), Provider = "mysql", Host = "h", Database = "d", UserName = "u", Password = "123456" });
        var json = JsonSerializer.Serialize(new { n = 1234567, s = "pw=123456!", arr = new Object[] { 123456, "x123456y" } }, DbToolJson.Options);
        var outJson = DbToolJson.Scrub(json, svc);
        using var doc = JsonDocument.Parse(outJson);
        Assert.Equal(1234567, doc.RootElement.GetProperty("n").GetInt32());
        Assert.Equal("pw=****!", doc.RootElement.GetProperty("s").GetString());
        Assert.Equal(123456, doc.RootElement.GetProperty("arr")[0].GetInt32());
        Assert.Equal("x****y", doc.RootElement.GetProperty("arr")[1].GetString());
    }

    [Fact]
    public void ToolCalls_EchoInAgentAudit_WithToolNames()
    {
        var tools = Tools(fx);
        var cid = NewUserConn(fx);
        var sid = Sid();
        fx.Modes.SetSession(sid, PermissionModes.AskWrite);
        Call(tools, "db_query_readonly", new { connectionId = cid, sql = "SELECT 1", sessionId = sid });
        Call(tools, "db_query_readonly", new { connectionId = cid, sql = "UPDATE items SET name='q' WHERE id=1", sessionId = sid });
        Call(tools, "db_execute_write", new { connectionId = cid, sql = "UPDATE items SET name='q' WHERE id=1", sessionId = sid });
        var page = fx.Queries.Audit(new LogFilter { Kind = "agent", SessionId = sid });
        Assert.Equal(3, page.Total);
        var items = page.Items.OrderBy(x => x.Id).ToList();
        Assert.Equal("db_query_readonly", items[0].Tool);
        Assert.Equal(DbAgentCodes.NotReadonlySql, items[1].Code);
        Assert.Equal("awaiting_approval", items[2].Status);
    }
}

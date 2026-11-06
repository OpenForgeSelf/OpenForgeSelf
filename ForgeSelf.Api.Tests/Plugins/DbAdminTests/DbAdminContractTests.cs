using System.Text.Json;
using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>前后端已定契约（docs/03「已定契约」）的回归守卫。</summary>
[Collection(DbAdminCollection.Name)]
public class DbAdminContractTests(DbAdminTestFixture fx)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private String NewConn(String provider = "sqlite")
    {
        var file = fx.CreateTargetDb("ct");
        return fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("ct"), Provider = provider, Database = file }).Id;
    }

    [Fact]
    public void ProviderIds_AreLowercase_InputCaseInsensitive()
    {
        Assert.All(fx.Connections.ListProviders(), p => Assert.Equal(p.Id.ToLowerInvariant(), p.Id));
        var id = NewConn("SQLite");
        Assert.Equal("sqlite", fx.Connections.Get(id).Provider);
        Assert.True(fx.Connections.Test(new ConnectionInput { Provider = "SQLITE", Database = fx.CreateTargetDb("ci") }).Ok);
    }

    [Fact]
    public void QueryJson_ColumnsAreStrings_KindReadOrWrite_DestructiveFlag()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewConn();
        var r = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT id, name FROM items" }, QueryContext.Ui);
        var j = JsonSerializer.SerializeToElement(r, Web);
        Assert.Equal(JsonValueKind.String, j.GetProperty("columns")[0].ValueKind);
        Assert.Equal(JsonValueKind.Array, j.GetProperty("columnTypes").ValueKind);
        Assert.Equal("read", j.GetProperty("kind").GetString());

        var d = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "DROP TABLE items" }, QueryContext.Ui);
        Assert.Equal("awaiting_approval", d.Status);
        Assert.Equal("write", d.Kind);
        Assert.True(d.Destructive);
        var a = fx.Queries.ListApprovals("pending").Single(x => x.Id == d.ApprovalId);
        Assert.Equal("write", a.Kind);
        Assert.True(a.Destructive);
        var aj = JsonSerializer.SerializeToElement(a, Web);
        Assert.Equal(JsonValueKind.Number, aj.GetProperty("id").ValueKind);
        foreach (var f in new[] { "connectionId", "sqlRedacted", "kind", "destructive", "status", "source", "createTime" })
            Assert.True(aj.TryGetProperty(f, out _), f);
        fx.Queries.Resolve(d.ApprovalId!.Value, false, false, QueryContext.Ui);

        Assert.All(fx.Queries.History(new LogFilter { ConnectionId = cid }).Items, h => Assert.Contains(h.Kind, new[] { "read", "write" }));
    }

    [Fact]
    public void TestResult_OkPlusCode()
    {
        // 2026-11-06 用户输入2：驱动预检关闭 → 失败回显底层错误（sql_error），不再判 driver_missing。
        // 用 SQLite 缺文件路径校验失败形态，避免单测依赖外部数据库/网络。
        var missing = Path.Combine(fx.Dir, "contract_missing.db");
        var j = JsonSerializer.SerializeToElement(fx.Connections.Test(new ConnectionInput { Provider = "sqlite", Database = missing }), Web);
        Assert.False(j.GetProperty("ok").GetBoolean());
        Assert.Equal("sql_error", j.GetProperty("code").GetString());
        Assert.False(String.IsNullOrWhiteSpace(j.GetProperty("message").GetString()));
    }

    [Fact]
    public void PendingApprovals_StableArray_NoRawSql()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewConn();
        fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "INSERT INTO items (note) VALUES ('password=Hunter2x')" }, QueryContext.Ui);
        var j = JsonSerializer.SerializeToElement(fx.Queries.ListApprovals("pending"), Web);
        Assert.Equal(JsonValueKind.Array, j.ValueKind);
        Assert.True(j.GetArrayLength() <= 200);
        Assert.DoesNotContain("Hunter2x", j.GetRawText());
        // 轮询幂等：连续两次结果一致
        Assert.Equal(j.GetRawText(), JsonSerializer.SerializeToElement(fx.Queries.ListApprovals("pending"), Web).GetRawText());
    }

    [Fact]
    public void AgentEcho_ViaAuditKindAgent()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewConn();
        var sid = "echo-" + Guid.NewGuid().ToString("N")[..6];
        var agent = new DbAdminAgentService(fx.Connections, fx.Queries);
        agent.QueryReadonly(cid, "SELECT * FROM items WHERE note = @t", new() { ["t"] = "tokenValueXyz" }, sid);
        var w = agent.ExecuteWrite(cid, "INSERT INTO items (name) VALUES ('a')", null, sid);
        agent.QueryReadonly(cid, "DELETE FROM items WHERE id=1", null, sid);
        fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT 1" }, QueryContext.Ui); // UI 不进 agent 回显

        var page = fx.Queries.Audit(new LogFilter { Kind = "agent", SessionId = sid });
        Assert.Equal(3, page.Total);
        var items = page.Items.OrderBy(x => x.Id).ToList();
        Assert.Equal("db_query_readonly", items[0].Tool);
        Assert.Equal("ok", items[0].Status);
        Assert.Equal("read", items[0].SqlKind);
        Assert.Equal(0, items[0].RowCount);
        Assert.False(items[0].Truncated);
        Assert.Equal("agent", items[0].Source);
        Assert.Equal("db_execute_write", items[1].Tool);
        Assert.Equal("awaiting_approval", items[1].Status);
        Assert.Equal(w.ApprovalId, items[1].ApprovalId);
        Assert.Equal("rejected", items[2].Status);
        Assert.Equal(DbErrorCodes.RejectedHard, items[2].Code);
        Assert.DoesNotContain("tokenValueXyz", JsonSerializer.Serialize(page, Web));

        Assert.True(fx.Queries.Audit(new LogFilter { Kind = "agent", Source = "agent", ConnectionId = cid, Status = "awaiting_approval" }).Total >= 1);

        // agent 解析审批也有回显（非破坏性可批）
        agent.ResolveApproval(w.ApprovalId!.Value, true, sid);
        var last = fx.Queries.Audit(new LogFilter { Kind = "agent", SessionId = sid, PageSize = 1 }).Items.Single();
        Assert.Equal("db_resolve_approval", last.Tool);
        Assert.Equal("ok", last.Status);
        Assert.Equal(cid, last.ConnectionId);
    }

    [Fact]
    public void PluginJson_FrontendEntryMatchesWebBuild()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ForgeSelf.slnx"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var plugin = Path.Combine(dir!, "Plugins", "DbAdmin");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(plugin, "plugin.json")));
        var fe = doc.RootElement.GetProperty("frontend");
        Assert.Equal("web/dist/index.js", fe.GetProperty("entry").GetString());
        Assert.Equal("/db-admin", fe.GetProperty("route").GetString());
        Assert.Contains("DbAdminView", fe.GetProperty("views").EnumerateArray().Select(v => v.GetString()));
        var index = File.ReadAllText(Path.Combine(plugin, "web", "src", "index.ts"));
        Assert.Contains("DbAdminView", index);
        var vite = File.ReadAllText(Path.Combine(plugin, "web", "vite.config.ts"));
        Assert.Contains("'index.js'", vite);
    }
}

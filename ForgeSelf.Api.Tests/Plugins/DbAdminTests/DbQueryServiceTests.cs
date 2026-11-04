using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

[Collection(DbAdminCollection.Name)]
public class DbQueryServiceTests(DbAdminTestFixture fx)
{
    private const String SysId = DbConnectionService.SystemIdPrefix + DbAdminTestFixture.SystemConnName;

    private String NewUserConn(Int32 rows = 0, Int32 noteLen = 0, Boolean ro = false)
    {
        var file = fx.CreateTargetDb("q");
        var v = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("q"), Provider = "sqlite", Database = file, Readonly = ro });
        if (rows > 0)
        {
            var dal = DAL.Create(fx.Connections.Resolve(v.Id).ConnName);
            var note = new String('n', noteLen);
            dal.Execute($"WITH RECURSIVE c(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM c WHERE x < {rows}) INSERT INTO items (name, note) SELECT 'row' || x, '{note}' FROM c");
        }
        return v.Id;
    }

    private QueryOutcome Ui(String cid, String sql, Int32 page = 1, Int32 size = 50, Dictionary<String, Object?>? ps = null) =>
        fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = sql, Page = page, PageSize = size, Params = ps }, QueryContext.Ui);

    [Fact]
    public void Read_Pagination_ServerSide()
    {
        var cid = NewUserConn(120);
        var p1 = Ui(cid, "SELECT id, name FROM items ORDER BY id");
        Assert.Equal("ok", p1.Status);
        Assert.Equal(120, p1.Total);
        Assert.Equal(50, p1.Rows!.Count);
        Assert.Equal(new[] { "id", "name" }, p1.Columns);

        var p3 = Ui(cid, "SELECT id, name FROM items ORDER BY id;", page: 3);
        Assert.Equal(20, p3.Rows!.Count);
        Assert.Equal(101L, Convert.ToInt64(p3.Rows[0][0]));

        var p4 = Ui(cid, "SELECT id FROM items", page: 4);
        Assert.Empty(p4.Rows!);

        var big = Ui(cid, "SELECT id FROM items", size: 100000);
        Assert.Equal(ResultLimiter.UiMaxPageSize, big.PageSize);
    }

    [Fact]
    public void Read_Parameterized()
    {
        var cid = NewUserConn(10);
        var r = Ui(cid, "SELECT name FROM items WHERE id = @id", ps: new() { ["id"] = 3 });
        Assert.Equal("ok", r.Status);
        Assert.Single(r.Rows!);
        Assert.Equal("row3", r.Rows![0][0]);

        // 注入载荷作为参数值不会被执行
        var inj = Ui(cid, "SELECT count(*) FROM items WHERE name = @n", ps: new() { ["n"] = "x' OR '1'='1" });
        Assert.Equal(0L, Convert.ToInt64(inj.Rows![0][0]));
    }

    [Fact]
    public void Agent_RowCap_1000_Truncated()
    {
        var cid = NewUserConn(1500);
        var r = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT id FROM items" }, QueryContext.Agent("s1"));
        Assert.Equal("ok", r.Status);
        Assert.Equal(1000, r.Rows!.Count);
        Assert.True(r.Truncated);

        var small = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT id FROM items WHERE id <= 10" }, QueryContext.Agent("s1"));
        Assert.Equal(10, small.Rows!.Count);
        Assert.False(small.Truncated);
    }

    [Fact]
    public void Agent_ByteCap_256KB_Truncated()
    {
        var cid = NewUserConn(600, noteLen: 1000);
        var r = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT id, note FROM items" }, QueryContext.Agent("s1"));
        Assert.True(r.Truncated);
        Assert.True(r.Rows!.Count < 600);
        var bytes = System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(r));
        Assert.True(bytes <= ResultLimiter.AgentMaxBytes, $"回包 {bytes} 字节超过上限");
    }

    [Fact]
    public void SystemConnection_ReadOk_WriteHardRejected_InAnyMode()
    {
        var read = Ui(SysId, "SELECT * FROM host_items");
        Assert.Equal("ok", read.Status);

        foreach (var mode in new[] { PermissionModes.AskWrite, PermissionModes.Auto })
        {
            fx.Modes.SetGlobal(mode);
            foreach (var sql in new[] { "INSERT INTO host_items (name) VALUES ('x')", "UPDATE host_items SET name='y' WHERE id=1", "DROP TABLE host_items", "CREATE TABLE z (id INT)" })
            {
                var r = Ui(SysId, sql);
                Assert.Equal("rejected", r.Status);
                Assert.Equal(DbErrorCodes.ReadonlyConnection, r.Code);
            }
        }
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        Assert.Equal(2L, Convert.ToInt64(Ui(SysId, "SELECT count(*) FROM host_items").Rows![0][0]));
        Assert.Equal(0, DbApproval.FindCount(DbApproval._.ConnectionId == SysId, null, null, 0, 0));
    }

    [Fact]
    public void UserReadonlyConnection_WriteRejected()
    {
        var cid = NewUserConn(ro: true);
        var r = Ui(cid, "INSERT INTO items (name) VALUES ('x')");
        Assert.Equal(DbErrorCodes.ReadonlyConnection, r.Code);
    }

    [Fact]
    public void HardReject_NeverExecutes_AndAudited()
    {
        var cid = NewUserConn(5);
        var r = Ui(cid, "DELETE FROM items");
        Assert.Equal("rejected", r.Status);
        Assert.Equal(DbErrorCodes.RejectedHard, r.Code);
        Assert.Equal(5L, Convert.ToInt64(Ui(cid, "SELECT count(*) FROM items").Rows![0][0]));
        Assert.True(DbAuditLog.FindCount(DbAuditLog._.Kind == "rejected" & DbAuditLog._.ConnectionId == cid, null, null, 0, 0) > 0);
    }

    [Fact]
    public void AskWrite_Approval_Then_Executes()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewUserConn(3);
        var r = Ui(cid, "UPDATE items SET name = @n WHERE id = @id", ps: new() { ["n"] = "changed", ["id"] = 1 });
        Assert.Equal("awaiting_approval", r.Status);
        Assert.NotNull(r.ApprovalId);
        Assert.False(r.Destructive);
        Assert.Equal("row1", Ui(cid, "SELECT name FROM items WHERE id=1").Rows![0][0]);

        Assert.Contains(fx.Queries.ListApprovals("pending"), a => a.Id == r.ApprovalId);
        var done = fx.Queries.Resolve(r.ApprovalId!.Value, approve: true, confirmDestructive: false, QueryContext.Ui);
        Assert.Equal("ok", done.Status);
        Assert.Equal(1, done.AffectedRows);
        Assert.Equal("changed", Ui(cid, "SELECT name FROM items WHERE id=1").Rows![0][0]);
        Assert.Equal("approved", DbApproval.Find(DbApproval._.Id == r.ApprovalId.Value)!.Status);

        // 已处理不可重复
        Assert.Throws<DbAdminException>(() => fx.Queries.Resolve(r.ApprovalId.Value, true, false, QueryContext.Ui));
    }

    [Fact]
    public void Reject_Approval_DoesNotExecute()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewUserConn(3);
        var r = Ui(cid, "DELETE FROM items WHERE id = 1");
        var res = fx.Queries.Resolve(r.ApprovalId!.Value, approve: false, confirmDestructive: false, QueryContext.Ui);
        Assert.Equal("rejected", res.Status);
        Assert.Equal(3L, Convert.ToInt64(Ui(cid, "SELECT count(*) FROM items").Rows![0][0]));
    }

    [Fact]
    public void Destructive_NeedsUiSecondConfirm_AgentCannotApprove()
    {
        fx.Modes.SetGlobal(PermissionModes.Auto);
        var cid = NewUserConn(1);
        Ui(cid, "CREATE TABLE tmp_drop (id INT)");

        var r = Ui(cid, "DROP TABLE tmp_drop");
        Assert.Equal("awaiting_approval", r.Status); // Auto 下破坏性仍询问
        Assert.True(r.Destructive);

        Assert.Equal(DbErrorCodes.ConfirmationRequired,
            Assert.Throws<DbAdminException>(() => fx.Queries.Resolve(r.ApprovalId!.Value, true, false, QueryContext.Ui)).Code);
        Assert.Equal(DbErrorCodes.ConfirmationRequired,
            Assert.Throws<DbAdminException>(() => fx.Queries.Resolve(r.ApprovalId!.Value, true, true, QueryContext.Agent("s"))).Code);

        var ok = fx.Queries.Resolve(r.ApprovalId!.Value, true, true, QueryContext.Ui);
        Assert.Equal("ok", ok.Status);
        Assert.DoesNotContain(fx.Connections.ListTables(cid), t => t.Name == "tmp_drop");
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
    }

    [Fact]
    public void AutoMode_NormalWrite_RunsDirectly_SessionModeIsolated()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewUserConn();
        // Agent 会话级 Auto 不影响全局
        fx.Queries.SetMode("Auto", QueryContext.Agent("sess-auto"));
        Assert.Equal(PermissionModes.AskWrite, fx.Modes.Global);

        var a = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "INSERT INTO items (name) VALUES ('agent')" }, QueryContext.Agent("sess-auto"));
        Assert.Equal("ok", a.Status);
        Assert.Equal(1, a.AffectedRows);

        var other = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "INSERT INTO items (name) VALUES ('other')" }, QueryContext.Agent("sess-other"));
        Assert.Equal("awaiting_approval", other.Status);

        Assert.Throws<DbAdminException>(() => fx.Queries.SetMode("Auto", QueryContext.Agent(null)));
        Assert.Throws<DbAdminException>(() => fx.Queries.SetMode("yolo", QueryContext.Ui));
    }

    [Fact]
    public void Audit_And_History_AreRedacted()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var cid = NewUserConn(1);
        var secret = "Zq" + Guid.NewGuid().ToString("N")[..10];
        Ui(cid, $"SELECT * FROM items WHERE note = 'password={secret}'");
        Ui(cid, $"INSERT INTO items (note) VALUES ('Bearer {secret}')");
        Ui(cid, "SELECT * FROM items WHERE note = @p", ps: new() { ["p"] = secret });
        Ui(cid, $"DELETE FROM items -- token={secret}");
        fx.Connections.Test(new ConnectionInput { Provider = "mysql", Host = "h", Database = "d", UserName = "u", Password = secret });

        var audits = DbAuditLog.FindAll(null, null, null, 0, 0);
        var hist = DbQueryHistory.FindAll(null, null, null, 0, 0);
        var approvals = DbApproval.FindAll(null, null, null, 0, 0);
        Assert.All(audits, a => Assert.DoesNotContain(secret, (a.SqlRedacted ?? "") + (a.Detail ?? "")));
        Assert.All(hist, h => Assert.DoesNotContain(secret, (h.SqlRedacted ?? "") + (h.Error ?? "")));
        Assert.All(approvals, a => Assert.DoesNotContain(secret, a.SqlRedacted ?? ""));
        Assert.All(DbConnection.FindAll(null, null, null, 0, 0), c => Assert.DoesNotContain(secret, (c.PasswordEnc ?? "") + (c.ConnStrEnc ?? "")));

        // 历史/审计查询接口可分页过滤
        var h1 = fx.Queries.History(new LogFilter { ConnectionId = cid, PageSize = 2 });
        Assert.True(h1.Total >= 3);
        Assert.Equal(2, h1.Items.Count);
        var au = fx.Queries.Audit(new LogFilter { Kind = "rejected", ConnectionId = cid });
        Assert.True(au.Total >= 1);
    }

    [Fact]
    public void AgentFacade_QueryReadonly_RefusesWrites()
    {
        var cid = NewUserConn(2);
        var agent = new DbAdminAgentService(fx.Connections, fx.Queries);
        Assert.DoesNotContain(agent.ListConnections(), c => c.Id == cid && c.Readonly);
        var w = agent.QueryReadonly(cid, "INSERT INTO items (name) VALUES ('x')", null, "s");
        Assert.Equal("rejected", w.Status);
        var r = agent.QueryReadonly(cid, "SELECT * FROM items", null, "s");
        Assert.Equal("ok", r.Status);
        Assert.Equal(2, r.Rows!.Count);
    }

    [Fact]
    public void UnknownConnection_NotFound()
    {
        Assert.Equal(DbErrorCodes.NotFound, Ui("999999", "SELECT 1").Code);
        Assert.Equal(DbErrorCodes.NotFound, Ui("sys:NoSuchConn", "SELECT 1").Code);
    }
}

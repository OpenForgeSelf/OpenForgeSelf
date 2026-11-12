using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>验收 P2（只认本层 WHERE）与 P3（CREATE TRIGGER 的 BEGIN…END 块）回归</summary>
public class SqlGuardScopeTests
{
    [Theory]
    [InlineData("WITH x AS (SELECT * FROM a WHERE id=1) DELETE FROM t")]
    [InlineData("UPDATE t SET a=(SELECT b FROM u WHERE id=1)")]
    [InlineData("DELETE FROM t WHERE_flag")]
    [InlineData("DELETE FROM t -- WHERE id = 1")]
    [InlineData("DELETE FROM t /* WHERE id = 1 */")]
    [InlineData("UPDATE t SET a = 'WHERE id=1'")]
    [InlineData("UPDATE t SET a = \"where\"")]
    [InlineData("UPDATE t SET a = 1 FROM (SELECT * FROM u WHERE x = 1) s")]
    [InlineData("DELETE FROM t USING u WHERE_ok")]
    [InlineData("WITH d AS (DELETE FROM t) SELECT * FROM d WHERE 1 = 1")]
    [InlineData("WITH d AS (UPDATE t SET a = 1 RETURNING *) SELECT * FROM d WHERE a = 1")]
    [InlineData("EXPLAIN ANALYZE UPDATE t SET a = (SELECT 1 WHERE 1 = 1)")]
    public void UnboundedDml_WhereOnlyInSubquery_Rejected(String sql)
    {
        var c = SqlGuard.Classify(sql);
        Assert.Equal(SqlCategory.Rejected, c.Category);
    }

    [Theory]
    [InlineData("DELETE FROM t WHERE id IN (SELECT id FROM u WHERE x = 1)")]
    [InlineData("UPDATE t SET a = (SELECT b FROM u WHERE u.id = t.id) WHERE t.id > 0")]
    [InlineData("WITH x AS (SELECT id FROM a WHERE k = 1) DELETE FROM t WHERE id IN (SELECT id FROM x)")]
    [InlineData("WITH x AS (SELECT 1) UPDATE t SET a = 2 WHERE id = 3")]
    [InlineData("UPDATE t SET note = 'no where here' WHERE id = 1")]
    [InlineData("DELETE FROM t WHERE note = 'DELETE FROM x' -- trailing WHERE comment")]
    [InlineData("DELETE FROM t WHERE (id = 1)")]
    [InlineData("UPDATE t SET a = CASE WHEN b > 0 THEN 1 ELSE 0 END WHERE id = 1")]
    public void BoundedDml_TopLevelWhere_Write(String sql) =>
        Assert.Equal(SqlCategory.Write, SqlGuard.Classify(sql).Category);

    [Theory]
    [InlineData("SELECT * FROM t FOR UPDATE")]
    [InlineData("SELECT * FROM t WHERE note = 'UPDATE x SET y=1'")]
    public void ReadWithDmlWordsInModifiersOrStrings_Read(String sql) =>
        Assert.Equal(SqlCategory.Read, SqlGuard.Classify(sql).Category);

    [Fact]
    public void CreateTable_ForeignKeyOnDeleteCascade_IsWrite() =>
        Assert.Equal(SqlCategory.Write, SqlGuard.Classify("CREATE TABLE c (id INT, pid INT REFERENCES p(id) ON DELETE CASCADE ON UPDATE CASCADE)").Category);

    [Theory]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN UPDATE counter SET n = n + 1 WHERE id = 1; END")]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN UPDATE counter SET n = n + 1 WHERE id = 1; END;")]
    [InlineData("CREATE TEMP TRIGGER IF NOT EXISTS trg BEFORE DELETE ON items FOR EACH ROW BEGIN INSERT INTO log(v) VALUES (CASE WHEN OLD.id > 0 THEN 'p' ELSE 'n' END); INSERT INTO log(v) VALUES ('x;y'); END;")]
    [InlineData("create trigger trg after update of name on items begin select case when new.name is null then raise(abort, 'no') end; end")]
    public void CreateTrigger_BeginEndBlock_NotMultiStatement_StaysDdlWrite(String sql)
    {
        var c = SqlGuard.Classify(sql);
        Assert.Equal(SqlCategory.Write, c.Category);
        Assert.Equal("write", c.Kind);
    }

    [Theory]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN SELECT 1; END; DROP TABLE items")]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN ATTACH DATABASE 'x' AS y; END")]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN SELECT load_extension('evil'); END")]
    [InlineData("CREATE TRIGGER trg AFTER INSERT ON items BEGIN SELECT CASE WHEN 1 THEN 1 END; END; END; DELETE FROM items")]
    [InlineData("BEGIN; DELETE FROM items WHERE id = 1; COMMIT")]
    [InlineData("BEGIN TRANSACTION; SELECT 1; END")]
    [InlineData("CREATE TABLE x (id INT); BEGIN SELECT 1; END")]
    [InlineData("SELECT CASE WHEN 1 THEN 2 END; SELECT 1")]
    public void BlockAwareSplit_DoesNotHideOtherStatements(String sql) =>
        Assert.Equal(SqlCategory.Rejected, SqlGuard.Classify(sql).Category);
}

[Collection(DbAdminCollection.Name)]
public class SqlGuardTriggerExecutionTests(DbAdminTestFixture fx)
{
    [Fact]
    public void CreateTrigger_GoesThroughApproval_ThenWorksOnSqlite()
    {
        fx.Modes.SetGlobal(PermissionModes.AskWrite);
        var file = fx.CreateTargetDb("trg", "CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY, name TEXT, note TEXT); CREATE TABLE IF NOT EXISTS log (v TEXT)");
        var cid = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("trg"), Provider = "sqlite", Database = file }).Id;

        var r = fx.Queries.Execute(new QueryRequest
        {
            ConnectionId = cid,
            Sql = "CREATE TRIGGER trg_ins AFTER INSERT ON items BEGIN INSERT INTO log (v) VALUES (CASE WHEN NEW.name IS NULL THEN 'null' ELSE NEW.name END); END;",
        }, QueryContext.Ui);
        Assert.Equal("awaiting_approval", r.Status); // 仍属 DDL 写，AskWrite 下需审批
        Assert.False(r.Destructive);
        var ok = fx.Queries.Resolve(r.ApprovalId!.Value, true, false, QueryContext.Ui);
        Assert.Equal("ok", ok.Status);

        fx.Modes.SetGlobal(PermissionModes.Auto);
        try
        {
            Assert.Equal("ok", fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "INSERT INTO items (name) VALUES ('hello')" }, QueryContext.Ui).Status);
        }
        finally { fx.Modes.SetGlobal(PermissionModes.AskWrite); }
        var log = fx.Queries.Execute(new QueryRequest { ConnectionId = cid, Sql = "SELECT v FROM log" }, QueryContext.Ui);
        Assert.Equal("hello", log.Rows!.Single()[0]);
    }
}

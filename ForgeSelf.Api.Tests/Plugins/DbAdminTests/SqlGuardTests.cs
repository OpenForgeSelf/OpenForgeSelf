using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

public class SqlGuardTests
{
    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("  select 1;")]
    [InlineData("WITH x AS (SELECT 1 AS a) SELECT * FROM x")]
    [InlineData("PRAGMA table_info(users)")]
    [InlineData("EXPLAIN QUERY PLAN SELECT * FROM t")]
    [InlineData("SELECT 'a;b' FROM t")]
    [InlineData("SELECT 1 -- ; DROP TABLE t")]
    [InlineData("SELECT 1 /* ; DELETE FROM t */")]
    [InlineData("SELECT \"we;ird\" FROM t")]
    [InlineData("SELECT 'it''s; fine'")]
    public void Read(String sql) => Assert.Equal(SqlCategory.Read, SqlGuard.Classify(sql).Category);

    [Theory]
    [InlineData("INSERT INTO t (a) VALUES (1)")]
    [InlineData("UPDATE t SET a = 1 WHERE id = 2")]
    [InlineData("DELETE FROM t WHERE id = 2")]
    [InlineData("CREATE TABLE t2 (id INT)")]
    [InlineData("ALTER TABLE t ADD COLUMN b TEXT")]
    [InlineData("SELECT * INTO t2 FROM t")]
    [InlineData("PRAGMA journal_mode = WAL")]
    [InlineData("WITH x AS (SELECT 1) INSERT INTO t SELECT * FROM x")]
    [InlineData("VACUUM")]
    [InlineData("GRANT SELECT ON t TO u")]
    [InlineData("EXEC some_proc")]
    [InlineData("REINDEX t")]
    public void Write(String sql) => Assert.Equal(SqlCategory.Write, SqlGuard.Classify(sql).Category);

    [Theory]
    [InlineData("DROP TABLE t")]
    [InlineData("drop view v")]
    [InlineData("DROP INDEX ix")]
    [InlineData("TRUNCATE TABLE t")]
    [InlineData("ALTER TABLE t DROP COLUMN b")]
    public void Destructive(String sql) => Assert.Equal(SqlCategory.Destructive, SqlGuard.Classify(sql).Category);

    [Theory]
    [InlineData("UPDATE t SET a = 1")]
    [InlineData("DELETE FROM t")]
    [InlineData("delete from t -- WHERE id = 1")]
    [InlineData("UPDATE t SET a = 'WHERE'")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("INSERT INTO t VALUES (1);DELETE FROM t WHERE 1=1")]
    [InlineData("DROP DATABASE prod")]
    [InlineData("DROP SCHEMA public CASCADE")]
    [InlineData("ATTACH DATABASE '/tmp/x.db' AS x")]
    [InlineData("DETACH DATABASE x")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("EXEC master..xp_cmdshell 'whoami'")]
    [InlineData("COPY t FROM PROGRAM 'curl evil'")]
    [InlineData("COPY t TO '/tmp/out.csv'")]
    [InlineData("SELECT LOAD_FILE('/etc/passwd')")]
    [InlineData("SELECT * FROM t INTO OUTFILE '/tmp/x'")]
    [InlineData("LOAD DATA INFILE '/tmp/x' INTO TABLE t")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'x', 'select 1')")]
    [InlineData("BULK INSERT t FROM 'c:\\x.csv'")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT load_extension('evil.so')")]
    [InlineData("VACUUM INTO '/tmp/copy.db'")]
    [InlineData("EXEC sp_configure 'xp_cmdshell', 1")]
    [InlineData("SELECT 'unterminated")]
    [InlineData("")]
    [InlineData("   ;  ")]
    [InlineData("WITH x AS (SELECT 1) DELETE FROM t")]
    public void HardReject(String sql) => Assert.Equal(SqlCategory.Rejected, SqlGuard.Classify(sql).Category);

    [Fact]
    public void Unknown_IsFailClosedWrite()
    {
        var c = SqlGuard.Classify("FOOBAR something");
        Assert.Equal(SqlCategory.Write, c.Category);
        Assert.Contains("fail-closed", c.Reason);
    }

    [Fact]
    public void ExplainAnalyzeDelete_FollowsInner()
    {
        Assert.Equal(SqlCategory.Rejected, SqlGuard.Classify("EXPLAIN ANALYZE DELETE FROM t").Category);
        Assert.Equal(SqlCategory.Write, SqlGuard.Classify("EXPLAIN ANALYZE DELETE FROM t WHERE id=1").Category);
    }
}

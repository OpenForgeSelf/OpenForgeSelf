using System.Security.Cryptography;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

public class RedactionAndCryptoTests
{
    [Theory]
    [InlineData("INSERT INTO cfg VALUES ('password=Hunter2!')", "Hunter2!")]
    [InlineData("UPDATE c SET v='Server=db;Uid=sa;Pwd=S3cr3t' WHERE id=1", "S3cr3t")]
    [InlineData("SELECT 'Bearer abc.def.ghi'", "abc.def.ghi")]
    [InlineData("CREATE USER bob IDENTIFIED BY 'TopSecret'", "TopSecret")]
    [InlineData("ALTER ROLE bob WITH PASSWORD 'pgpass'", "pgpass")]
    [InlineData("SELECT 'postgres://u:pw123@host/db'", "pw123")]
    public void RedactSql_HidesSecrets(String sql, String secret)
    {
        var r = SqlRedactor.RedactSql(sql);
        Assert.DoesNotContain(secret, r);
        Assert.Contains(SqlRedactor.Mask, r);
    }

    [Fact]
    public void RedactSql_KeepsOrdinaryLiterals()
    {
        Assert.Equal("SELECT * FROM t WHERE name = 'alice'", SqlRedactor.RedactSql("SELECT * FROM t WHERE name = 'alice'"));
    }

    [Fact]
    public void RedactText_ConnectionString()
    {
        var r = SqlRedactor.RedactText("Data Source=h;User ID=sa;Password=p@ss;token=xyz");
        Assert.DoesNotContain("p@ss", r);
        Assert.DoesNotContain("xyz", r);
    }

    [Fact]
    public void DescribeParams_NeverShowsValues()
    {
        var d = SqlRedactor.DescribeParams(new Dictionary<String, Object?> { ["pwd"] = "zzSecret", ["id"] = 5 });
        Assert.DoesNotContain("zzSecret", d);
        Assert.Contains("pwd=****", d);
    }

    private static String NewDir() => Path.Combine(Path.GetTempPath(), "ForgeSelfDbAdminTests", "crypto_" + Guid.NewGuid().ToString("N")[..8]);

    [Fact]
    public void Protector_RoundTrip_AndRandomNonce()
    {
        var p = new FileKeySecretProtector(NewDir());
        var c1 = p.Protect("s3cret-密码");
        var c2 = p.Protect("s3cret-密码");
        Assert.NotEqual(c1, c2);
        Assert.StartsWith("v1:", c1);
        Assert.DoesNotContain("s3cret", c1);
        Assert.Equal("s3cret-密码", p.Unprotect(c1));
        Assert.Null(p.Protect(""));
        Assert.Null(p.Unprotect(null));
    }

    [Fact]
    public void Protector_KeyPersists_AcrossInstances()
    {
        var dir = NewDir();
        var c = new FileKeySecretProtector(dir).Protect("abc");
        Assert.True(File.Exists(Path.Combine(dir, FileKeySecretProtector.KeyFileName)));
        Assert.Equal("abc", new FileKeySecretProtector(dir).Unprotect(c));
    }

    [Fact]
    public void Protector_OtherKey_OrTamper_Fails()
    {
        var c = new FileKeySecretProtector(NewDir()).Protect("abc")!;
        Assert.ThrowsAny<CryptographicException>(() => new FileKeySecretProtector(NewDir()).Unprotect(c));
        var bytes = Convert.FromBase64String(c[3..]);
        bytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => new FileKeySecretProtector(NewDir()).Unprotect("v1:" + Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Limiter_RowAndByteCaps()
    {
        var cols = new[] { "a" };
        var rows = Enumerable.Range(0, 2000).Select(i => new Object?[] { i });
        var (r1, t1) = ResultLimiter.Limit(rows, cols, 1000, 256 * 1024);
        Assert.Equal(1000, r1.Count);
        Assert.True(t1);

        var big = Enumerable.Range(0, 1000).Select(_ => new Object?[] { new String('x', 1024) });
        var (r2, t2) = ResultLimiter.Limit(big, cols, 1000, 256 * 1024);
        Assert.True(t2);
        Assert.True(r2.Count < 256);
        var json = System.Text.Json.JsonSerializer.Serialize(r2);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= 256 * 1024);
    }
}

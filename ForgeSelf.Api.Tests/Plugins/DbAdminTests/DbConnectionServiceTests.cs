using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

[Collection(DbAdminCollection.Name)]
public class DbConnectionServiceTests(DbAdminTestFixture fx)
{
    private ConnectionView CreateSqlite(String file, String? password = null, Boolean ro = false) =>
        fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("c"), Provider = "sqlite", Database = file, Password = password, Readonly = ro });

    [Fact]
    public void Create_MasksPassword_AndEncryptsAtRest()
    {
        var v = CreateSqlite(fx.CreateTargetDb("pw"), "PlainP@ss1");
        Assert.Equal(SqlRedactor.Mask, v.Password);
        Assert.Equal("user", v.Kind);

        var e = DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!;
        Assert.NotEqual("PlainP@ss1", e.PasswordEnc);
        Assert.DoesNotContain("PlainP@ss1", e.PasswordEnc);
        Assert.Equal("PlainP@ss1", fx.Protector.Unprotect(e.PasswordEnc));

        // 列表 / 详情均无明文
        Assert.All(fx.Connections.List(), c => Assert.NotEqual("PlainP@ss1", c.Password));
        Assert.Equal(SqlRedactor.Mask, fx.Connections.Get(v.Id).Password);
    }

    [Fact]
    public void Update_EmptyOrMaskedPassword_KeepsOld()
    {
        var v = CreateSqlite(fx.CreateTargetDb("upd"), "Keep1");
        fx.Connections.Update(v.Id, new ConnectionInput { Password = "" });
        fx.Connections.Update(v.Id, new ConnectionInput { Password = SqlRedactor.Mask, Readonly = true });
        var e = DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!;
        Assert.Equal("Keep1", fx.Protector.Unprotect(e.PasswordEnc));
        Assert.True(e.Readonly);

        fx.Connections.Update(v.Id, new ConnectionInput { Password = "New2" });
        e = DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!;
        Assert.Equal("New2", fx.Protector.Unprotect(e.PasswordEnc));
    }

    [Fact]
    public void ConnStrMode_EncryptsWholeString_AndMasksOnGet()
    {
        var file = fx.CreateTargetDb("cs");
        var v = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("cs"), Provider = "sqlite", Mode = "connstr", ConnStr = $"Data Source={file};Password=CsSecret9" });
        Assert.DoesNotContain("CsSecret9", v.ConnStr);
        var e = DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!;
        Assert.DoesNotContain("CsSecret9", e.ConnStrEnc);
    }

    [Fact]
    public void DuplicateName_Rejected()
    {
        var file = fx.CreateTargetDb("dup");
        var name = DbAdminTestFixture.UniqueName("dup");
        fx.Connections.Create(new ConnectionInput { Name = name, Provider = "sqlite", Database = file });
        var ex = Assert.Throws<DbAdminException>(() => fx.Connections.Create(new ConnectionInput { Name = name, Provider = "sqlite", Database = file }));
        Assert.Equal(DbErrorCodes.InvalidRequest, ex.Code);
    }

    [Fact]
    public void Delete_RemovesConfigOnly_FileStays()
    {
        var file = fx.CreateTargetDb("del");
        var v = CreateSqlite(file);
        fx.Connections.Delete(v.Id);
        Assert.Null(DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id)));
        Assert.True(File.Exists(file));
        Assert.Equal(DbErrorCodes.NotFound, Assert.Throws<DbAdminException>(() => fx.Connections.Get(v.Id)).Code);
    }

    [Fact]
    public void SystemConnection_ListedReadonly_NotEditable()
    {
        var sys = fx.Connections.List().Single(c => c.Id == DbConnectionService.SystemIdPrefix + DbAdminTestFixture.SystemConnName);
        Assert.True(sys.Readonly);
        Assert.Equal("system", sys.Kind);
        Assert.Equal(DbErrorCodes.ReadonlyConnection, Assert.Throws<DbAdminException>(() => fx.Connections.Update(sys.Id, new ConnectionInput { Name = "x" })).Code);
        Assert.Equal(DbErrorCodes.ReadonlyConnection, Assert.Throws<DbAdminException>(() => fx.Connections.Delete(sys.Id)).Code);
    }

    [Fact]
    public void UserConnection_ToSystemDbFile_Rejected()
    {
        var ex = Assert.Throws<DbAdminException>(() => CreateSqlite(fx.SystemDbFile));
        Assert.Equal(DbErrorCodes.ReadonlyConnection, ex.Code);
    }

    [Fact]
    public void Test_Sqlite_Ok_And_RegistersIsolatedConnName()
    {
        var file = fx.CreateTargetDb("test");
        var r = fx.Connections.Test(new ConnectionInput { Provider = "sqlite", Database = file });
        Assert.True(r.Ok, r.Message);

        var v = CreateSqlite(file);
        var rc = fx.Connections.Resolve(v.Id);
        Assert.Equal(DbConnectionService.UserConnPrefix + v.Id, rc.ConnName);
        Assert.False(rc.IsSystem);
    }

    [Fact]
    public void Test_MissingSqliteFile_Errors_AndDoesNotCreateFile()
    {
        var file = Path.Combine(fx.Dir, "nope_" + Guid.NewGuid().ToString("N")[..6] + ".db");
        var r = fx.Connections.Test(new ConnectionInput { Provider = "sqlite", Database = file });
        Assert.False(r.Ok);
        Assert.Equal(DbErrorCodes.SqlError, r.Code);
        Assert.False(File.Exists(file));
    }

    [Theory]
    [InlineData("mysql", "MySqlConnector")]
    [InlineData("postgresql", "Npgsql")]
    [InlineData("sqlserver", "Microsoft.Data.SqlClient")]
    public void Test_DriverMissing_ClearHint(String provider, String package)
    {
        var r = fx.Connections.Test(new ConnectionInput { Provider = provider, Host = "127.0.0.1", Database = "db", UserName = "u", Password = "SecretPw" });
        Assert.False(r.Ok);
        Assert.Equal(DbErrorCodes.DriverMissing, r.Code);
        Assert.Contains(package, r.Message);
        Assert.DoesNotContain("SecretPw", r.Message);

        var prov = fx.Connections.ListProviders().Single(p => p.Id == provider);
        Assert.False(prov.DriverAvailable);
        Assert.False(prov.Tested);
    }

    [Fact]
    public void Schema_ListAndDescribe()
    {
        var file = fx.CreateTargetDb("schema", "CREATE TABLE IF NOT EXISTS people (id INTEGER PRIMARY KEY, name TEXT NOT NULL, age INT)");
        var v = CreateSqlite(file);
        var tables = fx.Connections.ListTables(v.Id);
        Assert.Contains(tables, t => t.Name == "people");
        var d = fx.Connections.DescribeTable(v.Id, "people");
        Assert.Contains(d.Columns, c => c.Name == "id" && c.PrimaryKey);
        Assert.Contains(d.Columns, c => c.Name == "name");
        Assert.Equal(DbErrorCodes.NotFound, Assert.Throws<DbAdminException>(() => fx.Connections.DescribeTable(v.Id, "nope")).Code);
    }
}

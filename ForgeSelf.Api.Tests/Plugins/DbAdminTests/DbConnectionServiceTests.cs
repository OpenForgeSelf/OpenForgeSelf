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

    [Fact]
    public void ListProviders_AllAvailable_WhenDriverProbeDisabled()
    {
        // 2026-11-06 用户输入2：暂时关闭驱动预检，provider 不再标「需驱动」
        Assert.False(DriverCatalog.ProbeEnabled);
        Assert.All(fx.Connections.ListProviders(), p => Assert.True(p.DriverAvailable));
    }

    [Fact]
    public void Resolve_NonSqlite_NoDriverGate()
    {
        // 预检关闭后不再由探测结果拦截：建一条 MySQL 配置即可进入解析、注册 ConnName
        var v = fx.Connections.Create(new ConnectionInput
        {
            Name = DbAdminTestFixture.UniqueName("mysql"),
            Provider = "mysql", Host = "127.0.0.1", Port = 3306, Database = "db", UserName = "u", Password = "SecretPw",
        });
        var rc = fx.Connections.Resolve(v.Id);
        Assert.Equal(DbConnectionService.UserConnPrefix + v.Id, rc.ConnName);
        Assert.Equal("mysql", rc.Provider);

        // 测连失败时回显底层错误（不判 driver_missing、不泄露密码）——SQLite 缺文件路径无需外网即可判定
        var r = fx.Connections.Test(new ConnectionInput { Provider = "sqlite", Database = Path.Combine(fx.Dir, "missing_driver_gate.db") });
        Assert.False(r.Ok);
        Assert.Equal(DbErrorCodes.SqlError, r.Code);
        Assert.False(String.IsNullOrWhiteSpace(r.Message));
        Assert.DoesNotContain("SecretPw", r.Message);
    }

    [Fact]
    public void Oracle_ProviderRegistered_AndConnStrBuilt()
    {
        // 目录项：id/XCodeProvider/默认端口/未实测
        var def = DriverCatalog.Find("oracle");
        Assert.NotNull(def);
        Assert.Equal("Oracle", def!.XCodeProvider);
        Assert.Equal(1521, def.DefaultPort);
        Assert.False(def.Tested);
        Assert.Contains(fx.Connections.ListProviders(), p => p.Id == "oracle" && p.DefaultPort == 1521 && !p.Tested);

        // 表单连接：EZConnect 连接串构造（host:port/service + User Id/Password）
        var v = fx.Connections.Create(new ConnectionInput
        {
            Name = DbAdminTestFixture.UniqueName("ora"),
            Provider = "oracle", Host = "127.0.0.1", Port = 1521, Database = "ORCL", UserName = "ora_user", Password = "OraPw123",
        });
        Assert.Equal("oracle", v.Provider);
        var e = DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!;
        var cs = fx.Connections.BuildConnStr(e);
        Assert.Contains("127.0.0.1:1521/ORCL", cs);
        Assert.Contains("ora_user", cs);
        Assert.Contains("OraPw123", cs);

        // Resolve 注册 ConnName（预检关闭，不拦驱动）
        var rc = fx.Connections.Resolve(v.Id);
        Assert.Equal(DbConnectionService.UserConnPrefix + v.Id, rc.ConnName);
        Assert.Equal("oracle", rc.Provider);

        // 测连：本机无 Oracle 驱动/实例 → 失败回显底层错误（sql_error + 脱敏，不泄露密码）
        var r = fx.Connections.Test(new ConnectionInput { Provider = "oracle", Host = "127.0.0.1", Port = 1521, Database = "ORCL", UserName = "ora_user", Password = "OraPw123" });
        Assert.False(r.Ok);
        Assert.Equal(DbErrorCodes.SqlError, r.Code);
        Assert.False(String.IsNullOrWhiteSpace(r.Message));
        Assert.DoesNotContain("OraPw123", r.Message);
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

    // —— 多库（2026-11-07 输入5）机制级：Database 可选矩阵 + 库枚举（03-plan S5.11/S5.12）——

    [Fact]
    public void Database_Optional_ServerTypes_SqliteStillRequired()
    {
        // server 型（AC1 判据）：Database 可空 → 创建成功、落库 null
        var v = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("nd"), Provider = "mysql", Host = "127.0.0.1", Port = 3306, UserName = "u", Password = "p" });
        Assert.Null(DbConnection.Find(DbConnection._.Id == Int32.Parse(v.Id))!.Database);

        // sqlite：文件路径仍必填（FR1 矩阵：必填文案不变）
        Assert.Throws<DbAdminException>(() => fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("nd2"), Provider = "sqlite" }));
    }

    [Fact]
    public void ListDatabases_SqliteMainOnly_SystemConnRejected()
    {
        var file = fx.CreateTargetDb("ml");
        var cid = fx.Connections.Create(new ConnectionInput { Name = DbAdminTestFixture.UniqueName("ml"), Provider = "sqlite", Database = file }).Id;
        var d = fx.Connections.ListDatabases(cid);
        Assert.Equal("sqlite", d.Provider);
        var main = Assert.Single(d.Databases);
        Assert.Equal("main", main.Name);
        Assert.Equal("main", main.Kind);
        Assert.True(main.IsDefault);

        var ex = Assert.Throws<DbAdminException>(() => fx.Connections.ListDatabases(DbConnectionService.SystemIdPrefix + DbAdminTestFixture.SystemConnName));
        Assert.Equal(DbErrorCodes.InvalidRequest, ex.Code);
    }
}

using ForgeSelf.Api.Plugins.DbAdmin.Data;
using ForgeSelf.Api.Plugins.DbAdmin.Services;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>
/// DbAdmin 测试夹具：插件库 / 模拟系统库 / 用户目标库全部落在
/// <c>{系统temp}/ForgeSelfDbAdminTests/{随机}</c>（铁律10/11：随机隔离目录、只建不删）。
/// 整个集合共享一份，集合内串行（DAL 连接名是进程级静态）。
/// </summary>
public sealed class DbAdminTestFixture
{
    public const String SystemConnName = "DbAdminTestHost";

    public String Dir { get; }
    public String SystemDbFile { get; }
    public FileKeySecretProtector Protector { get; }
    public DbAuditWriter Audit { get; } = new();
    public DbConnectionService Connections { get; }
    public DbQueryService Queries { get; }
    public PermissionModeStore Modes { get; }

    public DbAdminTestFixture()
    {
        Dir = Path.Combine(Path.GetTempPath(), "ForgeSelfDbAdminTests", DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Dir);

        // 插件自有库
        DAL.AddConnStr(DbAdminTables.ConnName, $"Data Source={Path.Combine(Dir, "DbAdmin.db")}", null, "SQLite");
        Assert.True(DbAdminTables.EnsureCreated(), "DbAdmin 测试库建表失败");

        // 模拟宿主 / 其它插件库（系统只读连接）
        SystemDbFile = Path.Combine(Dir, "HostDemo.db");
        DAL.AddConnStr(SystemConnName, $"Data Source={SystemDbFile}", null, "SQLite");
        var sys = DAL.Create(SystemConnName);
        sys.Execute("CREATE TABLE IF NOT EXISTS host_items (id INTEGER PRIMARY KEY, name TEXT)");
        sys.Execute("INSERT INTO host_items (name) VALUES ('a'), ('b')");

        Protector = new FileKeySecretProtector(Path.Combine(Dir, "data"));
        Connections = new DbConnectionService(Protector, new SqliteOnlyProbe(), Audit, () => [SystemConnName]);
        Modes = new PermissionModeStore(Path.Combine(Dir, "data"));
        Queries = new DbQueryService(Connections, Modes, Audit);
    }

    /// <summary>新建一个已存在的用户 SQLite 目标库文件（经独立连接名建表，文件随之创建）</summary>
    public String CreateTargetDb(String tag, String? ddl = null)
    {
        var file = Path.Combine(Dir, $"target_{tag}_{Guid.NewGuid().ToString("N")[..6]}.db");
        var seedName = "DbAdminTestSeed_" + Guid.NewGuid().ToString("N")[..8];
        DAL.AddConnStr(seedName, $"Data Source={file}", null, "SQLite");
        var dal = DAL.Create(seedName);
        dal.Execute(ddl ?? "CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY, name TEXT, note TEXT)");
        return file;
    }

    public static String UniqueName(String prefix) => prefix + "_" + Guid.NewGuid().ToString("N")[..8];
}

/// <summary>驱动探测替身：仅 SQLite 可用（模拟其他驱动缺失）</summary>
public sealed class SqliteOnlyProbe : IDriverProbe
{
    public Boolean IsAvailable(ProviderDef def) => def.Id == "sqlite";
}

[CollectionDefinition(Name)]
public sealed class DbAdminCollection : ICollectionFixture<DbAdminTestFixture>
{
    public const String Name = "DbAdmin";
}

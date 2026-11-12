using System;
using System.IO;
using ForgeSelf.Api.Plugins.DbAdmin.Data;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DbAdminTests;

/// <summary>
/// F1（2026-11-05，P1 用户连接跨宿主升级重启丢失）根因机制守卫：
/// XCode 在连接名未登记时，<c>EntityFactory.InitConnection</c> 会自动兜底
/// <c>Data Source=Data\&lt;ConnName&gt;.db</c>（<b>相对进程 CWD</b>）。51888 实例上该兜底把库写进了
/// versions/&lt;ver&gt;/Data/（版本层），宿主升级换版本目录即丢用户数据——
/// 违反 packaging-upgrade-backup §1.7（用户数据必须外置）。
/// 守卫点（纯连接串字面量断言）：
/// ① 未登记连接名 → 兜底串为<b>相对路径</b> Data\&lt;conn&gt;.db（版本层形态实锤）；
/// ② 已登记连接名 → 使用登记的绝对路径（宿主登记先行的正确姿势，经 fixture 合规入口）。
/// DbAdminPlugin.Apply 不抢跑建表由 dev-stack 重启链实测 + 宿主日志断言覆盖（05-evidence F1 节）。
/// </summary>
[Collection(DbAdminCollection.Name)]
public class DbAdminFallbackGuardTests
{
    private readonly DbAdminTestFixture _fx;

    public DbAdminFallbackGuardTests(DbAdminTestFixture fx) => _fx = fx;

    [Fact]
    public void 未登记连接名_InitConnection_兜底为相对Data路径()
    {
        // 探针连接名不在 fixture 登记集内（fixture 只绑 DbAdmin/DbAdminTestHost/DbAdminTestSeed_*）
        var conn = DbAdminTestFixture.UniqueName("DbAdminFallbackProbe");
        // 前提自查：该连接名确实未登记（阳性对照，防同源自洽）
        Assert.False(DAL.ConnStrs!.ContainsKey(conn));

        EntityFactory.InitConnection(conn);

        var dataSource = ExtractDataSource(DAL.Create(conn).ConnStr);
        // 兜底实锤：相对路径（非盘符根）+ Data\<conn>.db 形态 —— CWD 在哪库就落哪（升级即漂移）
        Assert.False(Path.IsPathRooted(dataSource), "未登记兜底应为相对路径，实得: " + dataSource);
        Assert.EndsWith(Path.Combine("Data", conn + ".db"), dataSource);
    }

    [Fact]
    public void 已登记连接名_InitConnection_使用登记的绝对路径()
    {
        // 经 fixture 合规入口登记（PC-014：禁手搓连接串；CreateTargetDb 封装首绑）
        var registered = _fx.CreateTargetDb("fallback_guard");

        // 从 DAL 找到该文件的登记连接名（CreateTargetDb 返回文件，登记名由 fixture 生成）
        var seedEntry = DAL.ConnStrs!.First(kv =>
            kv.Value.Contains(Path.GetFileName(registered), StringComparison.OrdinalIgnoreCase));
        var conn = seedEntry.Key;

        EntityFactory.InitConnection(conn);

        var dataSource = ExtractDataSource(DAL.Create(conn).ConnStr);
        Assert.Equal(Path.GetFullPath(registered), Path.GetFullPath(dataSource));
        Assert.True(Path.IsPathRooted(dataSource), "登记串应为绝对路径");
    }

    /// <summary>从连接串抽 Data Source 值（DAL.ConnStr 返回完整连接串而非纯路径）。</summary>
    private static String ExtractDataSource(String? connectionString)
    {
        const String key = "Data Source=";
        var idx = connectionString!.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        Assert.True(idx >= 0, "连接串应含 Data Source 项: " + connectionString);
        var rest = connectionString.Substring(idx + key.Length);
        var semi = rest.IndexOf(';');
        return (semi >= 0 ? rest.Substring(0, semi) : rest).Trim();
    }
}
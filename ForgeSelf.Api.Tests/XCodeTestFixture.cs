using XCode.DataAccessLayer;
using XCode;
using System.Reflection;
using NewLife.Log;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// XCode 测试数据库辅助类，为测试提供临时 SQLite 数据库
/// </summary>
public class XCodeTestFixture
{
    private readonly string _dbDir;
    private static readonly string[] _connNames = new[]
    {
        "ForgeSelf",
        "MemorySystem",
        "QuickLinks",
        "Scheduler",
        "ScriptRunner",
        "WorkflowEngine",
        "AIAgent",
        "TodoTracker",
        "ProxyCapture",
        "ImGateway",
        "FileTools"
    };

    public XCodeTestFixture()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        XTrace.Log.Level = LogLevel.Error;

        foreach (var connName in _connNames)
        {
            var dbPath = Path.Combine(_dbDir, $"{connName}.db");
            var connStr = $"Data Source={dbPath}";
            DAL.AddConnStr(connName, connStr, null, "SQLite");
        }

        EnsureTablesCreated();
    }

    /// <summary>
    /// 注册连接串后，为每个连接名主动建表（反向工程）。
    /// 用 <see cref="EntityFactory.InitConnection"/> 触发该连接名下所有实体的建表，
    /// 避免测试运行期出现 "no such table" 错误。
    /// 依据 DeepWiki（NewLifeX/NewLife.XCode）：InitConnection 会获取该连接的 DAL 实例，
    /// 在 Migration 开启时调用 dal.SetTables(...) 创建/更新所有关联实体表。
    /// </summary>
    public static void EnsureTablesCreated()
    {
        foreach (var connName in _connNames)
        {
            try
            {
                EntityFactory.InitConnection(connName);
            }
            catch
            {
                // 个别连接名可能暂无关联实体，忽略
            }
        }
    }

    /// <summary>
    /// 清空全部测试连接名下所有业务表数据（每测试方法前调用，保证用例间零串扰）。
    /// 只作用于 <see cref="_connNames"/> 指向的测试临时库（构造函数注册的随机目录），
    /// 与任何真实数据目录无交集；跳过 sqlite_/sys_ 等系统表。
    /// </summary>
    public static void ClearAllData()
    {
        foreach (var connName in _connNames)
        {
            try
            {
                var dal = DAL.Create(connName);
                foreach (var table in dal.Tables)
                {
                    var name = table.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.StartsWith("sys_", StringComparison.OrdinalIgnoreCase)) continue;
                    dal.Execute($"DELETE FROM {name}");
                }
            }
            catch
            {
                // 个别连接暂无可清空的表（或表已不存在）时忽略，不阻断测试
            }
        }
    }
}

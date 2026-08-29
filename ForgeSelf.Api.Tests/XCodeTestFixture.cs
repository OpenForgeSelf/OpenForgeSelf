using XCode.DataAccessLayer;
using XCode;
using System.Reflection;
using NewLife.Log;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// XCode 测试数据库辅助类，为测试提供临时 SQLite 数据库
/// </summary>
public class XCodeTestFixture : IDisposable
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
        "ProxyCapture"
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
    private static void EnsureTablesCreated()
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dbDir))
            {
                Directory.Delete(_dbDir, true);
            }
        }
        catch
        {
        }
    }
}

using XCode.DataAccessLayer;
using XCode;
using System.Reflection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Tests;

/// <summary>
/// XCode 测试数据库辅助类，为测试提供临时 SQLite 数据库
/// </summary>
public class XCodeTestFixture : IDisposable
{
    private readonly string _dbDir;
    private readonly string[] _connNames = new[]
    {
        "OpenForgeSelf",
        "MemorySystem",
        "QuickLinks",
        "Scheduler",
        "ScriptRunner",
        "WorkflowEngine",
        "AIAgent"
    };

    public XCodeTestFixture()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"OpenForgeSelfTest_{Guid.NewGuid():N}");
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

    private static void EnsureTablesCreated()
    {
        var entityTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(EntityBase)))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            try
            {
                var metaProp = entityType.GetProperty("Meta", BindingFlags.Static | BindingFlags.Public);
                if (metaProp != null)
                {
                    var meta = metaProp.GetValue(null);
                    if (meta != null)
                    {
                        var createTableMethod = meta.GetType().GetMethod("CreateTable", Type.EmptyTypes);
                        createTableMethod?.Invoke(meta, null);
                    }
                }
            }
            catch
            {
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

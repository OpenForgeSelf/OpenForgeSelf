using NewLife;
using NewLife.Data;
using System.Reflection;
using XCode;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Data;

public static class XCodeConfig
{
    /// <summary>
    /// 全部 XCode 数据库连接名的唯一真源（name → 文件名）。
    /// 新增库只需在此加一行，连接串注册与库初始化共用本字典，绝不漏设。
    /// </summary>
    public static IReadOnlyDictionary<string, string> DbFiles { get; } = new Dictionary<string, string>
    {
        ["OpenForgeSelf"] = "OpenForgeSelf.db",
        ["MemorySystem"] = "MemorySystem.db",
        ["QuickLinks"] = "QuickLinks.db",
        ["Scheduler"] = "Scheduler.db",
        ["ScriptRunner"] = "ScriptRunner.db",
        ["WorkflowEngine"] = "WorkflowEngine.db",
        ["AIAgent"] = "AIAgent.db",
        ["TodoTracker"] = "TodoTracker.db"
    };

    public static void AddXCode(this IServiceCollection services, IConfiguration configuration, string dataDirectory)
    {
        // 确保数据根目录存在
        if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

        // 仅登记文件名；连接串 Data Source 统一由 dataDirectory 派生绝对路径。
        // 新增库只需在 DbFiles 加一行，路径绝不会漏设。
        foreach (var (name, file) in DbFiles)
        {
            // 若 appsettings 显式给连接串（用户自定义绝对路径），优先使用；否则统一派生到数据根
            var connStr = configuration.GetConnectionString(name)
                ?? $"Data Source={Path.Combine(dataDirectory, file)}";
            DAL.AddConnStr(name, connStr, null, "SQLite");
        }

        XCodeSetting.Current.ShowSQL = false;
    }

    public static void InitializeXCodeDatabase(this IApplicationBuilder app, IWebHostEnvironment env, string dataDirectory)
    {
        if (env.IsEnvironment("Testing")) return;

        var dataDir = dataDirectory;
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        // 与 AddXCode 共用 DbFiles 唯一真源，新增库不会漏初始化
        foreach (var connName in DbFiles.Keys)
        {
            try
            {
                var dal = DAL.Create(connName);
                var version = dal.Db.ServerVersion;
                NewLife.Log.XTrace.Log.Info("{0} 数据库连接成功 (ServerVersion={1})", connName, version);
            }
            catch (Exception ex)
            {
                NewLife.Log.XTrace.Log.Warn("{0} 数据库连接失败: {1}", connName, ex.Message);
            }
        }

        try
        {
            EnsureTablesCreated();
            NewLife.Log.XTrace.Log.Info("所有 XCode 实体表初始化完成");
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn("自动建表失败: {0}", ex.Message);
        }
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
            catch (Exception ex)
            {
                NewLife.Log.XTrace.Log.Debug("实体 {0} 建表失败: {1}", entityType.Name, ex.Message);
            }
        }
    }
}

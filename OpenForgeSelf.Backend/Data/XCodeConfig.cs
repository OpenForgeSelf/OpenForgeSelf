using NewLife;
using NewLife.Data;
using System.Reflection;
using XCode;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Data;

public static class XCodeConfig
{
    public static void AddXCode(this IServiceCollection services, IConfiguration configuration)
    {
        var connStrings = new Dictionary<string, string>
        {
            ["OpenForgeSelf"] = configuration.GetConnectionString("OpenForgeSelf") ?? "Data Source=Data\\OpenForgeSelf.db",
            ["MemorySystem"] = configuration.GetConnectionString("MemorySystem") ?? "Data Source=Data\\MemorySystem.db",
            ["QuickLinks"] = configuration.GetConnectionString("QuickLinks") ?? "Data Source=Data\\QuickLinks.db",
            ["Scheduler"] = configuration.GetConnectionString("Scheduler") ?? "Data Source=Data\\Scheduler.db",
            ["ScriptRunner"] = configuration.GetConnectionString("ScriptRunner") ?? "Data Source=Data\\ScriptRunner.db",
            ["WorkflowEngine"] = configuration.GetConnectionString("WorkflowEngine") ?? "Data Source=Data\\WorkflowEngine.db",
            ["AIAgent"] = configuration.GetConnectionString("AIAgent") ?? "Data Source=Data\\AIAgent.db",
            ["TodoTracker"] = configuration.GetConnectionString("TodoTracker") ?? "Data Source=Data\\TodoTracker.db"
        };

        foreach (var (name, connStr) in connStrings)
        {
            DAL.AddConnStr(name, connStr, null, "SQLite");
        }

        XCodeSetting.Current.ShowSQL = false;
    }

    public static void InitializeXCodeDatabase(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsEnvironment("Testing")) return;

        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        var connNames = new[]
        {
            "OpenForgeSelf",
            "MemorySystem",
            "QuickLinks",
            "Scheduler",
            "ScriptRunner",
            "WorkflowEngine",
            "AIAgent",
            "TodoTracker"
        };

        foreach (var connName in connNames)
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

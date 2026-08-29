using NewLife;
using NewLife.Data;
using ForgeSelf.Abstractions;
using System.Reflection;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Data;

public static class XCodeConfig
{
    /// <summary>数据根下的插件数据子目录名，与插件数据目录服务同源（单一真源，避免命名漂移）。</summary>
    public const string PluginDataRootName = IDataLocationService.PluginDataRootName;

    /// <summary>宿主库连接名：库文件直接落在数据根（如 <c>~/.forgeself/ForgeSelf.db</c>）。</summary>
    private static readonly string[] HostDbs = { "ForgeSelf" };

    /// <summary>
    /// 插件库（连接名 → 插件 Id）：库落在 <c>{数据根}/{PluginDataRootName}/{插件Id}/{连接名}.db</c>，
    /// 与插件调用 <c>ctx.GetPluginDataDirectory()</c> 得到的数据目录完全重合，插件数据与插件库不散落数据根。
    /// 新增插件库只需在此加一行；插件 Id 必须与各插件 <c>plugin.json</c> 的 Id 一致（kebab-case，
    /// 见 <see cref="IDataLocationService.PluginDataRootName"/> 旁的命名规范说明）。
    /// </summary>
    public static IReadOnlyDictionary<string, string> PluginDbs { get; } = new Dictionary<string, string>
    {
        ["MemorySystem"] = "memory-system",
        ["QuickLinks"] = "quick-links",
        ["Scheduler"] = "scheduler",
        ["ScriptRunner"] = "script-runner",
        ["WorkflowEngine"] = "workflow-engine",
        ["AIAgent"] = "ai-agent",
        ["TodoTracker"] = "todo-tracker",
        ["ProxyCapture"] = "proxy-capture"
    };

    /// <summary>
    /// 全部 XCode 数据库连接名的唯一真源（连接名 → 相对数据根的相对路径）。
    /// <list type="bullet">
    /// <item>宿主库：<c>{文件名}</c>，直接落数据根。</item>
    /// <item>插件库：<c>Plugins/{插件Id}/{文件名}</c>，按插件隔离。</item>
    /// </list>
    /// 新增库只需在 <see cref="HostDbs"/> 或 <see cref="PluginDbs"/> 加一行，
    /// 连接串注册与库初始化共用本字典，绝不漏设。
    /// </summary>
    public static IReadOnlyDictionary<string, string> DbFiles { get; } = BuildDbFiles();

    private static Dictionary<string, string> BuildDbFiles()
    {
        var map = new Dictionary<string, string>();
        foreach (var host in HostDbs)
            map[host] = host + ".db";
        // 库文件名 = 连接名 + .db（与 XCode 模型一致：连接名即数据库名）
        foreach (var (connName, pluginId) in PluginDbs)
            map[connName] = Path.Combine(PluginDataRootName, pluginId, connName + ".db");
        return map;
    }

    public static void AddXCode(this IServiceCollection services, IConfiguration configuration, string dataDirectory)
    {
        // 确保数据根目录存在
        if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

        // 仅登记相对路径；连接串 Data Source 统一由 dataDirectory 派生绝对路径。
        // 新增库只需在 HostDbs/PluginDbs 加一行，路径绝不会漏设。
        foreach (var (name, relativePath) in DbFiles)
        {
            var fullPath = Path.Combine(dataDirectory, relativePath);
            // 插件库位于 Plugins/{id}/ 子目录，须先建目录否则 SQLite 打开失败（Error 14）。
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 仅当用户显式配置了「绝对路径」连接串时才采纳（允许自定义存放位置）。
            // 相对路径（如历史遗留的 Data\ForgeSelf.db）会被 NewLife 解析到程序目录，
            // 违背「数据统一落在数据根」的目标，且发布目录下 Data/ 往往不存在导致 Error 14 → 一律忽略并派生。
            var configured = configuration.GetConnectionString(name);
            string connStr;
            if (IsAbsoluteDataSource(configured, out var customPath))
            {
                connStr = configured!;
                NewLife.Log.XTrace.Log.Info("{0} 使用自定义绝对路径连接串: {1}", name, customPath);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(configured))
                    NewLife.Log.XTrace.Log.Warn("{0} 的连接串为相对路径，已忽略并派生到数据根: {1}", name, configured);
                connStr = $"Data Source={fullPath}";
            }

            DAL.AddConnStr(name, connStr, null, "SQLite");
        }

        XCodeSetting.Current.ShowSQL = false;
    }

    /// <summary>
    /// 判断连接串是否指定了绝对路径的数据源（形如 <c>Data Source=C:\x\y.db</c>）。
    /// 相对路径、空值、无 Data Source 项一律返回 false。
    /// </summary>
    private static bool IsAbsoluteDataSource(string? connectionString, out string? dataSource)
    {
        dataSource = null;
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        const string key = "Data Source=";
        var idx = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var value = connectionString[(idx + key.Length)..];
        var semi = value.IndexOf(';');
        if (semi >= 0) value = value[..semi];

        value = value.Trim().Trim('"').Trim();
        dataSource = value;
        return Path.IsPathRooted(value);
    }

    public static void InitializeXCodeDatabase(this IApplicationBuilder app, IWebHostEnvironment env, string dataDirectory)
    {
        if (env.IsEnvironment("Testing")) return;

        var dataDir = dataDirectory;
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        // 插件库在 Plugins/{id}/ 子目录，此处兜底建目录（SQLite 父目录不存在时打开失败 Error 14）。
        foreach (var relativePath in DbFiles.Values)
        {
            var dir = Path.GetDirectoryName(Path.Combine(dataDir, relativePath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
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

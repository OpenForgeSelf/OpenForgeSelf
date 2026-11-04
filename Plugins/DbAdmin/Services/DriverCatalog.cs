using System.Reflection;
using ForgeSelf.Api.Plugins.DbAdmin.Models;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>provider 定义</summary>
public record ProviderDef(String Id, String DisplayName, String XCodeProvider, Int32? DefaultPort, Boolean Tested, String DriverPackage, String[] FactoryTypes);

/// <summary>驱动探测契约（测试可替换）</summary>
public interface IDriverProbe
{
    Boolean IsAvailable(ProviderDef def);
}

/// <summary>
/// provider 目录与驱动探测。驱动打包决策（docs/05 §2，已写回文档）：
/// MVP <b>只依赖宿主随附的 SQLite</b>（XCode.SQLite），插件<b>不打包</b> MySQL / PostgreSQL / SQL Server 驱动；
/// 探测不到驱动时返回 <c>driver_missing</c> + NuGet 包名，<b>不</b>交给 XCode 自动下载（离线/代理环境会卡住且引入未审计 DLL）。
/// 用户可把驱动 DLL 放入宿主程序目录或本插件目录后重启生效。
/// </summary>
public static class DriverCatalog
{
    public static readonly IReadOnlyList<ProviderDef> Providers =
    [
        new("sqlite", "SQLite", "SQLite", null, true, "System.Data.SQLite（宿主已随附）",
            ["System.Data.SQLite.SQLiteFactory, System.Data.SQLite", "Microsoft.Data.Sqlite.SqliteFactory, Microsoft.Data.Sqlite"]),
        new("mysql", "MySQL / MariaDB", "MySql", 3306, false, "MySqlConnector",
            ["MySqlConnector.MySqlConnectorFactory, MySqlConnector", "MySql.Data.MySqlClient.MySqlClientFactory, MySql.Data"]),
        new("postgresql", "PostgreSQL", "PostgreSQL", 5432, false, "Npgsql",
            ["Npgsql.NpgsqlFactory, Npgsql"]),
        new("sqlserver", "SQL Server", "SqlServer", 1433, false, "Microsoft.Data.SqlClient",
            ["Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient", "System.Data.SqlClient.SqlClientFactory, System.Data.SqlClient"]),
    ];

    public static ProviderDef? Find(String? id) =>
        Providers.FirstOrDefault(p => String.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
                                   || String.Equals(p.XCodeProvider, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>判断异常是否属于「驱动缺失 / 加载失败」</summary>
    public static Boolean IsDriverException(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException or DllNotFoundException or ReflectionTypeLoadException)
                return true;
            var m = e.Message ?? "";
            if (m.Contains("驱动", StringComparison.Ordinal) || m.Contains("driver", StringComparison.OrdinalIgnoreCase)
                || m.Contains("DbProviderFactory", StringComparison.OrdinalIgnoreCase) || m.Contains("Could not load", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static String DriverMissingMessage(ProviderDef def) =>
        $"{def.DisplayName} 驱动未安装：本插件 MVP 未随附该驱动。请将 NuGet 包「{def.DriverPackage}」的 DLL 放入宿主程序目录或 DbAdmin 插件目录后重启。";
}

/// <summary>默认探测：已加载程序集 → Type.GetType → 宿主/插件目录下的 DLL 文件</summary>
public sealed class DefaultDriverProbe : IDriverProbe
{
    private readonly String[] _probeDirs;

    public DefaultDriverProbe(params String[] extraDirs)
    {
        var dirs = new List<String> { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "Plugins") };
        var self = Path.GetDirectoryName(typeof(DefaultDriverProbe).Assembly.Location);
        if (!String.IsNullOrEmpty(self)) dirs.Add(self);
        dirs.AddRange(extraDirs.Where(d => !String.IsNullOrWhiteSpace(d)));
        _probeDirs = dirs.Distinct().ToArray();
    }

    public Boolean IsAvailable(ProviderDef def)
    {
        foreach (var full in def.FactoryTypes)
        {
            var parts = full.Split(',', 2, StringSplitOptions.TrimEntries);
            var typeName = parts[0];
            var asmName = parts.Length > 1 ? parts[1] : "";

            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == asmName)) return true;
            try
            {
                if (Type.GetType(full, throwOnError: false) != null) return true;
            }
            catch { /* 忽略，继续探测文件 */ }
            foreach (var dir in _probeDirs)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, asmName + ".dll"))) return true;
                }
                catch { }
            }
        }
        return false;
    }
}

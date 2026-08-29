using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api;

/// <summary>
/// <see cref="IDataLocationService"/> 默认实现：按运行形态解析数据根。
/// 开发模式 → 程序目录 Data/；否则 → 用户主目录 ~/.forgeself。
/// </summary>
public class DataLocationService : IDataLocationService
{
    private readonly string _hostDataDirectory;

    public DataLocationService(IWebHostEnvironment environment)
    {
        _hostDataDirectory = ResolveHostDataDirectory(environment);
    }

    public string GetHostDataDirectory() => _hostDataDirectory;

    public string GetPluginDataDirectory(string pluginId)
    {
        var safe = Sanitize(pluginId);
        return Path.Combine(_hostDataDirectory, IDataLocationService.PluginDataRootName, safe);
    }

    /// <summary>按运行形态解析宿主数据根（实例：依赖 IWebHostEnvironment，最准确）。</summary>
    private static string ResolveHostDataDirectory(IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return Path.Combine(AppContext.BaseDirectory, "Data");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself");
    }

    /// <summary>
    /// 无 IWebHostEnvironment 场景（如 NewLife <c>Config&lt;T&gt;</c> 静态构造）按环境变量判定，等价于 <see cref="IWebHostEnvironment.IsDevelopment"/>。
    /// dotnet run 默认 ASPNETCORE_ENVIRONMENT=Development；发布 exe 默认 Production。
    /// </summary>
    public static string ResolveHostDataDirectory()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrEmpty(env) && env.Equals("Development", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(AppContext.BaseDirectory, "Data");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself");
    }

    /// <summary>把插件 id 清洗为合法目录名，防止路径穿越。</summary>
    private static string Sanitize(string pluginId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(pluginId.Where(c => c != '\0' && !invalid.Contains(c)).ToArray());
        return string.IsNullOrEmpty(safe) ? "unknown" : safe;
    }
}

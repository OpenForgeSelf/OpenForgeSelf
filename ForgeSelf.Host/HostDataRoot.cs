namespace ForgeSelf.Api;

/// <summary>
/// 宿主数据根。API 与托盘进程共用这一份解析，避免两个进程各写一套路径后读到不同的配置文件。
/// </summary>
public static class HostDataRoot
{
    /// <summary>
    /// 按环境变量判定开发态。dotnet run 默认 ASPNETCORE_ENVIRONMENT=Development；发布后的 exe 默认不是。
    /// </summary>
    public static string ResolveFromEnvironment()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isDevelopment = !string.IsNullOrEmpty(env)
            && env.Equals("Development", StringComparison.OrdinalIgnoreCase);
        return Resolve(isDevelopment);
    }

    /// <summary>
    /// FORGESELF_DATA_ROOT 优先；否则开发态用程序目录 data/，其余用用户主目录 ~/.forgeself。
    /// </summary>
    public static string Resolve(bool isDevelopment)
    {
        var overridden = Environment.GetEnvironmentVariable("FORGESELF_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overridden))
            return overridden;

        if (isDevelopment)
            return Path.Combine(AppContext.BaseDirectory, "data");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".forgeself");
    }
}

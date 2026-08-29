namespace ForgeSelf.Api.Tests.Infrastructure;

/// <summary>
/// 内存数据库配置，用于集成测试
/// </summary>
public static class InMemoryDatabaseConfiguration
{
    /// <summary>
    /// 创建内存数据库的数据库名称
    /// </summary>
    public static string GetDatabaseName(string? suffix = null)
    {
        var baseName = "ForgeSelf_TestDb";
        return string.IsNullOrEmpty(suffix) 
            ? $"{baseName}_{Guid.NewGuid():N}" 
            : $"{baseName}_{suffix}_{Guid.NewGuid():N}";
    }
}
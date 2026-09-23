using ForgeSelf.Api.Plugins.McpCenter.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// McpGatewayConfig 加载行为：config.json 大小写不敏感（用户手改友好）、缺失时写默认、环境变量优先。
/// 用例数据库不涉及（纯文件+环境变量），无需夹具。
/// </summary>
public sealed class McpGatewayConfigTests
{
    [Fact]
    public void Load_HandWrittenLowercaseKeys_AppliesValues()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mcpcfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                "{\"port\": 19001, \"listenHost\": \"127.0.0.1\", \"token\": \"hand-token\"}");
            var cfg = McpGatewayConfig.Load(dir);
            Assert.Equal(19001, cfg.Port);
            Assert.Equal("127.0.0.1", cfg.ListenHost);
            Assert.Equal("hand-token", cfg.Token);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesDefaultsAndIsIdempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mcpcfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg1 = McpGatewayConfig.Load(dir);
            Assert.Equal(McpGatewayConfig.DefaultPort, cfg1.Port);
            Assert.True(File.Exists(Path.Combine(dir, "config.json")));

            // 幂等：二次加载与首次一致
            var cfg2 = McpGatewayConfig.Load(dir);
            Assert.Equal(cfg1.Port, cfg2.Port);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_EnvPortOverridesConfigFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mcpcfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var original = Environment.GetEnvironmentVariable(McpGatewayConfig.EnvPort);
        try
        {
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"Port\": 19002}");
            Environment.SetEnvironmentVariable(McpGatewayConfig.EnvPort, "19003");
            var cfg = McpGatewayConfig.Load(dir);
            Assert.Equal(19003, cfg.Port);
        }
        finally
        {
            if (original == null) Environment.SetEnvironmentVariable(McpGatewayConfig.EnvPort, null);
            else Environment.SetEnvironmentVariable(McpGatewayConfig.EnvPort, original);
            Directory.Delete(dir, recursive: true);
        }
    }
}

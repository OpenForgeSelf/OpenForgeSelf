using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 外部服务器配置模型与存储测试（v2.1.0）：校验（传输枚举/URL scheme 白名单/command 白名单/id 规范）、
/// external-servers.json 原子持久化（含密钥明文保留）。
/// </summary>
public class McpExternalConfigTests
{
    private static string TempDataDir() =>
        Path.Combine(Path.GetTempPath(), "mcpcenter-config-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Validate_StreamableHttp_RequiresHttpUrl()
    {
        var cfg = new McpExternalServerConfig { Id = "deepwiki", Name = "DeepWiki", Transport = "streamable-http", Url = "file:///etc/passwd" };
        var ex = Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg));
        Assert.Contains("http/https", ex.Message);
    }

    [Fact]
    public void Validate_Stdio_CommandWhitelist()
    {
        var cfg = new McpExternalServerConfig { Id = "bad", Name = "Bad", Transport = "stdio", Command = "rm" };
        var ex = Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg));
        Assert.Contains("白名单", ex.Message);
    }

    [Fact]
    public void Validate_UnknownTransport_Rejected()
    {
        var cfg = new McpExternalServerConfig { Id = "x", Name = "X", Transport = "websocket", Url = "http://a.b" };
        var ex = Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg));
        Assert.Contains("传输类型不支持", ex.Message);
    }

    [Fact]
    public void Validate_IdKebabCase_DotsAndSpacesRejected()
    {
        var cfg = new McpExternalServerConfig { Id = "my server", Name = "X", Transport = "streamable-http", Url = "http://a.b" };
        Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg));

        var cfg2 = new McpExternalServerConfig { Id = "my.server", Name = "X", Transport = "streamable-http", Url = "http://a.b" };
        Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg2));
    }

    [Fact]
    public void Validate_NormalizesToLowercaseIdAndTrims()
    {
        var cfg = new McpExternalServerConfig { Id = " DeepWiki ", Name = "  DeepWiki  ", Transport = "STREAMABLE-HTTP", Url = " https://mcp.deepwiki.com/mcp " };
        var normalized = ExternalServersStore.ValidateAndNormalize(cfg);
        Assert.Equal("deepwiki", normalized.Id);
        Assert.Equal("DeepWiki", normalized.Name);
        Assert.Equal("streamable-http", normalized.Transport);
        Assert.Equal("https://mcp.deepwiki.com/mcp", normalized.Url);
    }

    [Fact]
    public void Store_SaveThenLoad_RoundTripsWithSecretsIntact()
    {
        var store = new ExternalServersStore(TempDataDir());
        var cfg = new McpExternalServerConfig
        {
            Id = "deepwiki",
            Name = "DeepWiki",
            Enabled = true,
            Transport = "streamable-http",
            Url = "https://mcp.deepwiki.com/mcp",
            Headers = new() { ["Authorization"] = "Bearer super-secret-token" },
            Env = new() { ["KEY"] = "v" }
        };
        store.Save(new List<McpExternalServerConfig> { cfg });

        var loaded = store.Load();
        var item = Assert.Single(loaded);
        Assert.Equal("deepwiki", item.Id);
        Assert.Equal("Bearer super-secret-token", item.Headers["Authorization"]);
        Assert.Equal("v", item.Env["KEY"]);
    }

    [Fact]
    public void Store_MissingFile_ReturnsEmpty()
    {
        var store = new ExternalServersStore(TempDataDir());
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Manager_GetStates_MasksSecrets()
    {
        var store = new ExternalServersStore(TempDataDir());
        var cfg = new McpExternalServerConfig
        {
            Id = "deepwiki",
            Name = "DeepWiki",
            Transport = "streamable-http",
            Url = "https://mcp.deepwiki.com/mcp",
            Headers = new() { ["Authorization"] = "Bearer abcdef1234" },
            Env = new() { ["API_KEY"] = "xyz7890" }
        };
        store.Save(new List<McpExternalServerConfig> { cfg });

        var manager = new McpClientManager(store);
        var state = Assert.Single(manager.GetStates());
        Assert.Equal("****1234", state.HeadersMasked["Authorization"]);
        Assert.Equal("****7890", state.EnvMasked["API_KEY"]);
        Assert.DoesNotContain("abcdef1234", state.HeadersMasked["Authorization"]);
        Assert.DoesNotContain("Bearer", state.HeadersMasked["Authorization"]);
        Assert.False(state.Connected);
    }
}

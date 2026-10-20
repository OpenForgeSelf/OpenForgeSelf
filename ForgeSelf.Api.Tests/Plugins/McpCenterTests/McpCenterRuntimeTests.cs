using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// McpCenterRuntime 单元测试（034 v2.0.0，网关配置 API 的后端核心）：
/// ① GET 视图令牌脱敏（不回显明文）；② PUT 校验（端口范围 / 空请求体）与失败回滚；
/// ③ PUT 持久化（config.json 落盘，键 port/listenHost/token）；④ 空串令牌=清除鉴权。
/// 数据根用随机后缀临时目录（测试只创建使用，不删除——数据安全铁律 10）。
/// </summary>
public class McpCenterRuntimeTests
{
    private static string MakeTempDir()
        => Path.Combine(Path.GetTempPath(), "mcp-center-runtime", Guid.NewGuid().ToString("N"));

    private static McpCenterRuntime CreateRuntime(string dir, out McpGatewayConfig config)
    {
        config = McpGatewayConfig.Load(dir); // 缺省自动写 config.json
        var ctx = new Mock<IContext>();
        var store = new ExternalServersStore(Path.Combine(Path.GetTempPath(), "mcpcenter-tests", Guid.NewGuid().ToString("N")));
        var forwarder = new UniversalToolForwarder(ctx.Object, new McpClientManager(store));
        // 网关改用官方 MCP SDK 后构造入参为业务转发器（不再经自研 McpJsonRpcHandler）
        var server = new McpGatewayServer(config, forwarder, "2.0.0-test");
        return new McpCenterRuntime(dir, config, server, "2.0.0-test");
    }

    private static McpCenterConfigDto GetInfo(McpCenterRuntime runtime)
        => runtime.GetInfo();

    // ---------- GET 视图与令牌脱敏 ----------

    [Fact]
    public void 未设置令牌时视图标记hasToken为false且掩码为空()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out _);

        var info = GetInfo(runtime);

        Assert.False(info.HasToken);
        Assert.Equal("", info.TokenMasked);
        Assert.Equal(18890, info.Port); // 默认
        Assert.Equal("127.0.0.1", info.ListenHost);
        Assert.Equal("http://127.0.0.1:18890", info.ListenUrl);
    }

    [Fact]
    public void 设置令牌后视图仅返回掩码不回显明文()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        config.Token = "secret-token-1234";

        var info = GetInfo(runtime);

        Assert.True(info.HasToken);
        Assert.DoesNotContain("secret-token-1234", info.TokenMasked);
        Assert.EndsWith("1234", info.TokenMasked);
        Assert.Contains("••••", info.TokenMasked);
    }

    [Fact]
    public void 短令牌掩码为四个圆点()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        config.Token = "ab";

        Assert.Equal("••••", GetInfo(runtime).TokenMasked);
    }

    // ---------- PUT 校验与回滚 ----------

    [Fact]
    public void 端口越界时抛异常且配置不变()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);

        var ex = Assert.ThrowsAsync<ArgumentException>(
            () => runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { Port = 80 })).Result;

        Assert.Contains("1024-65535", ex.Message);
        Assert.Equal(18890, config.Port); // 回滚到原值
        Assert.Equal("127.0.0.1", config.ListenHost);
    }

    [Fact]
    public void 空请求体抛异常()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out _);

        Assert.ThrowsAsync<ArgumentException>(
            () => runtime.ApplyUpdateAsync(null!)).Wait();
    }

    // ---------- PUT 持久化 ----------

    [Fact]
    public void 更新配置后configJson落盘且视图反映新值()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out _);

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto
        {
            Port = 19191,
            ListenHost = "0.0.0.0",
            Token = "new-token-9999"
        }).Result;

        Assert.Equal(19191, updated.Port);
        Assert.Equal("0.0.0.0", updated.ListenHost);
        Assert.True(updated.HasToken);
        Assert.EndsWith("9999", updated.TokenMasked);

        // config.json 已持久化（键 port/listenHost/token）
        var path = Path.Combine(dir, "config.json");
        Assert.True(File.Exists(path));
        var raw = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
        Assert.Equal(19191, raw.GetProperty("port").GetInt32());
        Assert.Equal("0.0.0.0", raw.GetProperty("listenHost").GetString());
        Assert.Equal("new-token-9999", raw.GetProperty("token").GetString());
    }

    [Fact]
    public void 令牌传空串表示清除鉴权()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        config.Token = "old-token";

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { Token = "" }).Result;

        Assert.False(updated.HasToken);
        Assert.Equal("", updated.TokenMasked);
    }

    [Fact]
    public void 令牌不传时保留原值()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        config.Token = "keep-me";

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { Port = 19192 }).Result;

        Assert.True(updated.HasToken);
        Assert.EndsWith("p-me", updated.TokenMasked);
    }

    // ---------- 会话模式可配置（v2.4.0） ----------

    [Fact]
    public void 更新会话模式为有状态后视图与落盘均反映且热重启成功()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        Assert.True(config.Stateless); // 新加载默认无状态

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { Stateless = false }).Result;

        // 视图反映新值（GetInfo 与 PUT 响应同源）
        Assert.False(updated.Stateless);
        Assert.False(GetInfo(runtime).Stateless);

        // config.json 落盘 stateless=false，重启后加载仍为有状态
        var raw = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir, "config.json")));
        Assert.False(raw.GetProperty("stateless").GetBoolean());
        Assert.False(McpGatewayConfig.Load(dir).Stateless);
    }

    // ---------- 外部 MCP 执行超时可配置（v2.5.2） ----------

    [Fact]
    public void 更新执行超时后视图与落盘均反映新值()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        Assert.Equal(300000, config.ExecutionTimeoutMs); // 默认 5 分钟
        Assert.Equal(300000, GetInfo(runtime).ExecutionTimeoutMs);

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { ExecutionTimeoutMs = 600000 }).Result;

        Assert.Equal(600000, updated.ExecutionTimeoutMs);
        Assert.Equal(600000, GetInfo(runtime).ExecutionTimeoutMs);

        // config.json 落盘新键，重启后加载仍为该值（AC-2 保存即落盘）
        var raw = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir, "config.json")));
        Assert.Equal(600000, raw.GetProperty("executionTimeoutMs").GetInt32());
        Assert.Equal(600000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(999)]
    [InlineData(3600001)]
    public void 执行超时越界拒绝且原值不变(int bad)
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);

        var ex = Assert.ThrowsAsync<ArgumentException>(
            () => runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { ExecutionTimeoutMs = bad })).Result;
        Assert.Contains("执行超时须在", ex.Message);

        // 拒绝后配置与落盘均保持默认（控制器层把 ArgumentException 映射 400）
        Assert.Equal(300000, config.ExecutionTimeoutMs);
        Assert.Equal(300000, GetInfo(runtime).ExecutionTimeoutMs);
    }

    [Fact]
    public void 执行超时不传时保持原值()
    {
        var dir = MakeTempDir();
        var runtime = CreateRuntime(dir, out var config);
        config.ExecutionTimeoutMs = 120000;

        var updated = runtime.ApplyUpdateAsync(new McpCenterConfigUpdateDto { Port = 19193 }).Result;

        Assert.Equal(120000, updated.ExecutionTimeoutMs);
    }
}

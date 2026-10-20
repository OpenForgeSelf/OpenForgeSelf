using System.Text;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 外部 MCP 执行超时可配置（v2.5.2，PILOT 2026-10-18）：
/// ① McpGatewayConfig 三级优先序（env → config.json → 默认 300000）与非法回落；
/// ② 三传输执行超时参数化 + HttpClient 单位（FromMilliseconds，AC-5 bug 守卫）；
/// ③ McpClientManager.CreateTransport 按网关配置注入（AC-2 贯穿）；
/// ④ 运行期慢响应按配置预算超时、报错文案带实际秒数。
/// 子超时不变量（PC-010 lens，AC-6）由 McpStdioFramingTests 复跑守卫。
/// </summary>
public sealed class McpGatewayConfigTimeoutTests
{
    private static string MakeTempDir()
    {
        // 显式创建：部分用例先写 config.json 再 Load（Load 只在缺文件分支自建目录）
        var dir = Path.Combine(Path.GetTempPath(), "mcpcfg_timeout", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---------- ① 配置加载 ----------

    [Fact]
    public void Load_NoFile_DefaultsToFiveMinutes_AndPersistedGeneratedFile()
    {
        var dir = MakeTempDir();
        try
        {
            var cfg = McpGatewayConfig.Load(dir);
            Assert.Equal(300000, cfg.ExecutionTimeoutMs);
            Assert.Equal(McpGatewayConfig.DefaultExecutionTimeoutMs, cfg.ExecutionTimeoutMs);
            // 生成的默认 config.json 应含新键（落盘单点）
            var json = File.ReadAllText(Path.Combine(dir, "config.json"));
            Assert.Contains("executionTimeoutMs", json, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_ConfigFileKey_AppliesValue()
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                """{"port": 18890, "listenHost": "127.0.0.1", "token": "", "stateless": true, "executionTimeoutMs": 60000}""");
            Assert.Equal(60000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_LegacyConfigFileWithoutKey_KeepsDefault_NoError()
    {
        var dir = MakeTempDir();
        try
        {
            // 老版本升级场景：无 executionTimeoutMs 键 → 默认 5 分钟，不报错
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                """{"port": 18890, "listenHost": "127.0.0.1", "token": "", "stateless": true}""");
            Assert.Equal(300000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Theory]
    [InlineData(999)]      // 低于下限
    [InlineData(-5)]       // 负值
    [InlineData(3600001)]  // 超上限
    public void Load_ConfigFileOutOfRangeValue_FallsBackDefault(int bad)
    {
        var dir = MakeTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                $$"""{"port": 18890, "listenHost": "127.0.0.1", "token": "", "stateless": true, "executionTimeoutMs": {{bad}}}""");
            Assert.Equal(300000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Load_EnvOverridesConfigFile()
    {
        var dir = MakeTempDir();
        var original = Environment.GetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                """{"port": 18890, "listenHost": "127.0.0.1", "token": "", "stateless": true, "executionTimeoutMs": 60000}""");
            Environment.SetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs, "10000");
            Assert.Equal(10000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs, original);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("99999999")]
    public void Load_EnvInvalid_KeepsConfigFileValue(string bad)
    {
        var dir = MakeTempDir();
        var original = Environment.GetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "config.json"),
                """{"port": 18890, "listenHost": "127.0.0.1", "token": "", "stateless": true, "executionTimeoutMs": 60000}""");
            Environment.SetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs, bad);
            Assert.Equal(60000, McpGatewayConfig.Load(dir).ExecutionTimeoutMs);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpGatewayConfig.EnvExecutionTimeoutMs, original);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(300000, true)]
    [InlineData(3600000, true)]
    [InlineData(3600001, false)]
    public void IsValidExecutionTimeout_Boundaries(int ms, bool expected)
        => Assert.Equal(expected, McpGatewayConfig.IsValidExecutionTimeout(ms));

    // ---------- ② 传输注入 + HttpClient 单位（AC-5） ----------

    [Fact]
    public void StreamableHttp_InjectedTimeout_UsedForHttpClientTimeout_InMilliseconds()
    {
        var transport = new StreamableHttpMcpTransport("https://fake.example/mcp", null, null, 45000);
        try
        {
            Assert.Equal(45000, transport.EffectiveRequestTimeoutMs);
            // bug 守卫：原 FromSeconds(45000)=12.5 小时；必须等于 FromMilliseconds(45000)
            Assert.Equal(TimeSpan.FromMilliseconds(45000), transport.HttpTimeout);
        }
        finally { transport.CloseAsync().GetAwaiter().GetResult(); }
    }

    [Fact]
    public void LegacySse_InjectedTimeout_UsedForHttpClientTimeout_InMilliseconds()
    {
        var transport = new LegacySseMcpTransport("https://fake.example/sse", null, null, 45000);
        try
        {
            Assert.Equal(45000, transport.EffectiveRequestTimeoutMs);
            Assert.Equal(TimeSpan.FromMilliseconds(45000), transport.HttpTimeout);
        }
        finally { transport.CloseAsync().GetAwaiter().GetResult(); }
    }

    [Fact]
    public async Task StreamableHttp_SlowServer_TimesOutWithConfiguredBudget_AndMessageShowsSeconds()
    {
        var transport = new StreamableHttpMcpTransport("https://fake.example/mcp", null, new SlowHandler(3000), 1000);
        try
        {
            var ex = await Assert.ThrowsAsync<McpClientException>(() => transport.ListToolsAsync(CancellationToken.None));
            Assert.Contains("请求超时（1s）", ex.Message);
        }
        finally { await transport.CloseAsync(); }
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        private readonly int _delayMs;
        public SlowHandler(int delayMs) => _delayMs = delayMs;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayMs, cancellationToken); // 超时取消时在此抛出，被 transport 归一为 McpClientException
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"result":{"tools":[]}}""", Encoding.UTF8, "application/json")
            };
        }
    }

    // ---------- ③ Manager 贯穿注入 ----------

    [Fact]
    public void CreateTransport_InjectsGatewayExecutionTimeout_ForHttpTransports()
    {
        var store = new ExternalServersStore(MakeTempDir());
        var config = new McpGatewayConfig { ExecutionTimeoutMs = 12345 };
        var manager = new McpClientManager(store, config);

        var http = Assert.IsType<StreamableHttpMcpTransport>(
            manager.CreateTransport(new McpExternalServerConfig { Id = "x", Transport = "streamable-http", Url = "https://fake.example/mcp" }));
        Assert.Equal(12345, http.EffectiveRequestTimeoutMs);

        var sse = Assert.IsType<LegacySseMcpTransport>(
            manager.CreateTransport(new McpExternalServerConfig { Id = "y", Transport = "http-sse", Url = "https://fake.example/sse" }));
        Assert.Equal(12345, sse.EffectiveRequestTimeoutMs);
    }

    [Fact]
    public void CreateTransport_WithoutConfigInjection_UsesFiveMinuteDefault()
    {
        var store = new ExternalServersStore(MakeTempDir());
        var manager = new McpClientManager(store);
        var http = Assert.IsType<StreamableHttpMcpTransport>(
            manager.CreateTransport(new McpExternalServerConfig { Id = "x", Transport = "streamable-http", Url = "https://fake.example/mcp" }));
        Assert.Equal(300000, http.EffectiveRequestTimeoutMs);
    }

    [Fact]
    public async Task CreateTransport_LazyReadsRuntimeUpdatedTimeout()
    {
        // 与 Runtime 共享同一 config 实例：PUT 改值后（不重启进程），下次建连读到新值
        var dir = MakeTempDir();
        try
        {
            var config = McpGatewayConfig.Load(dir);
            var store = new ExternalServersStore(dir);
            var manager = new McpClientManager(store, config);

            config.ExecutionTimeoutMs = 88000; // 模拟 ApplyUpdateAsync 赋值
            var http = Assert.IsType<StreamableHttpMcpTransport>(
                manager.CreateTransport(new McpExternalServerConfig { Id = "x", Transport = "streamable-http", Url = "https://fake.example/mcp" }));
            Assert.Equal(88000, http.EffectiveRequestTimeoutMs);
            await http.CloseAsync();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

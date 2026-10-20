using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 类守卫 G2/G3（PC-008「MCP 客户端协议合规缺失」，2026-10-17 修复批立）：
/// - G2 形状直测：initialize 载荷必须由 McpClientProtocol.BuildInitializeParams 单点构造且三件齐全；
/// - G3 源码扫描：三个传输源文件禁止再手写 initialize 匿名对象（复发即在 CI 红灯），
///   且必须引用 canonical 构造与 initialized 通知常量；
/// - M2 防护：ExternalServersStore 拒绝掩码形态的 Headers/Env 值落盘。
/// </summary>
public class McpClientProtocolGuardTests
{
    private static readonly string[] TransportFiles =
    {
        "StreamableHttpMcpTransport.cs",
        "StdioMcpTransport.cs",
        "LegacySseMcpTransport.cs",
    };

    /// <summary>从测试输出目录向上定位仓库根（以 ForgeSelf.slnx 为锚）。</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void BuildInitializeParams_CarriesAllThreeMandatoryFields()
    {
        var json = JsonSerializer.Serialize(McpClientProtocol.BuildInitializeParams("2025-06-18"));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2025-06-18", root.GetProperty("protocolVersion").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("capabilities").ValueKind);
        var ci = root.GetProperty("clientInfo");
        Assert.False(string.IsNullOrWhiteSpace(ci.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(ci.GetProperty("version").GetString()));
    }

    [Theory]
    [MemberData(nameof(TransportFileNames))]
    public void Transport_Initialize_MustUseCanonicalParamsBuilder_AndSendInitializedNotification(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "Plugins", "McpCenter", "Services", "McpClient", fileName);
        Assert.True(File.Exists(path), $"传输源文件缺失：{path}");
        var source = File.ReadAllText(path);

        // canonical fix：initialize 载荷一律经 McpClientProtocol 构造
        Assert.Contains("McpClientProtocol.BuildInitializeParams", source);
        // 禁止回退到手写匿名对象（同类复发的形状：new { protocolVersion = ...）
        Assert.True(
            !System.Text.RegularExpressions.Regex.IsMatch(source, @"new\s*\{\s*protocolVersion"),
            $"{fileName} 又手写了 initialize 匿名载荷——clientInfo 缺失缺陷（PC-008）会复发");
        // 握手后必发 initialized 通知（经 canonical 常量引用）
        Assert.Contains("McpClientProtocol.InitializedNotificationMethod", source);
    }

    public static TheoryData<string> TransportFileNames()
    {
        var data = new TheoryData<string>();
        foreach (var f in TransportFiles) data.Add(f);
        return data;
    }

    /* ── PC-010（2026-10-18）：JSON-RPC 请求序列化 null params 必须省略键（FastMCP 系收 null 沉默实测） ── */

    [Fact]
    public void SerializeRequest_NullParams_OmitsParamsKey()
    {
        var json = McpClientProtocol.SerializeRequest(7, "tools/list", null);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("params", out _), "params=null 时不得输出键（实测使 FastMCP 服务器完全沉默）");
        Assert.Equal(7, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("tools/list", doc.RootElement.GetProperty("method").GetString());
    }

    [Fact]
    public void SerializeRequest_WithParams_CarriesParamsObject()
    {
        var json = McpClientProtocol.SerializeRequest(1, "ping", new { x = 1 });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("params").ValueKind);
    }

    [Theory]
    [MemberData(nameof(TransportFileNames))]
    public void Transport_MustUseCanonicalRequestSerializer_NotInlineNullCapablePayload(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "Plugins", "McpCenter", "Services", "McpClient", fileName);
        var source = File.ReadAllText(path);
        Assert.Contains("McpClientProtocol.SerializeRequest", source);
        // 禁止回退手写带 @params=null 的匿名请求（同类复发即红灯）
        Assert.True(
            !System.Text.RegularExpressions.Regex.IsMatch(source, @"@params\s*=\s*prms"),
            $"{fileName} 又手写了请求匿名载荷——null params 会被序列化成字面量 null，FastMCP 系服务器沉默（PC-010）");
    }

    [Fact]
    public void ValidateAndNormalize_RejectsMaskedHeaderAndEnvValues()
    {
        // 状态视图脱敏串（****尾4 / ****）被旧客户端当编辑真值往返 → 必须拒绝，防覆盖真实密钥（M2）
        var cfg = new McpExternalServerConfig
        {
            Id = "srv",
            Name = "Srv",
            Transport = "streamable-http",
            Url = "https://example.com/mcp",
            Headers = new() { ["Authorization"] = "****abcd" },
            Env = new() { ["API_KEY"] = "****" }
        };
        var ex = Assert.Throws<McpClientException>(() => ExternalServersStore.ValidateAndNormalize(cfg));
        Assert.Contains("脱敏展示串", ex.Message);

        // 真实长密钥（即便碰巧含星号）不受影响
        var ok = new McpExternalServerConfig
        {
            Id = "srv",
            Name = "Srv",
            Transport = "streamable-http",
            Url = "https://example.com/mcp",
            Headers = new() { ["Authorization"] = "Bearer sk-****extra-long-real-key" }
        };
        Assert.NotNull(ExternalServersStore.ValidateAndNormalize(ok));
    }
}

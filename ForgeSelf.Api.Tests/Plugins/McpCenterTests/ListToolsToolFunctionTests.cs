using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// list_tools 工具测试（v2.1.0 网关能力）：枚举宿主注册表全部已注册工具（名称/说明/参数格式），
/// 供万能工具 universal_tool 转发「发现工具」；支持关键字过滤与 includeSchema。
/// 注册表经 IContext 运行期解析（与生产一致：装配期 Get 不到，运行期 GetService 可得）。
/// v2.6.0（PILOT-072）增锁：合并 <see cref="IMcpExternalGateway.ListConnectedTools"/> 外部条目
/// （全名 mcp.&lt;id&gt;.&lt;名&gt;、source/ pluginId 语义、keyword/schema 同规则、契约缺失与报错时的降级）。
/// </summary>
public class ListToolsToolFunctionTests
{
    private sealed class FakeTool(string name, string desc, string schema) : IToolFunctionExtension
    {
        public string Id => "fake-" + name;
        public string Name { get; } = name;
        public string PluginId => "fake-plugin";
        public string Description { get; } = desc;
        public string ParametersJsonSchema { get; } = schema;
        public Task<string> ExecuteAsync(string parameters) => Task.FromResult("{}");
    }

    private static ListToolsToolFunction Build(params IToolFunctionExtension[] tools)
        => Build(tools, gateway: null);

    /// <summary>带外部网关替身的构建：gateway=null 时 ctx.Get&lt;IMcpExternalGateway&gt;() 返回 null（Moq 默认），与未装 McpCenter 同形。</summary>
    private static ListToolsToolFunction Build(IToolFunctionExtension[] tools, IMcpExternalGateway? gateway)
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.GetAllTools()).Returns(tools);
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.GetService(typeof(IToolRegistry))).Returns(registry.Object);
        if (gateway != null) ctx.Setup(c => c.Get<IMcpExternalGateway>()).Returns(gateway);
        return new ListToolsToolFunction(ctx.Object);
    }

    [Fact]
    public async Task Execute_Returns_All_Tools_Except_Self()
    {
        var fn = Build(
            new FakeTool("read_file", "读取文件", """{"type":"object"}"""),
            new FakeTool("write_file", "写入文件", """{"type":"object"}"""));

        var text = await fn.ExecuteAsync("{}");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("total").GetInt32());
        var names = root.GetProperty("tools").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Contains("read_file", names);
        Assert.Contains("write_file", names);
        Assert.DoesNotContain(ListToolsToolFunction.ToolName, names);
        // 默认不含 schema
        Assert.False(root.GetProperty("tools")[0].TryGetProperty("parametersSchema", out _));
    }

    [Fact]
    public async Task Execute_Keyword_Filters_ByName_And_Description()
    {
        var fn = Build(
            new FakeTool("read_file", "读取文件内容", """{"type":"object"}"""),
            new FakeTool("calculate", "数学计算", """{"type":"object"}"""));

        var byName = await fn.ExecuteAsync("""{"keyword":"read"}""");
        Assert.Equal(1, JsonDocument.Parse(byName).RootElement.GetProperty("total").GetInt32());

        var byDesc = await fn.ExecuteAsync("""{"keyword":"计算"}""");
        Assert.Equal(1, JsonDocument.Parse(byDesc).RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Execute_IncludeSchema_Attaches_ParametersSchema()
    {
        var fn = Build(new FakeTool("read_file", "读取文件", """{"type":"object","properties":{"path":{"type":"string"}}}"""));

        var text = await fn.ExecuteAsync("""{"includeSchema":true}""");
        using var doc = JsonDocument.Parse(text);
        var tool = doc.RootElement.GetProperty("tools")[0];

        Assert.True(tool.TryGetProperty("parametersSchema", out var schema));
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.GetProperty("properties").TryGetProperty("path", out _));
    }

    [Fact]
    public async Task Execute_RegistryMissing_Returns_EmptyList()
    {
        var fn = new ListToolsToolFunction(null);
        var text = await fn.ExecuteAsync("{}");
        Assert.Equal(0, JsonDocument.Parse(text).RootElement.GetProperty("total").GetInt32());
    }

    // ---------- v2.6.0 外部 MCP 工具合并（PILOT-072：决策 D10 修订——枚举可见、注册表隔离保持） ----------

    private static IMcpExternalGateway FakeGateway(params McpExternalToolInfo[] tools)
    {
        var gw = new Mock<IMcpExternalGateway>();
        gw.Setup(g => g.ListConnectedTools()).Returns(tools);
        return gw.Object;
    }

    private static McpExternalToolInfo Ext(string serverId, string name, string desc = "外部工具") =>
        new(McpExternalToolNames.BuildFullName(serverId, name), serverId, name, desc, """{"type":"object"}""");

    [Fact]
    public async Task Execute_Merges_External_Tools_With_FullName_And_Source_Marker()
    {
        var fn = Build(
            new[]
            {
                new FakeTool("read_file", "读取文件", """{"type":"object"}"""),
                new FakeTool("calculate", "数学计算", """{"type":"object"}""")
            },
            FakeGateway(Ext("deepwiki", "search"), Ext("deepwiki", "ask")));

        var text = await fn.ExecuteAsync("{}");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal(4, root.GetProperty("total").GetInt32()); // 宿主 2 + 外部 2（与网关/AIAgent 共用同一份出口）

        var entries = root.GetProperty("tools").EnumerateArray().ToList();
        var ext = Assert.Single(entries, e => e.GetProperty("name").GetString() == "mcp.deepwiki.search");
        Assert.Equal("mcp-external", ext.GetProperty("source").GetString());
        Assert.Equal("deepwiki", ext.GetProperty("pluginId").GetString()); // 外部条目 pluginId = 服务器 id
        Assert.Equal("registry", entries.First(e => e.GetProperty("name").GetString() == "read_file")
            .GetProperty("source").GetString());
    }

    [Fact]
    public async Task Execute_Keyword_Filters_External_By_FullName_And_Description()
    {
        var fn = Build(
            new[] { new FakeTool("read_file", "读取文件内容", """{"type":"object"}""") },
            FakeGateway(Ext("deepwiki", "search", "仓库问答检索"), Ext("other", "ping", "网络探测")));

        var byFullName = await fn.ExecuteAsync("""{"keyword":"mcp.deepwiki"}""");
        using (var doc = JsonDocument.Parse(byFullName))
            Assert.Equal(1, doc.RootElement.GetProperty("total").GetInt32()); // 仅外部 search 命中（宿主 read_file 名/描述均不含关键字，同步被筛）

        var byDesc = await fn.ExecuteAsync("""{"keyword":"问答"}""");
        using (var doc2 = JsonDocument.Parse(byDesc))
        {
            Assert.Equal(1, doc2.RootElement.GetProperty("total").GetInt32());
            Assert.Equal("mcp.deepwiki.search", doc2.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        }
    }

    [Fact]
    public async Task Execute_IncludeSchema_Attaches_External_ParametersSchema()
    {
        var fn = Build(
            new IToolFunctionExtension[0],
            FakeGateway(new McpExternalToolInfo(
                "mcp.deepwiki.ask", "deepwiki", "ask", "问答",
                """{"type":"object","properties":{"repoName":{"type":"string"}}}""")));

        var text = await fn.ExecuteAsync("""{"includeSchema":true}""");
        using var doc = JsonDocument.Parse(text);
        var tool = doc.RootElement.GetProperty("tools")[0];
        Assert.True(tool.TryGetProperty("parametersSchema", out var schema));
        Assert.True(schema.GetProperty("properties").TryGetProperty("repoName", out _));
    }

    [Fact]
    public async Task Execute_External_BadSchemaJson_Yields_Null_Schema_Not_Crash()
    {
        // 宿主/外部同规则（ParseSchema）：非法 JSON → null，宽松语义不得影响整次枚举
        var fn = Build(
            new IToolFunctionExtension[0],
            FakeGateway(new McpExternalToolInfo("mcp.x.y", "x", "y", "坏 schema", "not-json{")));

        var text = await fn.ExecuteAsync("""{"includeSchema":true}""");
        using var doc = JsonDocument.Parse(text);
        var tool = doc.RootElement.GetProperty("tools")[0];
        Assert.True(tool.TryGetProperty("parametersSchema", out var schema));
        Assert.Equal(JsonValueKind.Null, schema.ValueKind);
    }

    [Fact]
    public async Task Execute_GatewayMissing_Equals_Registry_Only_Before()
    {
        // McpCenter 未启用/已卸载（或契约未提供）：与 v2.5.x 现状逐字段等价，无 source 缺失条目
        var fn = Build(new[] { new FakeTool("read_file", "读取文件", """{"type":"object"}""") });

        var text = await fn.ExecuteAsync("{}");
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(1, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("registry", doc.RootElement.GetProperty("tools")[0].GetProperty("source").GetString());
    }

    [Fact]
    public async Task Execute_GatewayThrows_Still_Returns_Registry_Tools()
    {
        // Spec BC-4：外部合并段整体异常不得影响宿主工具枚举
        var gw = new Mock<IMcpExternalGateway>();
        gw.Setup(g => g.ListConnectedTools()).Throws(new InvalidOperationException("会话已死"));
        var fn = Build(new[] { new FakeTool("read_file", "读取文件", """{"type":"object"}""") }, gw.Object);

        var text = await fn.ExecuteAsync("{}");
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(1, doc.RootElement.GetProperty("total").GetInt32());
    }
}

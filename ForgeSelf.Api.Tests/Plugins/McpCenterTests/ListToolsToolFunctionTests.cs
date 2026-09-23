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
    {
        var registry = new Mock<IToolRegistry>();
        registry.Setup(r => r.GetAllTools()).Returns(tools);
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.GetService(typeof(IToolRegistry))).Returns(registry.Object);
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
}

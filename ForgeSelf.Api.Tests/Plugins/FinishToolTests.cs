using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// finish 完成工具单元测试（自主循环出口）：
/// 锁四类契约——① 定义契约（名字/Id/描述/必填参数）；② summary 提取语义（正常/空/坏 JSON/缺字段）；
/// ③ 执行结果语义（成功带 summary，失败带 error 且 success=false）；④ CreateDefinition 与实例定义一致
/// （自治模式下白名单过滤掉 finish 时按此定义强制补挂）。
/// </summary>
public class FinishToolTests
{
    private const string PluginId = "AIAgent";
    private const string ToolName = "finish";

    private static FinishToolFunction Make() => new(PluginId);

    [Fact]
    public void Definition_Name_And_Id_Should_Follow_Convention()
    {
        var tool = Make();

        Assert.Equal(ToolName, tool.Name);
        // Id = "插件名.工具名" 全小写（全库惯例：aiagent.universal_tool）
        Assert.Equal("aiagent.finish", tool.Id);
        Assert.Equal(PluginId, tool.PluginId);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
    }

    [Fact]
    public void Schema_Should_Require_Summary()
    {
        var tool = Make();

        using var doc = JsonDocument.Parse(tool.ParametersJsonSchema);
        var root = doc.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("properties").TryGetProperty("summary", out _));

        var required = root.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("summary", required);
    }

    [Fact]
    public async Task Execute_With_Summary_Returns_Success()
    {
        var result = await Make().ExecuteAsync("""{"summary":"改了 AIAgentService 循环终止条件"}""");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("改了 AIAgentService 循环终止条件", doc.RootElement.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Execute_With_Empty_Summary_Returns_Failure_Not_Throw()
    {
        var result = await Make().ExecuteAsync("""{"summary":"   "}""");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("缺少 summary", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Execute_With_Broken_Json_Returns_Failure_Not_Throw()
    {
        // 弱模型常见：参数不是合法 JSON。工具必须优雅报错而非抛异常（否则循环内 500）。
        var result = await Make().ExecuteAsync("not-json");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("""{"other":"x"}""")]
    [InlineData("not-json")]
    [InlineData("""{"summary":123}""")]
    public void TryExtractSummary_Incomplete_Input_Returns_Null(string? args)
    {
        Assert.Null(FinishToolFunction.TryExtractSummary(args));
    }

    [Fact]
    public void TryExtractSummary_With_Summary_Returns_Text()
    {
        Assert.Equal("done", FinishToolFunction.TryExtractSummary("""{"summary":"done","extra":1}"""));
    }

    [Fact]
    public void CreateDefinition_Matches_Instance_Definition()
    {
        var instance = Make();
        var def = FinishToolFunction.CreateDefinition();

        Assert.Equal(instance.Name, def.Function.Name);
        Assert.Equal(instance.Description, def.Function.Description);
        // schema 往返后语义不变（同为 JSON 对象且含 required=summary）
        Assert.Equal(
            JsonSerializer.Serialize(JsonDocument.Parse(instance.ParametersJsonSchema).RootElement),
            JsonSerializer.Serialize((JsonElement)def.Function.Parameters));
    }
}

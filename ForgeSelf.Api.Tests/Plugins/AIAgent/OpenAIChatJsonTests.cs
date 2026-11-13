using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// tool_choice 类级守卫：无 tools（缺失或空数组）时 tool_choice 必须省略，否则上游 400
/// （"When using tool_choice, tools must be set."）。
/// 问题类归档见 docs/11-troubleshooting/problem-classes/README.md。
/// </summary>
public class OpenAIChatJsonTests
{
    private static UnifiedChatRequest Request(string? toolChoice, List<UnifiedToolDefinition>? tools) => new()
    {
        Model = "m",
        Messages = [new UnifiedChatMessage { Role = "user", Content = "hi" }],
        ToolChoice = toolChoice,
        Tools = tools
    };

    [Fact]
    public void ToolChoiceWithoutTools_IsOmitted()
    {
        var json = OpenAIChatJson.FromUnified(Request("required", null));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("tool_choice", out _).Should().BeFalse();
    }

    [Fact]
    public void ToolChoiceWithEmptyTools_IsOmitted()
    {
        var json = OpenAIChatJson.FromUnified(Request("required", []));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("tool_choice", out _).Should().BeFalse();
    }

    [Fact]
    public void ToolChoiceWithTools_IsKept()
    {
        var tools = new List<UnifiedToolDefinition>
        {
            new()
            {
                Name = "complete_step",
                Description = "提交步骤产出",
                Parameters = new { type = "object", properties = new { } }
            }
        };

        var json = OpenAIChatJson.FromUnified(Request("required", tools));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("tool_choice").GetString().Should().Be("required");
        doc.RootElement.GetProperty("tools").GetArrayLength().Should().Be(1);
    }
}

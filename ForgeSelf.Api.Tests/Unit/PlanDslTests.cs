using System.Text.Json;
using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// Plan DSL 反序列化测试（tasks.md T005）：Plan JSON → AgentPlan。
/// </summary>
public class PlanDslTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_FullPlan_ParsesGoalAndSteps()
    {
        const string json = """
        {
          "goal": "整理本周工作报告",
          "steps": [
            { "id": "s1", "name": "收集素材", "objective": "收集本周工作数据", "expectedOutput": "素材清单", "allowedTools": ["read_file"], "mandatory": true },
            { "id": "s2", "name": "撰写报告", "objective": "根据素材撰写报告", "expectedOutput": "报告草稿", "mandatory": false }
          ]
        }
        """;

        var plan = JsonSerializer.Deserialize<AgentPlan>(json, JsonOptions);

        plan.Should().NotBeNull();
        plan!.Goal.Should().Be("整理本周工作报告");
        plan.Steps.Should().HaveCount(2);

        var s1 = plan.Steps[0];
        s1.Id.Should().Be("s1");
        s1.Name.Should().Be("收集素材");
        s1.Objective.Should().Be("收集本周工作数据");
        s1.ExpectedOutput.Should().Be("素材清单");
        s1.AllowedTools.Should().Contain("read_file");
        s1.Mandatory.Should().BeTrue();

        plan.Steps[1].Mandatory.Should().BeFalse();
        plan.Steps[1].AllowedTools.Should().BeNull();
    }

    [Fact]
    public void Deserialize_MissingAllowedTools_IsNull()
    {
        const string json = """{ "goal": "g", "steps": [ { "id": "s1", "name": "n", "objective": "o" } ] }""";

        var plan = JsonSerializer.Deserialize<AgentPlan>(json, JsonOptions);

        plan.Should().NotBeNull();
        plan!.Steps[0].AllowedTools.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptySteps_IsEmpty()
    {
        const string json = """{ "goal": "g", "steps": [] }""";

        var plan = JsonSerializer.Deserialize<AgentPlan>(json, JsonOptions);

        plan.Should().NotBeNull();
        plan!.Goal.Should().Be("g");
        plan.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_AgentPlan_RoundTrips()
    {
        var plan = new AgentPlan
        {
            Goal = "目标",
            Steps = { new AgentPlanStep { Id = "s1", Name = "步骤", Objective = "做什么", ExpectedOutput = "产出", AllowedTools = new List<string> { "read_file" }, Mandatory = true } }
        };

        var json = JsonSerializer.Serialize(plan);
        var back = JsonSerializer.Deserialize<AgentPlan>(json, JsonOptions);

        back.Should().NotBeNull();
        back!.Goal.Should().Be("目标");
        back.Steps.Should().ContainSingle().Which.AllowedTools.Should().Contain("read_file");
    }
}

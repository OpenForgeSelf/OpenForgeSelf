using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ForgeSelf.Api.Controllers.UnifiedAI;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 验证 AgentStreamTranslator 把 <see cref="AgentResponseUpdate"/> 翻译为 OpenAI SSE chunk 时，
/// 文本、工具调用、工具结果、结束原因都被正确输出，且 assistant 角色只在第一块出现。
///
/// 重点：工具调用（FunctionCallContent）必须被保留为 tool_calls 块 —— 这正是本项目原手写代理
/// 漏掉、导致「流式只返回第一段」的根因。此处用翻译器单测锁定该行为，防止回归。
/// </summary>
public class AgentStreamTranslatorTests
{
    private const string Id = "chatcmpl-test";
    private const string Model = "gpt-test";
    private const long Created = 1700000000L;

    [Fact]
    public void Translate_纯文本_输出Content块且首块带role()
    {
        var update = new AgentResponseUpdate
        {
            Contents = new List<AIContent> { new TextContent("你好") }
        };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks.Should().HaveCount(1);
        var delta = chunks[0].Choices![0].Delta;
        delta!.Content.Should().Be("你好");
        delta.Role.Should().Be("assistant"); // 首块带 role
        roleEmitted.Should().BeTrue();
    }

    [Fact]
    public void Translate_工具调用_输出tool_calls块含名称参数与索引()
    {
        var args = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };
        var update = new AgentResponseUpdate
        {
            Contents = new List<AIContent>
            {
                new FunctionCallContent("call_1", "add_numbers", args)
            }
        };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks.Should().HaveCount(1);
        var toolCalls = chunks[0].Choices![0].Delta!.ToolCalls;
        toolCalls.Should().NotBeNull().And.HaveCount(1);

        var tc = toolCalls![0];
        tc.Index.Should().Be(0);
        tc.Id.Should().Be("call_1");
        tc.Type.Should().Be("function");
        tc.Function!.Name.Should().Be("add_numbers");
        // 参数序列化为 JSON 且可反序列化还原
        tc.Function.Arguments.Should().Contain("a").And.Contain("b");
        using var doc = System.Text.Json.JsonDocument.Parse(tc.Function.Arguments);
        doc.RootElement.GetProperty("a").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("b").GetInt32().Should().Be(2);
        // 工具调用块必须带 role（OpenAI 要求 tool_calls 出现在 assistant 角色块内）
        chunks[0].Choices[0].Delta!.Role.Should().Be("assistant");
    }

    [Fact]
    public void Translate_多个工具调用_各自独立索引()
    {
        var update = new AgentResponseUpdate
        {
            Contents = new List<AIContent>
            {
                new FunctionCallContent("call_1", "get_current_datetime", null),
                new FunctionCallContent("call_2", "add_numbers", null)
            }
        };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks.Should().HaveCount(2);
        chunks[0].Choices![0].Delta!.ToolCalls![0].Index.Should().Be(0);
        chunks[1].Choices![0].Delta!.ToolCalls![0].Index.Should().Be(1);
    }

    [Fact]
    public void Translate_空参数_序列化为空对象()
    {
        var update = new AgentResponseUpdate
        {
            Contents = new List<AIContent>
            {
                new FunctionCallContent("call_1", "get_current_datetime", null)
            }
        };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks[0].Choices![0].Delta!.ToolCalls![0].Function!.Arguments.Should().Be("{}");
    }

    [Fact]
    public void Translate_工具结果_输出tool角色块()
    {
        var update = new AgentResponseUpdate
        {
            Contents = new List<AIContent>
            {
                new FunctionResultContent("call_1", "2026-08-08 12:00:00")
            }
        };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks.Should().HaveCount(1);
        var delta = chunks[0].Choices![0].Delta;
        delta!.Role.Should().Be("tool"); // 服务端工具执行结果以 tool 角色透出
        delta.Content.Should().Be("2026-08-08 12:00:00");
    }

    public static IEnumerable<object[]> FinishReasonCases()
    {
        yield return new object[] { ChatFinishReason.ToolCalls, "tool_calls" };
        yield return new object[] { ChatFinishReason.Stop, "stop" };
    }

    [Theory]
    [MemberData(nameof(FinishReasonCases))]
    public void Translate_结束原因_输出对应finish_reason(ChatFinishReason reason, string expected)
    {
        var update = new AgentResponseUpdate { FinishReason = reason };

        bool roleEmitted = false;
        var chunks = AgentStreamTranslator.Translate(update, Id, Model, Created, ref roleEmitted).ToList();

        chunks.Should().HaveCount(1);
        chunks[0].Choices![0].FinishReason.Should().Be(expected);
    }

    [Fact]
    public void Translate_完整工具循环_包含所有关键块且role仅首块()
    {
        // 模拟 agent 一次完整流式：开场文本 → 工具调用 → 工具结果 → 收尾文本 → 结束
        var updates = new List<AgentResponseUpdate>
        {
            new() { Contents = new List<AIContent> { new TextContent("我来帮你计算。") } },
            new() { Contents = new List<AIContent> { new FunctionCallContent("call_1", "add_numbers", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }) } },
            new() { Contents = new List<AIContent> { new FunctionResultContent("call_1", "3") } },
            new() { Contents = new List<AIContent> { new TextContent("结果是 3。") } },
            new() { FinishReason = ChatFinishReason.Stop }
        };

        bool roleEmitted = false;
        var allChunks = new List<OpenAIStreamChunk>();
        foreach (var u in updates)
            allChunks.AddRange(AgentStreamTranslator.Translate(u, Id, Model, Created, ref roleEmitted));

        // 关键断言：工具调用链路完整（这正是原 bug 漏掉的部分）
        var toolCallChunks = allChunks.Where(c => c.Choices![0].Delta!.ToolCalls != null).ToList();
        toolCallChunks.Should().HaveCount(1);
        toolCallChunks[0].Choices[0].Delta!.ToolCalls![0].Function!.Name.Should().Be("add_numbers");

        // 收尾文本存在
        allChunks.Should().Contain(c => c.Choices![0].Delta!.Content == "结果是 3。");

        // assistant 角色只在首块出现
        var roleChunks = allChunks.Where(c => c.Choices![0].Delta!.Role == "assistant").ToList();
        roleChunks.Should().HaveCount(1);
        roleChunks[0].Choices[0].Delta!.Content.Should().Be("我来帮你计算。");

        // 结束块存在
        allChunks.Should().Contain(c => c.Choices![0].FinishReason == "stop");
    }

    [Fact]
    public void Translate_roleEmitted_跨update仅一次()
    {
        // 第一个 update 已发出 role，后续 update 不应再带 role
        bool roleEmitted = false;
        var first = AgentStreamTranslator.Translate(
            new AgentResponseUpdate { Contents = new List<AIContent> { new TextContent("a") } },
            Id, Model, Created, ref roleEmitted).ToList();
        roleEmitted.Should().BeTrue();
        first[0].Choices![0].Delta!.Role.Should().Be("assistant");

        var second = AgentStreamTranslator.Translate(
            new AgentResponseUpdate { Contents = new List<AIContent> { new TextContent("b") } },
            Id, Model, Created, ref roleEmitted).ToList();
        second[0].Choices![0].Delta!.Role.Should().BeNull(); // 不再重复 role
        second[0].Choices[0].Delta!.Content.Should().Be("b");
    }
}

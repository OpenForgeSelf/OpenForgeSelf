using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenForgeSelf.Backend.Controllers.UnifiedAI;
using Xunit;
using Xunit.Abstractions;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 端到端验证 Agent Framework 能否「只用接口、由框架自己跑完工具循环」：
/// 直接用真实配置的 LLM 端点构造 AIAgent + 服务端演示工具（add_numbers / get_current_datetime），
/// 让模型自行决策调用工具并汇总，断言流式更新里确实出现了工具调用（FunctionCallContent）与最终文本。
///
/// 默认跳过（避免无密钥/本地 LLM 未启动导致常规测试失败）。设置环境变量 RUN_AGENT_LLM_TESTS=1 才执行。
/// 这是用户要求的「新增测试接口测试一下，特别是工具调用功能」的真实链路证明。
/// </summary>
public class AgentFrameworkToolCallIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public AgentFrameworkToolCallIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? GetEnv(string key) => Environment.GetEnvironmentVariable(key);

    [Fact]
    public async Task Agent_真实LLM_应触发工具调用并返回最终答案()
    {
        // 默认跳过：需显式开启且配置真实端点（避免无密钥/本地 LLM 未启动时误失败）
        if (GetEnv("RUN_AGENT_LLM_TESTS") != "1")
        {
            _output.WriteLine("SKIP：未设置 RUN_AGENT_LLM_TESTS=1，跳过真实 LLM 工具调用集成测试。");
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var endpoint = config["AI:ApiEndpoint"] ?? "http://localhost:1234/v1/chat/completions";
        var apiKey = config["AI:ApiKey"] ?? string.Empty;
        var model = config["AI:ModelName"] ?? "gpt-4";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _output.WriteLine("SKIP：appsettings.json 未配置 AI:ApiKey，跳过真实 LLM 工具调用集成测试。");
            return;
        }

        // 派生 OpenAI SDK 所需 base（去掉末尾 /chat/completions，确保含 /v1）
        var baseUrl = endpoint.Trim();
        if (baseUrl.EndsWith("/chat/completions", System.StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^"/chat/completions".Length];
        baseUrl = baseUrl.TrimEnd('/');
        if (!baseUrl.EndsWith("/v1", System.StringComparison.OrdinalIgnoreCase))
            baseUrl += "/v1";

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

        // 服务端工具：证明 agent 能完成「模型决策 → 执行工具 → 汇总」
        var demoTools = new List<AITool>
        {
            AIFunctionFactory.Create((Func<string>)AgentDemoTools.GetCurrentDateTime, "get_current_datetime"),
            AIFunctionFactory.Create((Func<int, int, int>)AgentDemoTools.AddNumbers, "add_numbers")
        };

        var agent = chatClient.AsAIAgent(
            name: "ForgeAgent",
            instructions: "你是一个助手，必须尽可能使用提供的工具来回答用户的计算与日期问题。",
            tools: demoTools);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "请调用 add_numbers 工具计算 123 加 456 等于多少？")
        };

        var toolCallsObserved = new List<FunctionCallContent>();
        var finalTexts = new List<string>();

        await foreach (var update in agent.RunStreamingAsync(messages))
        {
            if (update.Contents != null)
            {
                foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                    toolCallsObserved.Add(fc);
            }
            if (!string.IsNullOrEmpty(update.Text))
                finalTexts.Add(update.Text!);
        }

        _output.WriteLine($"观察到的工具调用数：{toolCallsObserved.Count}");
        foreach (var fc in toolCallsObserved)
            _output.WriteLine($"  -> 工具：{fc.Name} 参数：{fc.Arguments?.Count ?? 0} 项");

        toolCallsObserved.Should().NotBeEmpty("Agent Framework 应驱动模型调用了演示工具（add_numbers）");
        toolCallsObserved.Should().Contain(fc => fc.Name == "add_numbers", "应调用 add_numbers 完成计算");
        finalTexts.Should().NotBeEmpty("工具执行后 agent 应汇总出最终文本答案");

        var joined = string.Join("", finalTexts);
        _output.WriteLine($"最终答案文本：{joined}");
        joined.Should().Contain("579", "add_numbers(123,456) 的结果应为 579");
    }
}

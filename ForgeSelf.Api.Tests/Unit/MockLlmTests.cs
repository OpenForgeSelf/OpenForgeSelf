using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using ForgeSelf.Api.Tests;

namespace ForgeSelf.Api.Tests.Unit;

public class MockLlmTests
{
    private static MockLlmDocument Doc(params MockLlmRule[] rules) => new()
    {
        Found = true,
        Fallback = "未命中",
        Rules = rules
    };

    [Fact]
    public void Match_最长关键字优先_相同长度保留先出现的()
    {
        var doc = Doc(
            new MockLlmRule("ping", "短"),
            new MockLlmRule("ping-plugins", "长"),
            new MockLlmRule("ping", "后写的短"));

        MockLlmRules.Match(doc, "请 ping-plugins 一下").Should().Be("长");
        MockLlmRules.Match(doc, "只说 ping").Should().Be("短");
        MockLlmRules.Match(doc, "没有关键字").Should().Be("未命中");
    }

    [Fact]
    public void Load_按文件返回规则_缺文件与坏JSON有明确错误()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mock-llm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "mock-llm.json");
            File.WriteAllText(path, """
                {
                  "fallback": "兜底",
                  "rules": [
                    { "key": "", "content": "空key丢掉" },
                    { "key": "列出插件", "content": "没有插件" }
                  ]
                }
                """);

            var doc = MockLlmRules.Load(path);
            doc.Found.Should().BeTrue();
            doc.Invalid.Should().BeFalse();
            doc.Rules.Should().ContainSingle().Which.Key.Should().Be("列出插件");
            MockLlmRules.Match(doc, "帮我列出插件").Should().Be("没有插件");

            var missing = MockLlmRules.Load(Path.Combine(dir, "nope.json"));
            missing.Found.Should().BeFalse();
            missing.Error.Should().Contain("未找到 mock 规则文件");

            File.WriteAllText(path, "{");
            var broken = MockLlmRules.Load(path);
            broken.Invalid.Should().BeTrue();
            broken.Error.Should().Contain("无法解析");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Normalize_空端点补mock本地地址_空模型补mock()
    {
        var config = new AIProviderConfig { Name = "mock", ProviderType = AIProviderType.Mock };
        MockLlmRules.Normalize(config);
        config.Endpoint.Should().Be("mock://local");
        config.SupportedModels.Should().Equal("mock");

        var openai = new AIProviderConfig { ProviderType = AIProviderType.OpenAI, Endpoint = "" };
        MockLlmRules.Normalize(openai);
        openai.Endpoint.Should().BeEmpty();
    }

    [Fact]
    public async Task Chat_命中key返回content_流式同一段正文()
    {
        var provider = new MockLlmProvider(
            new AIProviderConfig { Name = "mock", ProviderType = AIProviderType.Mock, IsDefault = true },
            Doc(new MockLlmRule("ping", "pong")));

        var request = new UnifiedChatRequest
        {
            Model = "mock",
            Messages = new List<UnifiedChatMessage> { new() { Role = "user", Content = "请 ping" } }
        };

        var reply = await provider.ChatAsync(request);
        reply.Choices.Single().Content.Should().Be("pong");
        reply.FinishReason.Should().Be("stop");
        reply.Usage!.TotalTokens.Should().Be(reply.Usage.PromptTokens + reply.Usage.CompletionTokens);

        var chunks = new List<UnifiedStreamChunk>();
        await foreach (var chunk in provider.ChatStreamAsync(request))
            chunks.Add(chunk);

        chunks[0].DeltaContent.Should().Be("pong");
        chunks[0].FinishReason.Should().Be("stop");
        chunks[^1].IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Reload_Mock无端点也注册_且为默认提供方()
    {
        var registry = new AIProviderRegistry();
        await registry.ReloadAsync(new TestHttpClientFactory(), new[]
        {
            new AIProviderConfig
            {
                Name = "mock",
                ProviderType = AIProviderType.Mock,
                Endpoint = "",
                IsDefault = true
            }
        });

        var provider = registry.GetProviderByName("mock");
        provider.Should().BeOfType<MockLlmProvider>();
        provider!.IsDefault.Should().BeTrue();
        provider.ProviderType.Should().Be(AIProviderType.Mock);
        registry.GetDefaultProvider().Should().BeSameAs(provider);
        provider.SupportedModels.Should().Contain("mock");
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// 任务 0（QA 定性的中等严重度旁路）门禁：IM 网关经 <see cref="IChatCompletion"/> 的写路径必须走会话日志。
/// </summary>
/// <remarks>
/// 覆盖两类断言：
/// <list type="number">
/// <item><b>运行时不变量</b>：模型看到的每一条消息都能从日志重建（model ⊆ log）；
/// 日志里的坏数据必被模型看见（log → model，请求是日志的纯函数）。</item>
/// <item><b>源码级旁路门禁</b>：所有 <see cref="IChatCompletion"/> 实现里不得再出现
/// 直写投影表的调用（<c>SaveMessageAsync</c>）或读表构造模型输入（<c>GetHistoryAsync</c>）——
/// 旧实现正是靠这两处绕过日志，且会被投影的「前缀对齐」按「日志为空」删光（数据丢失路径）。</item>
/// </list>
/// </remarks>
[Collection("XCode")]
public class ChatCompletionWritePathTests
{
    // ---- 不变量 1（model ⊆ log）+ 不变量 2（log → model） ----
    [Fact]
    public async Task CompleteAsync_ModelInputComesFromLog_AndAssistantReplyIsLogged()
    {
        var fixture = new CompletionFixture();

        // 不变量 2：绕过一切业务入口，直接往日志塞一条「坏数据」
        fixture.Store.Append(fixture.SessionId, new UserMessageEvent(
            0, fixture.SessionId, DateTimeOffset.Now, "INJECTED-BAD-DATA", MessageSource.System));

        var result = await fixture.Completion.CompleteAsync(new ChatCompletionRequest
        {
            SessionId = fixture.SessionId,
            Message = "来自 IM 网关的用户消息",
            ChatModelId = "fake:fake-model"
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal("IM 网关回复", result.Content);

        var events = fixture.Store.Replay(fixture.SessionId);

        // 写路径：用户消息与助手回复都落在日志里（不再直写投影表）
        var userEvents = events.OfType<UserMessageEvent>().ToList();
        Assert.Contains(userEvents, e => e.Content == "来自 IM 网关的用户消息");
        Assert.Contains(userEvents, e => e.Content == "INJECTED-BAD-DATA");
        Assert.Contains(events.OfType<AssistantMessageEvent>(), e => e.Content == "IM 网关回复");
        // turn/step 结构事件同样落日志（B5 状态机）
        Assert.Contains(events, e => e.Type == "turn/start");
        Assert.Contains(events, e => e.Type == "turn/end");

        // 不变量 1：模型看到的每一条消息都必须能从日志重建
        var logContents = events
            .Select(e => e switch
            {
                UserMessageEvent u => u.Content,
                AssistantMessageEvent a => a.Content,
                ToolResultEvent r => r.ResultJson,
                _ => null
            })
            .Where(c => c != null)
            .ToList();

        Assert.NotEmpty(fixture.Provider.Requests);
        foreach (var request in fixture.Provider.Requests)
        {
            foreach (var message in request.Messages)
            {
                Assert.Contains(message.Content, logContents);
            }
        }

        // 不变量 2：日志里的坏数据必须被模型看见（请求是日志的纯函数）
        Assert.Contains(fixture.Provider.Requests[0].Messages, m => m.Content == "INJECTED-BAD-DATA");
    }

    // ---- PC-013 复发守卫（2026-10-30）：IM 网关不带模型时禁止把陈旧 AiConfig.ModelName 直发上游 ----
    // 实证（:51888 实例 2026-10-30 21:16/21:17）：企微通道 BoundChatModelId=null → ResolveModelId(null)
    // 回退 appsettings 残留 "qwythos-9b-v2"（不在启用模型目录）→ 上游 gpustack 404 Not Found。
    // 守卫语义 = PC-013 有序解析链在本入口的类级断言：目录内解析 / 目录空明确失败（零上游请求）。
    [Fact]
    public async Task CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName()
    {
        var fixture = new CompletionFixture(
            configuredModelName: "qwythos-9b-v2",
            catalog: new[]
            {
                new ModelIdentityDto
                {
                    ChatModelId = "fake:qwen3_8_vision",
                    ProviderName = "fake",
                    UpstreamModelId = "qwen3_8_vision"
                },
            });

        var result = await fixture.Completion.CompleteAsync(new ChatCompletionRequest
        {
            SessionId = fixture.SessionId,
            Message = "你好"
            // ChatModelId 不传 = 企微通道未绑定模型的现网形态
        });

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(fixture.Provider.Requests);
        Assert.Equal("qwen3_8_vision", fixture.Provider.Requests[0].Model);
        Assert.DoesNotContain(fixture.Provider.Requests, r => r.Model == "qwythos-9b-v2");
    }

    [Fact]
    public async Task CompleteAsync_NoModelBound_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall()
    {
        var fixture = new CompletionFixture(
            configuredModelName: "qwythos-9b-v2",
            catalog: Array.Empty<ModelIdentityDto>());

        var result = await fixture.Completion.CompleteAsync(new ChatCompletionRequest
        {
            SessionId = fixture.SessionId,
            Message = "你好"
        });

        Assert.False(result.Success);
        Assert.Contains("模型", result.Error);
        Assert.Empty(fixture.Provider.Requests);
    }

    // ---- 旁路门禁：所有 IChatCompletion 实现都不得直写投影表 / 读表构造输入 ----
    [Fact]
    public void Source_NoDirectTableWrites_InChatCompletionImplementations()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var productionDirs = new[]
        {
            Path.Combine(repoRoot, "ForgeSelf.Abstractions"),
            Path.Combine(repoRoot, "ForgeSelf.Core"),
            Path.Combine(repoRoot, "ForgeSelf.Api"),
            Path.Combine(repoRoot, "Plugins"),
        };

        var implementors = new List<string>();
        var hits = new List<string>();

        foreach (var dir in productionDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                if (!lines.Any(l => l.Contains(": IChatCompletion", StringComparison.Ordinal)))
                {
                    continue;
                }

                implementors.Add(Path.GetRelativePath(repoRoot, file));

                for (var i = 0; i < lines.Length; i++)
                {
                    // 注释行不算命中（说明「为什么不能用 SaveMessage」的文档本身会提到该名字）
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (lines[i].Contains("SaveMessage", StringComparison.Ordinal)
                        || lines[i].Contains("GetHistoryAsync", StringComparison.Ordinal))
                    {
                        hits.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        Assert.NotEmpty(implementors); // 门禁必须真的扫到实现，扫不到等于门禁失效
        Assert.True(hits.Count == 0,
            "IChatCompletion 实现仍存在直写投影表 / 读表构造模型输入的调用（写路径唯一走 ISessionStore）: "
            + string.Join(" | ", hits));
    }
}

/// <summary>
/// IM 补全测试脚手架：自建 DI（宿主契约用 Mock&lt;IContext&gt; 提供）+ 脚本化 provider，
/// 不启动 Web 主机即可断言 IM 路径的写路径与两条不变量。
/// </summary>
internal sealed class CompletionFixture
{
    public string SessionId { get; } = "im-" + Guid.NewGuid().ToString("N")[..8];

    public InMemorySessionStore Store { get; } = new();

    public ScriptedProvider Provider { get; } = new();

    public IChatCompletion Completion { get; }

    public CompletionFixture(string? configuredModelName = null, IReadOnlyList<ModelIdentityDto>? catalog = null)
    {
        Provider.Enqueue(Script.Text("IM 网关回复", finish: "stop"), repeatLast: true);

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<ISessionStore>()).Returns(Store);
        ctx.Setup(c => c.Get<IAIProviderRegistry>()).Returns(new FakeProviderRegistry(Provider));
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns(new FakeToolRegistry());
        ctx.Setup(c => c.Events).Returns(new EventBus());

        // PC-013 复现/守卫依赖：宿主 AI 配置（陈旧默认模型名）+ 启用模型目录（ChatModelResolver 数据源）
        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.GetAIConfig()).Returns(new AIConfig { ModelName = configuredModelName ?? string.Empty });
        ctx.Setup(c => c.Get<IConfigurationService>()).Returns(config.Object);

        var telemetry = new Mock<ITurnTelemetryQuery>();
        telemetry.Setup(t => t.GetModelCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((catalog ?? Array.Empty<ModelIdentityDto>()).ToList());
        ctx.Setup(c => c.Get<ITurnTelemetryQuery>()).Returns(telemetry.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IContext>(ctx.Object);
        services.AddScoped<IAIAgentService, AIAgentService>();
        services.AddScoped<AIAgentProjectionService>();
        // 与生产装配一致（AIAgentPlugin.AddServices）：解析器经 DI 注入 AIAgentService
        services.AddScoped<IChatModelResolver, ChatModelResolver>();

        Completion = new AIAgentChatCompletion(services.BuildServiceProvider());
    }
}

/// <summary>固定返回同一个 provider 的注册表替身。</summary>
internal sealed class FakeProviderRegistry : IAIProviderRegistry
{
    private readonly IAIProvider _provider;

    public FakeProviderRegistry(IAIProvider provider)
    {
        _provider = provider;
    }

    public IAIProvider? GetProviderByChatModelId(string chatModelId) => _provider;

    public IAIProvider? GetProviderByModel(string modelName) => _provider;

    public IAIProvider? GetDefaultProvider() => _provider;
}

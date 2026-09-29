using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using XCode;
using XCode.DataAccessLayer;
using AIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// B4（040）不变量判据测试：040 主线「一切进模型的东西必须先落日志，再从日志派生」。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>不变量 1（model ⊆ log）：模型看到的每一条消息都必须能从会话日志重建 ——
///       通过直插一条「表里有、日志没有」的孤儿 <c>ChatMessage</c> 行制造违例场景。
///       改序前模型输入来自 ChatMessage 表 → 孤儿消息进入模型但日志没有 → <b>红</b>；
///       改序后模型输入只从 <c>DeriveMessages</c> 派生 → <b>绿</b>。</item>
/// <item>不变量 2（log → model）：往日志里塞一条坏数据，模型必须看到（请求是日志的纯函数）。
///       改序前模型输入来自 ChatMessage 表 → 坏数据被无视 → <b>红</b>；改序后 <b>绿</b>。</item>
/// </list>
/// 隔离手法与 <c>ChatControllerIntegrationTests</code> 一致：临时库 + 连接串交换 + Testing 环境。
/// </remarks>
[Collection("XCode")]
public class ChatControllerInvariantTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SpyAIService _spy;
    private readonly ISessionStore _store;
    private readonly string _sessionId;

    public ChatControllerInvariantTests()
    {
        var tempDbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfInv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDbDir);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 换成可观测的 SpyAIService：记录每次 ChatAsync 实际收到的消息（= 模型真实看到的输入）
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<SpyAIService>();
                services.AddSingleton<IAIService>(sp => sp.GetRequiredService<SpyAIService>());
            });

            builder.UseEnvironment("Testing");
        });

        _client = _factory.CreateClient();

        // 隔离 XCode 的 ForgeSelf 连接到临时库（先触发宿主构建，再覆盖连接串）
        var _ = _factory.Services;
        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(tempDbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        _spy = _factory.Services.GetRequiredService<SpyAIService>();
        _store = _factory.Services.GetRequiredService<ISessionStore>();
        _sessionId = "inv-" + Guid.NewGuid().ToString("N")[..12];
    }

    // ---- 不变量 1：任何进入模型的消息都能从日志重建 ----
    [Fact]
    public async Task Invariant1_EveryModelVisibleMessage_IsRebuildableFromLog()
    {
        // Arrange：制造「表里有、日志没有」的孤儿消息（旧双写路径的典型残留形态）
        new ChatMessage
        {
            SessionId = _sessionId,
            Role = "user",
            Content = "ORPHAN-ONLY-IN-TABLE",
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        }.Insert();

        // Act：走一轮正常对话
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "第一轮正常消息",
            SessionId = _sessionId,
        });
        response.EnsureSuccessStatusCode();

        // Assert：模型看到的每一条消息都必须在日志里有对应事件
        var seen = _spy.Captured.SelectMany(call => call).ToList();
        Assert.NotEmpty(seen);
        foreach (var message in seen)
        {
            Assert.True(IsRebuildableFromLog(message),
                $"模型看到了日志里不存在的消息（Model-visible means logged 被破坏）: [{message.Role}] {message.Content}");
        }
    }

    // ---- 不变量 2：坏数据必被模型看见（请求是日志的纯函数） ----
    [Fact]
    public async Task Invariant2_BadDataInLog_MustBeSeenByModel()
    {
        // Arrange：绕过一切业务入口，直接往日志塞一条「坏数据」
        _store.Append(_sessionId, new UserMessageEvent(
            0, _sessionId, DateTimeOffset.Now, "INJECTED-BAD-DATA", MessageSource.System));

        // Act：再走一轮正常对话
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
        {
            Message = "第二轮正常消息",
            SessionId = _sessionId,
        });
        response.EnsureSuccessStatusCode();

        // Assert：下一轮 prompt 必须包含坏数据（否则日志不是真相源）
        Assert.Contains(_spy.Captured, call => call.Any(m => m.Content == "INJECTED-BAD-DATA"));
    }

    /// <summary>某条模型可见消息能否从会话日志重建（Role + Content 全等）。</summary>
    private bool IsRebuildableFromLog(AIChatMessage message)
    {
        var events = _store.Replay(_sessionId);
        return message.Role switch
        {
            "user" => events.OfType<UserMessageEvent>().Any(e => e.Content == message.Content),
            "assistant" => events.OfType<AssistantMessageEvent>().Any(e => e.Content == message.Content),
            "system" => events.OfType<SystemMessageEvent>().Any(e => e.Content == message.Content),
            "tool" => events.OfType<ToolResultEvent>().Any(e => e.ResultJson == message.Content),
            _ => false,
        };
    }

    // ---- 门禁 3：控制器不再直接写 ChatMessage 主表（源码级 grep 断言） ----
    [Fact]
    public void Source_NoDirectChatMessageWrites_InControllers()
    {
        // bin/Debug/net10.0-windows → 上溯 4 级到仓库根
        var repoRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");

        foreach (var relative in new[]
        {
            Path.Combine("ForgeSelf.Api", "Controllers", "ChatController.cs"),
            Path.Combine("Plugins", "AIAgent", "Controllers", "AIChatController.cs"),
            // B5（041）扩展：跨插件 IChatCompletion 实现（IM 网关主路径）也要纳入门禁 ——
            // 它曾靠 IPluginMessageService.SaveMessageAsync 直写投影表绕过日志（QA 定性的旁路）。
            Path.Combine("Plugins", "AIAgent", "Services", "AIAgentChatCompletion.cs"),
        })
        {
            var path = Path.GetFullPath(Path.Combine(repoRoot, relative));
            Assert.True(File.Exists(path), $"找不到源码文件: {relative}");

            // 旧双写入口：SaveMessageSafeAsync（宿主）/ SaveMessageAsync（插件）。
            // 只读端点（GET history）用 GetHistoryAsync 属于合法读，不在此门禁范围；
            // 「模型输入是否来自日志」由运行期不变量测试（Invariant1/2）与
            // ChatCompletionWritePathTests 的 IM 路径不变量测试负责，不靠 grep 近似。
            var hits = File.ReadAllLines(path)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))  // 注释行不算引用
                .Where(line => line.Contains("SaveMessage", StringComparison.Ordinal))
                .ToList();

            Assert.True(hits.Count == 0,
                $"{relative} 仍存在直接写消息表的调用（B4 后 ChatMessage/AIChatMessage 只读，写路径唯一走 ISessionStore）: {string.Join(" | ", hits)}");
        }
    }

    // ---- 门禁 8：多轮累积 —— 第 3 轮进入模型的消息含前 2 轮的 user/assistant ----
    [Fact]
    public async Task Multiturn_History_Accumulates()
    {
        for (var round = 1; round <= 3; round++)
        {
            var response = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
            {
                Message = $"第{round}轮消息",
                SessionId = _sessionId,
            });
            response.EnsureSuccessStatusCode();
        }

        // 第 3 轮（最后一次调用）的模型输入 = 前 2 轮的 user/assistant 各 2 条 + 本轮 user
        var thirdTurn = _spy.Captured[^1];
        Assert.Equal(
            new[] { "第1轮消息", "模拟响应: 收到 1 条消息", "第2轮消息", "模拟响应: 收到 3 条消息", "第3轮消息" },
            thirdTurn.Select(m => m.Content).ToArray());
        Assert.Equal(5, thirdTurn.Count);
    }
}

/// <summary>可观测 AI 服务替身：记录每次进入模型的消息序列。</summary>
internal sealed class SpyAIService : IAIService
{
    /// <summary>每次 ChatAsync 收到的完整消息列表（按调用顺序）。</summary>
    public List<List<AIChatMessage>> Captured { get; } = new();

    public Task<string> ChatAsync(List<AIChatMessage> messages)
    {
        Captured.Add(new List<AIChatMessage>(messages));
        return Task.FromResult($"模拟响应: 收到 {messages.Count} 条消息");
    }

    public Task<string> ChatAsync(List<AIChatMessage> messages, string? chatModelId)
        => ChatAsync(messages);

    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<AIChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Captured.Add(new List<AIChatMessage>(messages));
        await Task.Delay(1, cancellationToken);
        yield return "模拟";
        yield return "流式";
        yield return "响应";
    }

    public IAsyncEnumerable<string> ChatStreamAsync(
        List<AIChatMessage> messages, string? chatModelId, CancellationToken cancellationToken = default)
        => ChatStreamAsync(messages, cancellationToken);
}

using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 聊天会话服务单测（列表过滤/分页/排序/日期范围/会话详情）。
/// 由原 ChatRecordServiceTests 的 GetRecordsAsync 列表类用例迁移而来
/// （ChatRecord→ChatTurn/ChatSession 会话化重构：记录列表查询能力收敛到会话维度，
/// 旧的「按 SessionId 过滤记录」等价于新的「按 key 过滤会话」）。
/// </summary>
[Collection("XCode")]
public class ChatSessionServiceTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatSessionService _chatSessionService;
    private readonly ChatTurnService _chatTurnService;

    public ChatSessionServiceTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatSessionService = new ChatSessionService(_mockLogService.Object);
        _chatTurnService = new ChatTurnService(_mockLogService.Object);
    }

    private Task<ChatSession> UpsertAsync(string key, string style = "OpenAI_Chat", string? firstUserMsg = null, int messageCount = 1)
        => _chatSessionService.UpsertSessionAsync(key, SessionSource.Proxy, "gpt-4", ClientKind.Other, style, firstUserMsg, messageCount);

    [Fact]
    public async Task GetSessionsAsync_WithKeyFilter_ShouldReturnFilteredSessions()
    {
        // Arrange：同一 key 连续 3 轮（原用例为同 SessionId 存 3 条记录），另一个 key 存 1 轮
        var key1 = "filter-s1-" + Guid.NewGuid().ToString("N")[..16];
        var key2 = "filter-s2-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 3; i++)
        {
            await UpsertAsync(key1, messageCount: i + 1);
        }
        await UpsertAsync(key2, style: "Anthropic_Messages", messageCount: 2);

        // Act
        var (sessions, total) = await _chatSessionService.GetSessionsAsync(null, null, null, null, null, key1, 1, 20);

        // Assert：key1 只命中一个会话（3 轮合并），且轮次计数为 3
        total.Should().Be(1);
        sessions.Should().ContainSingle();
        sessions.Should().AllSatisfy(s => s.SessionKey.Should().Be(key1));
        sessions[0].RequestCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSessionsAsync_WithStyleFilter_ShouldReturnFilteredSessions()
    {
        // Arrange
        var prefix = "style-filter-" + Guid.NewGuid().ToString("N")[..8] + "-";
        var openaiKey = prefix + "1";
        var anthropicKey = prefix + "2";

        await UpsertAsync(openaiKey, style: "OpenAI_Chat");
        await UpsertAsync(anthropicKey, style: "Anthropic_Messages");

        // Act
        var (sessions, total) = await _chatSessionService.GetSessionsAsync(null, null, "Anthropic_Messages", null, null, prefix, 1, 20);

        // Assert
        total.Should().BeGreaterThanOrEqualTo(1);
        sessions.Select(s => s.SessionKey).Should().Contain(anthropicKey);
        sessions.Select(s => s.SessionKey).Should().NotContain(openaiKey);
        sessions.Where(s => s.SessionKey.StartsWith(prefix)).Should().AllSatisfy(s => s.Style.Should().Be("Anthropic_Messages"));
    }

    [Fact]
    public async Task GetSessionsAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange：10 个会话（原用例为同 SessionId 的 10 条记录）
        var prefix = "pagination-" + Guid.NewGuid().ToString("N")[..12] + "-";

        for (int i = 0; i < 10; i++)
        {
            await UpsertAsync(prefix + i.ToString("00"));
        }

        // Act - Page 1
        var (page1, total) = await _chatSessionService.GetSessionsAsync(null, null, null, null, null, prefix, 1, 3);

        // Assert
        total.Should().Be(10);
        page1.Should().HaveCount(3);

        // Act - Page 2
        var (page2, _) = await _chatSessionService.GetSessionsAsync(null, null, null, null, null, prefix, 2, 3);

        // Assert
        page2.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetSessionsAsync_ShouldReturnSessionsInDescendingOrder()
    {
        // Arrange：按创建顺序 A→B→C，UpdatedTime 递增，期望返回 C→B→A
        var prefix = "order-" + Guid.NewGuid().ToString("N")[..12] + "-";

        await UpsertAsync(prefix + "a");
        await UpsertAsync(prefix + "b");
        await UpsertAsync(prefix + "c");

        // Act
        var (sessions, _) = await _chatSessionService.GetSessionsAsync(null, null, null, null, null, prefix, 1, 10);

        // Assert
        sessions.Should().HaveCount(3);
        sessions.Should().BeInDescendingOrder(s => s.UpdatedTime);
    }

    [Fact]
    public async Task GetSessionsAsync_WithDateRange_ShouldFilterCorrectly()
    {
        // Arrange：刚创建的会话 CreatedTime ≈ 当前时间
        var prefix = "date-range-" + Guid.NewGuid().ToString("N")[..12] + "-";
        await UpsertAsync(prefix + "1");
        await UpsertAsync(prefix + "2");

        // Act - 覆盖"现在"的区间
        var from = DateTime.Now.AddDays(-1);
        var to = DateTime.Now.AddHours(1);
        var (sessions, _) = await _chatSessionService.GetSessionsAsync(null, null, null, from, to, prefix, 1, 10);

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Should().AllSatisfy(s =>
        {
            s.CreatedTime.Should().BeOnOrAfter(from);
            s.CreatedTime.Should().BeOnOrBefore(to);
        });

        // Act - 未来区间应查不到本批会话
        var (futureSessions, futureTotal) = await _chatSessionService.GetSessionsAsync(
            null, null, null, DateTime.Now.AddDays(1), DateTime.Now.AddDays(2), prefix, 1, 10);

        // Assert
        futureTotal.Should().Be(0);
        futureSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSessionsAsync_EmptyFilters_ShouldReturnAll()
    {
        // Arrange：无过滤时靠分页能力返回（共享库中可能有其它会话，用 key 收敛断言范围）
        var prefix = "nofilter-" + Guid.NewGuid().ToString("N")[..12] + "-";

        for (int i = 0; i < 5; i++)
        {
            await UpsertAsync(prefix + i.ToString("00"));
        }

        // Act
        var (sessions, total) = await _chatSessionService.GetSessionsAsync(null, null, null, null, null, prefix, 1, 100);

        // Assert
        total.Should().Be(5);
        sessions.Should().HaveCount(5);
    }

    [Fact]
    public async Task UpsertSessionAsync_SameKey_ShouldIncrementRequestCount()
    {
        // Arrange
        var key = "upsert-same-" + Guid.NewGuid().ToString("N")[..16];

        // Act
        var first = await _chatSessionService.UpsertSessionAsync(key, SessionSource.Proxy, "gpt-4", ClientKind.Other, "OpenAI_Chat", "第一条用户消息", 1);
        var second = await _chatSessionService.UpsertSessionAsync(key, SessionSource.Proxy, "gpt-4", ClientKind.Other, "OpenAI_Chat", "第二条用户消息", 2);

        // Assert：幂等 upsert（同 Id，轮次 +1；Title/FirstUserMsg 仅首轮写入）
        first.Id.Should().BeGreaterThan(0);
        second.Id.Should().Be(first.Id);
        second.RequestCount.Should().Be(2);
        second.Title.Should().Be("第一条用户消息");
        second.FirstUserMsg.Should().Be("第一条用户消息");
        second.Source.Should().Be("Proxy");
        second.ClientKind.Should().Be("Other");
    }

    [Fact]
    public async Task GetSessionAsync_ExistingId_ShouldReturnSessionAndTurns()
    {
        // Arrange：一个会话挂两轮（乱序插入，验证按 TurnIndex 升序返回）
        var key = "detail-" + Guid.NewGuid().ToString("N")[..16];
        var session = await UpsertAsync(key);
        session.Id.Should().BeGreaterThan(0);

        await _chatTurnService.SaveTurnAsync(new ChatTurn
        {
            ChatSessionId = session.Id,
            TurnIndex = 2,
            SessionKey = key,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            CreatedTime = DateTime.Now
        });
        await _chatTurnService.SaveTurnAsync(new ChatTurn
        {
            ChatSessionId = session.Id,
            TurnIndex = 1,
            SessionKey = key,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            CreatedTime = DateTime.Now
        });

        // Act
        var (found, turns) = await _chatSessionService.GetSessionAsync(session.Id);

        // Assert
        found.Should().NotBeNull();
        found!.SessionKey.Should().Be(key);
        turns.Should().HaveCount(2);
        turns.Should().BeInAscendingOrder(t => t.TurnIndex);
    }

    [Fact]
    public async Task GetSessionAsync_NonExistingId_ShouldReturnNullAndEmptyTurns()
    {
        // Act
        var (session, turns) = await _chatSessionService.GetSessionAsync(999999999);

        // Assert
        session.Should().BeNull();
        turns.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordTurnStatsAsync_ShouldAccumulateTokensAndLastStatus()
    {
        // Arrange
        var key = "stats-" + Guid.NewGuid().ToString("N")[..16];
        var session = await UpsertAsync(key);

        // Act
        await _chatSessionService.RecordTurnStatsAsync(session.Id, 200, 10, 5);
        await _chatSessionService.RecordTurnStatsAsync(session.Id, 200, 7, 3);

        // Assert
        var reloaded = ChatSession.FindById(session.Id);
        reloaded.Should().NotBeNull();
        reloaded!.LastStatus.Should().Be(200);
        reloaded.TotalPromptTokens.Should().Be(17);
        reloaded.TotalCompletionTokens.Should().Be(8);
    }
}

using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 聊天轮次服务单测（保存/取单条类）。
/// 由原 ChatRecordServiceTests 的「保存/按 Id 取单条」用例迁移而来
/// （ChatRecord→ChatTurn 会话化重构：SessionId→SessionKey，新增 ChatSessionId/TurnIndex 等列）。
/// 列表/分页/过滤类用例已迁移至 ChatSessionServiceTests。
/// </summary>
[Collection("XCode")]
public class ChatTurnServiceTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatTurnService _chatTurnService;

    public ChatTurnServiceTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatTurnService = new ChatTurnService(_mockLogService.Object);
    }

    [Fact]
    public async Task SaveTurnAsync_ShouldSaveTurnAndReturnSuccess()
    {
        // Arrange
        var sessionKey = "test-save-" + Guid.NewGuid().ToString("N")[..16];
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestHeaders = "{}",
            RequestBody = "{\"model\":\"gpt-4\",\"messages\":[]}",
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = "{\"choices\":[]}",
            Temperature = 0.7,
            MaxTokens = 1000,
            MessageCount = 3,
            ToolCallCount = 0,
            HasReasoning = false,
            DurationMs = 500,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        turn.Id.Should().BeGreaterThan(0);

        var savedTurn = ChatTurn.FindById(turn.Id);
        savedTurn.Should().NotBeNull();
        savedTurn!.SessionKey.Should().Be(sessionKey);
        savedTurn.Style.Should().Be("OpenAI_Chat");
        savedTurn.Model.Should().Be("gpt-4");
        savedTurn.Temperature.Should().Be(0.7);
        savedTurn.MaxTokens.Should().Be(1000);
        savedTurn.MessageCount.Should().Be(3);
        savedTurn.ToolCallCount.Should().Be(0);
        savedTurn.DurationMs.Should().Be(500);

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnTurn()
    {
        // Arrange
        var sessionKey = "test-getbyid-" + Guid.NewGuid().ToString("N")[..16];
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "OpenAI_Responses",
            Model = "gpt-4o",
            RequestMethod = "POST",
            RequestPath = "/v1/responses",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 1.0,
            MessageCount = 1,
            ToolCallCount = 0,
            CreatedTime = DateTime.Now
        };
        await _chatTurnService.SaveTurnAsync(turn);

        // Act
        var result = await _chatTurnService.GetByIdAsync(turn.Id);

        // Assert
        result.Should().NotBeNull();
        result.SessionKey.Should().Be(sessionKey);
        result.Style.Should().Be("OpenAI_Responses");
        result.Model.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _chatTurnService.GetByIdAsync(999999999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveTurnAsync_WithReasoning_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = "reasoning-" + Guid.NewGuid().ToString("N")[..16];
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "Anthropic_Messages",
            Model = "claude-3-opus",
            RequestMethod = "POST",
            RequestPath = "/v1/anthropic/messages",
            RequestBody = "{\"thinking\":true}",
            ResponseStatus = 200,
            ResponseBody = "{\"thinking\":[{\"type\":\"thinking\"}]}",
            Temperature = 0.7,
            MessageCount = 2,
            ToolCallCount = 0,
            HasReasoning = true,
            DurationMs = 2000,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var savedTurn = ChatTurn.FindById(turn.Id);
        savedTurn.Should().NotBeNull();
        savedTurn!.HasReasoning.Should().BeTrue();
    }

    [Fact]
    public async Task SaveTurnAsync_WithToolCalls_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = "toolcalls-" + Guid.NewGuid().ToString("N")[..16];
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{\"tools\":[]}",
            ResponseStatus = 200,
            ResponseBody = "{\"choices\":[{\"message\":{\"tool_calls\":[{\"id\":\"1\",\"name\":\"test\"}]}}]}",
            Temperature = 0.7,
            MessageCount = 2,
            ToolCallCount = 3,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var savedTurn = ChatTurn.FindById(turn.Id);
        savedTurn.Should().NotBeNull();
        savedTurn!.ToolCallCount.Should().Be(3);
    }

    [Fact]
    public async Task SaveTurnAsync_WithMaxTokensZero_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = "zero-maxtokens-" + Guid.NewGuid().ToString("N")[..16];
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MaxTokens = 0,
            MessageCount = 1,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var savedTurn = ChatTurn.FindById(turn.Id);
        savedTurn.Should().NotBeNull();
        savedTurn!.MaxTokens.Should().Be(0);
    }

    [Fact]
    public async Task SaveTurnAsync_WithLargeRequestBody_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = "large-body-" + Guid.NewGuid().ToString("N")[..16];
        var largeBody = new string('x', 8000);
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = largeBody,
            ResponseStatus = 200,
            ResponseBody = largeBody,
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert（持久化是 best-effort：即便超长被 XCode 拦截也不抛异常，此处仅确认流程走完）
        var savedTurn = ChatTurn.FindById(turn.Id);
        savedTurn.Should().NotBeNull();
    }
}

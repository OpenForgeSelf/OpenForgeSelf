using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 聊天会话视图控制器（ChatRecordsController，路由 api/chat-sessions）集成测试。
/// 会话化重构后控制器只依赖 IChatSessionService（列表 GetSessions / 详情 GetSession），
/// Arrange 全走 mock，不触碰数据库。用例意图沿用原 ChatRecords 版本（列表/详情/参数校验）。
/// </summary>
public class ChatRecordsControllerIntegrationTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IChatSessionService> _mockSessionService;
    private readonly string _uniquePrefix;

    public ChatRecordsControllerIntegrationTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockSessionService = new Mock<IChatSessionService>();
        _uniquePrefix = "ctrl-test-" + Guid.NewGuid().ToString("N")[..8] + "-";
    }

    private ChatRecordsController CreateController()
    {
        return new ChatRecordsController(_mockSessionService.Object, _mockLogService.Object);
    }

    private static ChatSession NewSession(long id, string key, string style = "OpenAI_Chat") => new()
    {
        Id = id,
        SessionKey = key,
        Source = "Proxy",
        ClientKind = "Other",
        Style = style,
        Model = "gpt-4",
        Title = "test-title",
        RequestCount = 1,
        MessageCount = 2,
        CreatedTime = DateTime.Now,
        UpdatedTime = DateTime.Now
    };

    private static object? Prop(object? obj, string name) => obj?.GetType()?.GetProperty(name)?.GetValue(obj);

    [Fact]
    public async Task GetSessions_WithNoFilter_ShouldReturnOk()
    {
        // Arrange
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "get-all";
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 20))
            .ReturnsAsync(([NewSession(1, sessionKey)], 1));

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, null, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        Prop(okResult.Value, "success").Should().Be(true);
        Prop(okResult.Value, "data").Should().NotBeNull();
    }

    [Fact]
    public async Task GetSessions_WithKeyFilter_ShouldReturnFiltered()
    {
        // Arrange：原「按 SessionId 过滤记录」迁移为「按 key 过滤会话」
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "filter-sid";
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, null, null, null, sessionKey, 1, 20))
            .ReturnsAsync(([NewSession(1, sessionKey)], 1));

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, sessionKey, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        _mockSessionService.Verify(s => s.GetSessionsAsync(null, null, null, null, null, sessionKey, 1, 20), Times.Once);
        var data = Prop(okResult.Value, "data") as System.Collections.IEnumerable;
        data.Should().NotBeNull();
        data!.Cast<ChatSession>().Should().OnlyContain(s => s.SessionKey == sessionKey);
    }

    [Fact]
    public async Task GetSessions_WithStyleFilter_ShouldReturnFiltered()
    {
        // Arrange
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "filter-style";
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, "Anthropic_Messages", null, null, null, 1, 20))
            .ReturnsAsync(([NewSession(1, sessionKey, "Anthropic_Messages")], 1));

        // Act
        var result = await controller.GetSessions(null, null, "Anthropic_Messages", null, null, null, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockSessionService.Verify(s => s.GetSessionsAsync(null, null, "Anthropic_Messages", null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetSession_ExistingId_ShouldReturnOk()
    {
        // Arrange：原 GetById 迁移为 GetSession（会话 + 轮次明细）
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "get-by-id";
        var session = NewSession(1, sessionKey);
        var turns = new List<ChatTurn>
        {
            new()
            {
                Id = 10,
                ChatSessionId = 1,
                TurnIndex = 1,
                SessionKey = sessionKey,
                Style = "OpenAI_Chat",
                Model = "gpt-4",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{\"test\": true}",
                ResponseStatus = 200,
                ResponseBody = "{\"ok\": true}",
                Temperature = 0.7,
                MessageCount = 2,
                CreatedTime = DateTime.Now
            }
        };
        _mockSessionService.Setup(s => s.GetSessionAsync(1)).ReturnsAsync((session, turns));

        // Act
        var result = await controller.GetSession(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        Prop(okResult.Value, "success").Should().Be(true);
        Prop(okResult.Value, "data").Should().NotBeNull();
    }

    [Fact]
    public async Task GetSession_NonExistingId_ShouldReturnNotFound()
    {
        // Arrange
        var controller = CreateController();
        _mockSessionService
            .Setup(s => s.GetSessionAsync(999999999))
            .ReturnsAsync(((ChatSession?)null, new List<ChatTurn>()));

        // Act
        var result = await controller.GetSession(999999999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessions_WithPagination_ShouldReturnCorrectPageSize()
    {
        // Arrange
        var controller = CreateController();
        var sessions = new List<ChatSession>
        {
            NewSession(1, _uniquePrefix + "pagination-0"),
            NewSession(2, _uniquePrefix + "pagination-1")
        };
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 2))
            .ReturnsAsync((sessions, 5));

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, null, 1, 2);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var data = Prop(okResult.Value, "data") as System.Collections.IEnumerable;
        data.Should().NotBeNull();
        data!.Cast<ChatSession>().Should().HaveCount(2);
        Prop(okResult.Value, "total").Should().Be(5);
        Prop(okResult.Value, "pageSize").Should().Be(2);
    }

    [Fact]
    public async Task GetSessions_PageBelowOne_ShouldDefaultToPage1()
    {
        // Arrange
        var controller = CreateController();
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 20))
            .ReturnsAsync((new List<ChatSession>(), 0));

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, null, 0, 20);

        // Assert：控制器应把 page<1 归一为 1 再传给服务
        result.Should().BeOfType<OkObjectResult>();
        _mockSessionService.Verify(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetSessions_PageSizeAboveMax_ShouldDefaultTo100()
    {
        // Arrange
        var controller = CreateController();
        _mockSessionService
            .Setup(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 100))
            .ReturnsAsync((new List<ChatSession>(), 0));

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, null, 1, 200);

        // Assert：控制器应把 pageSize>100 归一为 100 再传给服务
        result.Should().BeOfType<OkObjectResult>();
        _mockSessionService.Verify(s => s.GetSessionsAsync(null, null, null, null, null, null, 1, 100), Times.Once);
    }

    [Fact]
    public async Task GetSession_WithValidSession_ShouldContainAllFields()
    {
        // Arrange：会话全字段 + 轮次全字段
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "all-fields";
        var session = new ChatSession
        {
            Id = 2,
            SessionKey = sessionKey,
            Source = "Proxy",
            Title = "hello",
            Model = "gpt-4o",
            Provider = "test-provider",
            Style = "OpenAI_Responses",
            ClientKind = "Other",
            RequestCount = 1,
            MessageCount = 2,
            FirstUserMsg = "hello",
            TotalPromptTokens = 10,
            TotalCompletionTokens = 5,
            LastStatus = 200,
            CreatedTime = DateTime.Now,
            UpdatedTime = DateTime.Now
        };
        var turns = new List<ChatTurn>
        {
            new()
            {
                Id = 20,
                ChatSessionId = 2,
                TurnIndex = 1,
                SessionKey = sessionKey,
                Style = "OpenAI_Responses",
                Model = "gpt-4o",
                RequestMethod = "POST",
                RequestPath = "/v1/responses",
                RequestHeaders = "{\"Content-Type\":\"application/json\"}",
                RequestBody = "{\"input\":\"hello\"}",
                ResponseStatus = 200,
                ResponseHeaders = "{\"X-Custom\":\"value\"}",
                ResponseBody = "{\"output\":\"hi\"}",
                Temperature = 0.8,
                MaxTokens = 500,
                MessageCount = 2,
                ToolCallCount = 1,
                HasReasoning = true,
                DurationMs = 1200,
                CreatedTime = DateTime.Now
            }
        };
        _mockSessionService.Setup(s => s.GetSessionAsync(2)).ReturnsAsync((session, turns));

        // Act
        var result = await controller.GetSession(2);

        // Assert：响应 data 内含 session 与 turns 两块数据
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var data = Prop(okResult.Value, "data");
        data.Should().NotBeNull();
        Prop(data, "session").Should().NotBeNull();
        Prop(data, "turns").Should().NotBeNull();
    }
}

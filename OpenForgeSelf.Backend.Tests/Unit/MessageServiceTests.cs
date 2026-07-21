using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

[Collection("XCode")]
public class MessageServiceTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly MessageService _messageService;

    public MessageServiceTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _messageService = new MessageService(_mockLogService.Object);
    }

    [Fact]
    public async Task SaveMessageAsync_ShouldSaveMessageAndReturnId()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "s1-" + Guid.NewGuid().ToString("N")[..16];
        var role = "user";
        var content = "这是一条测试消息";

        // Act
        var messageId = await service.SaveMessageAsync(sessionId, role, content);

        // Assert
        messageId.Should().BeGreaterThan(0);

        var savedMessage = ChatMessage.FindById(messageId);
        savedMessage.Should().NotBeNull();
        savedMessage!.SessionId.Should().Be(sessionId);
        savedMessage.Role.Should().Be(role);
        savedMessage.Content.Should().Be(content);

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task SaveMessageAsync_ShouldSetCorrectTimestamps()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "s2-" + Guid.NewGuid().ToString("N")[..16];
        var beforeSave = DateTime.Now;

        // Act
        var messageId = await service.SaveMessageAsync(sessionId, "assistant", "AI响应");

        // Assert
        var savedMessage = ChatMessage.FindById(messageId);
        savedMessage.Should().NotBeNull();
        savedMessage!.CreateTime.Should().BeCloseTo(beforeSave, TimeSpan.FromSeconds(5));
        savedMessage.UpdateTime.Should().BeCloseTo(beforeSave, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnMessagesForSession()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "history-" + Guid.NewGuid().ToString("N")[..16];

        await service.SaveMessageAsync(sessionId, "user", "消息1");
        await service.SaveMessageAsync(sessionId, "assistant", "响应1");
        await service.SaveMessageAsync(sessionId, "user", "消息2");

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(3);
        history[0].Role.Should().Be("user");
        history[0].Content.Should().Be("消息1");
        history[1].Role.Should().Be("assistant");
        history[1].Content.Should().Be("响应1");
        history[2].Role.Should().Be("user");
        history[2].Content.Should().Be("消息2");

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnMessagesInCorrectOrder()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "order-" + Guid.NewGuid().ToString("N")[..16];

        await service.SaveMessageAsync(sessionId, "user", "第一条");
        await Task.Delay(100);
        await service.SaveMessageAsync(sessionId, "assistant", "第二条");
        await Task.Delay(100);
        await service.SaveMessageAsync(sessionId, "user", "第三条");

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(3);
        history.Should().BeInAscendingOrder(m => m.CreateTime);
    }

    [Fact]
    public async Task GetHistoryAsync_WithLimit_ShouldReturnLimitedMessages()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "limit-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 10; i++)
        {
            await service.SaveMessageAsync(sessionId, "user", $"消息{i}");
        }

        // Act
        var history = await service.GetHistoryAsync(sessionId, 5);

        // Assert
        history.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetHistoryAsync_ForEmptySession_ShouldReturnEmptyList()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "empty-" + Guid.NewGuid().ToString("N")[..16];

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldNotReturnMessagesFromOtherSessions()
    {
        // Arrange
        var service = _messageService;
        var sessionId1 = "session-1-" + Guid.NewGuid().ToString("N");
        var sessionId2 = "session-2-" + Guid.NewGuid().ToString("N");

        await service.SaveMessageAsync(sessionId1, "user", "会话1消息");
        await service.SaveMessageAsync(sessionId2, "user", "会话2消息");

        // Act
        var history = await service.GetHistoryAsync(sessionId1);

        // Assert
        history.Should().HaveCount(1);
        history[0].Content.Should().Be("会话1消息");
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldDeleteAllMessagesForSession()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "del-" + Guid.NewGuid().ToString("N")[..16];

        await service.SaveMessageAsync(sessionId, "user", "消息1");
        await service.SaveMessageAsync(sessionId, "assistant", "响应1");
        await service.SaveMessageAsync(sessionId, "user", "消息2");

        // Act
        await service.DeleteSessionAsync(sessionId);

        // Assert
        var messages = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId);
        messages.Should().BeEmpty();

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldNotDeleteMessagesFromOtherSessions()
    {
        // Arrange
        var service = _messageService;
        var sessionId1 = "keep-session-" + Guid.NewGuid().ToString("N");
        var sessionId2 = "delete-session-" + Guid.NewGuid().ToString("N");

        await service.SaveMessageAsync(sessionId1, "user", "保留的消息");
        await service.SaveMessageAsync(sessionId2, "user", "要删除的消息");

        // Act
        await service.DeleteSessionAsync(sessionId2);

        // Assert
        var remainingMessages = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId1);
        remainingMessages.Should().HaveCount(1);
        remainingMessages[0].Content.Should().Be("保留的消息");
    }

    [Fact]
    public async Task DeleteSessionAsync_ForEmptySession_ShouldNotThrow()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "delempty-" + Guid.NewGuid().ToString("N")[..16];

        // Act & Assert
        var action = () => service.DeleteSessionAsync(sessionId);
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveMessageAsync_WithLongContent_ShouldSaveSuccessfully()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "long-" + Guid.NewGuid().ToString("N")[..16];
        var longContent = new string('A', 2000);

        // Act
        var messageId = await service.SaveMessageAsync(sessionId, "user", longContent);

        // Assert
        messageId.Should().BeGreaterThan(0);
        var savedMessage = ChatMessage.FindById(messageId);
        savedMessage!.Content.Should().Be(longContent);
    }

    [Fact]
    public async Task SaveMessageAsync_WithDifferentRoles_ShouldSaveCorrectly()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "roles-" + Guid.NewGuid().ToString("N")[..16];

        // Act
        var userMsgId = await service.SaveMessageAsync(sessionId, "user", "用户消息");
        var assistantMsgId = await service.SaveMessageAsync(sessionId, "assistant", "助手消息");
        var systemMsgId = await service.SaveMessageAsync(sessionId, "system", "系统消息");

        // Assert
        var userMsg = ChatMessage.FindById(userMsgId);
        userMsg!.Role.Should().Be("user");

        var assistantMsg = ChatMessage.FindById(assistantMsgId);
        assistantMsg!.Role.Should().Be("assistant");

        var systemMsg = ChatMessage.FindById(systemMsgId);
        systemMsg!.Role.Should().Be("system");
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnCorrectModelStructure()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "model-" + Guid.NewGuid().ToString("N")[..16];
        await service.SaveMessageAsync(sessionId, "user", "测试内容");

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(1);
        var message = history[0];
        message.Id.Should().BeGreaterThan(0);
        message.SessionId.Should().Be(sessionId);
        message.Role.Should().Be("user");
        message.Content.Should().Be("测试内容");
        message.CreateTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        message.UpdateTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveMessageAsync_WithSpecialCharacters_ShouldSaveCorrectly()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "special-" + Guid.NewGuid().ToString("N")[..16];
        var specialContent = "特殊字符测试: \n\t\r\"'<>&中文日本語한국어";

        // Act
        var messageId = await service.SaveMessageAsync(sessionId, "user", specialContent);

        // Assert
        var savedMessage = ChatMessage.FindById(messageId);
        savedMessage!.Content.Should().Be(specialContent);
    }

    [Fact]
    public async Task GetHistoryAsync_WithDefaultLimit_ShouldReturn50Messages()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "deflimit-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 60; i++)
        {
            await service.SaveMessageAsync(sessionId, "user", $"消息{i}");
        }

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(50);
    }
}

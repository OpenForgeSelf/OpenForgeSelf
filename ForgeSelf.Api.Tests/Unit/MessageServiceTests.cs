using ForgeSelf.Api.Entities;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 消息服务单元测试（只读面：GetHistoryAsync / DeleteSessionAsync）。
/// </summary>
/// <remarks>
/// B6（040 §2.5 随批项）：宿主侧 <c>IMessageService.SaveMessageAsync</c> 已删除
/// （B4 写路径改序后生产调用方为 0，直写 <c>ChatMessage</c> 是旁路种子，写路径唯一走 <c>ISessionStore.Append</c>）。
/// 因此本类不再有 Save 用例，历史/删除用例的种子数据改为直接 <see cref="ChatMessage"/> 落库
/// （模拟投影同步器写入的行），断言面与原用例等价。
/// </remarks>
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

    /// <summary>直接落一条投影行（B6 起 SaveMessageAsync 已删，种子改由实体直插，等价于投影同步器写入）。</summary>
    private static long Seed(string sessionId, string role, string content)
    {
        var message = new ChatMessage
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };
        message.Insert();
        return message.Id;
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnMessagesForSession()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "history-" + Guid.NewGuid().ToString("N")[..16];

        Seed(sessionId, "user", "消息1");
        Seed(sessionId, "assistant", "响应1");
        Seed(sessionId, "user", "消息2");

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

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnMessagesInCorrectOrder()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "order-" + Guid.NewGuid().ToString("N")[..16];

        Seed(sessionId, "user", "第一条");
        Seed(sessionId, "assistant", "第二条");
        Seed(sessionId, "user", "第三条");

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert：B4 起按 Id 稳定排序（投影行主键升序 == 日志顺序）
        history.Should().HaveCount(3);
        history.Select(m => m.Content).Should().ContainInOrder("第一条", "第二条", "第三条");
        history.Should().BeInAscendingOrder(m => m.Id);
    }

    [Fact]
    public async Task GetHistoryAsync_WithLimit_ShouldReturnLimitedMessages()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "limit-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 10; i++)
        {
            Seed(sessionId, "user", $"消息{i}");
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

        Seed(sessionId1, "user", "会话1消息");
        Seed(sessionId2, "user", "会话2消息");

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

        Seed(sessionId, "user", "消息1");
        Seed(sessionId, "assistant", "响应1");
        Seed(sessionId, "user", "消息2");

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

        Seed(sessionId1, "user", "保留的消息");
        Seed(sessionId2, "user", "要删除的消息");

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
    public async Task GetHistoryAsync_WithLongContent_ShouldReturnFullContent()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "long-" + Guid.NewGuid().ToString("N")[..16];
        var longContent = new string('A', 2000);
        Seed(sessionId, "user", longContent);

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(1);
        history[0].Content.Should().Be(longContent);
    }

    [Fact]
    public async Task GetHistoryAsync_WithDifferentRoles_ShouldReturnAllRoles()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "roles-" + Guid.NewGuid().ToString("N")[..16];

        Seed(sessionId, "user", "用户消息");
        Seed(sessionId, "assistant", "助手消息");
        Seed(sessionId, "system", "系统消息");

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Select(m => m.Role).Should().ContainInOrder("user", "assistant", "system");
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnCorrectModelStructure()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "model-" + Guid.NewGuid().ToString("N")[..16];
        Seed(sessionId, "user", "测试内容");

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
    public async Task GetHistoryAsync_WithSpecialCharacters_ShouldReturnExactContent()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "special-" + Guid.NewGuid().ToString("N")[..16];
        var specialContent = "特殊字符测试: \n\t\r\"'<>&中文日本語한국어";
        Seed(sessionId, "user", specialContent);

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(1);
        history[0].Content.Should().Be(specialContent);
    }

    [Fact]
    public async Task GetHistoryAsync_WithDefaultLimit_ShouldReturn50Messages()
    {
        // Arrange
        var service = _messageService;
        var sessionId = "deflimit-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 60; i++)
        {
            Seed(sessionId, "user", $"消息{i}");
        }

        // Act
        var history = await service.GetHistoryAsync(sessionId);

        // Assert
        history.Should().HaveCount(50);
    }
}

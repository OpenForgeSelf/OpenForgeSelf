namespace ForgeSelf.Api.Tests.Infrastructure;

/// <summary>
/// 测试数据工厂，用于创建测试数据
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// 创建测试用的聊天消息
    /// </summary>
    public static TestChatMessage CreateTestMessage(string? content = null, string? role = null)
    {
        return new TestChatMessage
        {
            Id = Guid.NewGuid(),
            Content = content ?? "Test message content",
            Role = role ?? "user",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建多个测试消息
    /// </summary>
    public static IEnumerable<TestChatMessage> CreateTestMessages(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return CreateTestMessage($"Test message {i + 1}");
        }
    }
}

/// <summary>
/// 测试用的聊天消息模型
/// </summary>
public class TestChatMessage
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
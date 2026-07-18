using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class ChatRecordServiceTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatRecordService _chatRecordService;

    public ChatRecordServiceTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatRecordService = new ChatRecordService(_mockLogService.Object);
    }

    [Fact]
    public async Task SaveRecordAsync_ShouldSaveRecordAndReturnSuccess()
    {
        // Arrange
        var sessionId = "test-save-" + Guid.NewGuid().ToString("N")[..16];
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        record.Id.Should().BeGreaterThan(0);

        var savedRecord = ChatRecord.FindById(record.Id);
        savedRecord.Should().NotBeNull();
        savedRecord!.SessionId.Should().Be(sessionId);
        savedRecord.Style.Should().Be("OpenAI_Chat");
        savedRecord.Model.Should().Be("gpt-4");
        savedRecord.Temperature.Should().Be(0.7);
        savedRecord.MaxTokens.Should().Be(1000);
        savedRecord.MessageCount.Should().Be(3);
        savedRecord.ToolCallCount.Should().Be(0);
        savedRecord.DurationMs.Should().Be(500);

        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnRecord()
    {
        // Arrange
        var sessionId = "test-getbyid-" + Guid.NewGuid().ToString("N")[..16];
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Act
        var result = await _chatRecordService.GetByIdAsync(record.Id);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(sessionId);
        result.Style.Should().Be("OpenAI_Responses");
        result.Model.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _chatRecordService.GetByIdAsync(999999999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordsAsync_WithSessionIdFilter_ShouldReturnFilteredRecords()
    {
        // Arrange
        var sessionId1 = "filter-s1-" + Guid.NewGuid().ToString("N")[..16];
        var sessionId2 = "filter-s2-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 3; i++)
        {
            await _chatRecordService.SaveRecordAsync(new ChatRecord
            {
                SessionId = sessionId1,
                Style = "OpenAI_Chat",
                Model = "gpt-4",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{}",
                ResponseStatus = 200,
                ResponseBody = "{}",
                Temperature = 0.7,
                MessageCount = i + 1,
                ToolCallCount = 0,
                CreatedTime = DateTime.Now
            });
        }

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId2,
            Style = "Anthropic_Messages",
            Model = "claude-3",
            RequestMethod = "POST",
            RequestPath = "/v1/anthropic/messages",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.5,
            MessageCount = 2,
            ToolCallCount = 0,
            CreatedTime = DateTime.Now
        });

        // Act
        var (records, total) = await _chatRecordService.GetRecordsAsync(sessionId1, null, null, null, 1, 20);

        // Assert
        total.Should().Be(3);
        records.Should().HaveCount(3);
        records.Should().AllSatisfy(r => r.SessionId.Should().Be(sessionId1));
    }

    [Fact]
    public async Task GetRecordsAsync_WithStyleFilter_ShouldReturnFilteredRecords()
    {
        // Arrange
        var prefix = "style-filter-" + Guid.NewGuid().ToString("N")[..8] + "-";

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = prefix + "1",
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now
        });

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = prefix + "2",
            Style = "Anthropic_Messages",
            Model = "claude-3",
            RequestMethod = "POST",
            RequestPath = "/v1/anthropic/messages",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.5,
            MessageCount = 1,
            CreatedTime = DateTime.Now
        });

        // Act
        var (records, total) = await _chatRecordService.GetRecordsAsync(null, "Anthropic_Messages", null, null, 1, 20);

        // Assert
        total.Should().BeGreaterThanOrEqualTo(1);
        records.Where(r => r.SessionId.StartsWith(prefix)).Should().AllSatisfy(r => r.Style.Should().Be("Anthropic_Messages"));
    }

    [Fact]
    public async Task GetRecordsAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var sessionId = "pagination-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 10; i++)
        {
            await _chatRecordService.SaveRecordAsync(new ChatRecord
            {
                SessionId = sessionId,
                Style = "OpenAI_Chat",
                Model = "gpt-4",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{}",
                ResponseStatus = 200,
                ResponseBody = $"{{\"index\":{i}}}",
                Temperature = 0.7,
                MessageCount = 1,
                CreatedTime = DateTime.Now.AddMinutes(-i * 10)
            });
        }

        // Act - Page 1
        var (page1, total) = await _chatRecordService.GetRecordsAsync(sessionId, null, null, null, 1, 3);

        // Assert
        total.Should().Be(10);
        page1.Should().HaveCount(3);

        // Act - Page 2
        var (page2, _) = await _chatRecordService.GetRecordsAsync(sessionId, null, null, null, 2, 3);

        // Assert
        page2.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRecordsAsync_ShouldReturnRecordsInDescendingOrder()
    {
        // Arrange
        var sessionId = "order-" + Guid.NewGuid().ToString("N")[..16];

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now.AddHours(-2)
        });

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now.AddHours(-1)
        });

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now
        });

        // Act
        var (records, _) = await _chatRecordService.GetRecordsAsync(sessionId, null, null, null, 1, 10);

        // Assert
        records.Should().HaveCount(3);
        records.Should().BeInDescendingOrder(r => r.CreatedTime);
    }

    [Fact]
    public async Task SaveRecordAsync_WithReasoning_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = "reasoning-" + Guid.NewGuid().ToString("N")[..16];
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var savedRecord = ChatRecord.FindById(record.Id);
        savedRecord.Should().NotBeNull();
        savedRecord!.HasReasoning.Should().BeTrue();
    }

    [Fact]
    public async Task SaveRecordAsync_WithToolCalls_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = "toolcalls-" + Guid.NewGuid().ToString("N")[..16];
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var savedRecord = ChatRecord.FindById(record.Id);
        savedRecord.Should().NotBeNull();
        savedRecord!.ToolCallCount.Should().Be(3);
    }

    [Fact]
    public async Task GetRecordsAsync_WithDateRange_ShouldFilterCorrectly()
    {
        // Arrange
        var sessionId = "date-range-" + Guid.NewGuid().ToString("N")[..16];

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now.AddDays(-2)
        });

        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.7,
            MessageCount = 1,
            CreatedTime = DateTime.Now.AddDays(-1)
        });

        // Act - only recent
        var from = DateTime.Now.AddDays(-1).AddHours(-1);
        var to = DateTime.Now.AddHours(1);
        var (records, _) = await _chatRecordService.GetRecordsAsync(sessionId, null, from, to, 1, 10);

        // Assert
        records.Where(r => r.CreatedTime >= from && r.CreatedTime <= to).Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task SaveRecordAsync_WithMaxTokensZero_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = "zero-maxtokens-" + Guid.NewGuid().ToString("N")[..16];
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var savedRecord = ChatRecord.FindById(record.Id);
        savedRecord.Should().NotBeNull();
        savedRecord!.MaxTokens.Should().Be(0);
    }

    [Fact]
    public async Task SaveRecordAsync_WithLargeRequestBody_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = "large-body-" + Guid.NewGuid().ToString("N")[..16];
        var largeBody = new string('x', 8000);
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var savedRecord = ChatRecord.FindById(record.Id);
        savedRecord.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRecordsAsync_EmptyFilters_ShouldReturnAll()
    {
        // Arrange
        var sessionId = "nofilter-" + Guid.NewGuid().ToString("N")[..16];

        for (int i = 0; i < 5; i++)
        {
            await _chatRecordService.SaveRecordAsync(new ChatRecord
            {
                SessionId = sessionId,
                Style = "OpenAI_Chat",
                Model = "gpt-4",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{}",
                ResponseStatus = 200,
                ResponseBody = "{}",
                Temperature = 0.7,
                MessageCount = 1,
                CreatedTime = DateTime.Now
            });
        }

        // Act
        var (records, total) = await _chatRecordService.GetRecordsAsync(sessionId, null, null, null, 1, 100);

        // Assert
        total.Should().Be(5);
        records.Should().HaveCount(5);
    }
}

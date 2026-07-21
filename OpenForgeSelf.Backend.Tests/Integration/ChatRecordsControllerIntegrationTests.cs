using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace OpenForgeSelf.Backend.Tests.Integration;

[Collection("XCode")]
public class ChatRecordsControllerIntegrationTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatRecordService _chatRecordService;
    private readonly string _uniquePrefix;

    public ChatRecordsControllerIntegrationTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatRecordService = new ChatRecordService(_mockLogService.Object);
        _uniquePrefix = "ctrl-test-" + Guid.NewGuid().ToString("N")[..8] + "-";
    }

    private ChatRecordsController CreateController()
    {
        return new ChatRecordsController(_chatRecordService, _mockLogService.Object);
    }

    [Fact]
    public async Task GetRecords_WithNoFilter_ShouldReturnOk()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "get-all";
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
            MessageCount = 2,
            CreatedTime = DateTime.Now
        });

        // Act
        var result = await controller.GetRecords(null, null, null, null, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRecords_WithSessionIdFilter_ShouldReturnFiltered()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "filter-sid";
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
            MessageCount = 2,
            CreatedTime = DateTime.Now
        });

        // Act
        var result = await controller.GetRecords(sessionId, null, null, null, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var response = okResult.Value;
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRecords_WithStyleFilter_ShouldReturnFiltered()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "filter-style";
        await _chatRecordService.SaveRecordAsync(new ChatRecord
        {
            SessionId = sessionId,
            Style = "Anthropic_Messages",
            Model = "claude-3",
            RequestMethod = "POST",
            RequestPath = "/v1/anthropic/messages",
            RequestBody = "{}",
            ResponseStatus = 200,
            ResponseBody = "{}",
            Temperature = 0.5,
            MessageCount = 2,
            CreatedTime = DateTime.Now
        });

        // Act
        var result = await controller.GetRecords(null, "Anthropic_Messages", null, null, 1, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ExistingId_ShouldReturnOk()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "get-by-id";
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        };
        await _chatRecordService.SaveRecordAsync(record);

        // Act
        var result = await controller.GetById(record.Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_NonExistingId_ShouldReturnNotFound()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetById(999999999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRecords_WithPagination_ShouldReturnCorrectPageSize()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "pagination";

        for (int i = 0; i < 5; i++)
        {
            await _chatRecordService.SaveRecordAsync(new ChatRecord
            {
                SessionId = sessionId + "-" + i,
                Style = "OpenAI_Chat",
                Model = "gpt-4",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{}",
                ResponseStatus = 200,
                ResponseBody = "{}",
                Temperature = 0.7,
                MessageCount = 1,
                CreatedTime = DateTime.Now.AddMinutes(-i * 5)
            });
        }

        // Act
        var result = await controller.GetRecords(null, null, null, null, 1, 2);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRecords_PageBelowOne_ShouldDefaultToPage1()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetRecords(null, null, null, null, 0, 20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRecords_PageSizeAboveMax_ShouldDefaultTo100()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetRecords(null, null, null, null, 1, 200);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WithValidRecord_ShouldContainAllFields()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "all-fields";
        var record = new ChatRecord
        {
            SessionId = sessionId,
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
        };
        await _chatRecordService.SaveRecordAsync(record);

        // Act
        var result = await controller.GetById(record.Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var data = okResult.Value?.GetType()?.GetProperty("data")?.GetValue(okResult.Value);
        data.Should().NotBeNull();
    }
}

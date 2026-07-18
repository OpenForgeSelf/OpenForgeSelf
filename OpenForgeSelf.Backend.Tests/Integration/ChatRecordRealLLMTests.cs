using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 聊天记录真实LLM配置集成测试 - 使用配置文件中的真实LLM配置进行测试
/// </summary>
public class ChatRecordRealLLMTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatRecordService _chatRecordService;
    private readonly IConfiguration _configuration;
    private readonly string _uniquePrefix;

    public ChatRecordRealLLMTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatRecordService = new ChatRecordService(_mockLogService.Object);
        _uniquePrefix = "real-llm-" + Guid.NewGuid().ToString("N")[..8] + "-";

        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "OpenForgeSelf.Backend"))
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    private ChatRecordsController CreateController()
    {
        return new ChatRecordsController(_chatRecordService, _mockLogService.Object);
    }

    [Fact]
    public void Configuration_ShouldHaveAISettings()
    {
        // Arrange & Act
        var apiEndpoint = _configuration["AI:ApiEndpoint"];
        var apiKey = _configuration["AI:ApiKey"];
        var modelName = _configuration["AI:ModelName"];

        // Assert
        apiEndpoint.Should().NotBeNullOrEmpty("配置文件中应包含 AI:ApiEndpoint");
        apiKey.Should().NotBeNullOrEmpty("配置文件中应包含 AI:ApiKey");
        modelName.Should().NotBeNullOrEmpty("配置文件中应包含 AI:ModelName");
    }

    [Fact]
    public async Task ChatRecordService_ShouldSaveRecordWithRealStyleFormat()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "real-style";
        var model = _configuration["AI:ModelName"] ?? "gpt-4";
        var apiEndpoint = _configuration["AI:ApiEndpoint"] ?? "http://localhost:1234/v1/chat/completions";

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = "你好，请介绍一下你自己" }
            },
            temperature = 0.7,
            max_tokens = 100,
            stream = false
        };

        var responseBody = new
        {
            id = "chatcmpl-test123",
            @object = "chat.completion",
            created = DateTimeOffset.Now.ToUnixTimeSeconds(),
            model = model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "你好！我是AI助手，很高兴为你服务。" },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
        };

        var record = new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = model,
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestHeaders = "{\"Authorization\":\"Bearer sk-***\",\"Content-Type\":\"application/json\"}",
            RequestBody = System.Text.Json.JsonSerializer.Serialize(requestBody),
            ResponseStatus = 200,
            ResponseHeaders = "{\"Content-Type\":\"application/json\"}",
            ResponseBody = System.Text.Json.JsonSerializer.Serialize(responseBody),
            Temperature = 0.7,
            MaxTokens = 100,
            MessageCount = 1,
            ToolCallCount = 0,
            HasReasoning = false,
            DurationMs = 350,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var savedRecord = await _chatRecordService.GetByIdAsync(record.Id);
        savedRecord.Should().NotBeNull();
        savedRecord!.SessionId.Should().Be(sessionId);
        savedRecord.Style.Should().Be("OpenAI_Chat");
        savedRecord.Model.Should().Be(model);
        savedRecord.RequestBody.Should().Contain("messages");
        savedRecord.ResponseBody.Should().Contain("assistant");
        savedRecord.Temperature.Should().Be(0.7);
        savedRecord.MaxTokens.Should().Be(100);
        savedRecord.DurationMs.Should().Be(350);
    }

    [Fact]
    public async Task ChatRecordsController_ShouldReturnRecordWithAllFields()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "all-fields";
        var model = _configuration["AI:ModelName"] ?? "test-model";

        var record = new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = model,
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestHeaders = "{}",
            RequestBody = "{\"model\":\"" + model + "\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hello\"}}]}",
            Temperature = 0.8,
            MaxTokens = 500,
            MessageCount = 2,
            ToolCallCount = 0,
            HasReasoning = false,
            DurationMs = 200,
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
    public async Task ChatRecord_WithOpenAIResponsesStyle_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = _uniquePrefix + "responses-style";
        var record = new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Responses",
            Model = "gpt-4o",
            RequestMethod = "POST",
            RequestPath = "/v1/responses",
            RequestHeaders = "{}",
            RequestBody = "{\"model\":\"gpt-4o\",\"input\":\"hello\",\"instructions\":\"be helpful\"}",
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = "{\"output\":[{\"type\":\"text\",\"text\":\"hi\"}]}",
            Temperature = 0.9,
            MaxTokens = 256,
            MessageCount = 1,
            ToolCallCount = 0,
            HasReasoning = false,
            DurationMs = 180,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var saved = await _chatRecordService.GetByIdAsync(record.Id);
        saved.Should().NotBeNull();
        saved!.Style.Should().Be("OpenAI_Responses");
        saved.RequestBody.Should().Contain("instructions");
        saved.ResponseBody.Should().Contain("output");
    }

    [Fact]
    public async Task ChatRecord_WithAnthropicStyle_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = _uniquePrefix + "anthropic-style";
        var record = new ChatRecord
        {
            SessionId = sessionId,
            Style = "Anthropic_Messages",
            Model = "claude-3-opus",
            RequestMethod = "POST",
            RequestPath = "/v1/anthropic/messages",
            RequestHeaders = "{}",
            RequestBody = "{\"model\":\"claude-3-opus\",\"max_tokens\":1024,\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}",
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = "{\"content\":[{\"type\":\"text\",\"text\":\"hi\"}],\"role\":\"assistant\"}",
            Temperature = 0.6,
            MaxTokens = 1024,
            MessageCount = 1,
            ToolCallCount = 0,
            HasReasoning = true,
            DurationMs = 500,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var saved = await _chatRecordService.GetByIdAsync(record.Id);
        saved.Should().NotBeNull();
        saved!.Style.Should().Be("Anthropic_Messages");
        saved.HasReasoning.Should().BeTrue();
        saved.RequestBody.Should().Contain("claude-3-opus");
    }

    [Fact]
    public async Task ChatRecord_WithToolCalls_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionId = _uniquePrefix + "tool-calls";
        var requestBody = new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "user", content = "今天天气怎么样？" }
            },
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new { name = "get_weather", description = "获取天气", parameters = new { } }
                }
            },
            temperature = 0.7
        };

        var responseBody = new
        {
            id = "chatcmpl-tools",
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call_123",
                                type = "function",
                                function = new { name = "get_weather", arguments = "{\"city\":\"Beijing\"}" }
                            }
                        }
                    },
                    finish_reason = "tool_calls"
                }
            }
        };

        var record = new ChatRecord
        {
            SessionId = sessionId,
            Style = "OpenAI_Chat",
            Model = "gpt-4",
            RequestMethod = "POST",
            RequestPath = "/v1/chat/completions",
            RequestHeaders = "{}",
            RequestBody = System.Text.Json.JsonSerializer.Serialize(requestBody),
            ResponseStatus = 200,
            ResponseHeaders = "{}",
            ResponseBody = System.Text.Json.JsonSerializer.Serialize(responseBody),
            Temperature = 0.7,
            MaxTokens = 500,
            MessageCount = 1,
            ToolCallCount = 1,
            HasReasoning = false,
            DurationMs = 400,
            CreatedTime = DateTime.Now
        };

        // Act
        await _chatRecordService.SaveRecordAsync(record);

        // Assert
        var saved = await _chatRecordService.GetByIdAsync(record.Id);
        saved.Should().NotBeNull();
        saved!.ToolCallCount.Should().Be(1);
        saved.RequestBody.Should().Contain("tools");
        saved.ResponseBody.Should().Contain("tool_calls");
    }

    [Fact]
    public async Task ChatRecordsApi_ShouldReturnPagedResults()
    {
        // Arrange
        var controller = CreateController();
        var sessionId = _uniquePrefix + "paged";

        for (int i = 0; i < 15; i++)
        {
            await _chatRecordService.SaveRecordAsync(new ChatRecord
            {
                SessionId = sessionId + i,
                Style = "OpenAI_Chat",
                Model = "test-model",
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestBody = "{}",
                ResponseStatus = 200,
                ResponseBody = "{}",
                Temperature = 0.7,
                MaxTokens = 100,
                MessageCount = 1,
                ToolCallCount = 0,
                HasReasoning = false,
                DurationMs = 100 + i,
                CreatedTime = DateTime.Now.AddMinutes(-i)
            });
        }

        // Act
        var result = await controller.GetRecords(null, null, null, null, 1, 10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        // 验证响应中包含数据属性
        var resultType = okResult.Value!.GetType();
        resultType.GetProperty("success").Should().NotBeNull();
    }

    [Fact]
    public async Task ChatRecordsController_GetById_NonExisting_ShouldReturnNotFound()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetById(999999999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

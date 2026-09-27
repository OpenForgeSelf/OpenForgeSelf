using ForgeSelf.Api.Controllers;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 聊天记录真实LLM配置集成测试 - 使用配置文件中的真实LLM配置进行测试。
/// 会话化重构适配：记录保存走 ChatTurnService（ChatTurn + SessionKey），
/// 控制器走会话视图（ChatRecordsController 依赖 IChatSessionService：GetSessions/GetSession）。
/// 除配置断言外，各用例不真正调用 LLM，仅用配置值构造仿真请求/响应体。
/// </summary>
[Collection("XCode")]
public class ChatRecordRealLLMTests : IClassFixture<XCodeTestFixture>
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly ChatTurnService _chatTurnService;
    private readonly ChatSessionService _chatSessionService;
    private readonly IConfiguration _configuration;
    private readonly string _uniquePrefix;

    public ChatRecordRealLLMTests(XCodeTestFixture fixture)
    {
        _mockLogService = new Mock<ILogService>();
        _chatTurnService = new ChatTurnService(_mockLogService.Object);
        _chatSessionService = new ChatSessionService(_mockLogService.Object);
        _uniquePrefix = "real-llm-" + Guid.NewGuid().ToString("N")[..8] + "-";

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    private ChatRecordsController CreateController()
    {
        return new ChatRecordsController(_chatSessionService, _mockLogService.Object);
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
        // 批次D（seq48/62）：appsettings 明文密钥已置空，真实密钥走 AIProvider 加密存储；空串为预期状态
        apiKey.Should().NotBeNull("配置文件中应包含 AI:ApiKey 键（值允许为空）");
        modelName.Should().NotBeNullOrEmpty("配置文件中应包含 AI:ModelName");
    }

    [Fact]
    public async Task ChatTurnService_ShouldSaveTurnWithRealStyleFormat()
    {
        // Arrange
        var sessionKey = _uniquePrefix + "real-style";
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

        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
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
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var savedTurn = await _chatTurnService.GetByIdAsync(turn.Id);
        savedTurn.Should().NotBeNull();
        savedTurn!.SessionKey.Should().Be(sessionKey);
        savedTurn.Style.Should().Be("OpenAI_Chat");
        savedTurn.Model.Should().Be(model);
        savedTurn.RequestBody.Should().Contain("messages");
        savedTurn.ResponseBody.Should().Contain("assistant");
        savedTurn.Temperature.Should().Be(0.7);
        savedTurn.MaxTokens.Should().Be(100);
        savedTurn.DurationMs.Should().Be(350);
    }

    [Fact]
    public async Task ChatRecordsController_ShouldReturnSessionWithAllFields()
    {
        // Arrange：先 upsert 会话 + 保存一轮，再走控制器详情接口
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "all-fields";
        var model = _configuration["AI:ModelName"] ?? "test-model";

        var session = await _chatSessionService.UpsertSessionAsync(
            sessionKey, SessionSource.Proxy, model, ClientKind.Other, "OpenAI_Chat", "hi", 2, provider: "test-provider");
        session.Id.Should().BeGreaterThan(0);

        await _chatTurnService.SaveTurnAsync(new ChatTurn
        {
            ChatSessionId = session.Id,
            TurnIndex = 1,
            SessionKey = sessionKey,
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
        });

        // Act
        var result = await controller.GetSession(session.Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ChatTurn_WithOpenAIResponsesStyle_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = _uniquePrefix + "responses-style";
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
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
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var saved = await _chatTurnService.GetByIdAsync(turn.Id);
        saved.Should().NotBeNull();
        saved!.Style.Should().Be("OpenAI_Responses");
        saved.RequestBody.Should().Contain("instructions");
        saved.ResponseBody.Should().Contain("output");
    }

    [Fact]
    public async Task ChatTurn_WithAnthropicStyle_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = _uniquePrefix + "anthropic-style";
        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
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
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var saved = await _chatTurnService.GetByIdAsync(turn.Id);
        saved.Should().NotBeNull();
        saved!.Style.Should().Be("Anthropic_Messages");
        saved.HasReasoning.Should().BeTrue();
        saved.RequestBody.Should().Contain("claude-3-opus");
    }

    [Fact]
    public async Task ChatTurn_WithToolCalls_ShouldSaveCorrectly()
    {
        // Arrange
        var sessionKey = _uniquePrefix + "tool-calls";
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

        var turn = new ChatTurn
        {
            SessionKey = sessionKey,
            TurnIndex = 1,
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
        await _chatTurnService.SaveTurnAsync(turn);

        // Assert
        var saved = await _chatTurnService.GetByIdAsync(turn.Id);
        saved.Should().NotBeNull();
        saved!.ToolCallCount.Should().Be(1);
        saved.RequestBody.Should().Contain("tools");
        saved.ResponseBody.Should().Contain("tool_calls");
    }

    [Fact]
    public async Task ChatSessionsApi_ShouldReturnPagedResults()
    {
        // Arrange：15 个会话（原用例为 15 条记录，会话化后每 key 一会话）
        var controller = CreateController();
        var sessionKey = _uniquePrefix + "paged";

        for (int i = 0; i < 15; i++)
        {
            await _chatSessionService.UpsertSessionAsync(
                sessionKey + i, SessionSource.Proxy, "test-model", ClientKind.Other, "OpenAI_Chat", null, 1);
        }

        // Act
        var result = await controller.GetSessions(null, null, null, null, null, sessionKey, 1, 10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();

        // 验证响应中包含数据属性
        var resultType = okResult.Value!.GetType();
        resultType.GetProperty("success").Should().NotBeNull();
    }

    [Fact]
    public async Task ChatRecordsController_GetSession_NonExisting_ShouldReturnNotFound()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetSession(999999999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

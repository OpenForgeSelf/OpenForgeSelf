using System.Threading;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// ChatController 单元测试：验证持久化失败不应中断聊天主流程（N+6 修复1）。
/// </summary>
public class ChatControllerTests
{
    private static ChatController BuildController(
        Mock<IMessageService> messageService,
        Mock<IAIService> aiService)
    {
        // 历史查询默认返回空列表，避免测试因未模拟而拿到 null
        messageService.Setup(x => x.GetHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ForgeSelf.Api.Models.ChatMessageModel>());

        var log = new Mock<ILogService>();
        var session = new Mock<IChatSessionService>();
        session.Setup(x => x.UpsertSessionAsync(It.IsAny<string>(), It.IsAny<SessionSource>(), It.IsAny<string?>(), It.IsAny<ClientKind>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.FromResult<ChatSession>(null!));

        return new ChatController(aiService.Object, messageService.Object, log.Object, session.Object, null);
    }

    [Fact]
    public async Task SendMessage_WhenMessageInsertFails_ShouldStillReturnOkWithAiReply()
    {
        // Arrange：模拟 XCode 长度校验/DB 写入抛错（如超长文本）
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(x => x.SaveMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("长度限制2000字符"));

        var aiService = new Mock<IAIService>();
        aiService
            .Setup(x => x.ChatAsync(It.IsAny<List<ForgeSelf.Api.Models.AIChatMessage>>(), It.IsAny<string?>()))
            .ReturnsAsync("mock-ai-reply");

        var controller = BuildController(messageService, aiService);
        var request = new ChatRequest
        {
            Message = new string('x', 5000),
            SessionId = "sess-1",
            ChatModelId = "default:model"
        };

        // Act
        var result = await controller.SendMessage(request, CancellationToken.None);

        // Assert：插入失败不得导致 500，必须返回 200 + AI 回复（Id=0 表示持久化未成功但流程继续）
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChatResponse>(ok.Value);
        Assert.Equal("mock-ai-reply", response.Content);
        Assert.Equal(0, response.Id);
    }

    [Fact]
    public async Task SendMessageStream_WhenMessageInsertFails_ShouldStillStreamAiReply()
    {
        // Arrange
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(x => x.SaveMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("长度限制2000字符"));

        var aiService = new Mock<IAIService>();
        aiService
            .Setup(x => x.ChatStreamAsync(It.IsAny<List<ForgeSelf.Api.Models.AIChatMessage>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable("mock ", "stream ", "reply"));

        var controller = BuildController(messageService, aiService);
        // SSE 写入依赖 Response，单元测试需提供最小 HttpContext
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var request = new ChatRequest
        {
            Message = new string('x', 5000),
            SessionId = "sess-2",
            ChatModelId = "default:model"
        };

        // Act & Assert：流式方法在插入失败时不应抛异常（应捕获并继续推送）
        var exception = await Record.ExceptionAsync(() => controller.SendMessageStream(request, CancellationToken.None));
        Assert.Null(exception);
    }

    private static async IAsyncEnumerable<string> CreateAsyncEnumerable(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }
}

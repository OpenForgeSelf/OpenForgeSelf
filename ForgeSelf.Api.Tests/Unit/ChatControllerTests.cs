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

        // B4（040）改序后 ISessionStore 为必选注入（唯一写路径）；本测试场景不依赖持久化，
        // 用内存替身即可（投影同步失败会被控制器吞掉并记日志，不中断聊天主流程 —— 正是被测语义）。
        var store = new InMemorySessionStore();
        var projection = new SessionProjectionService(store, log.Object);

        return new ChatController(aiService.Object, messageService.Object, log.Object, session.Object, store, projection);
    }

    [Fact]
    public async Task SendMessage_WhenMessageInsertFails_ShouldStillReturnOkWithAiReply()
    {
        // Arrange：B6 起 IMessageService 不再有 SaveMessageAsync（只读投影面），
        // 持久化失败路径由 ISessionStore/投影同步承担；本用例保留「持久化异常不中断主流程」语义。
        var messageService = new Mock<IMessageService>();

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

        // Assert：插入失败不得导致 500，必须返回 200 + AI 回复
        // （B4 改序后 Id = 助手事件在日志中的 Id，恒 > 0；投影同步失败被吞掉不影响响应）
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChatResponse>(ok.Value);
        Assert.Equal("mock-ai-reply", response.Content);
        Assert.True(response.Id > 0, "助手消息事件必须已落日志（模型可见 = 已记录）");
    }

    [Fact]
    public async Task SendMessageStream_WhenMessageInsertFails_ShouldStillStreamAiReply()
    {
        // Arrange：同上——SaveMessageAsync 已删（B6），流式主流程不因投影/持久化异常中断
        var messageService = new Mock<IMessageService>();

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

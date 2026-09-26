using System.Net;
using Microsoft.Extensions.Hosting;
using Moq;
using Moq.Protected;
using FluentAssertions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// StagedUpdateService 状态机测试（spec 036）。
/// 用真实 UpdateChecker + mock HTTP（GitHub Releases JSON）驱动检查流程；
/// 不触发实际下载/代理（文件系统与进程副作用留待端到端验收）。
/// </summary>
public class StagedUpdateServiceTests : IDisposable
{
    private const string ReleasesJson = """
        [{"tag_name":"v9.9.9","draft":false,"prerelease":false,"body":"notes","assets":[
          {"name":"OpenForgeSelf-9.9.9-win-x64.zip","browser_download_url":"https://example.com/a.zip","digest":"sha256:aa","size":10}]}]
        """;

    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IHostApplicationLifetime> _lifetimeMock;

    public StagedUpdateServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(ReleasesJson, System.Text.Encoding.UTF8, "application/json"),
            });
        _httpClient = new HttpClient(_handlerMock.Object);
        _lifetimeMock = new Mock<IHostApplicationLifetime>();
    }

    public void Dispose()
    {
        _handlerMock.Reset();
        _httpClient.Dispose();
    }

    private StagedUpdateService CreateService(string currentVersion = "1.0.0.0")
    {
        var config = new UpdateConfig
        {
            Provider = "github",
            GitHubRepo = "test-owner/test-repo",
            Channel = "stable",
        };
        var checker = new UpdateChecker(config, _httpClient, "TestApp", currentVersion);
        return new StagedUpdateService(checker, _lifetimeMock.Object);
    }

    [Fact]
    public async Task CheckAsync_UpdateAvailable_SetsCheckedStateWithResult()
    {
        var service = CreateService();

        var result = await service.CheckAsync();

        result.HasUpdate.Should().BeTrue();
        var state = service.GetState();
        state.Status.Should().Be("checked");
        state.Check.Should().NotBeNull();
        state.Tag.Should().Be("v9.9.9");
    }

    [Fact]
    public async Task CheckAsync_NoUpdate_SetsIdleState()
    {
        var service = CreateService(currentVersion: "9.9.9.0");

        await service.CheckAsync();

        service.GetState().Status.Should().Be("idle");
    }

    [Fact]
    public async Task StartDownload_WithoutCheck_DoesNothing()
    {
        var service = CreateService();

        service.StartDownload().Should().BeFalse();
        service.GetState().Status.Should().Be("idle");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ApplyStaged_NotReady_ReturnsFalseAndFails()
    {
        var service = CreateService();

        service.ApplyStaged().Should().BeFalse();
        service.GetState().Status.Should().Be("failed");
        await Task.CompletedTask;
    }

    /// <summary>
    /// spec 036 实机竞态回归：StartDownload 返回瞬间状态必须已是 downloading。
    /// 旧实现先 Task.Run 再靠后台任务置 downloading，POST 响应/前端首轮轮询可能读到
    /// checked → 前端把 checked 当终止态停止轮询 → UI 永远等不到「重启并更新」。
    /// </summary>
    [Fact]
    public async Task StartDownload_ReturnsWithDownloadingImmediately_NotStaleChecked()
    {
        // 让资产下载响应延迟 3s，杜绝后台任务抢在断言前跑到 ready/failed 的干扰
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken _) =>
            {
                await Task.Delay(3000);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ReleasesJson, System.Text.Encoding.UTF8, "application/json"),
                };
            });

        var service = CreateService();
        var check = await service.CheckAsync();
        check.HasUpdate.Should().BeTrue();
        service.GetState().Status.Should().Be("checked");

        var started = service.StartDownload();

        started.Should().BeTrue();
        service.GetState().Status.Should().Be("downloading");
    }

    /// <summary>
    /// spec 036 实机根因回归：下载进行中再次 check，结论只返回，不得把 downloading
    /// 覆盖成 checked（前端会把 checked 当终止态永久停止轮询 →「重启并更新」按钮消失）。
    /// </summary>
    [Fact]
    public async Task CheckAsync_WhileDownloading_DoesNotClobberState()
    {
        // 资产下载慢（6s），releases 列表快——保证第二次 check 完成时下载仍在进行中
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.RequestUri?.ToString().Contains("/releases/assets") == true)
                    await Task.Delay(6000);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ReleasesJson, System.Text.Encoding.UTF8, "application/json"),
                };
            });

        var service = CreateService();
        (await service.CheckAsync()).HasUpdate.Should().BeTrue();
        service.StartDownload().Should().BeTrue();
        service.GetState().Status.Should().Be("downloading");

        var second = await service.CheckAsync();

        second.HasUpdate.Should().BeTrue();
        service.GetState().Status.Should().Be("downloading");
    }
}

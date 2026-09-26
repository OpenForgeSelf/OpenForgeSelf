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
    public void ApplyStaged_NotReady_ReturnsFalseAndFails()
    {
        var service = CreateService();

        service.ApplyStaged().Should().BeFalse();
        service.GetState().Status.Should().Be("failed");
    }
}

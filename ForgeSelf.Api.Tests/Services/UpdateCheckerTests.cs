using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using FluentAssertions;

namespace ForgeSelf.Api.Tests.Services;

public class UpdateCheckerTests : IDisposable
{
    private readonly UpdateConfig _config;
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly string _testAppName = "TestApp";

    public UpdateCheckerTests()
    {
        _config = new UpdateConfig
        {
            ServerUrl = "http://test-update-server.com",
            Channel = "stable",
            CheckTimeoutSeconds = 5,
            DownloadTimeoutSeconds = 300,
        };
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _httpClient = new HttpClient(_handlerMock.Object);
    }

    public void Dispose()
    {
        _handlerMock.Reset();
        _httpClient.Dispose();
    }

    // ================================================================
    // 版本比较逻辑（新版本 > 当前版本）
    // ================================================================

    [Fact]
    public async Task CheckForUpdateAsync_NewVersionAvailable_ReturnsHasUpdateTrue()
    {
        // Arrange
        var responseJson = """{"success":true,"data":{"hasUpdate":true,"latestVersion":"2.0.0.0"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("2.0.0.0"));
        result.CurrentVersion.Should().Be(new Version("1.0.0.0"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_SameVersion_ReturnsHasUpdateFalse()
    {
        // Arrange
        var responseJson = """{"success":true,"data":{"hasUpdate":false,"latestVersion":"1.0.0.0"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeFalse();
        result.IsSuccess.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("1.0.0.0"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_CurrentVersionNewer_ReturnsHasUpdateFalse()
    {
        // Arrange
        var responseJson = """{"success":true,"data":{"hasUpdate":false,"latestVersion":"1.0.0.0"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "2.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeFalse();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdateAsync_VersionWithThreeParts_WorksCorrectly()
    {
        // Arrange — 服务器返回 3 段版本号 "1.1.0"，Version.TryParse 直接解析为 1.1.0
        var responseJson = """{"success":true,"data":{"hasUpdate":true,"latestVersion":"1.1.0"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("1.1.0"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_VersionWithTwoParts_WorksCorrectly()
    {
        // Arrange — 服务器返回 2 段版本号 "1.1"，Version.TryParse 直接解析为 1.1
        var responseJson = """{"success":true,"data":{"hasUpdate":true,"latestVersion":"1.1"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("1.1"));
    }

    // ================================================================
    // 检查结果解析（有更新/无更新/错误）
    // ================================================================

    [Fact]
    public async Task CheckForUpdateAsync_HasUpdateResponse_ParsesAllFields()
    {
        // Arrange
        var responseJson = """
            {
                "success": true,
                "data": {
                    "hasUpdate": true,
                    "latestVersion": "1.1.0.0",
                    "downloadUrl": "http://example.com/pkg.zip",
                    "packageHash": "sha256:abcdef1234567890abcdef1234567890",
                    "packageSize": 52428800,
                    "releaseNotes": "## v1.1.0\n\nNew features"
                }
            }
            """;
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("1.1.0.0"));
        result.DownloadUrl.Should().Be("http://example.com/pkg.zip");
        result.PackageHash.Should().Be("sha256:abcdef1234567890abcdef1234567890");
        result.PackageSize.Should().Be(52428800);
        result.ReleaseNotes.Should().Be("## v1.1.0\n\nNew features");
        result.CheckTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoUpdateResponse_ParsesCorrectly()
    {
        // Arrange
        var responseJson = """{"success":true,"data":{"hasUpdate":false,"latestVersion":"1.0.0.0"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeFalse();
        result.LatestVersion.Should().Be(new Version("1.0.0.0"));
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdateAsync_ErrorResponse_SetsErrorMessage()
    {
        // Arrange
        var responseJson = """{"success":false,"error":{"code":"APP_NOT_FOUND","message":"应用 TestApp 未注册"}}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        // 注：IsSuccess 在 try 块末尾被设为 true，因为 HTTP 请求成功（200 OK）；
        // 但 ErrorMessage 由 ParseCheckResponse 正确设置，反映 response body 中的错误
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("APP_NOT_FOUND");
        result.ErrorMessage.Should().Contain("应用 TestApp 未注册");
    }

    [Fact]
    public async Task CheckForUpdateAsync_ErrorResponseWithoutErrorField_SetsGenericMessage()
    {
        // Arrange
        var responseJson = """{"success":false}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.ErrorMessage.Should().Be("服务器返回 success=false");
    }

    [Fact]
    public async Task CheckForUpdateAsync_MissingDataField_SetsErrorMessage()
    {
        // Arrange
        var responseJson = """{"success":true}""";
        SetupHttpResponse(responseJson);

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.ErrorMessage.Should().Be("响应中缺少 data 字段");
    }

    [Fact]
    public async Task CheckForUpdateAsync_MalformedJson_CaughtAndSetsErrorMessage()
    {
        // Arrange
        SetupHttpResponse("{invalid json}");

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        // JsonException 被 catch (Exception ex) 捕获，不抛出
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    // ================================================================
    // 超时处理
    // ================================================================

    [Fact]
    public async Task CheckForUpdateAsync_Timeout_SetsIsSuccessFalseAndErrorMessage()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("请求已取消"));

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("超时");
    }

    // ================================================================
    // 下载失败 / 网络不可达
    // ================================================================

    [Fact]
    public async Task CheckForUpdateAsync_HttpRequestException_SetsIsSuccessFalseAndErrorMessage()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("网络不可达"));

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("网络错误");
        result.ErrorMessage.Should().Contain("网络不可达");
    }

    [Fact]
    public async Task CheckForUpdateAsync_HttpRequestException_DoesNotCrash()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("服务器不可达"));

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert — 不崩溃，返回结果
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadPackageAsync_NetworkError_ThrowsHttpRequestException()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("服务器不可达"));

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var act = () => checker.DownloadPackageAsync("1.1.0.0", Path.GetTempFileName());

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*服务器不可达*");
    }

    [Fact]
    public async Task DownloadPackageAsync_Timeout_ThrowsHttpRequestException()
    {
        // Arrange
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("下载超时"));

        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var act = () => checker.DownloadPackageAsync("1.1.0.0", Path.GetTempFileName());

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*下载更新包超时*");
    }

    [Fact]
    public async Task DownloadPackageAsync_ServerUrlNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var configNoUrl = new UpdateConfig
        {
            ServerUrl = "",
            Channel = "stable",
            CheckTimeoutSeconds = 5,
            DownloadTimeoutSeconds = 300,
        };
        var checker = new UpdateChecker(configNoUrl, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var act = () => checker.DownloadPackageAsync("1.1.0.0", Path.GetTempFileName());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ServerUrl 未配置*");
    }

    [Fact]
    public async Task DownloadPackageAsync_NullVersion_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var act = () => checker.DownloadPackageAsync(null!, Path.GetTempFileName());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadPackageAsync_NullDestPath_ThrowsArgumentNullException()
    {
        // Arrange
        var checker = new UpdateChecker(_config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var act = () => checker.DownloadPackageAsync("1.1.0.0", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ================================================================
    // ServerUrl 未配置
    // ================================================================

    [Fact]
    public async Task CheckForUpdateAsync_ServerUrlNotConfigured_ReturnsNotSuccess()
    {
        // Arrange
        var configNoUrl = new UpdateConfig
        {
            ServerUrl = "",
            Channel = "stable",
            CheckTimeoutSeconds = 5,
            DownloadTimeoutSeconds = 300,
        };
        var checker = new UpdateChecker(configNoUrl, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // Act
        var result = await checker.CheckForUpdateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("ServerUrl 未配置");
    }

    // ================================================================
    // 构造函数参数验证
    // ================================================================

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateChecker(null!, _httpClient);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateChecker(_config, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    // ================================================================
    // 辅助方法
    // ================================================================

    private void SetupHttpResponse(string jsonContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content,
            });
    }
}
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

    // ================================================================
    // GitHub Releases provider（spec 036）
    // ================================================================

    private const string GitHubReleasesJson = """
        [
          {
            "tag_name": "v2.0.0",
            "draft": false,
            "prerelease": false,
            "body": "## 更新内容",
            "assets": [
              {
                "name": "OpenForgeSelf-2.0.0-win-x64.zip",
                "url": "https://api.github.com/repos/test-owner/test-repo/releases/assets/9876543",
                "browser_download_url": "https://example.com/forge.zip",
                "digest": "sha256:abcdef0123456789",
                "size": 123456
              }
            ]
          },
          {
            "tag_name": "v1.5.0",
            "draft": false,
            "prerelease": false,
            "body": "old",
            "assets": []
          }
        ]
        """;

    private UpdateConfig GitHubConfig(string channel = "stable") => new()
    {
        Provider = "github",
        GitHubApiUrl = "https://api.github.com",
        GitHubRepo = "test-owner/test-repo",
        Channel = channel,
        CheckTimeoutSeconds = 5,
        DownloadTimeoutSeconds = 60,
    };

    [Fact]
    public async Task GitHub_NewRelease_ReturnsHasUpdateWithAssetInfo()
    {
        SetupHttpResponse(GitHubReleasesJson);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeTrue();
        result.LatestVersionTag.Should().Be("v2.0.0");
        result.LatestVersion.Should().Be(new Version(2, 0, 0));
        result.DownloadUrl.Should().Be("https://example.com/forge.zip");
        result.PackageHash.Should().Be("sha256:abcdef0123456789");
        result.PackageSize.Should().Be(123456);
        result.ReleaseNotes.Should().Be("## 更新内容");
    }

    [Fact]
    public async Task GitHub_SameVersion_ReturnsNoUpdate()
    {
        SetupHttpResponse(GitHubReleasesJson);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "2.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task GitHub_StableChannel_ExcludesPrerelease()
    {
        var json = """
            [{"tag_name":"v3.0.0-beta.1","draft":false,"prerelease":true,"body":"","assets":[
              {"name":"OpenForgeSelf-3.0.0-beta.1-win-x64.zip","browser_download_url":"https://example.com/beta.zip","digest":"sha256:aa","size":1}]}]
            """;
        SetupHttpResponse(json);
        var checker = new UpdateChecker(GitHubConfig("stable"), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.IsSuccess.Should().BeTrue();
        result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task GitHub_BetaChannel_IncludesPrerelease()
    {
        var json = """
            [{"tag_name":"v3.0.0-beta.1","draft":false,"prerelease":true,"body":"","assets":[
              {"name":"OpenForgeSelf-3.0.0-beta.1-win-x64.zip","browser_download_url":"https://example.com/beta.zip","digest":"sha256:aa","size":1}]}]
            """;
        SetupHttpResponse(json);
        var checker = new UpdateChecker(GitHubConfig("beta"), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.HasUpdate.Should().BeTrue();
        result.LatestVersionTag.Should().Be("v3.0.0-beta.1");
    }

    [Fact]
    public async Task GitHub_PrereleaseNotNewerThanRelease_ReturnsNoUpdate()
    {
        // 当前已是正式版 2.0.0，最新（beta 通道可见）为 2.0.0-test → semver 预发布 < 正式版，不应提示更新
        var json = """
            [{"tag_name":"v2.0.0-test","draft":false,"prerelease":true,"body":"","assets":[
              {"name":"OpenForgeSelf-2.0.0-test-win-x64.zip","browser_download_url":"https://example.com/t.zip","digest":"sha256:aa","size":1}]}]
            """;
        SetupHttpResponse(json);
        var checker = new UpdateChecker(GitHubConfig("beta"), _httpClient, _testAppName, currentVersion: "2.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task GitHub_MissingRepo_FailsGracefully()
    {
        var config = GitHubConfig();
        config.GitHubRepo = "";
        var checker = new UpdateChecker(config, _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("GitHubRepo");
    }

    [Fact]
    public async Task GitHub_NoWinX64Asset_ReportedAsFailure()
    {
        var json = """
            [{"tag_name":"v2.0.0","draft":false,"prerelease":false,"body":"","assets":[]}]
            """;
        SetupHttpResponse(json);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        result.HasUpdate.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("win-x64");
    }

    [Fact]
    public async Task GitHub_PublicRepo_SendsNoAuthorizationHeader()
    {
        // 回归防护（2026-09-26）：更新源仓库已转公开，请求必须保持匿名。
        // 历史教训：匿名访问私有仓库时 GitHub 返回 404 而非 401，日志只显示「网络错误: 404」，
        // 无法区分「仓库不存在」与「缺 token」。若日后重新引入 Bearer，本用例必须同步变更。
        SetupHttpResponse(GitHubReleasesJson);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        await checker.CheckForUpdateAsync();

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Headers.Authorization == null),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GitHub_Download_UsesCachedAssetUrlAndWritesFile()
    {
        SetupHttpResponse(GitHubReleasesJson);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        // 先检查（缓存资产 API 直链），再下载。
        // 回归防护：下载必须走资产 API url（octet-stream 直链，公开/私有仓库均可用），
        // DisplayUrl/DownloadUrl 字段保留 browser_download_url 供人读。
        await checker.CheckForUpdateAsync();

        var destPath = Path.Combine(Path.GetTempPath(), $"forge-{Guid.NewGuid():N}.zip");
        try
        {
            await checker.DownloadPackageAsync("v2.0.0", destPath);
            File.Exists(destPath).Should().BeTrue();
            (await File.ReadAllTextAsync(destPath)).Should().Contain("OpenForgeSelf-2.0.0-win-x64.zip");
        }
        finally
        {
            if (File.Exists(destPath)) File.Delete(destPath);
        }

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.RequestUri != null && r.RequestUri.ToString() == "https://api.github.com/repos/test-owner/test-repo/releases/assets/9876543"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GitHub_Check_KeepsBrowserUrlForDisplay()
    {
        SetupHttpResponse(GitHubReleasesJson);
        var checker = new UpdateChecker(GitHubConfig(), _httpClient, _testAppName, currentVersion: "1.0.0.0");

        var result = await checker.CheckForUpdateAsync();

        // 展示链接仍是 browser_download_url（人类可点开），资产 API url 只用于下载
        result.DownloadUrl.Should().Be("https://example.com/forge.zip");
    }

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
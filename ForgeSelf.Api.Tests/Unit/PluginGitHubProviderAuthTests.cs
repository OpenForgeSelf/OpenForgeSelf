using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ForgeSelf.Api.Plugins.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>插件 GitHub 消费面凭据接线单测：绑定令牌以 Bearer 出站；未绑定/空令牌绝不带 Authorization。</summary>
public class PluginGitHubProviderAuthTests
{
    /// <summary>记录每个请求的 Authorization 头，并按 URL 路由假响应。</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Requests { get; } = new();
        private readonly Dictionary<string, (int Status, string Body, byte[]? Bytes)> _routes;

        public RecordingHandler(Dictionary<string, (int, string, byte[]?)> routes) => _routes = routes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add((url, request.Headers.Authorization?.ToString()));
            var (status, body, bytes) = _routes.TryGetValue(url, out var r) ? r : (404, "{}", null);
            var resp = new HttpResponseMessage((HttpStatusCode)status);
            resp.Content = bytes != null
                ? new ByteArrayContent(bytes)
                : new StringContent(body, Encoding.UTF8, "application/json");
            return Task.FromResult(resp);
        }
    }

    private const string ReleaseUrl = "https://api.github.com/repos/owner/repo/releases/latest";
    private const string ZipUrl = "https://github.com/owner/repo/releases/download/v1.0.0/Plugin.X-1.0.0.zip";
    private const string SumsUrl = "https://github.com/owner/repo/releases/download/v1.0.0/SHA256SUMS.txt";

    private static (RecordingHandler Handler, string ZipName, byte[] ZipBytes) HappyRelease()
    {
        var zip = Encoding.UTF8.GetBytes("fake-plugin-zip");
        var hash = Convert.ToHexString(SHA256.HashData(zip));
        var release = $$"""
            {"assets":[{"name":"Plugin.X-1.0.0.zip","browser_download_url":"{{ZipUrl}}"},{"name":"SHA256SUMS.txt","browser_download_url":"{{SumsUrl}}"}]}
            """;
        var sums = $"{hash}  Plugin.X-1.0.0.zip\n";
        var handler = new RecordingHandler(new()
        {
            [ReleaseUrl] = (200, release, null),
            [ZipUrl] = (200, "", zip),
            [SumsUrl] = (200, sums, null),
        });
        return (handler, "Plugin.X-1.0.0.zip", zip);
    }

    [Fact]
    public async Task WithBoundToken_AttachesBearerHeader()
    {
        var (handler, name, zip) = HappyRelease();
        var provider = new PluginGitHubProvider(new HttpClient(handler), null, () => "gho_TOKEN");
        var (bytes, fileName) = await provider.DownloadLatestAsync("https://github.com/owner/repo", CancellationToken.None);
        fileName.Should().Be(name);
        bytes.Should().Equal(zip);
        handler.Requests.Should().OnlyContain(r => r.Authorization == "Bearer gho_TOKEN");
    }

    [Fact]
    public async Task WithoutBinding_NoAuthorizationHeader()
    {
        var (handler, _, _) = HappyRelease();
        var provider = new PluginGitHubProvider(new HttpClient(handler), null, null);
        await provider.DownloadLatestAsync("https://github.com/owner/repo", CancellationToken.None);
        handler.Requests.Should().OnlyContain(r => r.Authorization == null);
    }

    [Fact]
    public async Task NullOrEmptyToken_NoAuthorizationHeader()
    {
        var (handler, _, _) = HappyRelease();
        var provider = new PluginGitHubProvider(new HttpClient(handler), null, () => null);
        await provider.DownloadLatestAsync("https://github.com/owner/repo", CancellationToken.None);

        var handler2 = HappyRelease().Handler;
        var provider2 = new PluginGitHubProvider(new HttpClient(handler2), null, () => "");
        await provider2.DownloadLatestAsync("https://github.com/owner/repo", CancellationToken.None);

        handler.Requests.Should().OnlyContain(r => r.Authorization == null);
        handler2.Requests.Should().OnlyContain(r => r.Authorization == null);
    }

    [Fact]
    public async Task SumsMismatch_Throws()
    {
        var zip = Encoding.UTF8.GetBytes("fake-plugin-zip");
        var release = $$"""
            {"assets":[{"name":"Plugin.X-1.0.0.zip","browser_download_url":"{{ZipUrl}}"},{"name":"SHA256SUMS.txt","browser_download_url":"{{SumsUrl}}"}]}
            """;
        var handler = new RecordingHandler(new()
        {
            [ReleaseUrl] = (200, release, null),
            [ZipUrl] = (200, "", zip),
            [SumsUrl] = (200, "deadbeef  Plugin.X-1.0.0.zip\n", null),
        });
        var provider = new PluginGitHubProvider(new HttpClient(handler));
        var act = () => provider.DownloadLatestAsync("https://github.com/owner/repo", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidDataException>().WithMessage("*SHA256SUMS*");
    }

    [Fact]
    public void Parse_RejectsNonGithubUrl()
    {
        var act = () => PluginGitHubProvider.Parse("https://gitlab.com/owner/repo");
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_StripsGitSuffix()
    {
        var (owner, repo) = PluginGitHubProvider.Parse("https://github.com/owner/repo.git");
        owner.Should().Be("owner");
        repo.Should().Be("repo");
    }
}

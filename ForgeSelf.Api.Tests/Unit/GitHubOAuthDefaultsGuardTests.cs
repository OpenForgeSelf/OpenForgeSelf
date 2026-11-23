using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>GitHub OAuth 配置守卫（O2 内置 client_id）：默认端点/作用域锚定 + appsettings 内置值防丢。</summary>
public class GitHubOAuthDefaultsGuardTests
{
    private const string EmbeddedClientId = "Ov23likdtDN2AlQvAO33";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("未定位到仓库根（缺 ForgeSelf.slnx）");
    }

    [Fact]
    public void FromConfiguration_EmptySection_FallsBackToGithubDefaults()
    {
        var config = GitHubOAuthConfig.FromConfiguration(new ConfigurationBuilder().Build());
        config.DeviceCodeUrl.Should().Be("https://github.com/login/device/code");
        config.TokenUrl.Should().Be("https://github.com/login/oauth/access_token");
        config.ApiBase.Should().Be("https://api.github.com");
        config.Scopes.Should().Be("repo workflow");
        config.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void FromConfiguration_WithClientId_IsConfigured()
    {
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHubOAuth:ClientId"] = EmbeddedClientId })
            .Build();
        var config = GitHubOAuthConfig.FromConfiguration(section);
        config.ClientId.Should().Be(EmbeddedClientId);
        config.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Appsettings_EmbedsGitHubOAuthClientId()
    {
        var path = Path.Combine(RepoRoot(), "ForgeSelf.Api", "appsettings.json");
        File.Exists(path).Should().BeTrue($"appsettings.json 应存在于 {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        doc.RootElement.TryGetProperty("GitHubOAuth", out var section).Should().BeTrue("appsettings.json 必须含 GitHubOAuth 节（内置 client_id，O2 批）");
        section.TryGetProperty("ClientId", out var clientId).Should().BeTrue();
        clientId.GetString().Should().Be(EmbeddedClientId, "内置 OAuth App 的 client_id 不得丢失或被改");
    }

    [Fact]
    public void Appsettings_GitHubOAuthScopes_ContainRepoAndWorkflow()
    {
        var path = Path.Combine(RepoRoot(), "ForgeSelf.Api", "appsettings.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var scopes = doc.RootElement.GetProperty("GitHubOAuth").GetProperty("Scopes").GetString();
        scopes.Should().Contain("repo").And.Contain("workflow");
    }
}

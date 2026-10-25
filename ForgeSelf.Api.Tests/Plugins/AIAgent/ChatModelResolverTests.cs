using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// 对话模型解析链守卫（2026-10-25）：内置执行/Run 恢复不再硬编码模型名。
/// 判据 = 四条优先级 + 「全不命中返回 null（调用方必须明确失败，禁止静默降级）」。
/// </summary>
public class ChatModelResolverTests
{
    private static ChatModelResolver New(string? configuredModel, params ModelIdentityDto[] catalog)
    {
        var ctx = new Context();

        var telemetry = new Mock<ITurnTelemetryQuery>();
        telemetry.Setup(x => x.GetModelCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog.ToList());

        var config = new Mock<IConfigurationService>();
        config.Setup(x => x.GetAIConfig()).Returns(new AIConfig { ModelName = configuredModel ?? string.Empty });

        ctx.Register<ITurnTelemetryQuery>(telemetry.Object);
        ctx.Register<IConfigurationService>(config.Object);
        return new ChatModelResolver(ctx);
    }

    private static ModelIdentityDto Model(string chatModelId, string provider = "default", string upstream = "", string alias = "")
        => new() { ChatModelId = chatModelId, ProviderName = provider, UpstreamModelId = upstream, Alias = alias };

    [Fact]
    public async Task 显式指定模型原样返回_不查目录()
    {
        var sut = New("in-catalog-model", Model("in-catalog-model"));

        var resolved = await sut.ResolveAsync("other-provider:some-model");

        Assert.Equal("other-provider:some-model", resolved);
    }

    [Fact]
    public async Task 宿主默认模型命中目录时补provider前缀()
    {
        var sut = New("DeepSeek-V4-Flash", Model("qwen3_8_vision"), Model("deepseek-v4-flash", upstream: "deepseek-v4-flash"));

        var resolved = await sut.ResolveAsync(null);

        Assert.Equal("default:deepseek-v4-flash", resolved);
    }

    [Fact]
    public async Task 宿主默认模型不在目录时改用目录首个启用模型_不静默套用()
    {
        var sut = New("qwythos-9b-v2", Model("qwen3_8_vision"), Model("deepseek-v4-flash"));

        var resolved = await sut.ResolveAsync(null);

        Assert.Equal("default:qwen3_8_vision", resolved);
    }

    [Fact]
    public async Task 启用目录为空返回null_调用方必须明确失败()
    {
        var sut = New("qwythos-9b-v2");

        var resolved = await sut.ResolveAsync(null);

        Assert.Null(resolved);
    }
}

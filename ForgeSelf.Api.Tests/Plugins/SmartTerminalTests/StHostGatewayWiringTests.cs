using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>
/// StHostGateway 接线级回归（2026-11-14 输入18/19，PC-034）。
/// <para>
/// 缺陷：StHostGateway 构造函数注入 <see cref="IServiceProvider"/>，而它注册在 smart-terminal
/// 插件子 DI 容器（SmartTerminalPlugin.Apply → ctx.Get&lt;IServiceCollection&gt;()）——MS DI 对
/// IServiceProvider 自解析 = 子容器本体，对其 CreateHostScope() 只会回到子容器，
/// 宿主契约（IChatCompletion / ISecretEncryptionService）恒解析为 null → 助手/凭据恒 424。
/// </para>
/// <para>
/// 本测试按真实宿主装配复刻接线：根 Context（seed 宿主根 provider）+ AIAgent 式共享表注册 +
/// 插件子容器（PluginManager.cs:781 姿势：AddSingleton&lt;IContext&gt;(fiber.Context)），
/// 从子容器解析 IStHostGateway 后断言能拿到契约。mock 掉的是模型调用，不是 DI 接线。
/// </para>
/// </summary>
public class StHostGatewayWiringTests
{
    private sealed class FakeChatCompletion : IChatCompletion
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatCompletionResult { SessionId = request.SessionId ?? "fake", Content = "ok", Success = true });
    }

    private sealed class FakeSecretEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => "v2:fake";
        public string Decrypt(string cipherText) => "plain";
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = "plain"; return true; }
        public string Mask(string plainText) => "sk-****";
    }

    /// <summary>按真实宿主装配：返回 (root Context, 插件子 provider, 注册的 chat 实例)。调用方 dispose 两个容器。</summary>
    private static (Context Root, ServiceProvider PluginProvider, FakeChatCompletion Chat) BuildRealWiring()
    {
        var root = new Context();
        var pluginCtx = root.Derive();

        // 宿主根 provider：ISecretEncryptionService 在宿主 DI 注册（AesSecretEncryptionService，真宿主形态）
        var host = new ServiceCollection()
            .AddSingleton<ISecretEncryptionService>(new FakeSecretEncryption())
            .BuildServiceProvider();

        // PluginManager.ProvideHostServices 的 seed 姿势：宿主根 provider + 逐契约解析实例进 root 共享表
        root.Register(typeof(IServiceProvider), host);
        root.Register(typeof(ISecretEncryptionService), host.GetRequiredService<ISecretEncryptionService>());

        // AIAgent 插件侧注册姿势（AIAgentChatCompletion.RegisterChatCompletion：ctx.Register 进共享表）
        var chat = new FakeChatCompletion();
        root.Register(typeof(IChatCompletion), chat);

        // 插件子 DI 容器（PluginManager.MountPlugin：AddSingleton<IContext>(fiber.Context) + 插件自注册）
        var child = new ServiceCollection();
        child.AddSingleton<IContext>(pluginCtx);
        child.AddSingleton<IStHostGateway, StHostGateway>();
        var provider = child.BuildServiceProvider();

        return (root, provider, chat);
    }

    [Fact]
    public void TryChat_Resolves_Host_Contract_Through_Real_Plugin_Wiring()
    {
        var (root, provider, chat) = BuildRealWiring();
        using var _ = root;
        using var __ = provider;

        // 阳性对照（隔离诊断）：共享表注册侧是通的——插件 ctx 直取能拿到契约
        var pluginCtx = provider.GetRequiredService<IContext>();
        pluginCtx.Get<IChatCompletion>().Should().BeSameAs(chat,
            "AIAgent 注册进共享表的 IChatCompletion 必须经插件 ctx 父链可见；此断言失败说明装配复刻有误而非产品缺陷");

        // 被测行为：从插件子容器解析出的 gateway 必须能拿到同一契约实例
        var gateway = provider.GetRequiredService<IStHostGateway>();
        gateway.TryChat().Should().BeSameAs(chat,
            "StHostGateway 经 IContext 通路（共享表/宿主 seed）解析宿主契约，而不是经插件子容器自解析");
    }

    [Fact]
    public void TrySecrets_Resolves_Host_Contract_Through_Real_Plugin_Wiring()
    {
        var (root, provider, _) = BuildRealWiring();
        using var _ = root;
        using var __ = provider;

        var gateway = provider.GetRequiredService<IStHostGateway>();
        gateway.TrySecrets().Should().NotBeNull(
            "ISecretEncryptionService 由宿主 ProvideHostServices seed 进共享表，插件侧必须经 IContext 解析到");
    }

    [Fact]
    public void TryChat_Missing_Contract_Returns_Null_Keeps_424_Graceful_Contract()
    {
        // ai-agent 缺席/禁用场景：共享表无 IChatCompletion → null（TerminalSessionService 据此回 424，会话不受影响）
        using var root = new Context();
        var pluginCtx = root.Derive();

        var child = new ServiceCollection();
        child.AddSingleton<IContext>(pluginCtx);
        child.AddSingleton<IStHostGateway, StHostGateway>();
        using var provider = child.BuildServiceProvider();

        var gateway = provider.GetRequiredService<IStHostGateway>();
        gateway.TryChat().Should().BeNull("契约缺席时保持 null → 上层 424 host_module_unavailable（03-架构与接口.md §9）");
        gateway.TrySecrets().Should().BeNull("同上：宿主未 seed 凭据加密契约时保持 null");
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>执行期解析宿主加密与对话网关，构造函数不注入宿主契约。</summary>
public interface IStHostGateway
{
    ISecretEncryptionService? TrySecrets();
    IChatCompletion? TryChat();
}

/// <summary>
/// 经插件 <see cref="IContext"/> 解析宿主契约（共享表/宿主 seed，PC-034 canonical fix）。
/// 历史缺陷（2026-11-14 输入18 实证）：本类曾注入 <see cref="IServiceProvider"/>——插件子 DI
/// 容器对它 MS 自解析 = 子容器本体，<c>CreateHostScope()</c> 回到子容器，
/// 宿主契约（IChatCompletion/ISecretEncryptionService 不在子容器）恒解析为 null → 助手/凭据恒 424。
/// 宿主契约只能经 IContext 通路拿：IChatCompletion 由 ai-agent 注册进共享表
/// （AIAgentChatCompletion.RegisterChatCompletion），ISecretEncryptionService 由宿主
/// ProvideHostServices seed（PluginManager.HostProvidedServiceContracts）。
/// </summary>
public sealed class StHostGateway : IStHostGateway
{
    private readonly IContext _ctx;

    public StHostGateway(IContext ctx) => _ctx = ctx;

    public ISecretEncryptionService? TrySecrets() => _ctx.Get<ISecretEncryptionService>();

    public IChatCompletion? TryChat() => _ctx.Get<IChatCompletion>();
}

using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>执行期解析宿主加密与对话网关，构造函数不注入宿主契约。</summary>
public interface IStHostGateway
{
    ISecretEncryptionService? TrySecrets();
    IChatCompletion? TryChat();
}

public sealed class StHostGateway : IStHostGateway
{
    private readonly IServiceProvider _services;

    public StHostGateway(IServiceProvider services) => _services = services;

    public ISecretEncryptionService? TrySecrets() => TryGet<ISecretEncryptionService>();

    public IChatCompletion? TryChat() => TryGet<IChatCompletion>();

    private T? TryGet<T>() where T : class
    {
        try
        {
            using var scope = _services.CreateHostScope();
            return scope.ServiceProvider.GetService<T>();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[smart-terminal] 宿主模块 {0} 不可用: {1}", typeof(T).Name, ex.Message);
            return null;
        }
    }
}

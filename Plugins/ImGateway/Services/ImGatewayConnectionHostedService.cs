using Microsoft.Extensions.Hosting;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 把长连接通道的生命周期挂到宿主（ASP.NET Core）上：
/// 宿主启动完成 → 拉起长连接；宿主停止 → 断开长连接。
///
/// 注意：这里**不注入 IHostApplicationLifetime**。插件的 DI 接缝（IContext.Get&lt;IServiceCollection&gt;）
/// 在集成测试（WebApplicationFactory）等场景下解析不到该服务，注入会导致宿主启动直接抛
/// "Unable to resolve service for type IHostApplicationLifetime"，拖垮所有集成测试。
/// 停止时机由 StopAsync 保证，无需额外令牌。
/// 回调型通道不经过这里（管理器只挑 WebSocket 形态）。
/// </summary>
public class ImGatewayConnectionHostedService : IHostedService
{
    private readonly ImGatewayConnectionManager _manager;

    public ImGatewayConnectionHostedService(ImGatewayConnectionManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// 拉起长连接。不把 cancellationToken 作为宿主停止令牌传入——它是「启动超时」令牌，
    /// 一旦取消会误停刚建好的长连接；停止一律交给 <see cref="StopAsync"/>。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _manager.StartAll();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _manager.StopAll();
        return Task.CompletedTask;
    }
}

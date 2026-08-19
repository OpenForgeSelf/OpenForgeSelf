using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// MVC ActionDescriptor 变更通知提供者（动态端点移除用）。
/// 注册为宿主 DI 单例 <see cref="IActionDescriptorChangeProvider"/>：ASP.NET Core 的
/// <c>ApplicationPartManager.ApplicationParts</c> 集合变更本身不会自动触发 ActionDescriptor 重建
/// （框架源码确认：<c>DefaultActionDescriptorCollectionProvider</c> 仅响应注入的
/// <see cref="IActionDescriptorChangeProvider"/>，<c>ApplicationPartManager</c> 无内置 ChangeToken），
/// 因此在卸载插件移除其 <c>AssemblyPart</c> 后，由 <see cref="PluginManager"/> 调用
/// <see cref="NotifyChange"/> 主动通知刷新，使已卸载插件的控制器端点从路由中消失。
/// </summary>
/// <remarks>
/// 实现要点（避免回调重订阅死循环）：
/// <c>ChangeToken.OnChange</c> 在 token 触发回调时会先取新 token、执行消费者、再用新 token 重订阅；
/// 若「先取消、后换新 CTS」，重订阅会拿到已取消的 token，导致回调被立即再次触发（递归）。
/// 因此本实现严格「先换新 CTS、再取消旧 CTS」：回调内取到的总是未取消的新 token。
/// </remarks>
public sealed class MvcActionDescriptorChangeProvider : IActionDescriptorChangeProvider
{
    private readonly object _lock = new();
    private CancellationTokenSource _cts = new();

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        lock (_lock)
        {
            return new CancellationChangeToken(_cts.Token);
        }
    }

    /// <summary>
    /// 通知 ActionDescriptor 集合已失效，触发框架按当前 ApplicationPartManager 重建。
    /// 幂等：无注册监听者时安全无操作。
    /// </summary>
    public void NotifyChange()
    {
        CancellationTokenSource old;
        lock (_lock)
        {
            old = _cts;
            _cts = new CancellationTokenSource();
        }

        old.Cancel();
    }
}

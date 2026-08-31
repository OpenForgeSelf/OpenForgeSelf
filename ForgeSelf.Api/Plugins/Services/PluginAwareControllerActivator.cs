using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>
/// 动态端点插件控制器激活器（解决热更新后插件端点 500）。
/// <para>
/// 背景：插件控制器（如 sems 的 ProjectsController）的构造依赖（如 <c>IProjectService</c>）
/// 定义在插件程序集内部。宿主 Build 时经 <see cref="IPluginServiceRegistry.CollectForwardDescriptors"/>
/// 捕获的转发描述符绑定的是<b>当时</b>加载的程序集类型身份；插件热更新（Disable→重载新版）后
/// 新程序集重建了同一接口的<b>新类型身份</b>，宿主 DI 仍用旧身份描述符去解析 → Resolve 失败 → 500。
/// </para>
/// <para>
/// 本激活器<b>优先</b>从插件可变注册表实时 <see cref="IPluginServiceRegistry.Resolve"/> 控制器类型：
/// 注册表按「当前挂载的程序集」索引，重载后即指向新类型身份，故不依赖宿主 DI 的陈旧转发描述符。
/// 未命中（宿主原生控制器）回退从宿主请求服务按构造参数激活（与 MVC 默认行为一致）。
/// </para>
/// </summary>
public class PluginAwareControllerActivator : IControllerActivator
{
    private readonly IPluginServiceRegistry _registry;
    private readonly IServiceProvider _services;

    public PluginAwareControllerActivator(IPluginServiceRegistry registry, IServiceProvider services)
    {
        _registry = registry;
        _services = services;
    }

    /// <inheritdoc />
    public object Create(ControllerContext context)
    {
        var controllerType = context.ActionDescriptor.ControllerTypeInfo.AsType();

        // 插件控制器：经由插件子 provider 实时解析（含其服务依赖 + 重载后的新类型身份）。
        var resolved = _registry.Resolve(controllerType);
        if (resolved != null)
        {
            return resolved;
        }

        // 宿主原生控制器：与 MVC 默认行为一致，从宿主请求服务按构造参数解析激活。
        return ActivatorUtilities.CreateInstance(_services, controllerType);
    }

    /// <inheritdoc />
    public void Release(ControllerContext context, object controller)
    {
        // 插件控制器由子 provider 每次新建（Transient），完成后释放即可；宿主控制器亦由 Disposable 释放。
        if (controller is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
        }
        else if (controller is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
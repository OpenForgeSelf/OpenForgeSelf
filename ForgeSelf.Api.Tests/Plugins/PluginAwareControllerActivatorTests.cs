using System;
using System.Reflection;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// 回归保护：宿主原生控制器依赖 scoped 服务时，<see cref="PluginAwareControllerActivator"/>
/// 必须经由 <see cref="HttpContext.RequestServices"/>（request scope）激活，而非注入的 root provider。
/// 此前 fallback 用 root provider 激活 <c>AIModelController</c>/<c>ChatController</c>（依赖
/// <c>IAIModelService</c>/<c>ILogService</c>）会抛「Cannot resolve scoped service ... from root provider」→ 500。
/// 本测试锁定修复（输入 N+7 / N+8 / N+11），防止作用域 500 复发。
/// </summary>
public class PluginAwareControllerActivatorTests
{
    private interface IScopedDep
    {
    }

    private sealed class ScopedDep : IScopedDep
    {
    }

    private sealed class HostController : ControllerBase
    {
        public HostController(IScopedDep dep) => Dep = dep;

        public IScopedDep Dep { get; }
    }

    [Fact]
    public void Create_HostControllerWithScopedDependency_ResolvesFromRequestScope_NotRoot()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedDep, ScopedDep>();
        var root = services.BuildServiceProvider();
        var requestScope = root.CreateScope();

        // registry 为空 → 宿主控制器不会命中插件索引，必然走 request-scope fallback 分支。
        var activator = new PluginAwareControllerActivator(new PluginServiceRegistry(), root);

        var controllerType = typeof(HostController);
        var actionDescriptor = new ControllerActionDescriptor { ControllerTypeInfo = controllerType.GetTypeInfo() };
        var httpContext = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider };
        var controllerContext = new ControllerContext { HttpContext = httpContext, ActionDescriptor = actionDescriptor };

        var instance = activator.Create(controllerContext);

        var controller = Assert.IsType<HostController>(instance);
        Assert.NotNull(controller.Dep);
        // 必须来自请求 scope（request scope 内能解析 scoped），而非 root provider——证明修复生效、无 500 风险。
        Assert.Same(controller.Dep, requestScope.ServiceProvider.GetRequiredService<IScopedDep>());
    }
}

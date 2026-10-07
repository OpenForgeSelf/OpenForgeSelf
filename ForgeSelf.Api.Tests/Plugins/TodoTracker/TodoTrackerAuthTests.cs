using System.Reflection;
using ForgeSelf.Api.Plugins.TodoTracker.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 管理面鉴权守卫（PILOT-054 · FR-8.1 / AC-13，plugin-development 铁律 17）。
///
/// 宿主没有全局鉴权中间件，鉴权是<b>逐控制器显式</b>的 —— 漏一个就是"不带 token 也能改任务台账"。
/// 所以这里不列白名单，而是<b>扫插件程序集里所有控制器</b>：以后新加控制器自动被这条用例管住。
/// 形状参照 <c>Plugins/McpCenterTests/McpAdminAuthTests.cs</c>，覆盖面严一档（那边是显式清单）。
/// </summary>
public class TodoTrackerAuthTests
{
    private const string ApiKeyPolicy = "ApiKeyPolicy";

    /// <summary>本插件对外暴露的全部控制器。新增时必须同时登记进来，否则下一条用例会红。</summary>
    private static readonly Type[] ExpectedControllers =
    [
        typeof(TodosController),
        typeof(TodoProjectsController),
        typeof(TodoArtifactsController),
        typeof(TaskExecutionsController),
        typeof(TodoDispatchController)
    ];

    private static Assembly PluginAssembly => typeof(TodosController).Assembly;

    [Theory]
    [MemberData(nameof(AllControllers))]
    public void 全部控制器都必须类级带_ApiKeyPolicy_策略(Type controllerType)
    {
        var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        // 裸 [Authorize] 不算：宿主默认策略不是 ApiKeyPolicy，必须显式点名策略
        Assert.NotNull(attr);
        Assert.Equal(ApiKeyPolicy, attr!.Policy);
    }

    [Fact]
    public void 程序集里的控制器清单应与登记清单一一对应()
    {
        var found = AllControllerTypes();

        // 阳性对照：扫描本身要扫得到东西，否则"全都带鉴权"是空转
        Assert.True(found.Count >= ExpectedControllers.Length,
            $"至少应扫到 {ExpectedControllers.Length} 个控制器，实际 {found.Count} 个");
        Assert.Empty(ExpectedControllers.Except(found));
        Assert.Empty(found.Except(ExpectedControllers));
    }

    public static IEnumerable<object[]> AllControllers() =>
        AllControllerTypes().Select(t => new object[] { t });

    private static List<Type> AllControllerTypes() =>
        PluginAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();
}

using OpenForgeSelf.Backend.Tests.E2E.ApiClient;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpenForgeSelf.Backend.Tests.E2E.Tests;

/// <summary>
/// E2E 测试基类，提供 API 客户端和测试基础设施
/// </summary>
public class E2ETestBase : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;

    protected AIChatClient ChatClient { get; }
    protected SchedulerClient SchedulerClient { get; }
    protected WorkflowClient WorkflowClient { get; }

    public E2ETestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // E2E 测试使用真实服务配置
                ConfigureE2EServices(services);
            });
        });

        Client = Factory.CreateClient();

        ChatClient = new AIChatClient(Client);
        SchedulerClient = new SchedulerClient(Client);
        WorkflowClient = new WorkflowClient(Client);
    }

    /// <summary>
    /// 配置 E2E 测试服务
    /// </summary>
    protected virtual void ConfigureE2EServices(IServiceCollection services)
    {
        // 默认使用真实服务，子类可重写
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 等待一段时间（用于异步操作完成）
    /// </summary>
    protected static async Task WaitForAsync(Func<Task<bool>> condition, int maxWaitMs = 30000, int intervalMs = 500)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < TimeSpan.FromMilliseconds(maxWaitMs))
        {
            if (await condition())
                return;
            await Task.Delay(intervalMs);
        }
        throw new TimeoutException($"等待条件满足超时 ({maxWaitMs}ms)");
    }

    /// <summary>
    /// 等待一段时间
    /// </summary>
    protected static async Task DelayAsync(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }
}

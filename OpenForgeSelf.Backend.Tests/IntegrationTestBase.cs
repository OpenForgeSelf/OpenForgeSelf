namespace OpenForgeSelf.Backend.Tests;

/// <summary>
/// 集成测试基础设施，使用WebApplicationFactory创建测试服务器
/// </summary>
public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly IServiceProvider Services;

    public IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 配置测试服务，例如内存数据库
                ConfigureTestServices(services);
            });
        });

        Client = Factory.CreateClient();
        Services = Factory.Services;
    }

    /// <summary>
    /// 配置测试服务，子类可以重写此方法来自定义测试服务
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // 默认不添加任何服务，子类可以重写
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
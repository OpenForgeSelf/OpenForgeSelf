using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ScriptRunner.Models;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 回归（TODO 输入1·2026-09-20）：ScriptRunner 插件控制器经插件子 provider 激活时，
/// 宿主契约 IRuntimeDetector 必须经 Cordis 上下文（ctx.Get&lt;T&gt;()）在运行期解析，
/// 而不能在插件 Service/Controller 构造函数里直接注入——插件子 provider 只承载插件自有服务
/// （含 IContext），不含宿主契约，构造注入会抛
/// 「Unable to resolve service for type 'ForgeSelf.Abstractions.IRuntimeDetector'
/// while attempting to activate '...ScriptExecutor'」→ 500。
/// 本用例经真实 WebApplicationFactory 走完整 DI 链路，锁定该契约。
/// </summary>
public class ScriptRunnerDiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ScriptRunnerDiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // 与既有 IntegrationTests 一致：替换 AI 服务为模拟实现，避免测试触碰真实 AI 端点。
                var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIService));
                if (aiDescriptor != null)
                {
                    services.Remove(aiDescriptor);
                }
                services.AddSingleton<IAIService>(new MockAIServiceForIntegration());
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetRuntimes_ShouldResolvePluginControllerThroughCordisContext()
    {
        // Act：控制器激活即触发 ScriptExecutor 构造；修复前在此抛 DI 解析异常 → 500。
        var response = await _client.GetAsync("/api/scripts/runtimes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RuntimeEnvironment>>>();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }
}

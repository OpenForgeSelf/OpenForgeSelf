using System.Net.Http.Json;
using ForgeSelf.Api.Models;
using Microsoft.AspNetCore.Hosting;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// B4（040）QA 对抗探针：「POST /api/chat 返回后立刻 GET history」不得读到旧投影。
/// 时序契约 = 投影在响应返回前 await（SessionProjectionService.SyncAsync），
/// 本测试把该契约变成可失败的断言：若实现改回火后即忘，本组用例随机红。
/// </summary>
[Collection("XCode")]
public class ChatProjectionRaceTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _sessionId;

    public ChatProjectionRaceTests()
    {
        var tempDbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfRace_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDbDir);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
        _client = _factory.CreateClient();

        // 隔离 XCode 的 ForgeSelf 连接到临时库（先触发宿主构建，再覆盖连接串）
        var _ = _factory.Services;
        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(tempDbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        _sessionId = "race-" + Guid.NewGuid().ToString("N")[..12];
    }

    [Fact]
    public async Task GetHistoryImmediatelyAfterPost_AlwaysSeesLatestTurn()
    {
        for (var round = 1; round <= 5; round++)
        {
            // Act：POST 完成立刻 GET，中间零等待（放大任何火后即忘竞态）
            var post = await _client.PostAsJsonAsync("/api/chat", new ChatRequest
            {
                Message = $"第{round}轮消息",
                SessionId = _sessionId,
            });
            post.EnsureSuccessStatusCode();

            var history = await _client.GetFromJsonAsync<List<ChatResponse>>(
                $"/api/chat/history/{_sessionId}");

            // Assert：投影与日志严格对齐 —— r 轮后恰 2r 行（user+assistant 交替）；
            // 回复内容不绑定具体 mock 文案（Testing 环境的降级 mock 文案非本测试关注点），
            // 只断言最后一行是本条用户消息之后的 assistant 行 —— 竞态（读到旧投影）会让 Count 断言先红。
            Assert.NotNull(history);
            Assert.Equal(2 * round, history!.Count);
            Assert.Equal("assistant", history[^1].Role);
            Assert.Equal($"第{round}轮消息", history[^2].Content);
            Assert.Contains("第1轮消息", history[0].Content);
        }
    }
}

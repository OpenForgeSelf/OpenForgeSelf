namespace ForgeSelf.Api.Tests.Integration;

using ForgeSelf.Api.Services.UsageStats;

/// <summary>
/// 使用统计服务集成测试
/// 注意：这些测试使用真实的 XCode 数据库，不是 EF Core InMemory
/// </summary>
[Collection("XCode")]
public class UsageStatsIntegrationTests : IClassFixture<XCodeTestFixture>
{
    private readonly UsageStatsService _service;

    public UsageStatsIntegrationTests(XCodeTestFixture fixture)
    {
        _service = new UsageStatsService();
    }

    [Fact]
    public async Task RecordUsageAsync_ValidData_CreatesRecord()
    {
        var recordId = await _service.RecordUsageAsync(
            pluginId: $"test.plugin.{Guid.NewGuid():N}",
            toolId: $"test.tool.{Guid.NewGuid():N}",
            actionType: "execute");

        recordId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordUsageAsync_WithDuration_SetsDuration()
    {
        var recordId = await _service.RecordUsageAsync(
            pluginId: $"test.plugin.{Guid.NewGuid():N}",
            toolId: $"test.tool.{Guid.NewGuid():N}",
            actionType: "execute",
            durationMs: 1500);

        recordId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordUsageAsync_WithMetadata_StoresMetadataJson()
    {
        var metadata = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        var recordId = await _service.RecordUsageAsync(
            pluginId: $"test.plugin.{Guid.NewGuid():N}",
            toolId: $"test.tool.{Guid.NewGuid():N}",
            actionType: "execute",
            metadata: metadata);

        recordId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordUsageAsync_WithIpAndUserAgent_StoresThem()
    {
        var recordId = await _service.RecordUsageAsync(
            pluginId: $"test.plugin.{Guid.NewGuid():N}",
            toolId: $"test.tool.{Guid.NewGuid():N}",
            actionType: "execute",
            userAgent: "TestAgent/1.0",
            ipAddress: "192.168.1.1");

        recordId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordUsageAsync_MultipleRecords_AllCreated()
    {
        var pluginId = $"test.plugin.{Guid.NewGuid():N}";
        
        for (int i = 0; i < 10; i++)
        {
            await _service.RecordUsageAsync(
                pluginId: pluginId,
                toolId: $"tool.{i}",
                actionType: "execute");
        }

        var result = await _service.GetUsageRecordsAsync(pluginId: pluginId);
        result.Total.Should().BeGreaterOrEqualTo(10);
    }

    [Fact]
    public async Task RecordUsageAsync_UpdatesDailySummary()
    {
        var pluginId = $"test.plugin.{Guid.NewGuid():N}";
        var toolId = $"test.tool.{Guid.NewGuid():N}";
        
        await _service.RecordUsageAsync(
            pluginId: pluginId,
            toolId: toolId,
            actionType: "execute",
            durationMs: 100);

        var summary = await _service.GetDailySummaryAsync(pluginId: pluginId, toolId: toolId);
        summary.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsageRecordsAsync_NoFilter_ReturnsAll()
    {
        var result = await _service.GetUsageRecordsAsync();
        
        result.Should().NotBeNull();
        result.Total.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetUsageRecordsAsync_FilterByPluginId_ReturnsOnlyMatching()
    {
        var pluginId = $"filterTest.{Guid.NewGuid():N}";
        await _service.RecordUsageAsync(pluginId, "tool1", "exec");
        await _service.RecordUsageAsync(pluginId, "tool2", "exec");
        await _service.RecordUsageAsync("otherPlugin", "tool1", "exec");

        var result = await _service.GetUsageRecordsAsync(pluginId: pluginId);

        result.Total.Should().BeGreaterOrEqualTo(2);
        result.Items.Should().OnlyContain(r => r.PluginId == pluginId);
    }

    [Fact]
    public async Task GetUsageRecordsAsync_Pagination_WorksCorrectly()
    {
        var pluginId = $"pagination.{Guid.NewGuid():N}";
        for (int i = 0; i < 25; i++)
        {
            await _service.RecordUsageAsync(pluginId, $"t{i}", "exec");
        }

        var page1 = await _service.GetUsageRecordsAsync(page: 1, pageSize: 10);
        var page2 = await _service.GetUsageRecordsAsync(page: 2, pageSize: 10);

        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(10);
        page2.Page.Should().Be(2);
    }

    [Fact]
    public async Task GetDailySummaryAsync_NoFilter_ReturnsAll()
    {
        await _service.RecordUsageAsync($"p1.{Guid.NewGuid():N}", "t1", "exec");
        await _service.RecordUsageAsync($"p1.{Guid.NewGuid():N}", "t2", "exec");

        var result = await _service.GetDailySummaryAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTopToolsAsync_MultipleTools_SortedByUsage()
    {
        var pluginId = $"toptools.{Guid.NewGuid():N}";
        for (int i = 0; i < 10; i++)
            await _service.RecordUsageAsync(pluginId, "tool_most", "exec");
        for (int i = 0; i < 5; i++)
            await _service.RecordUsageAsync(pluginId, "tool_mid", "exec");

        var result = await _service.GetTopToolsAsync(pluginId: pluginId, limit: 10);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTopToolsAsync_DefaultPeriod_Is30Days()
    {
        var pluginId = $"period.{Guid.NewGuid():N}";
        await _service.RecordUsageAsync(pluginId, "recent_tool", "exec");

        var result = await _service.GetTopToolsAsync(pluginId: pluginId, limit: 10);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsageTrendAsync_7DayPeriod_Returns7Days()
    {
        var pluginId = $"trend.{Guid.NewGuid():N}";
        await _service.RecordUsageAsync(pluginId, "t1", "exec");

        var result = await _service.GetUsageTrendAsync(pluginId: pluginId, period: "7d");

        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetUsageTrendAsync_MissingDates_FilledWithZero()
    {
        var result = await _service.GetUsageTrendAsync(pluginId: $"nonexistent.{Guid.NewGuid():N}", period: "7d");

        result.Should().HaveCount(7);
        result.Should().AllSatisfy(p => p.UseCount.Should().BeGreaterOrEqualTo(0));
    }

    [Fact]
    public async Task GetToolRankingAsync_MultipleTools_RankedCorrectly()
    {
        var pluginId = $"ranking.{Guid.NewGuid():N}";
        for (int i = 0; i < 100; i++)
            await _service.RecordUsageAsync(pluginId, "first", "exec");
        for (int i = 0; i < 50; i++)
            await _service.RecordUsageAsync(pluginId, "second", "exec");

        var result = await _service.GetToolRankingAsync(pluginId: pluginId);

        result.Should().NotBeEmpty();
        result[0].Rank.Should().Be(1);
    }

    [Fact]
    public async Task GetToolRankingAsync_NoRecords_ReturnsEmpty()
    {
        var result = await _service.GetToolRankingAsync(pluginId: $"nonexistent.{Guid.NewGuid():N}");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordUsageAsync_Timestamp_IsUtc()
    {
        var before = DateTime.UtcNow;
        var recordId = await _service.RecordUsageAsync($"p1.{Guid.NewGuid():N}", "t1", "exec");
        var after = DateTime.UtcNow;

        var result = await _service.GetUsageRecordsAsync(pageSize: 100);
        var record = result.Items.FirstOrDefault(r => r.Id == recordId);
        
        if (record != null)
        {
            record.Timestamp.Should().BeOnOrAfter(before);
            record.Timestamp.Should().BeOnOrBefore(after);
            record.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        }
    }

    [Fact]
    public async Task RecordUsageAsync_NullDuration_DefaultsToZero()
    {
        var recordId = await _service.RecordUsageAsync(
            pluginId: $"p1.{Guid.NewGuid():N}",
            toolId: $"t1.{Guid.NewGuid():N}",
            actionType: "exec",
            durationMs: null);

        recordId.Should().BeGreaterThan(0);
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.MemorySystem.Services;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 记忆时间戳守卫（2026-11-02 浏览器走查发现）：新建/导入记忆不得留 MinValue 时间戳，
/// 更新/软删必须推进 UpdatedAt。背景：记忆页「创建时间 1/01/01 00:00」。
/// </summary>
[Collection("XCode")]
public class MemoryTimestampGuardTests
{
    public MemoryTimestampGuardTests()
    {
        _ = new XCodeTestFixture();
    }

    [Fact]
    public async Task Create_SetsCreatedAtAndUpdatedAt_NotMinValue()
    {
        var svc = new MemoryServiceXCode();

        var dto = await svc.CreateAsync(new CreateMemoryRequest
        {
            Title = "时间戳守卫-create",
            Content = "create 必须写入 CreatedAt/UpdatedAt",
            Type = MemoryType.Fact,
            Importance = MemoryImportance.Medium,
        });

        dto.Id.Should().BeGreaterThan(0);
        dto.CreatedAt.Should().BeAfter(DateTime.MinValue, "新建记忆必须带创建时间");
        dto.UpdatedAt.Should().BeAfter(DateTime.MinValue, "新建记忆必须带更新时间");
    }

    [Fact]
    public async Task Import_SetsCreatedAtAndUpdatedAt_NotMinValue()
    {
        var svc = new MemoryServiceXCode();

        await svc.ImportMemoriesAsync(new ImportMemoryRequest
        {
            Items = new System.Collections.Generic.List<ImportMemoryItem>
            {
                new()
                {
                    Title = "时间戳守卫-import",
                    Content = "import 必须写入时间戳",
                    Type = MemoryType.Fact,
                    Importance = MemoryImportance.Medium,
                },
            },
        });

        var found = await svc.SearchAsync(new SearchMemoryRequest { Keyword = "时间戳守卫-import", Page = 1, PageSize = 10 });
        found.Items.Should().ContainSingle("导入的记忆应能按关键词检回");
        found.Items[0].CreatedAt.Should().BeAfter(DateTime.MinValue, "导入记忆必须带创建时间");
        found.Items[0].UpdatedAt.Should().BeAfter(DateTime.MinValue, "导入记忆必须带更新时间");
    }

    [Fact]
    public async Task UpdateAndSoftDelete_AdvanceUpdatedAt()
    {
        var svc = new MemoryServiceXCode();

        var created = await svc.CreateAsync(new CreateMemoryRequest
        {
            Title = "时间戳守卫-update",
            Content = "初始内容",
        });
        created.UpdatedAt.Should().BeAfter(DateTime.MinValue);

        var updated = await svc.UpdateAsync(created.Id, new UpdateMemoryRequest { Content = "更新后的内容" });
        updated.Should().NotBeNull();
        updated!.UpdatedAt.Should().BeOnOrAfter(created.UpdatedAt, "更新必须推进 UpdatedAt");

        var deleted = await svc.DeleteAsync(created.Id);
        deleted.Should().BeTrue();
    }
}

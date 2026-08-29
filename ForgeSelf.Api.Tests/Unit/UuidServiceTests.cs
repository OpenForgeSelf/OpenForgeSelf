using ForgeSelf.Api.Plugins.DevTools.Models;
using ForgeSelf.Api.Plugins.DevTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class UuidServiceTests
{
    private readonly UuidService _service;

    public UuidServiceTests()
    {
        _service = new UuidService();
    }

    [Fact]
    public async Task GenerateUuidAsync_V4Version_ReturnsValidGuids()
    {
        // Act
        var result = await _service.GenerateUuidAsync("v4", 3, false, true);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be("v4");
        result.Count.Should().Be(3);
        result.Ids.Should().HaveCount(3);
        foreach (var id in result.Ids)
        {
            Guid.Parse(id).Should().NotBeEmpty();
            id.Should().Contain("-");
        }
    }

    [Fact]
    public async Task GenerateUuidAsync_V1Version_ReturnsValidGuids()
    {
        // Act
        var result = await _service.GenerateUuidAsync("v1", 3, false, true);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be("v1");
        result.Count.Should().Be(3);
        result.Ids.Should().HaveCount(3);
        foreach (var id in result.Ids)
        {
            var guid = Guid.Parse(id);
            guid.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task GenerateUuidAsync_Uppercase_ReturnsUppercaseIds()
    {
        // Act
        var result = await _service.GenerateUuidAsync("v4", 1, true, true);

        // Assert
        result.Ids[0].Should().Be(result.Ids[0].ToUpperInvariant());
    }

    [Fact]
    public async Task GenerateUuidAsync_WithoutHyphens_ReturnsIdsWithoutHyphens()
    {
        // Act
        var result = await _service.GenerateUuidAsync("v4", 1, false, false);

        // Assert
        result.Ids[0].Should().NotContain("-");
        result.Ids[0].Should().HaveLength(32);
    }

    [Fact]
    public async Task GenerateUuidAsync_InvalidVersion_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateUuidAsync("v7", 1, false, true);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*不支持的UUID版本*");
    }

    [Fact]
    public async Task GenerateUuidAsync_CountZero_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateUuidAsync("v4", 0, false, true);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*数量必须在 1-100 之间*");
    }

    [Fact]
    public async Task GenerateUuidAsync_CountExceeds100_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateUuidAsync("v4", 101, false, true);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*数量必须在 1-100 之间*");
    }

    [Fact]
    public async Task GenerateSnowflakeIdAsync_ValidParams_ReturnsValidIds()
    {
        // Act
        var result = await _service.GenerateSnowflakeIdAsync(1, 1, 3);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result.Ids.Should().HaveCount(3);
        foreach (var idInfo in result.Ids)
        {
            idInfo.Id.Should().BeGreaterThan(0);
            idInfo.WorkerId.Should().Be(1);
            idInfo.DatacenterId.Should().Be(1);
            idInfo.Timestamp.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        }
    }

    [Fact]
    public async Task GenerateSnowflakeIdAsync_InvalidWorkerId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateSnowflakeIdAsync(32, 1, 1);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*工作ID必须在 0-31 之间*");
    }

    [Fact]
    public async Task GenerateSnowflakeIdAsync_InvalidDatacenterId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateSnowflakeIdAsync(1, 32, 1);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*数据中心ID必须在 0-31 之间*");
    }

    [Fact]
    public async Task GenerateSnowflakeIdAsync_NegativeWorkerId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateSnowflakeIdAsync(-1, 1, 1);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UuidToGuidAsync_ValidUuid_ReturnsFormattedGuid()
    {
        // Arrange
        var originalGuid = Guid.NewGuid().ToString();

        // Act
        var result = await _service.UuidToGuidAsync(originalGuid, false, true);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().Be(originalGuid.ToLowerInvariant());
    }

    [Fact]
    public async Task UuidToGuidAsync_Uppercase_ReturnsUppercaseGuid()
    {
        // Arrange
        var originalGuid = Guid.NewGuid().ToString("D").ToLowerInvariant();

        // Act
        var result = await _service.UuidToGuidAsync(originalGuid, true, true);

        // Assert
        result.Result.Should().Be(originalGuid.ToUpperInvariant());
    }

    [Fact]
    public async Task UuidToGuidAsync_WithoutHyphens_ReturnsCompactGuid()
    {
        // Arrange
        var originalGuid = Guid.NewGuid().ToString("N");

        // Act
        var result = await _service.UuidToGuidAsync(originalGuid, false, false);

        // Assert
        result.Result.Should().NotContain("-");
        result.Result.Should().HaveLength(32);
    }

    [Fact]
    public async Task UuidToGuidAsync_EmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.UuidToGuidAsync("", false, true);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UUID不能为空*");
    }

    [Fact]
    public async Task UuidToGuidAsync_Whitespace_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.UuidToGuidAsync("   ", false, true);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*UUID不能为空*");
    }

    [Fact]
    public async Task UuidToGuidAsync_InvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.UuidToGuidAsync("not-a-valid-guid", false, true);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

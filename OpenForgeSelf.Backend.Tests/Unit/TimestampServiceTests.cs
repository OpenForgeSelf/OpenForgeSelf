using OpenForgeSelf.Backend.Plugins.DevTools.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class TimestampServiceTests
{
    private readonly TimestampService _service;

    public TimestampServiceTests()
    {
        _service = new TimestampService();
    }

    [Fact]
    public async Task GetCurrentTimestampAsync_ReturnsCurrentTimestamp()
    {
        // Act
        var result = await _service.GetCurrentTimestampAsync();

        // Assert
        result.Should().NotBeNull();
        result.TimestampSeconds.Should().BeGreaterThan(0);
        result.TimestampMilliseconds.Should().BeGreaterThan(0);
        result.TimestampMilliseconds.Should().BeGreaterThan(result.TimestampSeconds * 1000);
        result.DateTimeIso.Should().NotBeNullOrEmpty();
        result.DateTimeLocal.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TimestampToDateTimeAsync_SecondsTimestamp_ReturnsCorrectDateTime()
    {
        // Arrange - 2024-01-01 00:00:00 UTC 的秒时间戳
        var timestamp = 1704067200L;

        // Act
        var result = await _service.TimestampToDateTimeAsync(timestamp, "s");

        // Assert
        result.Should().NotBeNull();
        result.TimestampSeconds.Should().Be(timestamp);
        result.TimestampMilliseconds.Should().Be(timestamp * 1000);
        result.DateTimeIso.Should().Contain("2024-01-01");
    }

    [Fact]
    public async Task TimestampToDateTimeAsync_MillisecondsTimestamp_ReturnsCorrectDateTime()
    {
        // Arrange - 2024-01-01 00:00:00 UTC 的毫秒时间戳
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.TimestampToDateTimeAsync(timestamp, "ms");

        // Assert
        result.Should().NotBeNull();
        result.TimestampMilliseconds.Should().Be(timestamp);
        result.DateTimeIso.Should().Contain("2024-01-01");
    }

    [Fact]
    public async Task TimestampToDateTimeAsync_DefaultUnit_TreatsAsMilliseconds()
    {
        // Arrange
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.TimestampToDateTimeAsync(timestamp);

        // Assert
        result.TimestampMilliseconds.Should().Be(timestamp);
    }

    [Fact]
    public async Task DateTimeToTimestampAsync_ValidDateString_ReturnsTimestamp()
    {
        // Arrange
        var dateTime = "2024-01-01 12:00:00";

        // Act
        var result = await _service.DateTimeToTimestampAsync(dateTime);

        // Assert
        result.Should().NotBeNull();
        result.TimestampSeconds.Should().BeGreaterThan(0);
        result.DateTimeIso.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DateTimeToTimestampAsync_EmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.DateTimeToTimestampAsync("");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*日期时间不能为空*");
    }

    [Fact]
    public async Task DateTimeToTimestampAsync_WhitespaceString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.DateTimeToTimestampAsync("   ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*日期时间不能为空*");
    }

    [Fact]
    public async Task DateTimeToTimestampAsync_InvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.DateTimeToTimestampAsync("not-a-date");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无法解析日期时间格式*");
    }

    [Fact]
    public async Task FormatTimestampAsync_ValidTimestamp_ReturnsFormattedString()
    {
        // Arrange
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.FormatTimestampAsync(timestamp, "standard");

        // Assert
        result.Should().NotBeNull();
        result.DateTimeLocal.Should().Contain("2024");
    }

    [Fact]
    public async Task FormatTimestampAsync_Iso8601Preset_ReturnsIsoFormat()
    {
        // Arrange
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.FormatTimestampAsync(timestamp, "iso8601");

        // Assert
        result.Should().NotBeNull();
        result.Formats.Should().ContainKey("custom");
    }

    [Fact]
    public async Task FormatTimestampAsync_CustomFormat_ReturnsCustomFormat()
    {
        // Arrange
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.FormatTimestampAsync(timestamp, "yyyy/MM/dd");

        // Assert
        result.Should().NotBeNull();
        result.DateTimeLocal.Should().Contain("/");
    }

    [Fact]
    public async Task GetTimezoneListAsync_ReturnsNonEmptyList()
    {
        // Act
        var result = await _service.GetTimezoneListAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(tz => tz.Id.Contains("UTC") || tz.Id.Contains("GMT"));
    }

    [Fact]
    public async Task GetTimezoneListAsync_TimezonesHaveRequiredProperties()
    {
        // Act
        var result = await _service.GetTimezoneListAsync();

        // Assert
        foreach (var tz in result)
        {
            tz.Id.Should().NotBeNullOrEmpty();
            tz.DisplayName.Should().NotBeNullOrEmpty();
            tz.BaseUtcOffset.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task ConvertTimezoneAsync_ValidInput_ReturnsConvertedTimestamps()
    {
        // Arrange
        var timestamp = 1704067200L; // UTC timestamp

        // Act
        var result = await _service.ConvertTimezoneAsync(timestamp, "UTC", "China Standard Time");

        // Assert
        result.Should().NotBeNull();
        result.FromTimestamp.Should().BeGreaterThan(0);
        result.ToTimestamp.Should().BeGreaterThanOrEqualTo(result.FromTimestamp);
        result.FromZone.Should().Contain("UTC");
        result.ToZone.Should().Contain("China");
        result.FromDateTime.Should().NotBeNullOrEmpty();
        result.ToDateTime.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConvertTimezoneAsync_SameTimezone_ReturnsSameTimestamps()
    {
        // Arrange
        var timestamp = 1704067200L;

        // Act
        var result = await _service.ConvertTimezoneAsync(timestamp, "UTC", "UTC");

        // Assert
        result.FromTimestamp.Should().Be(result.ToTimestamp);
    }

    [Fact]
    public async Task TimestampToDateTimeAsync_WithTimezone_AppliesTimezone()
    {
        // Arrange
        var timestamp = 1704067200L;

        // Act
        var result = await _service.TimestampToDateTimeAsync(timestamp, "s", "China Standard Time");

        // Assert
        result.Should().NotBeNull();
        result.Timezone.Should().Contain("China");
    }

    [Fact]
    public async Task FormatTimestampAsync_AllPresets_ReturnsAllFormats()
    {
        // Arrange
        var timestamp = 1704067200000L;

        // Act
        var result = await _service.FormatTimestampAsync(timestamp, "standard");

        // Assert
        result.Should().NotBeNull();
        result.Formats.Should().ContainKey("custom"); // 自定义格式
    }
}

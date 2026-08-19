using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class CronParserTests
{
    private readonly CronParser _parser;

    public CronParserTests()
    {
        _parser = new CronParser();
    }

    #region IsValid Tests

    [Fact]
    public void IsValid_ValidCronExpression_ShouldReturnTrue()
    {
        // Arrange
        var expressions = new[]
        {
            "* * * * *",
            "0 0 * * *",
            "*/5 * * * *",
            "0 9-17 * * 1-5",
            "0 0 1 1 *",
            "30 14 * * MON-FRI",
            "0 0 * * SUN"
        };

        // Act & Assert
        foreach (var expr in expressions)
        {
            _parser.IsValid(expr).Should().BeTrue($"because '{expr}' should be valid");
        }
    }

    [Fact]
    public void IsValid_InvalidCronExpression_ShouldReturnFalse()
    {
        // Arrange
        var invalidExpressions = new[]
        {
            "",
            "   ",
            null!,
            "* * * *",
            "* * * * * *",
            "60 * * * *",
            "* 24 * * *",
            "* * 32 * *",
            "* * * 13 *",
            "* * * * 8"
        };

        // Act & Assert
        foreach (var expr in invalidExpressions)
        {
            _parser.IsValid(expr).Should().BeFalse($"because '{expr}' should be invalid");
        }
    }

    [Fact]
    public void IsValid_WithExtraWhitespace_ShouldStillBeValid()
    {
        // Arrange
        var expression = "  0   */2  *  *  *  ";

        // Act
        var result = _parser.IsValid(expression);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetNextRunTime - Common Expressions Tests

    [Fact]
    public void GetNextRunTime_EveryMinute_ShouldReturnNextMinute()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("* * * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        nextRun!.Value.ToLocalTime().Should().Be(new DateTime(2024, 6, 15, 10, 31, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextRunTime_EveryHourAtZeroMinute_ShouldReturnNextHour()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 * * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        nextRun!.Value.ToLocalTime().Should().Be(new DateTime(2024, 6, 15, 11, 0, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextRunTime_DailyAtMidnight_ShouldReturnNextMidnight()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 0 * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        nextRun!.Value.ToLocalTime().Should().Be(new DateTime(2024, 6, 16, 0, 0, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextRunTime_SpecificTime_ShouldReturnNextOccurrence()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("30 14 * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        nextRun!.Value.ToLocalTime().Should().Be(new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Local));
    }

    [Fact]
    public void GetNextRunTime_WeekdaysOnly_ShouldSkipWeekends()
    {
        // Arrange
        var saturday = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 9 * * 1-5", saturday);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.DayOfWeek.Should().Be(DayOfWeek.Monday);
        nextLocal.Hour.Should().Be(9);
        nextLocal.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextRunTime_EveryFiveMinutes_ShouldReturnNextFiveMinuteMark()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 23, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("*/5 * * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        nextRun!.Value.ToLocalTime().Minute.Should().Be(25);
    }

    #endregion

    #region GetNextRunTimes - Multiple Results Tests

    [Fact]
    public void GetNextRunTimes_RequestedCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Local);

        // Act
        var times = _parser.GetNextRunTimes("0 * * * *", afterTime, 5);

        // Assert
        times.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            var localTime = times[i].ToLocalTime();
            localTime.Minute.Should().Be(0);
            localTime.Hour.Should().Be(11 + i);
        }
    }

    [Fact]
    public void GetNextRunTimes_ZeroCount_ShouldReturnEmpty()
    {
        // Arrange
        var afterTime = DateTime.Now;

        // Act
        var times = _parser.GetNextRunTimes("* * * * *", afterTime, 0);

        // Assert
        times.Should().BeEmpty();
    }

    [Fact]
    public void GetNextRunTimes_NegativeCount_ShouldReturnEmpty()
    {
        // Arrange
        var afterTime = DateTime.Now;

        // Act
        var times = _parser.GetNextRunTimes("* * * * *", afterTime, -1);

        // Assert
        times.Should().BeEmpty();
    }

    [Fact]
    public void GetNextRunTimes_InvalidExpression_ShouldReturnEmpty()
    {
        // Arrange
        var afterTime = DateTime.Now;

        // Act
        var times = _parser.GetNextRunTimes("invalid", afterTime, 5);

        // Assert
        times.Should().BeEmpty();
    }

    #endregion

    #region Boundary Cases

    [Fact]
    public void GetNextRunTime_EndOfDay_ShouldRollOverToNextDay()
    {
        // Arrange
        var endOfDay = new DateTime(2024, 6, 15, 23, 59, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("* * * * *", endOfDay);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Day.Should().Be(16);
        nextLocal.Hour.Should().Be(0);
        nextLocal.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextRunTime_EndOfMonth_ShouldRollOverToNextMonth()
    {
        // Arrange
        var endOfMonth = new DateTime(2024, 6, 30, 23, 59, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 0 1 * *", endOfMonth);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Month.Should().Be(7);
        nextLocal.Day.Should().Be(1);
        nextLocal.Hour.Should().Be(0);
        nextLocal.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextRunTime_EndOfYear_ShouldRollOverToNextYear()
    {
        // Arrange
        var endOfYear = new DateTime(2024, 12, 31, 23, 59, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 0 1 1 *", endOfYear);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Year.Should().Be(2025);
        nextLocal.Month.Should().Be(1);
        nextLocal.Day.Should().Be(1);
    }

    [Fact]
    public void GetNextRunTime_SpecificMinuteHasPassed_ShouldGoToNextHour()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 14, 45, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("30 * * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Hour.Should().Be(15);
        nextLocal.Minute.Should().Be(30);
    }

    [Fact]
    public void GetNextRunTime_LeapYear_February29_ShouldWork()
    {
        // Arrange
        var afterTime = new DateTime(2024, 2, 28, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 12 29 2 *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Year.Should().Be(2024);
        nextLocal.Month.Should().Be(2);
        nextLocal.Day.Should().Be(29);
    }

    #endregion

    #region Named Day/Month Tests

    [Fact]
    public void GetNextRunTime_NamedMonths_ShouldWork()
    {
        // Arrange
        var afterTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 0 1 JAN,FEB,MAR *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Month.Should().Be(2);
        nextLocal.Day.Should().Be(1);
    }

    [Fact]
    public void GetNextRunTime_NamedWeekdays_ShouldWork()
    {
        // Arrange
        var monday = new DateTime(2024, 6, 17, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 9 * * MON,WED,FRI", monday);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    [Fact]
    public void GetNextRunTime_MONtoFRIRange_ShouldWork()
    {
        // Arrange
        var saturday = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 9 * * MON-FRI", saturday);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    #endregion

    #region Step Values Tests

    [Fact]
    public void GetNextRunTime_StepHours_ShouldWork()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 */6 * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Hour.Should().Be(12);
        nextLocal.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextRunTime_StepDays_ShouldWork()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 0 */10 * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Day.Should().Be(11);
    }

    #endregion

    #region Range Tests

    [Fact]
    public void GetNextRunTime_HourRange_ShouldWork()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 8, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 9-17 * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Hour.Should().Be(9);
        nextLocal.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextRunTime_HourRangeAfterEnd_ShouldGoToNextDay()
    {
        // Arrange
        var afterTime = new DateTime(2024, 6, 15, 18, 30, 0, DateTimeKind.Local);

        // Act
        var nextRun = _parser.GetNextRunTime("0 9-17 * * *", afterTime);

        // Assert
        nextRun.Should().NotBeNull();
        var nextLocal = nextRun!.Value.ToLocalTime();
        nextLocal.Day.Should().Be(16);
        nextLocal.Hour.Should().Be(9);
    }

    #endregion
}

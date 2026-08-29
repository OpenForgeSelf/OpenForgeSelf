using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class TimestampService : ITimestampService
{
    private static readonly Dictionary<string, string> _formatPresets = new()
    {
        { "iso8601", "yyyy-MM-ddTHH:mm:ssZ" },
        { "standard", "yyyy-MM-dd HH:mm:ss" },
        { "date", "yyyy/MM/dd" },
        { "us", "MM/dd/yyyy" },
        { "chinese", "yyyy年M月d日 H时m分" },
        { "dateOnly", "yyyy-MM-dd" },
        { "timeOnly", "HH:mm:ss" },
        { "yearMonth", "yyyy-MM" },
    };

    public Task<TimestampCurrentResult> GetCurrentTimestampAsync()
    {
        XTrace.Log.Debug("[DevTools] 获取当前时间戳");

        var now = DateTimeOffset.UtcNow;
        var result = new TimestampCurrentResult
        {
            TimestampSeconds = now.ToUnixTimeSeconds(),
            TimestampMilliseconds = now.ToUnixTimeMilliseconds(),
            DateTimeIso = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            DateTimeLocal = now.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return Task.FromResult(result);
    }

    public Task<TimestampConvertResult> TimestampToDateTimeAsync(long timestamp, string? timeUnit = null, string? timezone = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 时间戳转日期: {0}", timestamp);

            var dto = GetDateTimeOffset(timestamp, timeUnit);
            var tz = GetTimeZoneInfo(timezone);
            var converted = TimeZoneInfo.ConvertTime(dto, tz);

            var result = BuildConvertResult(converted, tz);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 时间戳转日期失败: {0}", ex.Message);
            throw new ArgumentException("时间戳转换失败: " + ex.Message, nameof(timestamp), ex);
        }
    }

    public Task<TimestampConvertResult> DateTimeToTimestampAsync(string dateTime, string? timeUnit = null, string? timezone = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 日期转时间戳: {0}", dateTime);

            if (string.IsNullOrWhiteSpace(dateTime))
                throw new ArgumentException("日期时间不能为空", nameof(dateTime));

            var tz = GetTimeZoneInfo(timezone);
            DateTime dt;

            if (!DateTime.TryParse(dateTime, out dt))
            {
                throw new ArgumentException("无法解析日期时间格式", nameof(dateTime));
            }

            var dto = new DateTimeOffset(dt, tz.BaseUtcOffset);
            var utcDto = dto.ToOffset(TimeSpan.Zero);

            var result = new TimestampConvertResult
            {
                TimestampSeconds = utcDto.ToUnixTimeSeconds(),
                TimestampMilliseconds = utcDto.ToUnixTimeMilliseconds(),
                DateTimeIso = utcDto.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DateTimeLocal = dto.ToString("yyyy-MM-dd HH:mm:ss"),
                Timezone = tz.Id,
                Formats = GenerateAllFormats(dto)
            };

            return Task.FromResult(result);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 日期转时间戳失败: {0}", ex.Message);
            throw new ArgumentException("日期转换失败: " + ex.Message, nameof(dateTime), ex);
        }
    }

    public Task<TimestampConvertResult> FormatTimestampAsync(long timestamp, string format, string? timeUnit = null, string? timezone = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 格式化时间戳: {0}, 格式: {1}", timestamp, format);

            var dto = GetDateTimeOffset(timestamp, timeUnit);
            var tz = GetTimeZoneInfo(timezone);
            var converted = TimeZoneInfo.ConvertTime(dto, tz);

            var formatString = _formatPresets.GetValueOrDefault(format?.ToLowerInvariant() ?? "standard", format ?? "yyyy-MM-dd HH:mm:ss");

            var result = new TimestampConvertResult
            {
                TimestampSeconds = converted.ToUnixTimeSeconds(),
                TimestampMilliseconds = converted.ToUnixTimeMilliseconds(),
                DateTimeIso = converted.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DateTimeLocal = converted.ToString(formatString),
                Timezone = tz.Id,
                Formats = new Dictionary<string, string>
                {
                    ["custom"] = converted.ToString(formatString)
                }
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 格式化时间戳失败: {0}", ex.Message);
            throw new ArgumentException("格式化失败: " + ex.Message, nameof(timestamp), ex);
        }
    }

    public Task<List<TimezoneItem>> GetTimezoneListAsync()
    {
        XTrace.Log.Debug("[DevTools] 获取时区列表");

        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new TimezoneItem
            {
                Id = tz.Id,
                DisplayName = tz.DisplayName,
                BaseUtcOffset = tz.BaseUtcOffset.ToString(@"hh\:mm")
            })
            .OrderBy(tz => tz.BaseUtcOffset)
            .ToList();

        return Task.FromResult(timezones);
    }

    public Task<TimezoneConvertResult> ConvertTimezoneAsync(long timestamp, string fromZone, string toZone, string? timeUnit = null)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 时区转换: {0} -> {1}", fromZone, toZone);

            var fromTz = GetTimeZoneInfo(fromZone);
            var toTz = GetTimeZoneInfo(toZone);

            var dto = GetDateTimeOffset(timestamp, timeUnit);
            var fromDto = TimeZoneInfo.ConvertTime(dto, fromTz);
            var toDto = TimeZoneInfo.ConvertTime(dto, toTz);

            var result = new TimezoneConvertResult
            {
                FromTimestamp = fromDto.ToUnixTimeSeconds(),
                ToTimestamp = toDto.ToUnixTimeSeconds(),
                FromDateTime = fromDto.ToString("yyyy-MM-dd HH:mm:ss"),
                ToDateTime = toDto.ToString("yyyy-MM-dd HH:mm:ss"),
                FromZone = fromTz.Id,
                ToZone = toTz.Id
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 时区转换失败: {0}", ex.Message);
            throw new ArgumentException("时区转换失败: " + ex.Message, nameof(timestamp), ex);
        }
    }

    private static DateTimeOffset GetDateTimeOffset(long timestamp, string? timeUnit)
    {
        var unit = (timeUnit ?? "ms").ToLowerInvariant();
        if (unit == "s" || unit == "second" || unit == "seconds")
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
    }

    private static TimeZoneInfo GetTimeZoneInfo(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }

    private static TimestampConvertResult BuildConvertResult(DateTimeOffset dto, TimeZoneInfo tz)
    {
        return new TimestampConvertResult
        {
            TimestampSeconds = dto.ToUnixTimeSeconds(),
            TimestampMilliseconds = dto.ToUnixTimeMilliseconds(),
            DateTimeIso = dto.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            DateTimeLocal = dto.ToString("yyyy-MM-dd HH:mm:ss"),
            Timezone = tz.Id,
            Formats = GenerateAllFormats(dto)
        };
    }

    private static Dictionary<string, string> GenerateAllFormats(DateTimeOffset dto)
    {
        var formats = new Dictionary<string, string>();
        foreach (var (name, format) in _formatPresets)
        {
            formats[name] = dto.ToString(format);
        }
        return formats;
    }
}

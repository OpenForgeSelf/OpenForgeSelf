using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface ITimestampService
{
    Task<TimestampCurrentResult> GetCurrentTimestampAsync();
    Task<TimestampConvertResult> TimestampToDateTimeAsync(long timestamp, string? timeUnit = null, string? timezone = null);
    Task<TimestampConvertResult> DateTimeToTimestampAsync(string dateTime, string? timeUnit = null, string? timezone = null);
    Task<TimestampConvertResult> FormatTimestampAsync(long timestamp, string format, string? timeUnit = null, string? timezone = null);
    Task<List<TimezoneItem>> GetTimezoneListAsync();
    Task<TimezoneConvertResult> ConvertTimezoneAsync(long timestamp, string fromZone, string toZone, string? timeUnit = null);
}

namespace ForgeSelf.Abstractions;

/// <summary>
/// Cron 表达式解析器契约（宿主实现：ForgeSelf.Api.Services.CronParser）。
/// 供 Scheduler 等插件按接口消费，避免直接依赖宿主实现类。
/// </summary>
public interface ICronParser
{
    bool IsValid(string cronExpression);
    DateTime? GetNextRunTime(string cronExpression, DateTime afterTime, TimeZoneInfo? timeZone = null);
    List<DateTime> GetNextRunTimes(string cronExpression, DateTime afterTime, int count, TimeZoneInfo? timeZone = null);
}

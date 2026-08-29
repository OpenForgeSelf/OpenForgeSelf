using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services;

public class CronParser : ICronParser
{
    private static readonly Dictionary<string, int> _monthMap = new()
    {
        { "JAN", 1 }, { "FEB", 2 }, { "MAR", 3 }, { "APR", 4 },
        { "MAY", 5 }, { "JUN", 6 }, { "JUL", 7 }, { "AUG", 8 },
        { "SEP", 9 }, { "OCT", 10 }, { "NOV", 11 }, { "DEC", 12 }
    };

    private static readonly Dictionary<string, int> _weekDayMap = new()
    {
        { "SUN", 0 }, { "MON", 1 }, { "TUE", 2 }, { "WED", 3 },
        { "THU", 4 }, { "FRI", 5 }, { "SAT", 6 }
    };

    public bool IsValid(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        try
        {
            var parts = cronExpression.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
                return false;

            ParseField(parts[0], 0, 59);
            ParseField(parts[1], 0, 23);
            ParseField(parts[2], 1, 31);
            ParseMonthField(parts[3]);
            ParseWeekDayField(parts[4]);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public DateTime? GetNextRunTime(string cronExpression, DateTime afterTime, TimeZoneInfo? timeZone = null)
    {
        var times = GetNextRunTimes(cronExpression, afterTime, 1, timeZone);
        return times.Count > 0 ? times[0] : null;
    }

    public List<DateTime> GetNextRunTimes(string cronExpression, DateTime afterTime, int count, TimeZoneInfo? timeZone = null)
    {
        var result = new List<DateTime>();
        if (count <= 0 || !IsValid(cronExpression))
            return result;

        timeZone ??= TimeZoneInfo.Local;

        var parts = cronExpression.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var minutes = ParseField(parts[0], 0, 59);
        var hours = ParseField(parts[1], 0, 23);
        var daysOfMonth = ParseField(parts[2], 1, 31);
        var months = ParseMonthField(parts[3]);
        var daysOfWeek = ParseWeekDayField(parts[4]);

        var tzAfterTime = TimeZoneInfo.ConvertTimeFromUtc(afterTime.Kind == DateTimeKind.Utc ? afterTime : afterTime.ToUniversalTime(), timeZone);
        var current = tzAfterTime.AddMinutes(1);
        current = new DateTime(current.Year, current.Month, current.Day, current.Hour, current.Minute, 0);

        int iterations = 0;
        int maxIterations = 366 * 24 * 60;

        while (result.Count < count && iterations < maxIterations)
        {
            iterations++;

            if (!months.Contains(current.Month))
            {
                current = current.AddMonths(1);
                current = new DateTime(current.Year, current.Month, 1, 0, 0, 0);
                continue;
            }

            if (!daysOfMonth.Contains(current.Day) || !daysOfWeek.Contains((int)current.DayOfWeek))
            {
                current = current.AddDays(1);
                current = new DateTime(current.Year, current.Month, current.Day, 0, 0, 0);
                continue;
            }

            if (!hours.Contains(current.Hour))
            {
                current = current.AddHours(1);
                current = new DateTime(current.Year, current.Month, current.Day, current.Hour, 0, 0);
                continue;
            }

            if (!minutes.Contains(current.Minute))
            {
                current = current.AddMinutes(1);
                continue;
            }

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(current, timeZone);
            result.Add(utcTime);

            current = current.AddMinutes(1);
        }

        return result;
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var values = new HashSet<int>();
        field = field.Trim().ToUpper();

        if (field == "*")
        {
            for (int i = min; i <= max; i++)
                values.Add(i);
            return values;
        }

        foreach (var part in field.Split(','))
        {
            var trimmedPart = part.Trim();
            int step = 1;

            if (trimmedPart.Contains('/'))
            {
                var stepParts = trimmedPart.Split('/');
                trimmedPart = stepParts[0].Trim();
                step = int.Parse(stepParts[1].Trim());
            }

            if (trimmedPart == "*")
            {
                for (int i = min; i <= max; i += step)
                    values.Add(i);
            }
            else if (trimmedPart.Contains('-'))
            {
                var rangeParts = trimmedPart.Split('-');
                int start = int.Parse(rangeParts[0].Trim());
                int end = int.Parse(rangeParts[1].Trim());

                if (start < min) start = min;
                if (end > max) end = max;

                for (int i = start; i <= end; i += step)
                    values.Add(i);
            }
            else
            {
                int val = int.Parse(trimmedPart);
                if (val >= min && val <= max)
                    values.Add(val);
                else
                    throw new ArgumentOutOfRangeException($"Value {val} is out of range [{min}-{max}]");
            }
        }

        return values;
    }

    private static HashSet<int> ParseMonthField(string field)
    {
        field = field.Trim().ToUpper();

        foreach (var kvp in _monthMap)
        {
            field = field.Replace(kvp.Key, kvp.Value.ToString());
        }

        return ParseField(field, 1, 12);
    }

    private static HashSet<int> ParseWeekDayField(string field)
    {
        field = field.Trim().ToUpper();

        foreach (var kvp in _weekDayMap)
        {
            field = field.Replace(kvp.Key, kvp.Value.ToString());
        }

        var values = ParseField(field, 0, 7);

        if (values.Contains(7))
        {
            values.Remove(7);
            values.Add(0);
        }

        return values;
    }
}

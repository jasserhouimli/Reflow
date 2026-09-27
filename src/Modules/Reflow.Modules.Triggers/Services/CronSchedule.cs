using Cronos;

namespace Reflow.Modules.Triggers.Services;

/// <summary>5-field cron + timezone. Pure; unit-tested.</summary>
public static class CronSchedule
{
    public static bool TryParse(string cron, out string? error)
    {
        try
        {
            CronExpression.Parse(cron, CronFormat.Standard);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = "Invalid cron expression: " + ex.Message;
            return false;
        }
    }

    public static bool TryTimezone(string timezone, out TimeZoneInfo? tz)
    {
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (Exception)
        {
            tz = null;
            return false;
        }
    }

    public static DateTime? NextOccurrence(string cron, string timezone, DateTime fromUtc)
    {
        if (!TryTimezone(timezone, out var tz) || tz is null)
            return null;
        var expr = CronExpression.Parse(cron, CronFormat.Standard);
        return expr.GetNextOccurrence(fromUtc, tz);
    }
}

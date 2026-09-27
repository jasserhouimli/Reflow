using Reflow.Modules.Triggers.Features.Schedules;
using Reflow.Modules.Triggers.Services;
using Xunit;

namespace Reflow.UnitTests;

public class TriggerUnitTests
{
    [Fact]
    public void ValidCron_Passes()
    {
        Assert.True(CronSchedule.TryParse("0 2 * * *", out _));
    }

    [Fact]
    public void BadCron_Fails()
    {
        Assert.False(CronSchedule.TryParse("not a cron", out var error));
        Assert.NotEmpty(error!);
    }

    [Fact]
    public void KnownTimezone_Passes()
    {
        Assert.True(CronSchedule.TryTimezone("UTC", out _));
    }

    [Fact]
    public void UnknownTimezone_Fails()
    {
        Assert.False(CronSchedule.TryTimezone("Mars/Olympus", out _));
    }

    [Fact]
    public void NextOccurrence_IsFuture()
    {
        var next = CronSchedule.NextOccurrence("0 2 * * *", "UTC", DateTime.UtcNow);
        Assert.NotNull(next);
        Assert.True(next > DateTime.UtcNow);
    }

    [Fact]
    public void ScheduleValidator_RejectsBadInput()
    {
        var errors = ScheduleValidator.Validate("bad", "Mars/Olympus", 7);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void WebhookTokens_AreUniqueAndHashed()
    {
        var a = WebhookToken.Generate();
        var b = WebhookToken.Generate();
        Assert.NotEqual(a, b);
        Assert.Equal(64, WebhookToken.Hash(a).Length);
        Assert.NotEqual(a, WebhookToken.Hash(a));
    }
}

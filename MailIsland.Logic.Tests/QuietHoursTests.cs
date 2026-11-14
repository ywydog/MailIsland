using MailIsland.Logic.Config;
using MailIsland.Logic.Mail;
using Xunit;

namespace MailIsland.Logic.Tests;

public class QuietHoursTests
{
    [Theory]
    [InlineData("21:00", "07:00", 22, 30, true)]
    [InlineData("21:00", "07:00", 6, 59, true)]
    [InlineData("21:00", "07:00", 7, 0, false)]
    [InlineData("21:00", "07:00", 12, 0, false)]
    [InlineData("09:00", "18:00", 10, 0, true)]
    [InlineData("09:00", "18:00", 18, 0, false)]
    [InlineData("09:00", "18:00", 8, 59, false)]
    public void DetectsQuietTime(string start, string end, int hour, int minute, bool expected)
    {
        var now = new DateTime(2026, 1, 1, hour, minute, 0);
        Assert.Equal(expected, QuietHours.IsQuietTime(now, start, end));
    }

    [Fact]
    public void SameStartAndEndIsNeverQuiet()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(QuietHours.IsQuietTime(now, "09:00", "09:00"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("09:70")]
    [InlineData("abc")]
    [InlineData("09-00")]
    public void InvalidTimeIsNotQuiet(string value)
    {
        Assert.False(QuietHours.IsValidTime(value));

        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(QuietHours.IsQuietTime(now, value, "07:00"));
    }

    [Fact]
    public void MatchesAnyEnabledPeriod()
    {
        var periods = new List<QuietHoursPeriod>
        {
            new() { Start = "12:00", End = "13:00" },
            new() { Start = "22:00", End = "07:00" },
        };

        Assert.True(QuietHours.IsQuietTime(new DateTime(2026, 1, 1, 12, 30, 0), periods));
        Assert.True(QuietHours.IsQuietTime(new DateTime(2026, 1, 1, 23, 0, 0), periods));
        Assert.False(QuietHours.IsQuietTime(new DateTime(2026, 1, 1, 15, 0, 0), periods));
    }

    [Fact]
    public void DisabledPeriodIsIgnored()
    {
        var periods = new List<QuietHoursPeriod>
        {
            new() { Start = "09:00", End = "18:00", Enabled = false },
            new() { Start = "22:00", End = "07:00" },
        };

        Assert.False(QuietHours.IsQuietTime(new DateTime(2026, 1, 1, 10, 0, 0), periods));
        Assert.True(QuietHours.IsQuietTime(new DateTime(2026, 1, 1, 23, 0, 0), periods));
    }

    [Fact]
    public void EmptyOrNullPeriodsIsNotQuiet()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(QuietHours.IsQuietTime(now, new List<QuietHoursPeriod>()));
        Assert.False(QuietHours.IsQuietTime(now, (IEnumerable<QuietHoursPeriod>?)null));
    }
}

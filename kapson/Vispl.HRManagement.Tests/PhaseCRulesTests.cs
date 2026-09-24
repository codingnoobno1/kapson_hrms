using Vispl.HRManagement.BM.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// The phase C rules the employee app's Attendance, Analytics and Leave screens show verbatim:
/// the Recent Attendance chip, the "vs Last Month" delta line, and leave type colour keys.
/// </summary>
public sealed class PhaseCRulesTests
{
    [Theory]
    [InlineData("Present", 0, "on-time", "On Time")]
    [InlineData("Present", null, "on-time", "On Time")]
    [InlineData("Present", 12, "late", "Late")]
    [InlineData("Partial", null, "half-day", "Half Day")]
    [InlineData("OnLeave", null, "leave", "Leave")]
    [InlineData("Absent", null, "absent", "Absent")]
    [InlineData("Holiday", null, "holiday", "Holiday")]
    public void Attendance_chip_follows_the_day_status_and_the_shift_delay(
        string status, int? delayMinutes, string code, string name)
    {
        var badge = AttendanceService.BadgeFor(status, delayMinutes);

        Assert.Equal(code, badge.Code);
        Assert.Equal(name, badge.Name);
    }

    [Fact]
    public void More_absences_read_as_bad_news()
    {
        var delta = AttendanceAnalyticsService.Delta(2, " days", higherIsBetter: false, "vs Last Month");

        Assert.Equal("↑ 2 days vs Last Month", delta.Text);
        Assert.Equal("up", delta.Direction);
        Assert.Equal("bad", delta.Tone);
    }

    [Fact]
    public void More_present_days_read_as_good_news()
    {
        var delta = AttendanceAnalyticsService.Delta(3, " days", higherIsBetter: true, "vs Last Month");

        Assert.Equal("↑ 3 days vs Last Month", delta.Text);
        Assert.Equal("good", delta.Tone);
    }

    [Fact]
    public void An_unchanged_figure_says_same()
    {
        var delta = AttendanceAnalyticsService.Delta(0, " days", higherIsBetter: false, "vs Last Month");

        Assert.Equal("→ Same vs Last Month", delta.Text);
        Assert.Equal("same", delta.Direction);
    }

    [Fact]
    public void A_falling_percentage_shows_one_decimal()
    {
        var delta = AttendanceAnalyticsService.Delta(-2.46m, "%", higherIsBetter: true, "vs Last Month");

        Assert.Equal("↓ 2.5% vs Last Month", delta.Text);
        Assert.Equal("bad", delta.Tone);
    }

    [Theory]
    [InlineData("PINK", "pink")]
    [InlineData(" green ", "green")]
    [InlineData("orange", "blue")]
    [InlineData(null, "blue")]
    public void Leave_tone_keys_are_normalised_to_the_palette(string? input, string expected) =>
        Assert.Equal(expected, LeaveTones.Normalize(input));
}

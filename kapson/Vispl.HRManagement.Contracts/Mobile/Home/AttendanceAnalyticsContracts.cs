using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Home;

/// <summary>The Select Period chips on Attendance Analytics.</summary>
public static class AttendancePeriods
{
    public const string ThisMonth = "this-month";
    public const string Last3Months = "last-3-months";
    public const string Last6Months = "last-6-months";
    public const string ThisYear = "this-year";

    public static IReadOnlyList<(string Key, string Label)> All { get; } =
    [
        (ThisMonth, "This Month"),
        (Last3Months, "Last 3 Months"),
        (Last6Months, "Last 6 Months"),
        (ThisYear, "This Year")
    ];
}

public sealed class AttendanceAnalyticsQuery
{
    [RegularExpression("^(this-month|last-3-months|last-6-months|this-year)$",
        ErrorMessage = "Use this-month, last-3-months, last-6-months or this-year.")]
    public string? Period { get; set; }

    /// <summary>
    /// A specific calendar month (the Full Month Attendance page's stepper), as an alternative to
    /// <see cref="Period"/> — set only when the caller means one exact month rather than a rolling
    /// window. When both are set, this wins.
    /// </summary>
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Use yyyy-MM, for example 2026-09.")]
    public string? Month { get; set; }
}

/// <summary>From and To are the window measured; To never passes today.</summary>
public sealed record AnalyticsPeriodDto(string Key, string Label, DateOnly From, DateOnly To, string ComparisonLabel);

/// <summary>"↑ 2.5% vs Last Month". Direction up/down/same; Tone good/bad — the app picks the colour.</summary>
public sealed record AnalyticsDeltaDto(string Text, string Direction, string Tone);

/// <summary>Keys: overall-attendance, days-present, days-absent, half-days.</summary>
public sealed record AnalyticsKpiDto(string Key, string Label, string Value, AnalyticsDeltaDto Delta);

public sealed record AnalyticsMonthDto(int Year, int Month, string Label, decimal AttendancePercent, int Present, int Absent, int HalfDays);

public sealed record AnalyticsWeekDto(string Label, decimal AttendancePercent);

/// <summary>Kinds: trend, watch-out, target, weekly. Server rules, not free text.</summary>
public sealed record AnalyticsInsightDto(string Kind, string Emoji, string Title, string Text);

/// <summary>Attendance rate = (Present + ½ × Half Days) ÷ working days (weekdays, less holidays and approved leave).</summary>
public sealed record AnalyticsStatisticsDto(int WorkingDays, int Present, int Absent, int HalfDays, decimal AttendanceRate);

/// <summary>Everything on the Attendance Analytics page, computed server-side (CR-6).</summary>
public sealed record AttendanceAnalyticsDto(
    AnalyticsPeriodDto Period,
    IReadOnlyList<AnalyticsKpiDto> Kpis,
    IReadOnlyList<AnalyticsMonthDto> Months,
    IReadOnlyList<AnalyticsWeekDto> Weeks,
    IReadOnlyList<AnalyticsInsightDto> Insights,
    AnalyticsStatisticsDto Statistics,
    int TargetPercent,
    DateTimeOffset GeneratedUtc);

/// <summary>An .xlsx the app saves and shares. Base64 inside the usual envelope.</summary>
public sealed record AttendanceReportFileDto(string FileName, string ContentType, string Base64Content);

public sealed record AttendanceReportEmailDto(bool Sent, string? SentTo, string? Failure);

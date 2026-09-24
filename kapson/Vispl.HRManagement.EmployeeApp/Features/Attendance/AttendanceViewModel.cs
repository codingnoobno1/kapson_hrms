using System.Collections.ObjectModel;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Api;

namespace Vispl.HRManagement.EmployeeApp.Features.Attendance;

/// <summary>
/// EMP-012 and EMP-020. One month of attendance: four tiles, the day list, and the calendar.
///
/// The month is held here and echoed back by the server, so the stepper and the grid cannot
/// disagree about which month is on display. Every figure — present, absent, half, holidays —
/// is computed server-side; this class arranges what comes back and calculates nothing.
/// </summary>
public sealed class AttendanceViewModel(MeApiClient me, AttendanceApiClient attendance) : ViewModelBase
{
    private int year = DateTime.Today.Year;
    private int month = DateTime.Today.Month;
    private string monthLabel = string.Empty;
    private int workingDays;
    private bool canGoForward;

    public ObservableCollection<KpiDto> Tiles { get; } = [];

    public ObservableCollection<AttendanceDayDto> Days { get; } = [];

    /// <summary>The Attendance tab's "Recent Attendance": the last 30 days, across month boundaries, newest first.</summary>
    public ObservableCollection<AttendanceDayDto> RecentDays { get; } = [];

    /// <summary>The Attendance tab always opens on this month; Full Month may have stepped elsewhere.</summary>
    public void ResetToCurrentMonth()
    {
        year = DateTime.Today.Year;
        month = DateTime.Today.Month;
    }

    /// <summary>Day status by date, for the calendar cell template.</summary>
    public Dictionary<DateOnly, AttendanceDayDto> ByDate { get; } = [];

    public int Year => year;

    public int Month => month;

    public string MonthLabel
    {
        get => monthLabel;
        private set => Set(ref monthLabel, value);
    }

    public int WorkingDays
    {
        get => workingDays;
        private set => Set(ref workingDays, value);
    }

    /// <summary>Never past the current month: attendance for a month that has not happened is not a thing.</summary>
    public bool CanGoForward
    {
        get => canGoForward;
        private set => Set(ref canGoForward, value);
    }

    public DateTime DisplayDate => new(year, month, 1);

    /// <summary>Raised when the month's data has changed, so a calendar can redraw its cells.</summary>
    public event EventHandler? MonthChanged;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RunAsync(async () =>
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);

            var kpis = await me.GetAttendanceKpisAsync(year, month, ct);
            var days = await attendance.GetMineAsync(from, to, ct);

            // The server echoes the month back. Trusting its label over a locally formatted one
            // means the header cannot drift from the data underneath it.
            MonthLabel = $"{kpis.MonthName} {kpis.Year}";
            WorkingDays = kpis.WorkingDays;

            Tiles.Clear();
            foreach (var tile in kpis.Kpis)
            {
                Tiles.Add(tile);
            }

            Days.Clear();
            ByDate.Clear();

            foreach (var day in days.OrderByDescending(item => item.Date))
            {
                Days.Add(day);
                ByDate[day.Date] = day;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            CanGoForward = new DateOnly(year, month, 1) < new DateOnly(today.Year, today.Month, 1);

            var recent = await attendance.GetMineAsync(today.AddDays(-30), today, ct);
            RecentDays.Clear();
            foreach (var day in recent.OrderByDescending(item => item.Date))
            {
                RecentDays.Add(day);
            }

            MonthChanged?.Invoke(this, EventArgs.Empty);

            if (Days.Count == 0)
            {
                Empty($"No attendance recorded for {MonthLabel}.");
                return;
            }

            Ready();
        });
    }

    public Task PreviousMonthAsync()
    {
        var moved = new DateOnly(year, month, 1).AddMonths(-1);
        year = moved.Year;
        month = moved.Month;

        Raise(nameof(DisplayDate));
        return LoadAsync();
    }

    public Task NextMonthAsync()
    {
        if (!CanGoForward)
        {
            return Task.CompletedTask;
        }

        var moved = new DateOnly(year, month, 1).AddMonths(1);
        year = moved.Year;
        month = moved.Month;

        Raise(nameof(DisplayDate));
        return LoadAsync();
    }

    /// <summary>The colour a calendar cell takes, or null for a date outside the loaded month.</summary>
    public AttendanceDayDto? DayFor(DateTime date) =>
        ByDate.TryGetValue(DateOnly.FromDateTime(date), out var day) ? day : null;
}

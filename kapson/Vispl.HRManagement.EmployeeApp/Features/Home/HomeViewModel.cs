using System.Collections.ObjectModel;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Api;

namespace Vispl.HRManagement.EmployeeApp.Features.Home;

/// <summary>
/// EMP-010. The identity card, the four tiles of 5.png, and the interviewer-only schedule card.
///
/// Tiles 3 and 4 are Pending Requests and Gate Passes (build plan D2). Anything the server still
/// returns carrying an <c>UnavailableReason</c> is filtered out rather than rendered as a dash.
/// </summary>
public sealed class HomeViewModel(MeApiClient me, NotificationsApiClient notifications) : ViewModelBase
{
    private EmployeeIdentityCardDto? identity;
    private EmployeeInterviewScheduleDto? interviews;
    private int unreadCount;
    private string greeting = "Welcome";

    public ObservableCollection<KpiDto> Tiles { get; } = [];

    public EmployeeIdentityCardDto? Identity
    {
        get => identity;
        private set => Set(ref identity, value);
    }

    /// <summary>Null until loaded, or when the schedule could not be read.</summary>
    public EmployeeInterviewScheduleDto? Interviews
    {
        get => interviews;
        private set => Set(ref interviews, value);
    }

    public int UnreadCount
    {
        get => unreadCount;
        private set => Set(ref unreadCount, value);
    }

    public string Greeting
    {
        get => greeting;
        private set => Set(ref greeting, value);
    }

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RunAsync(async () =>
        {
            var summary = await me.GetHomeSummaryAsync(ct);

            Identity = summary.Identity;
            Greeting = BuildGreeting(summary.Identity.DisplayName);

            Tiles.Clear();

            foreach (var tile in summary.Kpis.Where(item => string.IsNullOrWhiteSpace(item.UnavailableReason)))
            {
                Tiles.Add(tile);
            }

            if (Tiles.Count == 0)
            {
                Empty("Your tiles will appear once attendance and leave are set up for you.");
                return;
            }

            Ready();
        });

        // The badge and the interview card are softer concerns: a failure to read either must
        // not take the whole Home screen down with it.
        await RefreshUnreadAsync(ct);
        await RefreshInterviewsAsync(ct);
    }

    public async Task RefreshUnreadAsync(CancellationToken ct = default)
    {
        try
        {
            var page = await notifications.ListAsync(null, ct);
            UnreadCount = page.Items.Count(item => !item.IsRead);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unread count unavailable: {exception.Message}");
            UnreadCount = 0;
        }
    }

    private async Task RefreshInterviewsAsync(CancellationToken ct)
    {
        try
        {
            Interviews = await me.GetMyInterviewsAsync(ct);
        }
        catch (Exception exception)
        {
            // Hidden rather than broken: an interviewer sees no card this time and the rest of
            // Home still works. Nothing about the failure is shown as data.
            System.Diagnostics.Debug.WriteLine($"Interview schedule unavailable: {exception.Message}");
            Interviews = null;
        }
    }

    private static string BuildGreeting(string displayName)
    {
        var firstName = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                        ?? displayName;

        return DateTime.Now.Hour switch
        {
            < 12 => $"Good morning, {firstName}",
            < 17 => $"Good afternoon, {firstName}",
            _ => $"Good evening, {firstName}"
        };
    }
}

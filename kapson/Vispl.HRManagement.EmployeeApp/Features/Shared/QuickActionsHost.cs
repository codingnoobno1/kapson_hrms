using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Services;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Shared;

/// <summary>
/// EMP-063. What the raised coral (+) opens, on every tab (build plan D4): Punch in/out, Apply
/// Leave, Apply Gate Pass and Attendance correction.
///
/// Punch is complete here. The other three take the person to the tab where that form lives;
/// the forms themselves arrive in phases C and D of the build plan.
/// </summary>
public sealed class QuickActionsHost(
    AttendanceApiClient attendance,
    MeApiClient me,
    Leave.LeaveFlows leaveFlows,
    Presence.PresenceFlows presenceFlows)
{
    public void Present(AppSheet sheet) => _ = PresentAsync(sheet);

    /// <summary>
    /// Offers only what HR has switched on (HR portal → Settings → Employee App). If the switches
    /// cannot be read, everything is offered and the server's refusal is shown instead.
    /// </summary>
    private async Task PresentAsync(AppSheet sheet)
    {
        sheet.Present("Quick actions", Loading());

        Contracts.Mobile.Home.EmployeeAppFeaturesDto? features = null;
        try
        {
            features = await me.GetAppFeaturesAsync();
        }
        catch (MobileApiException)
        {
        }

        var actions = new List<QuickAction>();

        if (features?.AllowAppPunch ?? true)
        {
            actions.Add(new QuickAction("⏱", "Punch in / out", "Record your attendance now",
                Tokens.KpiRoseFrom, () => PresentPunchAsync(sheet)));
        }

        actions.Add(new QuickAction("\U0001F4DD", "Apply Leave", "Request time off",
            Tokens.KpiPeriwinkleFrom, () => leaveFlows.PresentApplyAsync(sheet)));

        if (features?.AllowGatePassRequests ?? true)
        {
            actions.Add(new QuickAction("\U0001F6AA", "Apply Gate Pass", "Early exit or late entry",
                Tokens.KpiPeachFrom, () => presenceFlows.PresentApplyPassAsync(sheet)));
        }

        if (features?.AllowAttendanceCorrections ?? true)
        {
            actions.Add(new QuickAction("\U0001F6E0", "Attendance correction", "Fix a missed punch",
                Tokens.KpiMintFrom, () => presenceFlows.PresentCorrectionAsync(sheet)));
        }

        requireLocation = features?.RequireLocationForPunch ?? false;

        sheet.Present("Quick actions", QuickActionsSheet.Build(actions));
    }

    /// <summary>HR's "Require location" switch, read with the quick actions (phase D, D-2).</summary>
    private bool requireLocation;

    /// <summary>
    /// One GPS fix for this punch, only when HR requires it. Nothing is tracked in the background, and
    /// a refused permission is reported rather than silently punching without the location HR asked for.
    /// </summary>
    private static async Task<Location> RequireLocationAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (status != PermissionStatus.Granted)
        {
            throw new InvalidOperationException("HR requires your location to punch. Allow location access for the app and try again.");
        }

        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)));

        return location ?? throw new InvalidOperationException("Your location could not be read. Turn on location services and try again.");
    }

    /// <summary>
    /// EMP-022. The punch sheet: the current state from the server, and one coral button.
    ///
    /// The <c>ClientEventId</c> is created once when the sheet opens and reused if the person
    /// retries after a dropped connection, so a retry is the same punch rather than a second one.
    /// </summary>
    private async Task PresentPunchAsync(AppSheet sheet)
    {
        sheet.Present("Punch in / out", Loading());

        try
        {
            var state = await attendance.GetPunchStateAsync();
            sheet.Present("Punch in / out", PunchBody(sheet, state, Guid.NewGuid().ToString("N")));
        }
        catch (MobileApiException exception)
        {
            sheet.Present("Punch in / out", Problem(exception));
        }
    }

    private View PunchBody(AppSheet sheet, PunchStateDto state, string clientEventId)
    {
        var action = string.Equals(state.NextAction, "out", StringComparison.OrdinalIgnoreCase) ? "out" : "in";

        var clock = new Label
        {
            Text = DateTime.Now.ToString("hh:mm tt"),
            FontSize = 40,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var feedback = new Label
        {
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };

        var last = state.LastPunch is null
            ? "You have not punched yet today."
            : $"Last punch: {Capitalise(state.LastPunch.Type)} at {state.LastPunch.OccurredUtc.ToLocalTime():hh:mm tt}";

        var button = new Button
        {
            Text = action == "out" ? "Check out" : "Check in",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 14,
            HeightRequest = 54,
            Margin = new Thickness(Tokens.SpaceGutter, 8)
        };

        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;
            button.Text = "Recording…";
            feedback.IsVisible = false;

            try
            {
                decimal? latitude = null;
                decimal? longitude = null;

                if (requireLocation)
                {
                    button.Text = "Reading location…";
                    var fix = await RequireLocationAsync();
                    latitude = (decimal)fix.Latitude;
                    longitude = (decimal)fix.Longitude;
                    button.Text = "Recording…";
                }

                var result = await attendance.PunchAsync(action, DeviceIdentity.Resolve(), clientEventId, latitude, longitude);

                feedback.Text = result.Warning is { Length: > 0 } warning
                    ? $"Recorded at {result.AcceptedUtc.ToLocalTime():hh:mm tt}. {warning}"
                    : $"Recorded at {result.AcceptedUtc.ToLocalTime():hh:mm tt}.";
                feedback.TextColor = Tokens.AttendancePresent;
                feedback.IsVisible = true;
                button.Text = "Done";

                await Task.Delay(1400);
                sheet.Hide();
            }
            catch (InvalidOperationException locationProblem)
            {
                feedback.Text = locationProblem.Message;
                feedback.TextColor = Tokens.StatusDanger;
                feedback.IsVisible = true;
                button.Text = action == "out" ? "Check out" : "Check in";
                button.IsEnabled = true;
            }
            catch (MobileApiException exception)
            {
                feedback.Text = exception switch
                {
                    MobileOfflineException => "No connection. Try again — the same punch will be sent, not a second one.",
                    MobileAccessDeniedException => "You are not allowed to punch from the app.",
                    MobileFeatureNotReadyException notReady => $"{notReady.Module} is not available yet.",
                    _ => exception.Message
                };
                feedback.TextColor = Tokens.StatusDanger;
                feedback.IsVisible = true;
                button.Text = action == "out" ? "Check out" : "Check in";
                button.IsEnabled = true;
            }
        };

        return new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(0, 12, 0, 28),
            Children =
            {
                Icons.Create(Icons.Clock, Tokens.BrandAccent, 34),
                clock,
                new Label
                {
                    Text = DateTime.Now.ToString("dddd, d MMMM yyyy"),
                    FontSize = 13,
                    TextColor = Tokens.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = last,
                    FontSize = 14,
                    TextColor = Tokens.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(Tokens.SpaceGutter, 10, Tokens.SpaceGutter, 0)
                },
                new Label
                {
                    Text = state.PolicySummary ?? string.Empty,
                    IsVisible = !string.IsNullOrWhiteSpace(state.PolicySummary),
                    FontSize = 12,
                    TextColor = Tokens.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(Tokens.SpaceGutter, 0)
                },
                button,
                feedback,

                // EW-057: said wherever a punch can be made.
                new Label
                {
                    Text = requireLocation
                        ? "This device's ID and your location at this moment are recorded with your punch."
                        : "This device's ID is recorded with your punch. Your location is not captured.",
                    FontSize = 11,
                    TextColor = Tokens.TextSecondary,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(Tokens.SpaceGutter, 6, Tokens.SpaceGutter, 0)
                }
            }
        };
    }

    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();

    private static View Loading() => new ActivityIndicator
    {
        IsRunning = true,
        Color = Tokens.BrandAccent,
        Margin = 32
    };

    private static View Problem(MobileApiException exception) => exception switch
    {
        MobileFeatureNotReadyException notReady => SheetContent.NotReady(notReady.Module),
        MobileAccessDeniedException => SheetContent.FieldCard(("Access", "You are not allowed to punch from the app.")),
        MobileOfflineException => SheetContent.FieldCard(("Offline", "No connection to the Kapsons server.")),
        _ => SheetContent.FieldCard(("Problem", exception.Message))
    };
}

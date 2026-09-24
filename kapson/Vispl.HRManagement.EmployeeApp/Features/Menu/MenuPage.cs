using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Menu;

/// <summary>
/// EMP-013. The Menu tab, exactly as the mockup groups it: My Profile, Leave Management,
/// Gate Pass.
///
/// Rows whose module is not built still appear and say so when tapped. Hiding them would make
/// the app look smaller than it is planned to be; letting them fail would be worse than either.
/// </summary>
public sealed class MenuPage : ContentPage
{
    private readonly EmployeeApiClient employees;
    private readonly PassesApiClient passes;
    private readonly MeApiClient me;
    private readonly MobileAuthenticator authenticator;
    private readonly MobileSession session;
    private readonly AppSheet sheet = new();
    private readonly BrandAppBar appBar = new();

    // Rows HR can switch off (HR portal → Settings → Employee App).
    private readonly MenuRow onboardingRow;
    private readonly MenuRow salaryRow;
    private readonly MenuRow bankRow;
    private readonly MenuRow applyPassRow;

    private EmployeeAppFeaturesDto? features;

    public MenuPage(
        EmployeeApiClient employees,
        PassesApiClient passes,
        MeApiClient me,
        MobileAuthenticator authenticator,
        MobileSession session,
        QuickActionsHost quickActions,
        Leave.LeaveFlows leaveFlows,
        Presence.PresenceFlows presenceFlows)
    {
        this.employees = employees;
        this.passes = passes;
        this.me = me;
        this.authenticator = authenticator;
        this.session = session;

        onboardingRow = new MenuRow("\U0001F4CB", "Employee Onboarding", "View onboarding status", ShowOnboardingAsync);
        salaryRow = new MenuRow("\U0001F4B0", "Salary Details", "View salary structure and components", ShowSalaryAsync);
        bankRow = new MenuRow("\U0001F3E6", "Bank Details", "View banking information", ShowBankAsync);
        applyPassRow = new MenuRow("\U0001F4DD", "Apply Gate Pass", "Request early exit or entry pass",
            () => presenceFlows.PresentApplyPassAsync(sheet));

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);

        var tabBar = new EmployeeTabBar(EmployeeTab.Menu, session);
        tabBar.QuickActionsRequested += (_, _) => quickActions.Present(sheet);
        BackgroundColor = Tokens.Background;
        Title = "Menu";

        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new MenuSectionHeader("\U0001F464", "My profile"),
                new MenuRow("\U0001F464", "Employee Profile", "View complete profile information", ShowProfileAsync),
                onboardingRow,
                salaryRow,
                bankRow,

                new MenuSectionHeader("\U0001F5D3", "Leave management"),
                new MenuRow("✏️", "Apply Leave", "Request a leave from work", () => leaveFlows.PresentApplyAsync(sheet)),
                new MenuRow("\U0001F4C4", "Leave History", "View your leave records", () => leaveFlows.PresentHistoryAsync(sheet)),
                new MenuRow("⏳", "Leave Pending for Approval", "Approve pending leave requests",
                    () => leaveFlows.PresentApprovalsAsync(sheet), isReady: session.Has("LEAVE.APPROVE")),

                new MenuSectionHeader("\U0001F6AA", "Gate pass"),
                applyPassRow,
                new MenuRow("\U0001F3AB", "My Gate Passes", "View and show your passes", () => presenceFlows.PresentMyPassesAsync(sheet)),
                new MenuRow("\U0001F6B6", "Going Out", "Tell HR you are stepping out, and when you are back",
                    () => presenceFlows.PresentMovementAsync(sheet)),
                new MenuRow("\U0001F6C2", "Passes Pending for Approval", "Approve gate pass requests",
                    () => presenceFlows.PresentApprovalsAsync(sheet, "passes"), isReady: session.Has("GATEPASS.APPROVE")),

                new MenuSectionHeader("\U0001F552", "Attendance"),
                new MenuRow("\U0001F6E0", "Attendance Correction", "Fix a missed or wrong punch",
                    () => presenceFlows.PresentCorrectionAsync(sheet)),
                new MenuRow("\U0001F4DD", "My Corrections", "Track your correction requests",
                    () => presenceFlows.PresentMyCorrectionsAsync(sheet)),
                new MenuRow("✅", "Corrections Pending for Approval", "Approve attendance corrections",
                    () => presenceFlows.PresentApprovalsAsync(sheet, "corrections"), isReady: session.Has("ATTENDANCE.APPROVE")),

                new MenuSectionHeader("\U0001F4CD", "On duty"),
                new MenuRow("\U0001F4CC", "Apply On Duty", "Request on-duty status",
                    () => presenceFlows.PresentApplyOnDutyAsync(sheet)),
                new MenuRow("\U0001F4C4", "My On Duty Requests", "View and track your on duty requests",
                    () => presenceFlows.PresentMyOnDutyAsync(sheet)),
                new MenuRow("\U0001F3AF", "Check On Duty Approval", "View and approve on-duty requests",
                    () => presenceFlows.PresentOnDutyApprovalsAsync(sheet), isReady: presenceFlows.CanApproveOnDuty),

                new MenuSectionHeader("⚙️", "Account"),
                new MenuRow("🎨", "Appearance", "Theme and widget colours", () => Shell.Current.GoToAsync("theme-settings")),
                new MenuRow("\U0001F514", "Notifications", "What has happened on your requests", GoToNotificationsAsync),
                new MenuRow("\U0001F6AA", "Sign out", "End this session on this device", SignOutAsync)
            }
        };

        sheet.Content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ],
            Children =
            {
                Row(appBar, 0),
                Row(new ScrollView { Content = stack }, 1),
                Row(tabBar, 2)
            }
        };

        Content = sheet;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ApplyFeaturesAsync();
    }

    /// <summary>
    /// Hides the rows HR has switched off. If the switches cannot be read the rows stay: the
    /// server refuses a switched-off feature anyway, and says so in the sheet.
    /// </summary>
    private async Task ApplyFeaturesAsync()
    {
        try
        {
            features = await me.GetAppFeaturesAsync();
            onboardingRow.IsVisible = features.ShowOnboarding;
            salaryRow.IsVisible = features.ShowSalaryDetails;
            bankRow.IsVisible = features.ShowBankDetails;
            applyPassRow.IsVisible = features.AllowGatePassRequests;
        }
        catch (MobileApiException)
        {
        }
    }

    /// <summary>EMP-042. Salary structure — fetched when opened, held only while the sheet shows.</summary>
    private Task ShowSalaryAsync() =>
        PresentAsync("Salary Details", async () => SelfServiceSheets.Salary(await me.GetMySalaryAsync()));

    /// <summary>EMP-043. The salary account, masked by the server.</summary>
    private Task ShowBankAsync() =>
        PresentAsync("Bank Details", async () =>
        {
            var account = await me.GetMyBankAccountAsync();
            var employeeId = session.EmployeeNumber;

            return SelfServiceSheets.Bank(
                account,
                requestChange: string.IsNullOrWhiteSpace(employeeId) || features?.AllowProfileChangeRequests == false
                    ? null
                    : () => ShowBankChangeRequestAsync(employeeId!, account));
        });

    /// <summary>
    /// EMP-044. The account number never leaves the device in the clear except over HTTPS to
    /// raise the request; the server encrypts it at rest and only ever hands the app a masked
    /// value back, including on this same screen after HR applies it.
    /// </summary>
    private Task ShowBankChangeRequestAsync(string employeeId, MyBankAccountDto current)
    {
        var bankName = new Entry { Text = current.BankName, FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "Bank name" };
        var holder = new Entry { Text = current.AccountHolderName, FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "Account holder name" };
        var accountNumber = new Entry { FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "New account number", Keyboard = Keyboard.Numeric };
        var ifsc = new Entry { Text = current.Ifsc, FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "IFSC" };
        var reason = new Editor { Placeholder = "Why are you asking for this change? (optional)", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60, FontSize = 14 };
        var error = new Label { FontSize = 12, TextColor = Tokens.StatusDanger, IsVisible = false, Margin = new Thickness(Tokens.SpaceGutter, 4) };

        var submit = new Button
        {
            Text = "Submit request",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            CornerRadius = 10,
            HeightRequest = 44,
            Margin = new Thickness(Tokens.SpaceGutter, 16, Tokens.SpaceGutter, 24)
        };

        submit.Clicked += async (_, _) =>
        {
            error.IsVisible = false;

            if (string.IsNullOrWhiteSpace(bankName.Text) || string.IsNullOrWhiteSpace(holder.Text) ||
                string.IsNullOrWhiteSpace(accountNumber.Text) || string.IsNullOrWhiteSpace(ifsc.Text))
            {
                error.Text = "Bank, account holder, account number and IFSC are all required.";
                error.IsVisible = true;
                return;
            }

            submit.IsEnabled = false;

            try
            {
                await employees.RaiseChangeRequestAsync(employeeId, new CreateEmployeeChangeRequest
                {
                    Section = EmployeeChangeRequestSections.BankAccount,
                    Changes =
                    [
                        new FieldChangeDto("BankName", current.BankName, bankName.Text.Trim()),
                        new FieldChangeDto("AccountHolderName", current.AccountHolderName, holder.Text.Trim()),
                        new FieldChangeDto("AccountNumber", current.MaskedAccountNumber, accountNumber.Text.Trim()),
                        new FieldChangeDto("Ifsc", current.Ifsc, ifsc.Text.Trim().ToUpperInvariant())
                    ],
                    Reason = string.IsNullOrWhiteSpace(reason.Text) ? null : reason.Text.Trim()
                });

                sheet.Present("Bank Details", SheetContent.FieldCard(
                    ("Request submitted", "HR will review this and apply it. You'll be notified either way.")));
            }
            catch (MobileApiException exception)
            {
                error.Text = exception.Message;
                error.IsVisible = true;
                submit.IsEnabled = true;
            }
        };

        var body = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                Field("Bank name", bankName),
                Field("Account holder name", holder),
                Field("New account number", accountNumber),
                Field("IFSC", ifsc),
                Field("Reason", reason),
                error,
                submit
            }
        };

        sheet.Present("Bank Details", new ScrollView { Content = body });
        return Task.CompletedTask;
    }

    /// <summary>EMP-041. Onboarding stage, progress and tasks.</summary>
    private Task ShowOnboardingAsync() =>
        PresentAsync("Employee Onboarding", async () => SelfServiceSheets.Onboarding(await me.GetMyOnboardingAsync()));

    private async Task PresentAsync(string title, Func<Task<View>> build)
    {
        sheet.Present(title, Loading());

        try
        {
            sheet.Present(title, await build());
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, Problem(exception));
        }
    }

    /// <summary>
    /// EMP-040. The profile sheet.
    ///
    /// The mockup shows resolved names — Operations, Kapsons Retail — where the DTOs carry
    /// codes. Until the masters cache is wired the code is shown as-is rather than a guessed
    /// label; a wrong department name is worse than an unfamiliar one.
    /// </summary>
    private async Task ShowProfileAsync()
    {
        var employeeId = session.EmployeeNumber;

        if (string.IsNullOrWhiteSpace(employeeId))
        {
            sheet.Present("Employee Profile", SheetContent.FieldCard(
                ("Profile", "This account is not linked to an employee record. Ask HR to check it.")));
            return;
        }

        sheet.Present("Employee Profile", Loading());

        try
        {
            var overview = await employees.GetOverviewAsync(employeeId);
            var personal = await employees.GetPersonalAsync(employeeId);

            var body = new VerticalStackLayout { Spacing = 0 };

            body.Add(SheetContent.FieldCard(
                ("Employee ID", overview.Summary.EmployeeNumber),
                ("Name", overview.Summary.DisplayName),
                ("Email", personal.Email),
                ("Phone", personal.Phone),
                ("Job title", overview.Summary.JobTitle),
                ("Department", overview.Summary.Department),
                ("Location", overview.Summary.Location),
                ("Joined", overview.JoinedOn?.ToString("d MMMM yyyy")),
                ("Reporting manager", overview.Manager?.DisplayName)));

            body.Add(new Label
            {
                Text = "Changes to your own record are raised as a request for HR to apply. " +
                       "You are never editing the master record directly.",
                FontSize = 11,
                TextColor = Tokens.TextSecondary,
                Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 20)
            });

            if (features?.AllowProfileChangeRequests != false)
            {
                var update = new Button
                {
                    Text = "Update Profile",
                    BackgroundColor = Tokens.BrandAccent,
                    TextColor = Tokens.OnAccent,
                    CornerRadius = 10,
                    FontSize = 13,
                    HeightRequest = 42,
                    Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 20)
                };

                update.Clicked += async (_, _) => await ShowUpdateProfileAsync(employeeId, personal);
                body.Add(update);
            }

            sheet.Present("Employee Profile", body);
        }
        catch (MobileApiException exception)
        {
            sheet.Present("Employee Profile", Problem(exception));
        }
    }

    /// <summary>
    /// EMP-041. Phone, preferred name and marital status are the only Personal-section fields
    /// this record actually carries for self-service today (there is no address or personal-email
    /// column) — submitting raises a change request (CR-7); HR applies or rejects it from the
    /// Change Requests queue on /hr/people.
    /// </summary>
    private static readonly (string Code, string Label)[] MaritalStatusOptions =
    [
        ("SINGLE", "Single"), ("MARRIED", "Married"), ("DIVORCED", "Divorced"), ("WIDOWED", "Widowed")
    ];

    private Task ShowUpdateProfileAsync(string employeeId, EmployeePersonalDto personal)
    {
        var phone = new Entry { Text = personal.Phone, FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "Phone number" };
        var preferredName = new Entry { Text = personal.PreferredName, FontSize = 14, TextColor = Tokens.TextPrimary, Placeholder = "Preferred name" };

        var maritalStatus = new Picker { FontSize = 14, TextColor = Tokens.TextPrimary, Title = "Marital status" };
        maritalStatus.ItemsSource = MaritalStatusOptions.Select(option => option.Label).ToList();
        var currentIndex = Array.FindIndex(MaritalStatusOptions, option => option.Code == personal.MaritalStatusCode);
        maritalStatus.SelectedIndex = currentIndex;

        var reason = new Editor
        {
            Placeholder = "Why are you asking for this change? (optional)",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 60,
            FontSize = 14
        };

        var error = new Label { FontSize = 12, TextColor = Tokens.StatusDanger, IsVisible = false, Margin = new Thickness(Tokens.SpaceGutter, 4) };

        var submit = new Button
        {
            Text = "Submit request",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            CornerRadius = 10,
            HeightRequest = 44,
            Margin = new Thickness(Tokens.SpaceGutter, 16, Tokens.SpaceGutter, 24)
        };

        submit.Clicked += async (_, _) =>
        {
            error.IsVisible = false;
            submit.IsEnabled = false;

            var changes = new List<FieldChangeDto>();

            if (phone.Text?.Trim() != (personal.Phone ?? string.Empty))
            {
                changes.Add(new FieldChangeDto("Phone", personal.Phone, phone.Text?.Trim()));
            }

            if (preferredName.Text?.Trim() != (personal.PreferredName ?? string.Empty))
            {
                changes.Add(new FieldChangeDto("PreferredName", personal.PreferredName, preferredName.Text?.Trim()));
            }

            var selectedCode = maritalStatus.SelectedIndex >= 0 ? MaritalStatusOptions[maritalStatus.SelectedIndex].Code : null;
            if (selectedCode != personal.MaritalStatusCode)
            {
                changes.Add(new FieldChangeDto("MaritalStatus", personal.MaritalStatusCode, selectedCode));
            }

            if (changes.Count == 0)
            {
                error.Text = "Nothing was changed.";
                error.IsVisible = true;
                submit.IsEnabled = true;
                return;
            }

            try
            {
                await employees.RaiseChangeRequestAsync(employeeId, new CreateEmployeeChangeRequest
                {
                    Section = EmployeeChangeRequestSections.Personal,
                    Changes = changes,
                    Reason = string.IsNullOrWhiteSpace(reason.Text) ? null : reason.Text.Trim()
                });

                sheet.Present("Update Profile", SheetContent.FieldCard(
                    ("Request submitted", "HR will review this and apply it. You'll be notified either way.")));
            }
            catch (MobileApiException exception)
            {
                error.Text = exception.Message;
                error.IsVisible = true;
                submit.IsEnabled = true;
            }
        };

        var body = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                Field("Phone", phone),
                Field("Preferred name", preferredName),
                Field("Marital status", maritalStatus),
                Field("Reason", reason),
                error,
                submit
            }
        };

        sheet.Present("Update Profile", new ScrollView { Content = body });
        return Task.CompletedTask;
    }

    private static View Field(string label, View input) => new VerticalStackLayout
    {
        Spacing = 4,
        Margin = new Thickness(Tokens.SpaceGutter, 6),
        Children =
        {
            new Label { Text = label, FontSize = 12, TextColor = Tokens.TextSecondary },
            new Border
            {
                BackgroundColor = Tokens.SurfaceMuted,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(10, 2),
                Content = input
            }
        }
    };

    private static Task GoToLeaveAsync() => Shell.Current.GoToAsync("//leave");

    private static Task GoToNotificationsAsync() => Shell.Current.GoToAsync("notifications");

    private async Task SignOutAsync()
    {
        var confirmed = await DisplayAlert(
            "Sign out",
            "You will need your username and password to sign back in.",
            "Sign out",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        await authenticator.SignOutAsync();

        Application.Current!.Windows[0].Page =
            App.Services.GetRequiredService<Features.Authentication.SignInPage>();
    }

    private static View Loading() => new ActivityIndicator
    {
        IsRunning = true,
        Color = Tokens.BrandAccent,
        Margin = 32
    };

    /// <summary>
    /// A failure inside a sheet gets the same treatment as one on a page: a 501 names its
    /// feature, and nothing renders a stack trace.
    /// </summary>
    private static View Problem(MobileApiException exception) => exception switch
    {
        MobileFeatureNotReadyException notReady => SheetContent.NotReady(notReady.Module),
        MobileAccessDeniedException => SheetContent.FieldCard(
            ("Access", "You do not have permission to see this.")),
        MobileOfflineException => SheetContent.FieldCard(
            ("Offline", "No connection to the Kapsons server.")),
        _ => SheetContent.FieldCard(("Problem", exception.Message))
    };

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}

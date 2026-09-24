using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Features.Leave;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Presence;

/// <summary>
/// Phase D presence sheets (plan §2.4, E02–E08): attendance correction, my corrections, apply gate
/// pass, my passes with the scannable credential, going out / I'm back, and the approvals sheet.
///
/// Every decision here calls the same service as the HR web: gate passes as the W08 Gate Pass
/// Approval List, corrections as the correction queue behind the Monthly Attendance Report. The
/// server enforces HR's switches (corrections, gate pass requests) whatever this screen shows.
/// </summary>
public sealed class PresenceFlows(
    AttendanceApiClient attendance, PassesApiClient passes, OnDutyApiClient onDuty, MobileSession session)
{
    // W08, sampled: DAY PASS and MULTI-DAY PASS pills on the HR web.
    private static readonly (Color Ground, Color Ink) DayPassPill = (Color.FromArgb("#EBF4FE"), Color.FromArgb("#2563EB"));
    private static readonly (Color Ground, Color Ink) MultiDayPill = (Color.FromArgb("#DCEEE0"), Color.FromArgb("#2F855A"));

    private static readonly (string Code, string Label)[] CorrectionReasons =
    [
        ("MISSED_PUNCH", "Forgot to punch"),
        ("DEVICE_ISSUE", "Device or app problem"),
        ("ON_DUTY", "On official duty outside"),
        ("OTHER", "Other")
    ];

    /// <summary>Raised after anything that changes attendance, passes or movement, so a page can reload.</summary>
    public event EventHandler? Changed;

    public bool CanApprovePasses => session.Has("GATEPASS.APPROVE");

    public bool CanApproveCorrections => session.Has("ATTENDANCE.APPROVE");

    public bool CanApproveOnDuty => session.Has("ONDUTY.APPROVE") || session.Has("TEAM.ONDUTY.APPROVE");

    // ---- E02 Attendance correction ------------------------------------------------

    public Task PresentCorrectionAsync(AppSheet sheet, AttendanceDayDto? day = null)
    {
        var today = DateTime.Today;
        var date = new DatePicker
        {
            Format = "d MMM yyyy",
            MinimumDate = today.AddDays(-30),
            MaximumDate = today,
            Date = day?.Date.ToDateTime(TimeOnly.MinValue) ?? today.AddDays(-1),
            TextColor = Tokens.TextPrimary
        };

        var checkIn = new TimePicker { Time = day?.CheckIn?.ToTimeSpan() ?? new TimeSpan(9, 0, 0), TextColor = Tokens.TextPrimary };
        var checkOut = new TimePicker { Time = day?.CheckOut?.ToTimeSpan() ?? new TimeSpan(18, 0, 0), TextColor = Tokens.TextPrimary };
        var includeOut = new Switch { IsToggled = true, OnColor = Tokens.BrandAccent };
        includeOut.Toggled += (_, _) => checkOut.IsEnabled = includeOut.IsToggled;

        var reason = new Picker { Title = "Reason", ItemsSource = CorrectionReasons.Select(item => item.Label).ToList(), SelectedIndex = 0, TextColor = Tokens.TextPrimary };
        var comment = new Editor { Placeholder = "Anything the approver should know", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70 };
        var error = ErrorLabel();
        var submit = LeaveFlows.PrimaryButton("Submit correction", Tokens.BrandAccent);

        submit.Clicked += async (_, _) =>
        {
            submit.IsEnabled = false;
            error.IsVisible = false;

            try
            {
                var workDate = date.Date.Date;
                var punches = new List<RequestedPunchDto> { new("in", ToUtc(workDate, checkIn.Time)) };

                if (includeOut.IsToggled)
                {
                    if (checkOut.Time <= checkIn.Time)
                    {
                        throw new InvalidOperationException("Check-out has to be after check-in.");
                    }

                    punches.Add(new RequestedPunchDto("out", ToUtc(workDate, checkOut.Time)));
                }

                await attendance.RequestCorrectionAsync(new CreateAttendanceCorrectionRequest
                {
                    Date = DateOnly.FromDateTime(workDate),
                    ReasonCode = CorrectionReasons[Math.Max(0, reason.SelectedIndex)].Code,
                    Comment = string.IsNullOrWhiteSpace(comment.Text) ? null : comment.Text.Trim(),
                    RequestedPunches = punches
                });

                Changed?.Invoke(this, EventArgs.Empty);
                await PresentMyCorrectionsAsync(sheet, "Correction sent for approval.");
            }
            catch (Exception exception) when (exception is MobileApiException or InvalidOperationException)
            {
                error.Text = exception.Message;
                error.IsVisible = true;
                submit.IsEnabled = true;
            }
        };

        sheet.Present("Attendance Correction", new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0, 4, 0, 28),
            Children =
            {
                Field("Date", date),
                Field("Correct check-in", checkIn),
                Toggle("Also correct check-out", includeOut),
                Field("Correct check-out", checkOut),
                Field("Reason", reason),
                Field("Comment", comment),
                error,
                submit,
                Note("An approver decides it. HR sees the same request on the web, and the day updates once it is approved.")
            }
        }, fullHeight: true);

        return Task.CompletedTask;
    }

    // ---- E03 My corrections ---------------------------------------------------------

    public async Task PresentMyCorrectionsAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "My Corrections";
        sheet.Present(title, LeaveFlows.Loading());

        try
        {
            var page = await attendance.GetMyCorrectionsAsync();
            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            if (page.Items.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("Corrections", "You have not asked for any corrections.")));
            }

            foreach (var correction in page.Items.OrderByDescending(item => item.Date))
            {
                var card = CardStack();
                card.Add(HeaderRow(correction.Date.ToString("ddd, d MMM yyyy"), StatusPill(correction.Status.Name, correction.Status.Tone)));
                card.Add(Line($"Asked for: {Punches(correction.RequestedPunches)}"));
                card.Add(Line($"Before: {Punches(correction.OriginalPunches)}"));
                card.Add(Line(correction.Reason));

                if (string.Equals(correction.Status.Code, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    var withdraw = SmallButton("Withdraw", Tokens.SecondaryButton, Tokens.TextPrimary);
                    var captured = correction;
                    withdraw.Clicked += async (_, _) =>
                    {
                        withdraw.IsEnabled = false;
                        await RunAsync(sheet, () => attendance.WithdrawCorrectionAsync(captured.Id),
                            () => PresentMyCorrectionsAsync(sheet, "Correction withdrawn."));
                    };
                    card.Add(withdraw);
                }

                body.Add(Card(card));
            }

            var newOne = LeaveFlows.PrimaryButton("New correction", Tokens.BrandAccent);
            newOne.Clicked += async (_, _) => await PresentCorrectionAsync(sheet);
            body.Add(newOne);

            sheet.Present(title, body, fullHeight: page.Items.Count > 3);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception));
        }
    }

    // ---- E04 Apply gate pass ----------------------------------------------------------

    public Task PresentApplyPassAsync(AppSheet sheet)
    {
        var multiDay = false;
        var dayButton = SegmentButton("Day pass", selected: true);
        var multiButton = SegmentButton("Multi-day pass", selected: false);

        var fromDate = new DatePicker { Format = "d MMM yyyy", MinimumDate = DateTime.Today, Date = DateTime.Today, TextColor = Tokens.TextPrimary };
        var toDate = new DatePicker { Format = "d MMM yyyy", MinimumDate = DateTime.Today, Date = DateTime.Today.AddDays(1), TextColor = Tokens.TextPrimary };
        var fromTime = new TimePicker { Time = RoundedNow(), TextColor = Tokens.TextPrimary };
        var toTime = new TimePicker { Time = RoundedNow().Add(TimeSpan.FromHours(2)), TextColor = Tokens.TextPrimary };
        var purpose = new Editor { Placeholder = "Purpose, e.g. Client site visit", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70 };
        var destination = new Entry { Placeholder = "Destination (optional)" };

        var toDateField = Field("To date", toDate);
        var timesRow = TwoColumns(Field("From time", fromTime), Field("To time", toTime));
        toDateField.IsVisible = false;

        void Select(bool isMultiDay)
        {
            multiDay = isMultiDay;
            Style(dayButton, !isMultiDay);
            Style(multiButton, isMultiDay);
            toDateField.IsVisible = isMultiDay;
            timesRow.IsVisible = !isMultiDay;
        }

        dayButton.Clicked += (_, _) => Select(false);
        multiButton.Clicked += (_, _) => Select(true);

        var error = ErrorLabel();
        var submit = LeaveFlows.PrimaryButton("Request gate pass", Tokens.BrandAccent);

        submit.Clicked += async (_, _) =>
        {
            error.IsVisible = false;

            if (string.IsNullOrWhiteSpace(purpose.Text))
            {
                error.Text = "Say what the pass is for.";
                error.IsVisible = true;
                return;
            }

            submit.IsEnabled = false;

            try
            {
                var from = DateOnly.FromDateTime(fromDate.Date);
                await passes.RequestPassAsync(new RequestGatePassRequest
                {
                    PassType = multiDay ? "MultiDayPass" : "DayPass",
                    FromDate = from,
                    ToDate = multiDay ? DateOnly.FromDateTime(toDate.Date) : from,
                    FromTime = multiDay ? null : TimeOnly.FromTimeSpan(fromTime.Time),
                    ToTime = multiDay ? null : TimeOnly.FromTimeSpan(toTime.Time),
                    Purpose = purpose.Text.Trim(),
                    DestinationLabel = string.IsNullOrWhiteSpace(destination.Text) ? null : destination.Text.Trim()
                });

                Changed?.Invoke(this, EventArgs.Empty);
                await PresentMyPassesAsync(sheet, "Gate pass requested. You'll be notified when it is decided.");
            }
            catch (MobileApiException exception)
            {
                error.Text = exception.Message;
                error.IsVisible = true;
                submit.IsEnabled = true;
            }
        };

        sheet.Present("Apply Gate Pass", new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0, 4, 0, 28),
            Children =
            {
                TwoColumns(dayButton, multiButton).WithMargin(new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 6)),
                Field("From date", fromDate),
                toDateField,
                timesRow,
                Field("Purpose", purpose),
                Field("Destination", destination),
                error,
                submit
            }
        }, fullHeight: true);

        return Task.CompletedTask;
    }

    // ---- E05 / E06 My gate passes and the credential ------------------------------------

    public async Task PresentMyPassesAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "My Gate Passes";
        sheet.Present(title, LeaveFlows.Loading());

        try
        {
            var rows = await passes.GetMyPassesAsync();
            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            if (rows.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("Gate passes", "You have not requested any passes.")));
            }

            foreach (var pass in rows.OrderByDescending(item => item.FromDate).Take(25))
            {
                var card = CardStack();
                card.Add(HeaderRow(Window(pass), StatusPill(pass.Status.Name, pass.Status.Tone)));
                card.Add(PassTypePill(pass.PassType));
                card.Add(Line(pass.Purpose));

                if (!string.IsNullOrWhiteSpace(pass.DecisionComment))
                {
                    card.Add(Line($"Note: {pass.DecisionComment}"));
                }

                var buttons = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
                var captured = pass;

                if (string.Equals(pass.Status.Code, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    var show = SmallButton("Show pass", Tokens.BrandAccent, Colors.White);
                    show.Clicked += async (_, _) => await PresentCredentialAsync(sheet, captured);
                    buttons.Add(show);
                }

                if (pass.Actions.FirstOrDefault(action => action.Action == "withdraw") is { IsAllowed: true })
                {
                    var withdraw = SmallButton("Withdraw", Tokens.SecondaryButton, Tokens.TextPrimary);
                    withdraw.Clicked += async (_, _) =>
                    {
                        withdraw.IsEnabled = false;
                        await RunAsync(sheet, () => passes.WithdrawPassAsync(captured.Id),
                            () => PresentMyPassesAsync(sheet, "Pass withdrawn."));
                    };
                    buttons.Add(withdraw);
                }

                if (buttons.Children.Count > 0)
                {
                    card.Add(buttons);
                }

                body.Add(Card(card));
            }

            var apply = LeaveFlows.PrimaryButton("Apply for a gate pass", Tokens.BrandAccent);
            apply.Clicked += async (_, _) => await PresentApplyPassAsync(sheet);
            body.Add(apply);

            sheet.Present(title, body, fullHeight: rows.Count > 2);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception));
        }
    }

    /// <summary>
    /// E06. The gate scans this QR. It is the signed verification token issued on approval — not
    /// the employee number — and it is held only while the sheet is open.
    /// </summary>
    private async Task PresentCredentialAsync(AppSheet sheet, GatePassRowDto pass)
    {
        const string title = "Gate Pass";
        sheet.Present(title, LeaveFlows.Loading(), fullHeight: true);

        try
        {
            var credential = await passes.GetCredentialAsync(pass.Id);
            var back = LeaveFlows.SecondaryButton("Back to my passes");
            back.Clicked += async (_, _) => await PresentMyPassesAsync(sheet);

            sheet.Present(title, new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(Tokens.SpaceGutter, 12, Tokens.SpaceGutter, 28),
                Children =
                {
                    PassTypePill(pass.PassType).WithMargin(new Thickness(0)),
                    new Label { Text = pass.DisplayName, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, HorizontalTextAlignment = TextAlignment.Center },
                    new Border
                    {
                        BackgroundColor = Colors.White,
                        Stroke = Tokens.Border,
                        StrokeThickness = 1,
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        Padding = 16,
                        HorizontalOptions = LayoutOptions.Center,
                        Content = new Image { Source = IdentityCard.RenderQr(credential.VerificationToken), WidthRequest = 240, HeightRequest = 240 }
                    },
                    new Label
                    {
                        Text = credential.ValidFrom == credential.ValidTo
                            ? $"Valid on {credential.ValidFrom:ddd, d MMM yyyy}"
                            : $"Valid {credential.ValidFrom:d MMM} – {credential.ValidTo:d MMM yyyy}",
                        FontSize = 14,
                        TextColor = Tokens.TextPrimary,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label { Text = pass.Purpose, FontSize = 13, TextColor = Tokens.TextSecondary, HorizontalTextAlignment = TextAlignment.Center },
                    new Label
                    {
                        Text = "Show this to security at the gate. Don't share a screenshot — the code is yours alone.",
                        FontSize = 11,
                        TextColor = Tokens.TextSecondary,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    back
                }
            }, fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception));
        }
    }

    // ---- E07 Going out / I'm back ---------------------------------------------------------

    public async Task PresentMovementAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "Going Out";
        sheet.Present(title, LeaveFlows.Loading());

        try
        {
            var movements = await passes.GetMyMovementsAsync();
            var open = movements.FirstOrDefault(item => item.ActualReturnUtc is null);
            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            if (open is not null)
            {
                var card = CardStack();
                card.Add(HeaderRow($"{open.Kind} · out since {open.GoingOutUtc.ToLocalTime():h:mm tt}",
                    StatusPill(open.IsOverdue ? "Overdue" : "Out", open.IsOverdue ? "danger" : "warning")));
                card.Add(Line($"Expected back {open.ExpectedReturnUtc.ToLocalTime():h:mm tt}"));
                card.Add(Line(open.Detail));
                body.Add(Card(card));

                var back = LeaveFlows.PrimaryButton("I'm back", Tokens.DecisionApprove);
                back.Clicked += async (_, _) =>
                {
                    back.IsEnabled = false;
                    await RunAsync(sheet, () => passes.RecordReturnAsync(open.Id, new ReturnMovementRequest()),
                        () => PresentMovementAsync(sheet, "Welcome back — your return is recorded."));
                };
                body.Add(back);
                sheet.Present(title, body);
                return;
            }

            var official = false;
            var personalButton = SegmentButton("Personal", selected: true);
            var officialButton = SegmentButton("Official", selected: false);
            personalButton.Clicked += (_, _) => { official = false; Style(personalButton, true); Style(officialButton, false); };
            officialButton.Clicked += (_, _) => { official = true; Style(personalButton, false); Style(officialButton, true); };

            var expectedBack = new TimePicker { Time = RoundedNow().Add(TimeSpan.FromHours(1)), TextColor = Tokens.TextPrimary };
            var detail = new Editor { Placeholder = "Where and why", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70 };
            var destination = new Entry { Placeholder = "Destination (optional)" };
            var error = ErrorLabel();
            var declare = LeaveFlows.PrimaryButton("I'm going out", Tokens.BrandAccent);

            declare.Clicked += async (_, _) =>
            {
                error.IsVisible = false;

                if (string.IsNullOrWhiteSpace(detail.Text))
                {
                    error.Text = "Say where you are going.";
                    error.IsVisible = true;
                    return;
                }

                declare.IsEnabled = false;

                try
                {
                    await passes.DeclareOutAsync(new DeclareMovementRequest
                    {
                        Kind = official ? "Official" : "Personal",
                        ExpectedReturnUtc = ToUtc(DateTime.Today, expectedBack.Time),
                        Detail = detail.Text.Trim(),
                        DestinationLabel = string.IsNullOrWhiteSpace(destination.Text) ? null : destination.Text.Trim()
                    });

                    Changed?.Invoke(this, EventArgs.Empty);
                    await PresentMovementAsync(sheet, "Recorded. Tap I'm back when you return.");
                }
                catch (MobileApiException exception)
                {
                    error.Text = exception.Message;
                    error.IsVisible = true;
                    declare.IsEnabled = true;
                }
            };

            body.Add(TwoColumns(personalButton, officialButton).WithMargin(new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 6)));
            body.Add(Field("Expected back", expectedBack));
            body.Add(Field("Reason", detail));
            body.Add(Field("Destination", destination));
            body.Add(error);
            body.Add(declare);
            body.Add(Note("HR sees who is off site on the web and is alerted when a return is overdue."));

            sheet.Present(title, body, fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception));
        }
    }

    // ---- Apply On Duty --------------------------------------------------------------------

    public Task PresentApplyOnDutyAsync(AppSheet sheet)
    {
        var purpose = new Entry { Placeholder = "e.g. Client Meeting, Conference, etc." };
        var fromDate = new DatePicker { Format = "d MMM yyyy", MinimumDate = DateTime.Today, Date = DateTime.Today, TextColor = Tokens.TextPrimary };
        var toDate = new DatePicker { Format = "d MMM yyyy", MinimumDate = DateTime.Today, Date = DateTime.Today, TextColor = Tokens.TextPrimary };
        var location = new Entry { Placeholder = "Enter location" };
        var reason = new Editor { Placeholder = "Provide reason for leave...", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70 };
        var error = ErrorLabel();
        var submit = LeaveFlows.PrimaryButton("Submit Application", Tokens.BrandAccent);

        submit.Clicked += async (_, _) =>
        {
            error.IsVisible = false;

            if (string.IsNullOrWhiteSpace(purpose.Text) || string.IsNullOrWhiteSpace(location.Text) ||
                string.IsNullOrWhiteSpace(reason.Text))
            {
                error.Text = "Purpose, location and reason are all required.";
                error.IsVisible = true;
                return;
            }

            submit.IsEnabled = false;

            try
            {
                await onDuty.RequestAsync(new RequestOnDutyRequest
                {
                    Purpose = purpose.Text.Trim(),
                    FromDate = DateOnly.FromDateTime(fromDate.Date),
                    ToDate = DateOnly.FromDateTime(toDate.Date),
                    Location = location.Text.Trim(),
                    Reason = reason.Text.Trim()
                });

                Changed?.Invoke(this, EventArgs.Empty);
                await PresentMyOnDutyAsync(sheet, "On duty requested. You'll be notified when it is decided.");
            }
            catch (MobileApiException exception)
            {
                error.Text = exception.Message;
                error.IsVisible = true;
                submit.IsEnabled = true;
            }
        };

        sheet.Present("Apply On Duty", new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0, 4, 0, 28),
            Children =
            {
                Field("On Duty Purpose", purpose),
                Field("From Date", fromDate),
                Field("To Date", toDate),
                Field("Location/Destination", location),
                Field("Reason", reason),
                error,
                submit
            }
        }, fullHeight: true);

        return Task.CompletedTask;
    }

    // ---- My on duty requests --------------------------------------------------------------

    public async Task PresentMyOnDutyAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "My On Duty Requests";
        sheet.Present(title, LeaveFlows.Loading());

        try
        {
            var rows = await onDuty.GetMineAsync();
            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            if (rows.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("On duty", "You have not requested to be on duty.")));
            }

            foreach (var request in rows.OrderByDescending(item => item.FromDate))
            {
                var card = CardStack();
                card.Add(HeaderRow(request.Purpose, StatusPill(request.Status.Name, request.Status.Tone)));
                card.Add(Line(Window(request)));
                card.Add(Line(request.Location));

                if (!string.IsNullOrWhiteSpace(request.DecisionComment))
                {
                    card.Add(Line($"Note: {request.DecisionComment}"));
                }

                if (request.Actions.FirstOrDefault(action => action.Action == "withdraw") is { IsAllowed: true })
                {
                    var captured = request;
                    var withdraw = SmallButton("Withdraw", Tokens.SecondaryButton, Tokens.TextPrimary);
                    withdraw.Clicked += async (_, _) =>
                    {
                        withdraw.IsEnabled = false;
                        await RunAsync(sheet, () => onDuty.WithdrawAsync(captured.Id),
                            () => PresentMyOnDutyAsync(sheet, "On duty request withdrawn."));
                    };
                    card.Add(withdraw);
                }

                body.Add(Card(card));
            }

            var apply = LeaveFlows.PrimaryButton("Apply On Duty", Tokens.BrandAccent);
            apply.Clicked += async (_, _) => await PresentApplyOnDutyAsync(sheet);
            body.Add(apply);

            sheet.Present(title, body, fullHeight: rows.Count > 2);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception));
        }
    }

    // ---- Check On Duty Approval ------------------------------------------------------------

    public async Task PresentOnDutyApprovalsAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "Check On Duty Approval";
        sheet.Present(title, LeaveFlows.Loading(), fullHeight: true);

        try
        {
            var pending = CanApproveOnDuty ? await onDuty.GetPendingAsync() : [];
            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            if (pending.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("On duty", "No on duty requests are waiting for a decision.")));
            }

            foreach (var request in pending)
            {
                var card = CardStack();
                card.Add(HeaderRow(request.DisplayName, StatusPill(Window(request), "info")));
                card.Add(Line(string.Join(" . ", new[] { request.Designation, request.DepartmentCode }.Where(part => !string.IsNullOrWhiteSpace(part)))));
                card.Add(Line(request.Purpose));
                card.Add(Line(request.Location));
                card.Add(Line(request.Reason));
                card.Add(DecisionRow(sheet,
                    approve: () => onDuty.DecideAsync(request.Id, new OnDutyDecisionRequest { Decision = "approve" }),
                    reject: comment => onDuty.DecideAsync(request.Id, new OnDutyDecisionRequest { Decision = "reject", Comment = comment }),
                    subject: $"{request.DisplayName}'s on duty request",
                    reload: notice => PresentOnDutyApprovalsAsync(sheet, notice)));
                body.Add(Card(card));
            }

            sheet.Present(title, body, fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception), fullHeight: true);
        }
    }

    private static string Window(OnDutyRowDto request) =>
        request.FromDate == request.ToDate
            ? $"{request.FromDate:ddd, d MMM yyyy}"
            : $"{request.FromDate:d MMM} - {request.ToDate:d MMM yyyy}";

    // ---- E08 Approvals: gate passes and corrections ------------------------------------------

    public async Task PresentApprovalsAsync(AppSheet sheet, string tab = "passes", string? notice = null)
    {
        const string title = "Presence Approvals";
        sheet.Present(title, LeaveFlows.Loading(), fullHeight: true);

        try
        {
            var pendingPasses = CanApprovePasses ? (await passes.GetPendingPassesAsync()).Rows.Items : [];
            var pendingCorrections = CanApproveCorrections ? (await attendance.GetCorrectionQueueAsync()).Items : [];

            if (!CanApprovePasses)
            {
                tab = "corrections";
            }

            var body = Body();

            if (notice is not null)
            {
                body.Add(Notice(notice));
            }

            var passesTab = SegmentButton($"Gate passes ({pendingPasses.Count})", tab == "passes");
            var correctionsTab = SegmentButton($"Corrections ({pendingCorrections.Count})", tab == "corrections");
            passesTab.IsEnabled = CanApprovePasses;
            correctionsTab.IsEnabled = CanApproveCorrections;
            passesTab.Clicked += async (_, _) => await PresentApprovalsAsync(sheet, "passes");
            correctionsTab.Clicked += async (_, _) => await PresentApprovalsAsync(sheet, "corrections");
            body.Add(TwoColumns(passesTab, correctionsTab).WithMargin(new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 10)));

            if (tab == "passes")
            {
                if (pendingPasses.Count == 0)
                {
                    body.Add(SheetContent.FieldCard(("Gate passes", "No gate passes are waiting for a decision.")));
                }

                foreach (var pass in pendingPasses)
                {
                    var card = CardStack();
                    card.Add(HeaderRow(pass.DisplayName, PassTypePill(pass.PassType)));
                    card.Add(Line(string.Join(" . ", new[] { pass.Designation, pass.DepartmentCode }.Where(part => !string.IsNullOrWhiteSpace(part)))));
                    card.Add(Line(Window(pass)));
                    card.Add(Line(pass.Purpose));
                    card.Add(DecisionRow(sheet,
                        approve: () => passes.DecidePassAsync(pass.Id, new GatePassDecisionRequest { Decision = "approve" }),
                        reject: reason => passes.DecidePassAsync(pass.Id, new GatePassDecisionRequest { Decision = "reject", Comment = reason }),
                        subject: $"{pass.DisplayName}'s pass",
                        reload: notice => PresentApprovalsAsync(sheet, "passes", notice)));
                    body.Add(Card(card));
                }
            }
            else
            {
                if (pendingCorrections.Count == 0)
                {
                    body.Add(SheetContent.FieldCard(("Corrections", "No corrections are waiting for a decision.")));
                }

                foreach (var correction in pendingCorrections)
                {
                    var card = CardStack();
                    card.Add(HeaderRow(correction.EmployeeName ?? "Employee", StatusPill(correction.Date.ToString("d MMM"), "info")));
                    card.Add(Line($"Asked for: {Punches(correction.RequestedPunches)}"));
                    card.Add(Line($"Recorded: {Punches(correction.OriginalPunches)}"));
                    card.Add(Line(correction.Reason));
                    card.Add(DecisionRow(sheet,
                        approve: () => attendance.DecideCorrectionAsync(correction.Id, new AttendanceCorrectionDecisionRequest { Decision = "approve" }),
                        reject: reason => attendance.DecideCorrectionAsync(correction.Id, new AttendanceCorrectionDecisionRequest
                        {
                            Decision = "reject",
                            ReasonCode = "REJECTED",
                            Comment = reason
                        }),
                        subject: $"{correction.EmployeeName ?? "the"} correction",
                        reload: notice => PresentApprovalsAsync(sheet, "corrections", notice)));
                    body.Add(Card(card));
                }
            }

            sheet.Present(title, body, fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, LeaveFlows.Problem(exception), fullHeight: true);
        }
    }

    /// <summary>Approve at once; Reject opens a reason box first, because the requester is shown it.</summary>
    private View DecisionRow(
        AppSheet sheet,
        Func<Task<Contracts.Mobile.Shared.MutationResultDto>> approve,
        Func<string, Task<Contracts.Mobile.Shared.MutationResultDto>> reject,
        string subject,
        Func<string, Task> reload)
    {
        var reason = new Editor { Placeholder = "Reason for rejecting", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 60, IsVisible = false };
        var approveButton = SmallButton("✓ Approve", Tokens.DecisionApprove, Colors.White);
        var rejectButton = SmallButton("✕ Reject", Tokens.BrandAccent, Colors.White);

        approveButton.Clicked += async (_, _) =>
        {
            approveButton.IsEnabled = false;
            await RunAsync(sheet, approve, () => reload($"Approved {subject}."));
        };

        rejectButton.Clicked += async (_, _) =>
        {
            if (!reason.IsVisible)
            {
                reason.IsVisible = true;
                rejectButton.Text = "✕ Confirm reject";
                return;
            }

            if (string.IsNullOrWhiteSpace(reason.Text))
            {
                reason.Placeholder = "A reason is required";
                return;
            }

            rejectButton.IsEnabled = false;
            await RunAsync(sheet, () => reject(reason.Text.Trim()), () => reload($"Rejected {subject}."));
        };

        return new VerticalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                reason,
                TwoColumns(approveButton, rejectButton)
            }
        };
    }

    // ---- Parts ---------------------------------------------------------------------------------

    private async Task RunAsync<T>(AppSheet sheet, Func<Task<T>> action, Func<Task> then)
    {
        try
        {
            await action();
            Changed?.Invoke(this, EventArgs.Empty);
            await then();
        }
        catch (MobileApiException exception)
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert("Not done", exception.Message, "OK");
            await then();
        }
    }

    private static DateTimeOffset ToUtc(DateTime date, TimeSpan time)
    {
        var local = date.Date + time;
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeSpan RoundedNow()
    {
        var now = DateTime.Now.TimeOfDay;
        return TimeSpan.FromMinutes(Math.Ceiling(now.TotalMinutes / 15) * 15);
    }

    private static string Window(GatePassRowDto pass) =>
        pass.FromDate == pass.ToDate
            ? $"{pass.FromDate:ddd, d MMM}{(pass.FromTime is { } from ? $" · {from:h:mm tt}" : "")}{(pass.ToTime is { } to ? $" – {to:h:mm tt}" : "")}"
            : $"{pass.FromDate:d MMM} – {pass.ToDate:d MMM yyyy}";

    private static string Punches(IReadOnlyList<AttendancePunchDto> punches) =>
        punches.Count == 0
            ? "no punches"
            : string.Join(" · ", punches.OrderBy(punch => punch.OccurredUtc)
                .Select(punch => $"{Capitalise(punch.Type)} {punch.OccurredUtc.ToLocalTime():h:mm tt}"));

    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();

    private static VerticalStackLayout Body() => new() { Spacing = 0, Padding = new Thickness(0, 4, 0, 28) };

    private static VerticalStackLayout CardStack() => new() { Spacing = 4, Padding = new Thickness(14, 12) };

    private static View Card(View content) => new Border
    {
        BackgroundColor = Tokens.Surface,
        Stroke = Color.FromArgb("#EEEEEE"),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 10 },
        Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 10),
        Content = content
    };

    private static View HeaderRow(string title, View trailing)
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
        grid.Add(new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, VerticalTextAlignment = TextAlignment.Center }, 0);
        grid.Add(trailing, 1);
        return grid;
    }

    private static Label Line(string text) => new() { Text = text, FontSize = 12, TextColor = Tokens.TextSecondary };

    private static View StatusPill(string text, string tone) => new Border
    {
        BackgroundColor = Tokens.ForTone(tone),
        StrokeThickness = 0,
        Padding = new Thickness(10, 3),
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        VerticalOptions = LayoutOptions.Center,
        Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Tokens.OnTone(tone) }
    };

    private static View PassTypePill(string passType)
    {
        var (ground, ink) = passType == "MultiDayPass" ? MultiDayPill : DayPassPill;

        return new Border
        {
            BackgroundColor = ground,
            StrokeThickness = 0,
            Padding = new Thickness(12, 3),
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = passType == "MultiDayPass" ? "MULTI-DAY PASS" : "DAY PASS", FontSize = 11, TextColor = ink }
        };
    }

    private static Button SmallButton(string text, Color ground, Color ink) => new()
    {
        Text = text,
        BackgroundColor = ground,
        TextColor = ink,
        FontSize = 13,
        CornerRadius = 6,
        HeightRequest = 38,
        Padding = new Thickness(14, 0)
    };

    private static Button SegmentButton(string text, bool selected)
    {
        var button = new Button { Text = text, FontSize = 13, CornerRadius = 999, HeightRequest = 38 };
        Style(button, selected);
        return button;
    }

    private static void Style(Button button, bool selected)
    {
        button.BackgroundColor = selected ? Tokens.BrandAccent : Tokens.PeriodChipInactive;
        button.TextColor = selected ? Colors.White : Tokens.PeriodChipInactiveText;
    }

    private static Grid TwoColumns(View left, View right)
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] };
        grid.Add(left, 0);
        grid.Add(right, 1);
        return grid;
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

    private static View Toggle(string text, Switch toggle)
    {
        var grid = new Grid
        {
            Margin = new Thickness(Tokens.SpaceGutter, 6),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        grid.Add(new Label { Text = text, FontSize = 14, TextColor = Tokens.TextPrimary, VerticalTextAlignment = TextAlignment.Center }, 0);
        grid.Add(toggle, 1);
        return grid;
    }

    private static Label ErrorLabel() => new()
    {
        FontSize = 12,
        TextColor = Tokens.StatusDanger,
        IsVisible = false,
        Margin = new Thickness(Tokens.SpaceGutter, 4)
    };

    private static Label Notice(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextColor = Tokens.DecisionApprove,
        Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8)
    };

    private static Label Note(string text) => new()
    {
        Text = text,
        FontSize = 11,
        TextColor = Tokens.TextSecondary,
        Margin = new Thickness(Tokens.SpaceGutter, 10, Tokens.SpaceGutter, 0)
    };
}

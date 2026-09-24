using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Leave;

/// <summary>
/// Phase C leave sheets, shared by the Leave tab, the Menu and the (+) quick actions:
/// EMP-030 Apply Leave, EMP-032 Leave History (4.png), EMP-033 request detail and EMP-035/036
/// Leave Pending for Approval.
///
/// Every number comes from the server: chargeable days from <c>leave/requests/calculate</c>, the
/// queue from <c>leave/approvals</c> — the same queue the HR portal's Leave page works from — and
/// decisions go through the same service the portal calls, so a request decided in either place
/// is gone from both.
/// </summary>
public sealed class LeaveFlows(LeaveApiClient leave, MobileSession session)
{
    /// <summary>Raised after anything that changes balances or requests, so a page can reload.</summary>
    public event EventHandler? Changed;

    public bool CanApprove => session.Has("LEAVE.APPROVE");

    // ---- EMP-032 Leave History ------------------------------------------------

    public async Task PresentHistoryAsync(AppSheet sheet)
    {
        sheet.Present("Leave History", Loading());

        try
        {
            var requests = await leave.GetAllMyRequestsAsync();
            var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 6, 0, 24) };

            if (requests.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("Leave history", "You have not applied for any leave yet.")));
            }

            foreach (var request in requests)
            {
                var row = HistoryRow(request);
                var captured = request;
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await PresentDetailAsync(sheet, captured.Id, () => PresentHistoryAsync(sheet));
                row.GestureRecognizers.Add(tap);
                body.Add(row);
            }

            sheet.Present("Leave History", body, fullHeight: requests.Count > 4);
        }
        catch (MobileApiException exception)
        {
            sheet.Present("Leave History", Problem(exception));
        }
    }

    /// <summary>C14: emoji, type, "5 Dec 2024 - 7 Dec 2024 (3 days) • Approved". Status is text, as the image.</summary>
    private static View HistoryRow(LeaveRequestListItemDto request)
    {
        var grid = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 14,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(new Label
        {
            Text = request.Emoji,
            FontSize = 26,
            VerticalTextAlignment = TextAlignment.Center
        }, 0);

        grid.Add(new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = request.LeaveTypeName ?? request.LeaveTypeCode,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Tokens.TextPrimary
                },
                new Label
                {
                    Text = $"{Range(request.From, request.To)} ({Days(request.Days)}) • {request.Status.Name}",
                    FontSize = 12,
                    TextColor = Tokens.TextSecondary
                }
            }
        }, 1);

        var card = new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Color.FromArgb("#EEEEEE"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 10),
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.06f, Radius = 6, Offset = new Point(0, 2) },
            Content = grid
        };

        SemanticProperties.SetDescription(card,
            $"{request.LeaveTypeName}, {Range(request.From, request.To)}, {request.Status.Name}");
        return card;
    }

    // ---- EMP-033 Request detail ------------------------------------------------

    public async Task PresentDetailAsync(AppSheet sheet, string requestId, Func<Task>? back = null)
    {
        sheet.Present("Leave request", Loading());

        try
        {
            var detail = await leave.GetRequestAsync(requestId);
            var summary = detail.Summary;
            var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

            body.Add(SheetContent.FieldCard(
                ("Leave type", $"{summary.Emoji} {summary.LeaveTypeName ?? summary.LeaveTypeCode}"),
                ("Dates", Range(summary.From, summary.To)),
                ("Days", Days(summary.Days)),
                ("Status", summary.Status.Name),
                ("Reason", detail.Reason),
                ("Applied on", summary.SubmittedUtc.ToLocalTime().ToString("d MMM yyyy, h:mm tt"))));

            if (detail.Workflow.Count > 0)
            {
                body.Add(SheetContent.SectionHeader("History"));
                body.Add(SheetContent.FieldCard(detail.Workflow
                    .Select(step => ($"{step.CompletedUtc?.ToLocalTime():d MMM, h:mm tt} · {step.Actor?.DisplayName}", (string?)step.Name))
                    .ToArray()));
            }

            if (detail.Actions.FirstOrDefault(action => action.Action == "withdraw") is { IsAllowed: true })
            {
                var withdraw = PrimaryButton("Withdraw request", Tokens.BrandAccent);
                withdraw.Clicked += async (_, _) =>
                {
                    withdraw.IsEnabled = false;
                    try
                    {
                        await leave.WithdrawAsync(requestId, "Withdrawn from the employee app.");
                        Changed?.Invoke(this, EventArgs.Empty);
                        if (back is not null) { await back(); } else { sheet.Hide(); }
                    }
                    catch (MobileApiException exception)
                    {
                        withdraw.IsEnabled = true;
                        body.Add(ErrorLabel(exception.Message));
                    }
                };
                body.Add(withdraw);
            }

            if (back is not null)
            {
                var backButton = SecondaryButton("Back");
                backButton.Clicked += async (_, _) => await back();
                body.Add(backButton);
            }

            sheet.Present("Leave request", body);
        }
        catch (MobileApiException exception)
        {
            sheet.Present("Leave request", Problem(exception));
        }
    }

    // ---- EMP-030 Apply Leave ----------------------------------------------------

    public async Task PresentApplyAsync(AppSheet sheet)
    {
        sheet.Present("Apply Leave", Loading());

        try
        {
            var policies = await leave.GetPoliciesAsync();
            var balances = await leave.GetBalancesAsync();

            if (policies.Count == 0)
            {
                sheet.Present("Apply Leave", SheetContent.FieldCard(("Leave", "No leave types are set up yet. HR configures these.")));
                return;
            }

            sheet.Present("Apply Leave", new ApplyLeaveForm(leave, policies, balances, async () =>
            {
                Changed?.Invoke(this, EventArgs.Empty);
                await PresentHistoryAsync(sheet);
            }), fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present("Apply Leave", Problem(exception));
        }
    }

    // ---- EMP-035/036 Leave Pending for Approval ---------------------------------

    public async Task PresentApprovalsAsync(AppSheet sheet, string? notice = null)
    {
        const string title = "Leave Pending for Approval";
        sheet.Present(title, Loading(), fullHeight: true);

        try
        {
            var queue = await leave.GetApprovalQueueAsync();
            var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 6, 0, 24) };

            if (notice is not null)
            {
                body.Add(new Label
                {
                    Text = notice,
                    FontSize = 13,
                    TextColor = Tokens.DecisionApprove,
                    Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8)
                });
            }

            body.Add(ApprovalBanner(queue.TotalPending));

            foreach (var row in queue.Rows)
            {
                body.Add(ApprovalCard(sheet, row));
            }

            if (queue.Rows.Count == 0)
            {
                body.Add(SheetContent.FieldCard(("Approvals", "Nothing is waiting for your decision.")));
            }

            sheet.Present(title, body, fullHeight: true);
        }
        catch (MobileApiException exception)
        {
            sheet.Present(title, Problem(exception), fullHeight: true);
        }
    }

    /// <summary>C15: "3 Leave request(s) awaiting approval".</summary>
    private static View ApprovalBanner(int pending) => new Border
    {
        BackgroundColor = Tokens.ApprovalBannerGround,
        Stroke = Tokens.ApprovalBannerBorder,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 6 },
        Padding = new Thickness(14, 12),
        Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 12),
        Content = new Label
        {
            Text = $"{pending} Leave request(s) awaiting approval",
            FontSize = 13,
            TextColor = Tokens.ApprovalBannerInk
        }
    };

    /// <summary>C16 + C17: the tinted approval card with Approve / Disapprove / Cancel Request.</summary>
    private View ApprovalCard(AppSheet sheet, LeaveApprovalRowDto row)
    {
        var (strip, ink) = Tokens.LeaveTone(row.ToneKey);

        // Header strip: type · Pending pill · ⓘ
        var header = new Grid
        {
            BackgroundColor = strip,
            Padding = new Thickness(12, 8),
            ColumnSpacing = 8,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        header.Add(new Label
        {
            Text = row.LeaveTypeName ?? row.LeaveTypeCode,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = ink,
            VerticalTextAlignment = TextAlignment.Center
        }, 0);

        header.Add(Pill("Pending", Tokens.StatusWarning, Colors.White), 1);

        var info = new Border
        {
            WidthRequest = 22,
            HeightRequest = 22,
            BackgroundColor = Tokens.InfoBadge,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "i",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        var infoTap = new TapGestureRecognizer();
        infoTap.Tapped += async (_, _) => await PresentDetailAsync(sheet, row.RequestId, () => PresentApprovalsAsync(sheet));
        info.GestureRecognizers.Add(infoTap);
        SemanticProperties.SetDescription(info, "Request details");
        header.Add(info, 2);

        // Employee row: initials avatar · name + role/department · EMP ID
        var person = new Grid
        {
            Padding = new Thickness(12, 10),
            ColumnSpacing = 10,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        person.Add(new Border
        {
            WidthRequest = 36,
            HeightRequest = 36,
            BackgroundColor = ink,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = Initials(row.DisplayName),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        }, 0);

        person.Add(new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = row.DisplayName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                new Label
                {
                    Text = string.Join(" . ", new[] { row.Designation, row.DepartmentName ?? row.DepartmentCode }
                        .Where(part => !string.IsNullOrWhiteSpace(part))),
                    FontSize = 12,
                    TextColor = Tokens.TextTertiary
                }
            }
        }, 1);

        person.Add(new VerticalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "EMP ID", FontSize = 11, TextColor = Tokens.TextSecondary, HorizontalTextAlignment = TextAlignment.End },
                new Label { Text = row.EmployeeNumber, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = ink, HorizontalTextAlignment = TextAlignment.End }
            }
        }, 2);

        // Facts: FROM · To · Duration · Applied On
        var facts = new Grid
        {
            Padding = new Thickness(12, 10),
            ColumnSpacing = 12,
            RowSpacing = 10,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)]
        };

        facts.Add(Fact("FROM", row.From.ToString("dd MMM yyyy")), 0, 0);
        facts.Add(Fact("To", row.To.ToString("dd MMM yyyy")), 1, 0);
        facts.Add(Fact("Duration", $"{row.Days:0.#} Days"), 0, 1);
        facts.Add(Fact("Applied On", row.SubmittedUtc.ToLocalTime().ToString("dd MMM yyyy")), 1, 1);

        var reason = new Border
        {
            BackgroundColor = Tokens.ReasonPlate,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = new Thickness(12, 10),
            Margin = new Thickness(12, 0, 12, 12),
            Content = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = "REASON", FontSize = 11, TextColor = Tokens.TextSecondary },
                    new Label { Text = row.Reason, FontSize = 13, TextColor = Color.FromArgb("#374151") }
                }
            }
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                header,
                person,
                new BoxView { HeightRequest = 1, Color = Tokens.Border, Margin = new Thickness(12, 0) },
                facts,
                reason
            }
        };

        if (row.IsOwn)
        {
            stack.Add(new Label
            {
                Text = "This is your own request — someone else has to decide it.",
                FontSize = 12,
                TextColor = Tokens.TextSecondary,
                Margin = new Thickness(12, 0, 12, 12)
            });
        }
        else
        {
            var buttons = new Grid
            {
                ColumnSpacing = 6,
                Margin = new Thickness(12, 0, 12, 12),
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                ]
            };

            var approve = DecisionButton("✓ Approve", Tokens.DecisionApprove);
            var disapprove = DecisionButton("⊘ Disapprove", Tokens.DecisionDisapprove);
            var cancel = DecisionButton("✕ Cancel Request", Tokens.DecisionCancel);

            approve.Clicked += async (_, _) =>
            {
                SetEnabled(false, approve, disapprove, cancel);
                await DecideAsync(sheet, row, "approve", null);
                SetEnabled(true, approve, disapprove, cancel);
            };
            disapprove.Clicked += (_, _) => PresentReason(sheet, row, "reject");
            cancel.Clicked += (_, _) => PresentReason(sheet, row, "cancel");

            buttons.Add(approve, 0);
            buttons.Add(disapprove, 1);
            buttons.Add(cancel, 2);
            stack.Add(buttons);
        }

        return new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Tokens.ApprovalCardBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 14),
            Content = stack
        };
    }

    /// <summary>Disapprove and Cancel both need a reason — the server refuses them without one.</summary>
    private void PresentReason(AppSheet sheet, LeaveApprovalRowDto row, string decision)
    {
        var title = decision == "reject" ? "Disapprove leave" : "Cancel leave request";

        var editor = new Editor
        {
            Placeholder = decision == "reject" ? "Why is this being disapproved?" : "Why is this being cancelled?",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 96,
            FontSize = 14,
            TextColor = Tokens.TextPrimary,
            BackgroundColor = Tokens.SurfaceMuted
        };

        var error = new Label { FontSize = 12, TextColor = Tokens.StatusDanger, IsVisible = false };
        var confirm = PrimaryButton(decision == "reject" ? "Disapprove" : "Cancel request",
            decision == "reject" ? Tokens.DecisionDisapprove : Tokens.DecisionCancel);
        var back = SecondaryButton("Back");

        confirm.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(editor.Text))
            {
                error.Text = "Please give a reason — the employee is shown it.";
                error.IsVisible = true;
                return;
            }

            confirm.IsEnabled = false;
            await DecideAsync(sheet, row, decision, editor.Text.Trim());
            confirm.IsEnabled = true;
        };
        back.Clicked += async (_, _) => await PresentApprovalsAsync(sheet);

        sheet.Present(title, new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(Tokens.SpaceGutter, 8, Tokens.SpaceGutter, 24),
            Children =
            {
                new Label
                {
                    Text = $"{row.DisplayName} · {row.LeaveTypeName ?? row.LeaveTypeCode} · {Range(row.From, row.To)}",
                    FontSize = 13,
                    TextColor = Tokens.TextSecondary
                },
                new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    BackgroundColor = Tokens.SurfaceMuted,
                    Padding = 6,
                    Content = editor
                },
                error,
                confirm,
                back
            }
        }, fullHeight: true);
    }

    private async Task DecideAsync(AppSheet sheet, LeaveApprovalRowDto row, string decision, string? comment)
    {
        try
        {
            await leave.DecideAsync(row.RequestId, new LeaveDecisionRequest { Decision = decision, Comment = comment });
            Changed?.Invoke(this, EventArgs.Empty);

            var verb = decision switch { "approve" => "Approved", "reject" => "Disapproved", _ => "Cancelled" };
            await PresentApprovalsAsync(sheet, $"{verb} {row.DisplayName}'s {row.LeaveTypeName ?? row.LeaveTypeCode}.");
        }
        catch (MobileApiException exception)
        {
            await PresentApprovalsAsync(sheet);
            await Application.Current!.Windows[0].Page!.DisplayAlert("Could not record the decision", exception.Message, "OK");
        }
    }

    // ---- Parts ----------------------------------------------------------------

    private static View Fact(string label, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children =
        {
            new Label { Text = label, FontSize = 11, TextColor = Tokens.TextSecondary },
            new Label { Text = value, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary }
        }
    };

    private static View Pill(string text, Color ground, Color ink) => new Border
    {
        BackgroundColor = ground,
        StrokeThickness = 0,
        Padding = new Thickness(10, 3),
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        VerticalOptions = LayoutOptions.Center,
        Content = new Label { Text = text, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = ink }
    };

    private static Button DecisionButton(string text, Color ground) => new()
    {
        Text = text,
        BackgroundColor = ground,
        TextColor = Colors.White,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        CornerRadius = 4,
        HeightRequest = 36,
        Padding = new Thickness(2, 0)
    };

    internal static Button PrimaryButton(string text, Color ground) => new()
    {
        Text = text,
        BackgroundColor = ground,
        TextColor = Colors.White,
        FontAttributes = FontAttributes.Bold,
        CornerRadius = 8,
        HeightRequest = 46,
        Margin = new Thickness(Tokens.SpaceGutter, 8, Tokens.SpaceGutter, 0)
    };

    internal static Button SecondaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Tokens.SecondaryButton,
        TextColor = Tokens.TextPrimary,
        CornerRadius = 8,
        HeightRequest = 46,
        Margin = new Thickness(Tokens.SpaceGutter, 8, Tokens.SpaceGutter, 0)
    };

    private static void SetEnabled(bool enabled, params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            button.IsEnabled = enabled;
        }
    }

    private static Label ErrorLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        TextColor = Tokens.StatusDanger,
        Margin = new Thickness(Tokens.SpaceGutter, 6)
    };

    internal static string Range(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("d MMM yyyy") : $"{from:d MMM yyyy} - {to:d MMM yyyy}";

    internal static string Days(decimal days) => $"{days:0.#} {(days == 1 ? "day" : "days")}";

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));

    internal static View Loading() => new ActivityIndicator { IsRunning = true, Color = Tokens.BrandAccent, Margin = 32 };

    internal static View Problem(MobileApiException exception) => exception switch
    {
        MobileFeatureNotReadyException notReady => SheetContent.NotReady(notReady.Module),
        MobileAccessDeniedException => SheetContent.FieldCard(("Access", "You do not have permission to see this.")),
        MobileOfflineException => SheetContent.FieldCard(("Offline", "No connection to the Kapsons server.")),
        _ => SheetContent.FieldCard(("Problem", exception.Message))
    };
}

/// <summary>
/// EMP-030 / 031. Type, dates, half days and reason; Calculate asks the server what the request
/// charges; Submit sends that calculation's token. Changing any input throws the calculation
/// away, so what is submitted is always what was shown.
/// </summary>
internal sealed class ApplyLeaveForm : ContentView
{
    private readonly LeaveApiClient leave;
    private readonly IReadOnlyList<LeavePolicySummaryDto> policies;
    private readonly IReadOnlyList<LeaveBalanceDto> balances;
    private readonly Func<Task> submitted;

    private readonly Picker typePicker = new() { Title = "Select leave type", FontSize = 15, TextColor = Tokens.TextPrimary };
    private readonly DatePicker fromPicker = new() { Format = "d MMM yyyy", FontSize = 15, TextColor = Tokens.TextPrimary };
    private readonly DatePicker toPicker = new() { Format = "d MMM yyyy", FontSize = 15, TextColor = Tokens.TextPrimary };
    private readonly Switch firstHalf = new() { OnColor = Tokens.BrandAccent };
    private readonly Switch lastHalf = new() { OnColor = Tokens.BrandAccent };
    private readonly View halfDayRow;
    private readonly Editor reason = new() { Placeholder = "Reason for leave", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, FontSize = 14 };
    private readonly Label balanceLabel = new() { FontSize = 12, TextColor = Tokens.TextSecondary };
    private readonly VerticalStackLayout calculationPanel = new() { Spacing = 0, IsVisible = false };
    private readonly Label error = new() { FontSize = 12, TextColor = Tokens.StatusDanger, IsVisible = false, Margin = new Thickness(Tokens.SpaceGutter, 4) };
    private readonly Button calculate = LeaveFlows.SecondaryButton("Calculate days");
    private readonly Button submit = LeaveFlows.PrimaryButton("Submit request", Tokens.BrandAccent);

    // One key per filled-in form: a retry after a dropped connection is the same request.
    private readonly string idempotencyKey = Guid.NewGuid().ToString("N");
    private LeaveCalculationDto? calculation;

    public ApplyLeaveForm(
        LeaveApiClient leave,
        IReadOnlyList<LeavePolicySummaryDto> policies,
        IReadOnlyList<LeaveBalanceDto> balances,
        Func<Task> submitted)
    {
        this.leave = leave;
        this.policies = policies;
        this.balances = balances;
        this.submitted = submitted;

        typePicker.ItemsSource = policies.Select(policy => $"{policy.Emoji} {policy.Name}").ToList();
        fromPicker.Date = DateTime.Today;
        toPicker.Date = DateTime.Today;

        halfDayRow = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Toggle("First day is a half day", firstHalf),
                Toggle("Last day is a half day", lastHalf)
            }
        };

        typePicker.SelectedIndexChanged += (_, _) => { Invalidate(); ShowPolicy(); };
        fromPicker.DateSelected += (_, _) =>
        {
            if (toPicker.Date < fromPicker.Date)
            {
                toPicker.Date = fromPicker.Date;
            }
            Invalidate();
        };
        toPicker.DateSelected += (_, _) => Invalidate();
        firstHalf.Toggled += (_, _) => Invalidate();
        lastHalf.Toggled += (_, _) => Invalidate();

        calculate.Clicked += async (_, _) => await CalculateAsync();
        submit.Clicked += async (_, _) => await SubmitAsync();
        submit.IsEnabled = false;
        submit.Opacity = 0.5;

        if (policies.Count > 0)
        {
            typePicker.SelectedIndex = 0;
        }

        Content = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0, 4, 0, 28),
            Children =
            {
                Field("Leave type", typePicker),
                balanceLabel.WithMargin(new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 6)),
                new Grid
                {
                    ColumnSpacing = 10,
                    ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
                    Children = { Column(Field("From", fromPicker), 0), Column(Field("To", toPicker), 1) }
                },
                halfDayRow.WithMargin(new Thickness(Tokens.SpaceGutter, 4)),
                Field("Reason", reason),
                calculate,
                calculationPanel,
                error,
                submit
            }
        };

        ShowPolicy();
    }

    private LeavePolicySummaryDto? Policy =>
        typePicker.SelectedIndex >= 0 && typePicker.SelectedIndex < policies.Count ? policies[typePicker.SelectedIndex] : null;

    private void ShowPolicy()
    {
        var policy = Policy;
        halfDayRow.IsVisible = policy?.AllowsHalfDay ?? false;

        if (!halfDayRow.IsVisible)
        {
            firstHalf.IsToggled = false;
            lastHalf.IsToggled = false;
        }

        var balance = policy is null ? null : balances.FirstOrDefault(item => item.LeaveTypeCode == policy.Code);
        balanceLabel.Text = balance is null ? string.Empty : $"Available: {balance.Available:0.#} {balance.Unit.ToLowerInvariant()}";
    }

    private void Invalidate()
    {
        calculation = null;
        calculationPanel.IsVisible = false;
        error.IsVisible = false;
        submit.IsEnabled = false;
        submit.Opacity = 0.5;
    }

    private async Task CalculateAsync()
    {
        if (Policy is not { } policy)
        {
            ShowError("Choose a leave type.");
            return;
        }

        calculate.IsEnabled = false;
        error.IsVisible = false;

        try
        {
            calculation = await leave.CalculateAsync(new LeaveCalculationRequest
            {
                LeaveTypeCode = policy.Code,
                From = DateOnly.FromDateTime(fromPicker.Date),
                To = DateOnly.FromDateTime(toPicker.Date),
                IsFirstDayPartial = firstHalf.IsToggled,
                IsLastDayPartial = lastHalf.IsToggled
            });

            RenderCalculation(calculation);
            submit.IsEnabled = true;
            submit.Opacity = 1;
        }
        catch (MobileApiException exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            calculate.IsEnabled = true;
        }
    }

    private void RenderCalculation(LeaveCalculationDto result)
    {
        calculationPanel.Clear();
        calculationPanel.Add(SheetContent.SectionHeader("Calculated by the server"));

        var fields = new List<(string, string?)>
        {
            ("Days requested", $"{result.RequestedDays:0.#}"),
            ("Days charged", $"{result.ChargeableDays:0.#}"),
            ("Balance", $"{result.BalanceBefore:0.#} → {result.BalanceAfter:0.#}")
        };

        if (result.ExcludedDates.Count > 0)
        {
            fields.Add(("Not charged (holidays / off days)", string.Join(", ", result.ExcludedDates.Select(date => date.ToString("d MMM")))));
        }

        foreach (var warning in result.Warnings)
        {
            fields.Add(("Note", warning));
        }

        calculationPanel.Add(SheetContent.FieldCard(fields.ToArray()));
        calculationPanel.IsVisible = true;
    }

    private async Task SubmitAsync()
    {
        if (Policy is not { } policy || calculation is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason.Text))
        {
            ShowError("Please give a reason for the leave.");
            return;
        }

        submit.IsEnabled = false;
        error.IsVisible = false;

        try
        {
            await leave.SubmitAsync(new CreateLeaveRequest
            {
                LeaveTypeCode = policy.Code,
                From = DateOnly.FromDateTime(fromPicker.Date),
                To = DateOnly.FromDateTime(toPicker.Date),
                IsFirstDayPartial = firstHalf.IsToggled,
                IsLastDayPartial = lastHalf.IsToggled,
                Reason = reason.Text.Trim(),
                CalculationToken = calculation.CalculationToken
            }, idempotencyKey);

            await submitted();
        }
        catch (MobileApiException exception) when (exception is MobileConflictException or MobileValidationFailedException
                                                   && exception.Message.Contains("calculat", StringComparison.OrdinalIgnoreCase))
        {
            // The token expired (30 minutes) or the rules changed: recalculate and let the person look again.
            await CalculateAsync();
            ShowError("The calculation was out of date and has been refreshed. Check the days and submit again.");
        }
        catch (MobileApiException exception)
        {
            ShowError(exception.Message);
            submit.IsEnabled = true;
        }
    }

    private void ShowError(string text)
    {
        error.Text = text;
        error.IsVisible = true;
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
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
        grid.Add(new Label { Text = text, FontSize = 14, TextColor = Tokens.TextPrimary, VerticalTextAlignment = TextAlignment.Center }, 0);
        grid.Add(toggle, 1);
        return grid;
    }

    private static View Column(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }
}

internal static class ViewExtensions
{
    public static T WithMargin<T>(this T view, Thickness margin) where T : View
    {
        view.Margin = margin;
        return view;
    }
}

using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;

namespace Vispl.HRManagement.EmployeeApp.Features.Leave;

/// <summary>
/// EMP-014. The Leave tab: balances, history, and an approver strip for anyone who holds
/// <c>LEAVE.APPROVE</c>.
///
/// Applying is Phase 2, and the reason is worth stating rather than stubbing: the server owns
/// the chargeable day count and issues a signed, short-lived token with it, which the submit
/// call requires. A form that guessed the number would be wrong in exactly the cases policy
/// exists for, so it waits until the calculate-then-submit handshake is wired properly.
/// </summary>
public sealed class LeavePage : ContentPage
{
    private readonly LeaveViewModel model;
    private readonly BrandAppBar appBar = new();
    private readonly VerticalStackLayout balanceList = new();
    private readonly VerticalStackLayout requestList = new();
    private readonly VerticalStackLayout content;
    private readonly StateView stateView = new();
    private readonly RefreshView refresher;
    private readonly AppSheet sheet = new();
    private readonly Border approverStrip;
    private readonly Label approverLabel;

    private readonly EmployeeTabBar tabBar = new(EmployeeTab.Leave);

    private readonly LeaveFlows flows;

    public LeavePage(LeaveViewModel model, QuickActionsHost quickActions, LeaveFlows flows)
    {
        this.model = model;
        this.flows = flows;
        flows.Changed += async (_, _) => await model.RefreshAsync();

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        tabBar.QuickActionsRequested += (_, _) => quickActions.Present(sheet);
        BackgroundColor = Tokens.Background;
        Title = "Leave";

        approverLabel = new Label
        {
            FontSize = Tokens.FontRowSubtitle,
            TextColor = Tokens.TextPrimary,
            VerticalTextAlignment = TextAlignment.Center
        };

        approverStrip = new Border
        {
            IsVisible = false,
            BackgroundColor = Tokens.BrandAccentTint,
            StrokeThickness = 0,
            Padding = 14,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceCardGap, Tokens.SpaceGutter, 0),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = approverLabel
        };

        var approverTap = new TapGestureRecognizer();
        approverTap.Tapped += async (_, _) => await flows.PresentApprovalsAsync(sheet);
        approverStrip.GestureRecognizers.Add(approverTap);

        content = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                approverStrip,
                Section("Balances"),
                balanceList,
                RecentHeader(),
                requestList,
                ApplyAction()
            }
        };

        stateView.ViewModel = model;
        stateView.RetryRequested += async (_, _) => await model.LoadAsync();

        refresher = new RefreshView
        {
            RefreshColor = Tokens.BrandAccent,
            Content = new ScrollView { Content = new Grid { Children = { content, stateView } } }
        };

        refresher.Refreshing += async (_, _) =>
        {
            await model.RefreshAsync();
            refresher.IsRefreshing = false;
        };

        sheet.Content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ],
            Children = { Row(appBar, 0), Row(refresher, 1), Row(tabBar, 2) }
        };

        Content = sheet;

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModelBase.State)
                or nameof(LeaveViewModel.PendingApprovals)
                or nameof(LeaveViewModel.CanApprove))
            {
                MainThread.BeginInvokeOnMainThread(Render);
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await model.LoadAsync();
    }

    private static View Section(string text) => new Label
    {
        Text = text,
        FontSize = Tokens.FontRowTitle,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.TextPrimary,
        Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 10)
    };

    private View ApplyAction()
    {
        var apply = new Button
        {
            Text = "Apply for leave",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            CornerRadius = 12,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 24)
        };

        apply.Clicked += async (_, _) => await flows.PresentApplyAsync(sheet);

        return apply;
    }

    /// <summary>"Recent requests" with "See all" opening the Leave History sheet (4.png).</summary>
    private View RecentHeader()
    {
        var seeAll = new Label
        {
            Text = "See all",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.BrandAccent,
            VerticalTextAlignment = TextAlignment.Center
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await flows.PresentHistoryAsync(sheet);
        seeAll.GestureRecognizers.Add(tap);

        var grid = new Grid
        {
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 10),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };

        grid.Add(new Label
        {
            Text = "Recent requests",
            FontSize = Tokens.FontRowTitle,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary
        }, 0);
        grid.Add(seeAll, 1);
        return grid;
    }

    private void Render()
    {
        content.IsVisible = model.IsReady || model.IsEmpty;

        approverStrip.IsVisible = model.CanApprove;
        tabBar.HasLeaveAttention = model.CanApprove && model.PendingApprovals > 0;
        approverLabel.Text = model.PendingApprovals == 0
            ? "Nothing is waiting for your approval."
            : $"{model.PendingApprovals} leave " +
              $"{(model.PendingApprovals == 1 ? "request is" : "requests are")} waiting for your approval.";

        balanceList.Clear();

        foreach (var balance in model.Balances)
        {
            var card = new LeaveBalanceCard();
            card.Bind(balance);
            balanceList.Add(card);
        }

        if (model.Balances.Count == 0)
        {
            balanceList.Add(Muted("No leave policies are assigned to you yet."));
        }

        requestList.Clear();

        foreach (var request in model.Requests.Take(10))
        {
            var row = new LeaveRequestRow();
            row.Bind(request, request.LeaveTypeName ?? model.NameFor(request.LeaveTypeCode));

            var captured = request;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await flows.PresentDetailAsync(sheet, captured.Id);
            row.GestureRecognizers.Add(tap);

            requestList.Add(row);
        }

        if (model.Requests.Count == 0)
        {
            requestList.Add(Muted("You have not applied for any leave yet."));
        }
    }

    private static View Muted(string text) => new Label
    {
        Text = text,
        FontSize = 13,
        TextColor = Tokens.TextSecondary,
        Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8)
    };

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
